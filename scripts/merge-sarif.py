#!/usr/bin/env python3
"""
Merges individual project SARIF files into a unified SARIF 2.1.0 report
or generates a fallback empty SARIF document.
"""
import glob
import json
import os
import sys

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
                merged_runs.extend(data.get("runs", []))
        except Exception as e:
            print(f"Warning reading {file}: {e}", file=sys.stderr)

    if not merged_runs:
        merged_runs = [{
            "tool": {
                "driver": {
                    "name": "SecurityCodeScan",
                    "version": "5.6.7"
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
        tool = sys.argv[3] if len(sys.argv) > 3 else "SecurityCodeScan"
        ver = sys.argv[4] if len(sys.argv) > 4 else "1.0.0"
        create_empty_sarif(out, tool, ver)
    else:
        results_dir = sys.argv[1] if len(sys.argv) > 1 else "sarif-results"
        output_file = sys.argv[2] if len(sys.argv) > 2 else "security-code-scan.sarif"
        merge_sarif(results_dir, output_file)
