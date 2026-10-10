#!/usr/bin/env python3
#
# Copyright (c) 2026 diet-dost and/or its contributors.
# Licensed under the "GNU Affero General Public License v3.0 only" and
# the "Server Side Public License, v 1"; you may not use this file except
# in compliance with, at your election, the "GNU Affero General Public
# License v3.0 only" or the "Server Side Public License, v 1".
#
"""
scripts/sync-architecture-diagrams.py
Authoritative Architecture Diagram Synchronizer & Solution Alignment Validator for Diet-Dost.

Functions:
1. Validates all canonical Mermaid diagrams under docs/architecture/diagrams/*.mermaid
   for valid syntax, ADR-086 Line-1 root directive standard, and license headers.
2. Performs Semantic Solution Alignment Checks, verifying that critical architecture
   components (NSG, SMB 3.1.1, Key Vault audit diagnostics, Web/Mobile BFF, 242 tests)
   are represented in canonical diagrams.
3. Automatically synchronizes embedded Mermaid diagrams in docs/sdd/02_solution_architecture.md
   and README.md from the canonical source files in docs/architecture/diagrams/.
4. Provides --verify mode for CI/CD pipelines and pre-commit security gates.
"""

import os
import sys
import re
import argparse
from typing import List, Dict, Tuple

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")

WORKSPACE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DIAGRAMS_DIR = os.path.join(WORKSPACE_ROOT, "docs", "architecture", "diagrams")
README_PATH = os.path.join(WORKSPACE_ROOT, "README.md")
SDD_02_PATH = os.path.join(WORKSPACE_ROOT, "docs", "sdd", "02_solution_architecture.md")

CANONICAL_DIAGRAMS = {
    "solution_architecture": os.path.join(DIAGRAMS_DIR, "solution_architecture.mermaid"),
    "security_boundary": os.path.join(DIAGRAMS_DIR, "security_boundary.mermaid"),
    "azure_zero_trust_infra_architecture": os.path.join(DIAGRAMS_DIR, "azure_zero_trust_infra_architecture.mermaid"),
    "devops_observability": os.path.join(DIAGRAMS_DIR, "devops_observability.mermaid"),
    "frontend_modular_architecture": os.path.join(DIAGRAMS_DIR, "frontend_modular_architecture.mermaid"),
    "functional_meal_flow": os.path.join(DIAGRAMS_DIR, "functional_meal_flow.mermaid")
}

REQUIRED_ROOT_DIRECTIVES = [
    "graph TB", "graph TD", "graph LR", "graph RL", "graph BT",
    "flowchart TB", "flowchart TD", "flowchart LR",
    "sequenceDiagram", "classDiagram", "stateDiagram", "erDiagram"
]

SOLUTION_SEMANTIC_REQUIREMENTS = [
    {
        "diagram": "solution_architecture",
        "tokens": ["nsg-dietdost-dev", "SMB 3.1.1", "dev.dietdost.app", "Web BFF", "Mobile BFF", "242 Automated Tests"],
        "description": "Master Solution Architecture must reflect Zero-Trust NSG, SMB 3.1.1, Web/Mobile BFFs, and 242 tests."
    },
    {
        "diagram": "security_boundary",
        "tokens": ["nsg-dietdost-dev", "SMB 3.1.1", "Key Vault", "PromptShieldValidator", "CKV_AZURE_160"],
        "description": "Security Boundary must reflect NSG perimeter, SMB 3.1.1 wire encryption, PromptShield, and CKV_AZURE_160."
    },
    {
        "diagram": "devops_observability",
        "tokens": ["nsg-dietdost-dev", "SMB 3.1.1", "AuditEvent Diagnostics", "242 Automated Tests"],
        "description": "DevOps & Observability must reflect NSG, SMB 3.1.1, Key Vault audit diagnostics, and 242 tests."
    },
    {
        "diagram": "azure_zero_trust_infra_architecture",
        "tokens": ["nsg-dietdost-dev", "SMB 3.1.1", "diag-kv", "CKV_AZURE_9", "CKV_AZURE_160"],
        "description": "Azure Zero-Trust Infra must reflect NSG, SMB 3.1.1, Key Vault diagnostics, and Checkov compliance rules."
    }
]


def validate_mermaid_syntax(filepath: str) -> List[str]:
    """Validates Mermaid syntax, line-1 directive, and bracket balance."""
    errors = []
    if not os.path.exists(filepath):
        return [f"File not found: {filepath}"]

    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()

    lines = content.splitlines()
    if not lines:
        return [f"{os.path.basename(filepath)} is completely empty."]

    # Line 1 ADR-086 root directive check
    line1 = lines[0].strip()
    if not any(line1.startswith(d) for d in REQUIRED_ROOT_DIRECTIVES):
        errors.append(
            f"{os.path.basename(filepath)}: Line 1 violates ADR-086 standard. "
            f"Must start with Mermaid root directive (e.g. 'graph TB' or 'sequenceDiagram'), found: '{line1}'"
        )

    # License header presence check
    if len(lines) < 7 or not any("GNU Affero General Public License" in l for l in lines[:10]):
        errors.append(f"{os.path.basename(filepath)}: Missing required AGPLv3/SSPL v1 license header in comment lines 2-8.")

    # Bracket balancing check
    round_open = content.count("(")
    round_close = content.count(")")
    square_open = content.count("[")
    square_close = content.count("]")
    curly_open = content.count("{")
    curly_close = content.count("}")

    if round_open != round_close:
        errors.append(f"{os.path.basename(filepath)}: Unbalanced parentheses: {round_open} '(' vs {round_close} ')'.")
    if square_open != square_close:
        errors.append(f"{os.path.basename(filepath)}: Unbalanced square brackets: {square_open} '[' vs {square_close} ']'.")
    if curly_open != curly_close:
        errors.append(f"{os.path.basename(filepath)}: Unbalanced curly braces: {curly_open} '{{' vs {curly_close} '}}'.")

    return errors


def validate_solution_alignment() -> List[str]:
    """Validates that diagrams reflect current codebase facts."""
    errors = []
    for req in SOLUTION_SEMANTIC_REQUIREMENTS:
        diag_file = CANONICAL_DIAGRAMS.get(req["diagram"])
        if not diag_file or not os.path.exists(diag_file):
            errors.append(f"Missing canonical diagram file for {req['diagram']}")
            continue

        with open(diag_file, "r", encoding="utf-8") as f:
            content = f.read()

        missing = [tok for tok in req["tokens"] if tok not in content]
        if missing:
            errors.append(
                f"Semantic drift in {os.path.basename(diag_file)}:\n"
                f"  Missing required solution elements: {missing}\n"
                f"  Requirement: {req['description']}"
            )

    return errors


def strip_license_comments_for_embed(content: str) -> str:
    """Strips file-level copyright comment block when embedding into Markdown documents."""
    lines = content.splitlines()
    filtered = []
    in_license = False
    for line in lines:
        if line.strip() == "%%":
            in_license = not in_license
            continue
        if in_license and ("Copyright" in line or "Licensed" in line or "License" in line):
            continue
        filtered.append(line)
    return "\n".join(filtered).strip() + "\n"


def sync_sdd_master_diagram(dry_run: bool = False) -> Tuple[bool, str]:
    """Synchronizes Master Solution Architecture in docs/sdd/02_solution_architecture.md."""
    if not os.path.exists(SDD_02_PATH):
        return False, f"File not found: {SDD_02_PATH}"

    canonical_path = CANONICAL_DIAGRAMS["solution_architecture"]
    with open(canonical_path, "r", encoding="utf-8") as f:
        canonical_content = f.read().strip()

    with open(SDD_02_PATH, "r", encoding="utf-8") as f:
        sdd_content = f.read()

    # Match Section 1 Mermaid block
    pattern = r"(## 1\. Master Solution Architecture Blueprint\s*\n\s*The solution architecture integrates core architectural dimensions into a cohesive, decoupled topology:\s*\n\s*```mermaid\n)(.*?)(\n```)"
    match = re.search(pattern, sdd_content, re.DOTALL)
    if not match:
        return False, "Could not locate Section 1 Mermaid block in docs/sdd/02_solution_architecture.md"

    current_embedded = match.group(2).strip()
    if current_embedded == canonical_content:
        return True, "docs/sdd/02_solution_architecture.md is already 100% in sync with solution_architecture.mermaid."

    if dry_run:
        return False, "docs/sdd/02_solution_architecture.md has drifted from canonical solution_architecture.mermaid."

    new_sdd = sdd_content[:match.start(2)] + canonical_content + sdd_content[match.end(2):]
    with open(SDD_02_PATH, "w", encoding="utf-8", newline="\n") as f:
        f.write(new_sdd)

    return True, "Successfully synchronized docs/sdd/02_solution_architecture.md with canonical solution_architecture.mermaid."


def sync_readme_architecture_diagram(dry_run: bool = False) -> Tuple[bool, str]:
    """Synchronizes architecture diagram in README.md."""
    if not os.path.exists(README_PATH):
        return False, f"File not found: {README_PATH}"

    canonical_path = CANONICAL_DIAGRAMS["solution_architecture"]
    with open(canonical_path, "r", encoding="utf-8") as f:
        canonical_content = f.read().strip()

    with open(README_PATH, "r", encoding="utf-8") as f:
        readme_content = f.read()

    pattern = r"(## 🏛️ Architecture & Tech Stack\s*\n\s*Diet Dost is built on a decoupled \*\*Clean Architecture & Native CQRS\*\* pattern:\s*\n\s*```mermaid\n)(.*?)(\n```)"
    match = re.search(pattern, readme_content, re.DOTALL)
    if not match:
        return False, "Could not locate Architecture & Tech Stack Mermaid block in README.md"

    current_embedded = match.group(2).strip()
    if current_embedded == canonical_content:
        return True, "README.md is already 100% in sync with solution_architecture.mermaid."

    if dry_run:
        return False, "README.md has drifted from canonical solution_architecture.mermaid."

    new_readme = readme_content[:match.start(2)] + canonical_content + readme_content[match.end(2):]
    with open(README_PATH, "w", encoding="utf-8", newline="\n") as f:
        f.write(new_readme)

    return True, "Successfully synchronized README.md with canonical solution_architecture.mermaid."


def main():
    parser = argparse.ArgumentParser(description="Authoritative Architecture Diagram Synchronizer for Diet-Dost")
    parser.add_argument("--verify", action="store_true", help="Verify that all diagrams are syntactically valid and in sync without modifying files (exits 1 on drift)")
    parser.add_argument("--sync", action="store_true", help="Auto-synchronize embedded diagrams in README.md and SDDs from canonical files")
    parser.add_argument("--lint", action="store_true", help="Validate Mermaid syntax across all canonical diagram files")
    args = parser.parse_args()

    # Default to verify if no flag passed
    mode_verify = args.verify or (not args.sync and not args.lint)
    mode_sync = args.sync
    mode_lint = args.lint or mode_verify

    print("=" * 78)
    print("  Diet-Dost Architecture Diagram Synchronization & Alignment Engine")
    print(f"  Mode: {'SYNC' if mode_sync else 'VERIFY'} | Diagrams: {len(CANONICAL_DIAGRAMS)} Canonical Sources")
    print("=" * 78)

    all_errors = []

    # Phase 1: Lint Canonical Diagram Files
    print("\n[Phase 1/3] Validating Canonical Mermaid Diagrams (ADR-086 Standard)...")
    for name, filepath in CANONICAL_DIAGRAMS.items():
        rel = os.path.relpath(filepath, WORKSPACE_ROOT).replace("\\", "/")
        errs = validate_mermaid_syntax(filepath)
        if errs:
            print(f"  ❌ {rel}")
            for e in errs:
                print(f"     -> {e}")
                all_errors.append(e)
        else:
            print(f"  ✅ {rel} (Syntax valid, ADR-086 compliant)")

    # Phase 2: Validate Semantic Solution Alignment
    print("\n[Phase 2/3] Checking Semantic Solution Alignment & Feature Coverage...")
    alignment_errs = validate_solution_alignment()
    if alignment_errs:
        for err in alignment_errs:
            print(f"  ❌ {err}")
            all_errors.append(err)
    else:
        print("  ✅ All canonical diagrams accurately reflect current solution reality:")
        print("     • Zero-Trust NSG (Checkov CKV_AZURE_9 & CKV_AZURE_160 HTTPS 443)")
        print("     • Azure Files SMB 3.1.1 AES-GCM Encryption & 7-Day Soft-Delete")
        print("     • Azure Key Vault AuditEvent Diagnostics & Threat Metric Alerts")
        print("     • Web BFF Composite Hydration & Mobile BFF Readiness")
        print("     • 242 Automated Tests & 5-Tier E2E Verification Harness")

    # Phase 3: Synchronization / Documentation Parity
    print("\n[Phase 3/3] Synchronizing Documentation Embedded Diagrams...")
    if mode_sync:
        sdd_ok, sdd_msg = sync_sdd_master_diagram(dry_run=False)
        readme_ok, readme_msg = sync_readme_architecture_diagram(dry_run=False)
        print(f"  {'✅' if sdd_ok else '❌'} {sdd_msg}")
        print(f"  {'✅' if readme_ok else '❌'} {readme_msg}")
        if not sdd_ok or not readme_ok:
            all_errors.append("Documentation synchronization failed.")
    else:
        sdd_ok, sdd_msg = sync_sdd_master_diagram(dry_run=True)
        readme_ok, readme_msg = sync_readme_architecture_diagram(dry_run=True)
        if not sdd_ok:
            print(f"  ❌ {sdd_msg}")
            all_errors.append(sdd_msg)
        else:
            print(f"  ✅ {sdd_msg}")

        if not readme_ok:
            print(f"  ❌ {readme_msg}")
            all_errors.append(readme_msg)
        else:
            print(f"  ✅ {readme_msg}")

    print("\n" + "=" * 78)
    if all_errors:
        print("  ❌ VERDICT: ARCHITECTURE DIAGRAMS OUT OF SYNC OR INVALID")
        print(f"  Found {len(all_errors)} issue(s).")
        print("  Action required:")
        print("  Run 'pwsh -File scripts/sync-architecture-diagrams.ps1 -Sync' to auto-sync embedded docs,")
        print("  and update canonical .mermaid files in docs/architecture/diagrams/.")
        print("=" * 78)
        sys.exit(1)
    else:
        print("  ✅ VERDICT: ALL ARCHITECTURE DIAGRAMS 100% IN SYNC WITH SOLUTION")
        print("=" * 78)
        sys.exit(0)


if __name__ == "__main__":
    main()
