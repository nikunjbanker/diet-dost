#!/usr/bin/env python3
"""
scripts/sync-adr-index.py
Authoritative ADR Index & Metadata Synchronizer for Diet-Dost.

Scans all domain subdirectories under docs/adr/ (architecture, security, devops, presentation, governance),
extracts structured metadata from atomic ADR fragments, generates machine-readable docs/adr/index.json,
and renders domain-partitioned markdown tables into docs/adr/README.md.
"""

import os
import sys
import re
import json
import glob
from datetime import datetime, timezone

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")

WORKSPACE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
ADR_DIR = os.path.join(WORKSPACE_ROOT, "docs", "adr")
INDEX_JSON_PATH = os.path.join(ADR_DIR, "index.json")
README_MD_PATH = os.path.join(ADR_DIR, "README.md")

CATEGORY_CONFIG = {
    "architecture": {
        "title": "Architecture & Domain Logic",
        "icon": "🏛️",
        "description": "Clean Architecture, CQRS, Database persistence, Options Pattern, and BFF facade specifications."
    },
    "security": {
        "title": "Security & Identity",
        "icon": "🛡️",
        "description": "Authentication, authorization, RBAC, Key Vault secrets, OWASP LLM defense, and code scanning gates."
    },
    "devops": {
        "title": "DevOps & Cloud Infrastructure",
        "icon": "🚀",
        "description": "Azure Container Apps, .NET Aspire deployment, SQLite SMB persistence, and GitHub OIDC CI/CD pipelines."
    },
    "presentation": {
        "title": "Presentation & UI/UX",
        "icon": "🎨",
        "description": "Client hydration, responsive mobile/tablet layout, touch ergonomics, and modal orchestration."
    },
    "governance": {
        "title": "Governance, Standards & Agentic Workflows",
        "icon": "⚖️",
        "description": "Token economics, GitHub Stacked PR protocol, ADR architecture, issue-driven workflow, and licensing."
    }
}

def parse_adr_file(filepath):
    rel_path = os.path.relpath(filepath, WORKSPACE_ROOT).replace("\\", "/")
    filename = os.path.basename(filepath)
    category = os.path.basename(os.path.dirname(filepath))
    
    # Extract number from filename (e.g. ADR-20260926-040-... -> 040)
    match_num = re.search(r"ADR-\d{8}-(\d{3})", filename)
    adr_number = int(match_num.group(1)) if match_num else 999
    adr_id = f"ADR-{adr_number:03d}"
    
    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()

    # Extract title from first H1
    h1_match = re.search(r"^#\s+(?:ADR-[^:]+:\s*)?(.+)$", content, re.MULTILINE)
    title = h1_match.group(1).strip() if h1_match else filename.replace(".md", "")
    # Clean brackets or prefix in title if present
    title = re.sub(r"^\[.*?\]:\s*", "", title)

    # Extract Status
    status_match = re.search(r"\*\*(?:Status|Lifecycle Status)\*\*:\s*`?([A-Za-z0-9_-]+)`?", content)
    status = status_match.group(1).upper() if status_match else "ACCEPTED"

    # Extract Change Type
    type_match = re.search(r"\*\*(?:Change Type)\*\*:\s*`?(\[[A-Za-z0-9_ -]+\]|[A-Za-z0-9_-]+)`?", content)
    change_type = type_match.group(1) if type_match else f"[{category.upper()}]"
    if not change_type.startswith("["):
        change_type = f"[{change_type}]"

    # Extract Date
    date_match = re.search(r"\*\*(?:Date|Date / Timestamp)\*\*:\s*`?([0-9T:+\- ]+)`?", content)
    if date_match:
        date_str = date_match.group(1).strip()
    else:
        # Fallback to date in filename
        date_file_match = re.search(r"ADR-(\d{4})(\d{2})(\d{2})", filename)
        if date_file_match:
            date_str = f"{date_file_match.group(1)}-{date_file_match.group(2)}-{date_file_match.group(3)}"
        else:
            date_str = "2026-10-10"

    # Affected Subsystems
    subsystem_match = re.search(r"\*\*(?:Affected Subsystems)\*\*:\s*`?([^`\n\r]+)`?", content)
    subsystem = subsystem_match.group(1).strip() if subsystem_match else category.capitalize()

    return {
        "id": adr_id,
        "number": adr_number,
        "title": title,
        "category": category,
        "status": status,
        "changeType": change_type,
        "date": date_str,
        "subsystem": subsystem,
        "filename": filename,
        "relativePath": rel_path
    }

def main():
    print(f"Scanning ADRs in: {ADR_DIR}")
    adr_files = glob.glob(os.path.join(ADR_DIR, "*", "ADR-*.md"))
    
    parsed_adrs = []
    for filepath in adr_files:
        parsed = parse_adr_file(filepath)
        parsed_adrs.append(parsed)

    # Sort numerically by ADR number
    parsed_adrs.sort(key=lambda x: x["number"])

    # Categorize counts
    category_counts = {}
    for cat in CATEGORY_CONFIG:
        category_counts[cat] = sum(1 for a in parsed_adrs if a["category"] == cat)

    # 1. Output docs/adr/index.json
    index_data = {
        "version": "1.0.0",
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "totalAdrs": len(parsed_adrs),
        "categoryCounts": category_counts,
        "records": parsed_adrs
    }

    with open(INDEX_JSON_PATH, "w", encoding="utf-8") as f:
        json.dump(index_data, f, indent=2, ensure_ascii=False)
    print(f"✅ Generated machine-readable index: {INDEX_JSON_PATH} ({len(parsed_adrs)} ADRs)")

    # 2. Output docs/adr/README.md
    readme_lines = [
        "<!--",
        "  Copyright (c) 2026 diet-dost and/or its contributors.",
        '  Licensed under the "GNU Affero General Public License v3.0 only" and',
        '  the "Server Side Public License, v 1"; you may not use this file except',
        '  in compliance with, at your election, the "GNU Affero General Public',
        '  License v3.0 only" or the "Server Side Public License, v 1".',
        "-->",
        "",
        "# Architectural Decision Records (ADRs) - Diet-Dost",
        "> **Classification**: Authoritative Architectural Decision Record (ADR) Registry & Standards  ",
        "> **Pattern**: Subsystem / Domain-Driven Atomic Fragment Pattern (Zero Merge Conflicts & Zero Baseline System Tokens)  ",
        f"> **Total Decisions**: {len(parsed_adrs)} Active Records Across 5 Domain Hierarchies  ",
        "> **Machine-Readable Registry**: [`docs/adr/index.json`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/index.json)  ",
        "> **Governance**: Dual AGPLv3 / SSPL v1, DPDPA 2023, ICMR-NIN 2024 Clinical Standards  ",
        "",
        "---",
        "",
        "## 1. Architecture Overview & Subsystem Organization",
        "",
        "To ensure high maintainability, rapid discoverability, and zero merge conflicts, all Architectural Decision Records in **Diet-Dost** are organized into **Domain / Subsystem Subdirectories**.",
        "",
        "### Key Principles:",
        "1. **Zero System Prompt Token Overhead**: Stored in `docs/adr/<category>/`, records consume **0 baseline tokens** in agent system prompts and are loaded strictly on-demand via `view_file`.",
        "2. **Domain Partitioning**: Decisions are partitioned into clean, cohesive domains (`architecture/`, `security/`, `devops/`, `presentation/`, `governance/`), capping directory sizes and keeping navigation intuitive.",
        "3. **100% PR Merge-Conflict Immunity**: Each decision introduces a dedicated, standalone file (`ADR-YYYYMMDD-NNN-<slug>.md`). Stacked or concurrent PRs never conflict on a single log file.",
        "4. **Automated Machine-Readable Indexing**: `docs/adr/index.json` and this document are automatically synchronized via `scripts/sync-adr-index.py`, eliminating manual Markdown table editing errors.",
        "",
        "---",
        "",
        "## 2. Domain Subsystems Summary",
        "",
        "| Subsystem Domain | Description | Total Decisions | Directory |",
        "| :--- | :--- | :--- | :--- |"
    ]

    for cat_key, cat_cfg in CATEGORY_CONFIG.items():
        count = category_counts.get(cat_key, 0)
        readme_lines.append(
            f"| **{cat_cfg['icon']} {cat_cfg['title']}** | {cat_cfg['description']} | **{count}** | [`docs/adr/{cat_key}/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/{cat_key}/) |"
        )

    readme_lines.extend([
        "",
        "---",
        "",
        "## 3. Active ADR Registry by Domain",
        ""
    ])

    # Append per-category tables
    for cat_key, cat_cfg in CATEGORY_CONFIG.items():
        cat_adrs = [a for a in parsed_adrs if a["category"] == cat_key]
        readme_lines.extend([
            f"### {cat_cfg['icon']} {cat_cfg['title']} (`docs/adr/{cat_key}/`)",
            f"> *{cat_cfg['description']}*",
            "",
            "| ADR ID | Title | Change Type | Status | File Path |",
            "| :--- | :--- | :--- | :--- | :--- |"
        ])
        for a in cat_adrs:
            readme_lines.append(
                f"| **{a['id']}** | {a['title']} | `{a['changeType']}` | `{a['status']}` | [`{a['filename']}`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/{a['relativePath']}) |"
            )
        readme_lines.append("")

    readme_lines.extend([
        "---",
        "",
        "## 4. Historical Decision Archive",
        "",
        "Historical decision entries from the initial project inception through the mobile strategy (`LOG-001` through `LOG-042`) are preserved with complete data integrity in the historical archive:",
        "",
        "* **Historical Archive**: [`docs/adr/archive/living_log_2026_09_archive.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/archive/living_log_2026_09_archive.md)",
        "",
        "---",
        "",
        "## 5. How to Author & Register a New ADR",
        "",
        "1. Identify the target subsystem domain folder (`architecture/`, `security/`, `devops/`, `presentation/`, or `governance/`).",
        "2. Copy the template from [`docs/adr/template.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/template.md).",
        "3. Create your atomic file: `docs/adr/<domain>/ADR-<YYYYMMDD>-<NNN>-<slug>.md`.",
        "4. Run the automated indexer to refresh `index.json` and `README.md`:",
        "   ```bash",
        "   pwsh -File scripts/sync-adr-index.ps1",
        "   # Or cross-platform Python:",
        "   python3 scripts/sync-adr-index.py",
        "   ```",
        ""
    ])

    with open(README_MD_PATH, "w", encoding="utf-8") as f:
        f.write("\n".join(readme_lines) + "\n")
    print(f"✅ Generated domain-categorized README: {README_MD_PATH}")

if __name__ == "__main__":
    main()
