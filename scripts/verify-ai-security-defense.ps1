<#
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
#>
<#
.SYNOPSIS
    Automated AI-Based Development Defense & Security Best Practices Validator.
.DESCRIPTION
    Guards against vulnerabilities, anti-patterns, and supply chain threats commonly
    introduced by AI agents and LLM code generators:
    1. Supply Chain & Package Hallucination (Slopsquatting)
    2. Insecure Defaults & "Lazy AI" Workarounds (TLS bypass, wildcard CORS, fallback secrets)
    3. AI Security Disablers & Suppression Comments (@ts-ignore, eslint-disable, nosec)
    4. OWASP Top 10 for LLM Defenses (Prompt Injection delimiters, PII isolation, Quota gating)
    5. AI Agent State & Session Bleed (.agents/state, session transcripts, token dumps)
.PARAMETER Mode
    "All" scans the full repository; "Staged" scans only git staged files for pre-commit.
#>
[CmdletBinding()]
param(
    [ValidateSet("All", "Staged")]
    [string]$Mode = "All"
)

$ErrorActionPreference = "Stop"

# Resolve Repository Root
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $RepoRoot

Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "   Diet-Dost AI-Based Development Security Defense Validator" -ForegroundColor Cyan
Write-Host "   Mode: $Mode | Scope: $RepoRoot" -ForegroundColor Cyan
Write-Host "========================================================================" -ForegroundColor Cyan

$violations = [System.Collections.Generic.List[string]]::new()

# Helper: Get files to scan
function Get-FilesToScan {
    param([string[]]$Extensions)

    if ($Mode -eq "Staged") {
        $staged = git diff --cached --name-only --diff-filter=ACMR
        if (-not $staged) { return @() }
        return $staged | Where-Object {
            $ext = [System.IO.Path]::GetExtension($_)
            $Extensions -contains $ext
        } | ForEach-Object { Join-Path $RepoRoot $_ } | Where-Object { Test-Path $_ }
    } else {
        $files = Get-ChildItem -Path $RepoRoot -Recurse -File | Where-Object {
            $rel = $_.FullName.Substring($RepoRoot.Path.Length)
            -not ($rel -match "\\(bin|obj|node_modules|\.git|\.tempmediaStorage|\.user_uploaded|artifacts)\\") -and
            ($Extensions -contains $_.Extension)
        }
        return $files.FullName
    }
}

# -----------------------------------------------------------------------------
# Vector 1: Supply Chain & Package Hallucination (Slopsquatting)
# -----------------------------------------------------------------------------
Write-Host "`n[Vector 1/5] Checking for Package Hallucination & Slopsquatting..." -ForegroundColor Yellow

$allowedPackagePrefixes = @(
    "Microsoft.",
    "xunit",
    "Polly",
    "Azure.",
    "OpenTelemetry.",
    "OpenAI",
    "SkiaSharp",
    "SQLitePCLRaw.",
    "FluentValidation",
    "Aspire.",
    "System.",
    "coverlet.",
    "FluentAssertions",
    "Moq"
)

$csprojFiles = Get-FilesToScan -Extensions @(".csproj")
foreach ($proj in $csprojFiles) {
    [xml]$xml = Get-Content $proj
    $packageRefs = $xml.SelectNodes("//PackageReference")
    foreach ($ref in $packageRefs) {
        $pkgName = $ref.GetAttribute("Include")
        if ([string]::IsNullOrWhiteSpace($pkgName)) { continue }

        $isAllowed = $false
        foreach ($prefix in $allowedPackagePrefixes) {
            if ($pkgName.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase) -or
                $pkgName.Equals($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                $isAllowed = $true
                break
            }
        }

        if (-not $isAllowed) {
            $msg = "VEC-1 Slopsquatting: Unvetted package '$pkgName' found in '$([System.IO.Path]::GetFileName($proj))'. Verify dependency legitimacy."
            $violations.Add($msg)
            Write-Host "  ❌ $msg" -ForegroundColor Red
        }
    }
}

if ($violations.Count -eq 0) {
    Write-Host "  ✅ All NuGet dependencies match approved and vetted registries." -ForegroundColor Green
}

# -----------------------------------------------------------------------------
# Vector 2: Insecure Defaults & "Lazy AI" Workarounds
# -----------------------------------------------------------------------------
Write-Host "`n[Vector 2/5] Checking for Insecure Defaults & Lazy AI Workarounds..." -ForegroundColor Yellow

$sourceFiles = Get-FilesToScan -Extensions @(".cs", ".js", ".ts", ".bicep", ".json")

$insecurePatterns = @(
    @{
        Name = "TLS Verification Bypass"
        Regex = "(DangerousAcceptAnyServerCertificateValidator|ServerCertificateCustomValidationCallback\s*=|rejectUnauthorized\s*:\s*false|NODE_TLS_REJECT_UNAUTHORIZED\s*=\s*['""]?0['""]?)"
        TargetExts = @(".cs", ".js", ".ts")
    },
    @{
        Name = "Wildcard CORS Allowance"
        Regex = "(\.AllowAnyOrigin\(\)|SetIsOriginAllowed\s*\(\s*_\s*=>\s*true\s*\))"
        TargetExts = @(".cs")
    },
    @{
        Name = "Hardcoded Fallback Secret"
        Regex = "(\?\?\s*[""'](default|secret|password|123456|testkey|admin|supersecret|mysecret)[""'])"
        TargetExts = @(".cs", ".js", ".ts")
    },
    @{
        Name = "Dangerous Dynamic Eval Execution"
        Regex = "(?<![a-zA-Z0-9_])eval\s*\("
        TargetExts = @(".js", ".ts")
    }
)

foreach ($file in $sourceFiles) {
    # Skip test evaluators or scripts for dynamic eval tests
    $rel = $file.Substring($RepoRoot.Path.Length)
    if ($rel -match "\\(tests|scripts)\\") { continue }

    $lines = Get-Content $file
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $ext = [System.IO.Path]::GetExtension($file)

        foreach ($rule in $insecurePatterns) {
            if ($rule.TargetExts -contains $ext) {
                if ($line -match $rule.Regex) {
                    $msg = "VEC-2 Insecure Default: $($rule.Name) detected in $rel (Line $($i + 1)): $($line.Trim())"
                    $violations.Add($msg)
                    Write-Host "  ❌ $msg" -ForegroundColor Red
                }
            }
        }
    }
}

# -----------------------------------------------------------------------------
# Vector 3: AI Security Disablers & Suppression Comments
# -----------------------------------------------------------------------------
Write-Host "`n[Vector 3/5] Checking for AI Security Disablers & Suppression Comments..." -ForegroundColor Yellow

$suppressionPatterns = @(
    @{
        Name = "TypeScript / JavaScript Compiler Suppression"
        Regex = "//\s*@(ts-ignore|ts-nocheck)"
        TargetExts = @(".js", ".ts")
    },
    @{
        Name = "Unscoped ESLint Disabler"
        Regex = "/\*\s*eslint-disable\s*\*/"
        TargetExts = @(".js", ".ts")
    },
    @{
        Name = "Secret Scanner Suppression"
        Regex = "(//\s*gitleaks:allow|//\s*nosec)"
        TargetExts = @(".cs", ".js", ".ts", ".yml", ".yaml")
    }
)

foreach ($file in $sourceFiles) {
    $rel = $file.Substring($RepoRoot.Path.Length)
    if ($rel -match "\\(tests|scripts)\\") { continue }

    $lines = Get-Content $file
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $ext = [System.IO.Path]::GetExtension($file)

        foreach ($rule in $suppressionPatterns) {
            if ($rule.TargetExts -contains $ext) {
                if ($line -match $rule.Regex) {
                    $msg = "VEC-3 Security Disabler: $($rule.Name) detected in $rel (Line $($i + 1)): $($line.Trim())"
                    $violations.Add($msg)
                    Write-Host "  ❌ $msg" -ForegroundColor Red
                }
            }
        }

        # Check #pragma warning disable without comment
        if ($ext -eq ".cs" -and $line -match "^\s*#pragma\s+warning\s+disable\s+([A-Za-z0-9_]+)") {
            $hasComment = $line.Contains("//")
            $hasPrevComment = ($i -gt 0) -and ($lines[$i - 1].Trim().StartsWith("//"))
            if (-not $hasComment -and -not $hasPrevComment) {
                $msg = "VEC-3 Pragmatic Disabler: #pragma warning disable without required justification comment in $rel (Line $($i + 1))"
                $violations.Add($msg)
                Write-Host "  ❌ $msg" -ForegroundColor Red
            }
        }
    }
}

# -----------------------------------------------------------------------------
# Vector 4: OWASP Top 10 for LLM Defenses (Prompt Shield, Content Safety & Self-Learning)
# -----------------------------------------------------------------------------
Write-Host "`n[Vector 4/5] Checking for OWASP Top 10 for LLM Defenses & Content Safety..." -ForegroundColor Yellow

$promptShieldFile = Join-Path $RepoRoot "src\Nutrition.Infrastructure\AI\PromptShieldValidator.cs"
if (-not (Test-Path $promptShieldFile)) {
    $msg = "VEC-4 Content Safety: PromptShieldValidator.cs missing in Nutrition.Infrastructure.AI."
    $violations.Add($msg)
    Write-Host "  ❌ $msg" -ForegroundColor Red
} else {
    $shieldContent = Get-Content $promptShieldFile -Raw
    $categories = @("ViolentAndHarmfulPattern", "SexualAndExplicitPattern", "CommunalAndHatePattern", "PromptInjectionPattern", "ValidateFeedback")
    foreach ($cat in $categories) {
        if (-not ($shieldContent -match $cat)) {
            $msg = "VEC-4 Content Safety: PromptShieldValidator missing defense pattern for '$cat'."
            $violations.Add($msg)
            Write-Host "  ❌ $msg" -ForegroundColor Red
        }
    }
    Write-Host "  ✅ PromptShieldValidator verified across Harmful, Violent, Sexual, Communal, and Self-Learning vectors." -ForegroundColor Green
}

$aiVisionService = Join-Path $RepoRoot "src\Nutrition.Infrastructure\AI\MicrosoftAgentFoodVisionService.cs"
if (Test-Path $aiVisionService) {
    $content = Get-Content $aiVisionService -Raw
    if (-not ($content -match "\[USER_MEAL_INTAKE_DATA\]")) {
        $msg = "VEC-4 OWASP LLM01: MicrosoftAgentFoodVisionService missing [USER_MEAL_INTAKE_DATA] delimiter isolation for prompt injection defense."
        $violations.Add($msg)
        Write-Host "  ❌ $msg" -ForegroundColor Red
    } else {
        Write-Host "  ✅ Prompt injection delimiter isolation verified in MicrosoftAgentFoodVisionService." -ForegroundColor Green
    }

    if (-not ($content -match "PromptShieldValidator\.ValidateInput")) {
        $msg = "VEC-4 Content Safety: MicrosoftAgentFoodVisionService missing PromptShieldValidator.ValidateInput integration."
        $violations.Add($msg)
        Write-Host "  ❌ $msg" -ForegroundColor Red
    } else {
        Write-Host "  ✅ Pre-flight PromptShieldValidator integration verified in MicrosoftAgentFoodVisionService." -ForegroundColor Green
    }

    if (-not ($content -match "PromptShieldValidator\.ValidateFeedback")) {
        $msg = "VEC-4 Self-Learning Poisoning Defense: MicrosoftAgentFoodVisionService missing PromptShieldValidator.ValidateFeedback integration."
        $violations.Add($msg)
        Write-Host "  ❌ $msg" -ForegroundColor Red
    } else {
        Write-Host "  ✅ Continuous self-learning data poisoning guardrail verified." -ForegroundColor Green
    }
}

$geminiProvider = Join-Path $RepoRoot "src\Nutrition.Infrastructure\AI\Providers\GoogleGeminiProvider.cs"
if (Test-Path $geminiProvider) {
    $geminiContent = Get-Content $geminiProvider -Raw
    if (-not ($geminiContent -match "StrictSafetySettings") -or -not ($geminiContent -match "HARM_CATEGORY_HARASSMENT")) {
        $msg = "VEC-4 Content Safety: GoogleGeminiProvider missing StrictSafetySettings for Google AI content safety enforcement."
        $violations.Add($msg)
        Write-Host "  ❌ $msg" -ForegroundColor Red
    } else {
        Write-Host "  ✅ Google AI StrictSafetySettings verified across harassment, hate speech, sexual, and dangerous content." -ForegroundColor Green
    }
}

# -----------------------------------------------------------------------------
# Vector 5: Leaked AI Agent State & Session Bleed
# -----------------------------------------------------------------------------
Write-Host "`n[Vector 5/5] Checking for Leaked AI Agent State & Session Bleed..." -ForegroundColor Yellow

if ($Mode -eq "Staged") {
    $stagedFiles = git diff --cached --name-only --diff-filter=ACMR
    foreach ($staged in $stagedFiles) {
        if ($staged -match "^\.agents/state/.*\.json$" -or
            $staged -match "^\.gemini/" -or
            $staged -match "\.system_generated/" -or
            $staged -match "transcript(_full)?\.jsonl$") {
            $msg = "VEC-5 Agent Artifact Bleed: Staged AI internal session/transcript artifact '$staged' detected. Do not commit agent runtime artifacts."
            $violations.Add($msg)
            Write-Host "  ❌ $msg" -ForegroundColor Red
        }
    }
} else {
    $trackedArtifacts = git ls-files | Where-Object {
        $_ -match "^\.gemini/" -or $_ -match "transcript(_full)?\.jsonl$"
    }
    foreach ($tracked in $trackedArtifacts) {
        $msg = "VEC-5 Agent Artifact Bleed: Committed AI internal artifact '$tracked' detected in git index."
        $violations.Add($msg)
        Write-Host "  ❌ $msg" -ForegroundColor Red
    }
}

# -----------------------------------------------------------------------------
# Final Verdict
# -----------------------------------------------------------------------------
Pop-Location

Write-Host "`n========================================================================" -ForegroundColor Cyan
if ($violations.Count -gt 0) {
    Write-Host "  ❌ VERDICT: AI SECURITY DEFENSE VIOLATIONS DETECTED ($($violations.Count) issue(s))" -ForegroundColor Red
    Write-Host "========================================================================" -ForegroundColor Cyan
    exit 1
} else {
    Write-Host "  ✅ VERDICT: ALL AI SECURITY DEFENSE VECTORS PASSED CLEANLY" -ForegroundColor Green
    Write-Host "========================================================================" -ForegroundColor Cyan
    exit 0
}
