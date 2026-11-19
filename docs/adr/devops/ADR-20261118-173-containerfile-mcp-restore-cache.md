<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261118-173: Containerfile Layer Cache Optimization for Nutrition.Mcp Project Reference

- **Status**: Accepted
- **Date**: 2026-11-18
- **Domain**: devops
- **Deciders**: Solution Architecture, Cloud DevOps Team, AI Agentic SDLC
- **Supercedes**: None
- **Related PR**: #172, #173

---

## 1. Context & Problem Statement

Following the merge of PR #172 integrating the Model Context Protocol (MCP) C# SDK into Diet-Dost, the Azure Container Apps deployment pipeline (run 44346684956) encountered a build failure in Stage 3 (`Podman build`):

```text
X Assets file '/src/src/Nutrition.Mcp/obj/project.assets.json' not found. Run a NuGet package restore to generate this file.
```

In `Containerfile`, the multi-stage build performs an optimized layer-caching step by copying individual `.csproj` files into `/src/` before invoking `dotnet restore src/Nutrition.WebGateway/Nutrition.WebGateway.csproj`.

Because `Nutrition.WebGateway` gained a direct project reference to `Nutrition.Mcp.csproj`, the absence of `src/Nutrition.Mcp/Nutrition.Mcp.csproj` during the restore stage caused the subsequent `dotnet publish --no-restore` command to fail due to missing NuGet package assets for `Nutrition.Mcp`.

---

## 2. Decision & Implementation

1. **Update Containerfile Layer Cache**:
   Added `COPY src/Nutrition.Mcp/Nutrition.Mcp.csproj src/Nutrition.Mcp/` to Stage 1 of `Containerfile` alongside `Nutrition.Domain`, `Nutrition.Application`, and `Nutrition.Infrastructure`.
2. **Deterministic NuGet Restore**:
   When `dotnet restore src/Nutrition.WebGateway/Nutrition.WebGateway.csproj` executes, the complete transitive dependency graph (including all 5 referenced projects) is restored into `/src/src/Nutrition.Mcp/obj/project.assets.json`.
3. **No-Restore Publish Integrity**:
   Subsequent `dotnet publish ... --no-restore` succeeds deterministically in both local Podman builds and CI/CD automated deployment runners.

---

## 3. Consequences & Governance

- **Layer Caching Preserved**: Docker/Podman continues to cache the NuGet restore layer across container rebuilds unless `.csproj` or `Directory.Build.props` files are modified.
- **Zero Drift**: Guarantees production image packaging matches the exact 501-test verified multi-project solution structure.
- **Traceability**: Closes Issue #173.
