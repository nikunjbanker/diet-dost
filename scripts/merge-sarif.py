#!/usr/bin/env python3
# ==============================================================================
# Copyright (c) 2026 diet-dost and/or its contributors.
# Licensed under the "GNU Affero General Public License v3.0 only" and
# the "Server Side Public License, v 1"; you may not use this file except
# in compliance with, at your election, the "GNU Affero General Public
# License v3.0 only" or the "Server Side Public License, v 1".
# ==============================================================================
"""
Merges individual project SARIF files into a unified SARIF 2.1.0 report
or generates a fallback empty SARIF document. Normalizes legacy SARIF 1.0.0
runs to strict OASIS SARIF 2.1.0 specifications required by GitHub Code Scanning.
"""
import glob
import json
import os
import sys

def normalize_run(run: dict) -> dict:
    tool = run.get("tool", {})
    if "driver" not in tool:
        name = tool.pop("name", "MicrosoftRoslynSecurity")
        version = tool.pop("version", "11.0.0")
        tool.pop("fileVersion", None)
        tool.pop("semanticVersion", None)
        tool.pop("language", None)
        tool["driver"] = {
            "name": name,
            "version": version
        }
    run.pop("rules", None)
    cleaned_results = []
    for res in run.get("results", []):
        msg = res.get("message")
        if isinstance(msg, str):
            res["message"] = {"text": msg}
        locations = res.get("locations", [])
        cleaned_locs = []
        for loc in locations:
            if "resultFile" in loc:
                rf = loc.pop("resultFile")
                loc["physicalLocation"] = {
                    "artifactLocation": {"uri": rf.get("uri", "")},
                    "region": rf.get("region", {})
                }
            cleaned_locs.append(loc)
        res["locations"] = cleaned_locs
        cleaned_results.append(res)
    run["results"] = cleaned_results
    return run

def create_empty_sarif(output_file: str, tool_name: str, version: str) -> None:
    sarif_doc = {
        "$schema": "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/master/Schemata/sarif-schema-2.1.0.json",
        "version": "2.1.0",
        "runs": [{
            "tool": {
                "driver": {
                    "name": tool_name,
                    "version": version
                }
            },
            "results": []
        }]
    }
    with open(output_file, "w", encoding="utf-8") as f:
        json.dump(sarif_doc, f, indent=2)
    print(f"Created fallback empty SARIF for {tool_name} at {output_file}")

def merge_sarif(results_dir: str, output_file: str) -> None:
    merged_runs = []
    for file in sorted(glob.glob(os.path.join(results_dir, "*.sarif"))):
        try:
            with open(file, "r", encoding="utf-8") as f:
                data = json.load(f)
                for r in data.get("runs", []):
                    merged_runs.append(normalize_run(r))
        except Exception as e:
            print(f"Warning reading {file}: {e}", file=sys.stderr)

    if not merged_runs:
        merged_runs = [{
            "tool": {
                "driver": {
                    "name": "MicrosoftRoslynSecurity",
                    "version": "11.0.0"
                }
            },
            "results": []
        }]

    sarif_doc = {
        "$schema": "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/master/Schemata/sarif-schema-2.1.0.json",
        "version": "2.1.0",
        "runs": merged_runs
    }

    with open(output_file, "w", encoding="utf-8") as f:
        json.dump(sarif_doc, f, indent=2)

    print(f"Aggregated {len(merged_runs)} SARIF run(s) across all projects into {output_file}")

if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "--empty":
        out = sys.argv[2] if len(sys.argv) > 2 else "empty.sarif"
        tool = sys.argv[3] if len(sys.argv) > 3 else "MicrosoftRoslynSecurity"
        ver = sys.argv[4] if len(sys.argv) > 4 else "11.0.0"
        create_empty_sarif(out, tool, ver)
    else:
        results_dir = sys.argv[1] if len(sys.argv) > 1 else "sarif-results"
        output_file = sys.argv[2] if len(sys.argv) > 2 else "security-code-scan.sarif"
        merge_sarif(results_dir, output_file)
