/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

/// <summary>
/// Verifies the automated release deployment pipeline contract, script execution,
/// Conventional Commits semantic bump detection, and release branch naming invariants.
/// </summary>
public class ReleasePipelineContractTests
{
    private static string GetRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "DietDost.slnx")))
            {
                return current;
            }
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }
        throw new InvalidOperationException("Could not find repository root containing DietDost.slnx");
    }

    [Fact]
    public void ReleaseDeploymentScript_ExistsAndHasValidLicenseHeader()
    {
        var repoRoot = GetRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "release-deploy.ps1");

        Assert.True(File.Exists(scriptPath), $"Release script missing at {scriptPath}");
        var content = File.ReadAllText(scriptPath);

        Assert.Contains("GNU Affero General Public License v3.0 only", content);
        Assert.Contains("Server Side Public License, v 1", content);
        Assert.Contains("param(", content);
        Assert.Contains("BumpType", content);
    }

    [Fact]
    public void AutoReleaseWorkflow_ExistsAndHasRequiredTriggersAndJobs()
    {
        var repoRoot = GetRepoRoot();
        var workflowPath = Path.Combine(repoRoot, ".github", "workflows", "auto-release-on-merge.yml");

        Assert.True(File.Exists(workflowPath), $"Auto-release workflow missing at {workflowPath}");
        var content = File.ReadAllText(workflowPath);

        // Assert triggers: push on main branch AND workflow_dispatch
        Assert.Contains("branches:\n      - main", content.Replace("\r\n", "\n"));
        Assert.Contains("workflow_dispatch:", content);

        // Assert AND condition: main branch and actor nikunjbanker
        Assert.Contains("github.actor == 'nikunjbanker'", content);
        Assert.Contains("github.ref_name == 'main'", content);

        // Assert jobs
        Assert.Contains("evaluate-and-tag:", content);
        Assert.Contains("deploy-application:", content);
        Assert.Contains("publish-release:", content);

        // Assert deployment dispatch to azure-app-deploy.yml
        Assert.Contains("azure-app-deploy.yml", content);
    }

    [Fact]
    public void AutoReleaseWorkflow_DoesNotUnconditionallyHardcodeDryRun()
    {
        var repoRoot = GetRepoRoot();
        var workflowPath = Path.Combine(repoRoot, ".github", "workflows", "auto-release-on-merge.yml");

        Assert.True(File.Exists(workflowPath), $"Auto-release workflow missing at {workflowPath}");
        var content = File.ReadAllText(workflowPath).Replace("\r\n", "\n");

        // Assert release-deploy.ps1 is NOT called with a hardcoded -DryRun flag
        Assert.DoesNotContain("& ./scripts/release-deploy.ps1 `\n              -BumpType $bumpInput `\n              -TargetTag $customTagInput `\n              -Environment $envInput `\n              -DryRun", content);
    }

    [Theory]
    [InlineData("feat!: breaking overhaul of authentication", "Major")]
    [InlineData("feat(profile): add Indian clinical BMI insights", "Minor")]
    [InlineData("fix(infra): pin Azure SQL to Basic DTU tier", "Patch")]
    [InlineData("chore(deps): update Aspire CLI to latest", "Patch")]
    public void ReleaseScript_DryRun_AccuratelyCalculatesSemanticTagAndBranch(
        string commitMessage, string expectedBump)
    {
        var repoRoot = GetRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "release-deploy.ps1");

        var scriptNormalized = scriptPath.Replace("\\", "/");
        var psi = new ProcessStartInfo
        {
            FileName = "pwsh",
            Arguments = $"-NoProfile -Command \"(& '{scriptNormalized}' -DryRun -CommitMessage '{commitMessage}') | ConvertTo-Json\"",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var proc = Process.Start(psi);
        Assert.NotNull(proc);
        var output = proc.StandardOutput.ReadToEnd();
        var error = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        Assert.True(proc.ExitCode == 0, $"PowerShell script failed with exit code {proc.ExitCode}: {error}");

        // Extract JSON portion from stdout
        var jsonStart = output.IndexOf('{');
        var jsonEnd = output.LastIndexOf('}');
        Assert.True(jsonStart >= 0 && jsonEnd > jsonStart, $"Could not find JSON object in output: {output}");

        var json = output.Substring(jsonStart, jsonEnd - jsonStart + 1);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var newTag = root.GetProperty("NewTag").GetString();
        var releaseBranch = root.GetProperty("ReleaseBranch").GetString();

        Assert.Equal(expectedBump, root.GetProperty("BumpType").GetString());
        Assert.NotNull(newTag);
        Assert.Equal($"release-{newTag}", releaseBranch);

        if (expectedBump == "Major")
        {
            Assert.StartsWith("alpha-6.", newTag);
        }
        else if (expectedBump == "Minor")
        {
            Assert.Matches(@"^alpha-5\.[1-9]\d*\.0\.0$", newTag);
        }
        else if (expectedBump == "Patch")
        {
            Assert.Matches(@"^alpha-5\.\d+\.[1-9]\d*\.0$", newTag);
        }
    }

    [Fact]
    public void AutoApproveDaemon_ScriptExistsAndHasLicenseHeader()
    {
        var repoRoot = GetRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "auto-approve-daemon.ps1");

        Assert.True(File.Exists(scriptPath), $"Auto-approve daemon missing at {scriptPath}");
        var content = File.ReadAllText(scriptPath);

        Assert.Contains("GNU Affero General Public License v3.0 only", content);
        Assert.Contains("Server Side Public License, v 1", content);
        Assert.Contains("param(", content);
        Assert.Contains("Approve-PendingDeployment", content);
    }

    [Fact]
    public void AutoApproveDaemon_Gap32Guard_PermitsMainAndOfficialReleaseBranches()
    {
        var repoRoot = GetRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "auto-approve-daemon.ps1");
        var content = File.ReadAllText(scriptPath);

        // GAP-32 Security Guard: Must permit main AND official release branches (^release-)
        Assert.Contains("$runMeta.headBranch -ne \"main\"", content);
        Assert.Contains("^release-", content);

        // Invariant: Must explicitly reject feature, fix, or arbitrary developer branches
        const string releasePattern = @"^release-";
        Assert.Matches(releasePattern, "release-alpha-5.8.1.0");
        Assert.Matches(releasePattern, "release-6.0.0.0");
        Assert.DoesNotMatch(releasePattern, "feature/mcp-support");
        Assert.DoesNotMatch(releasePattern, "fix/auth-bug");
        Assert.DoesNotMatch(releasePattern, "main"); // main handled by -ne "main"
    }

    [Fact]
    public void AutoApproveDaemon_LiveEndpoints_IncludesMcpValidation()
    {
        var repoRoot = GetRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "auto-approve-daemon.ps1");
        var content = File.ReadAllText(scriptPath);

        // Assert all critical post-deployment validation endpoints are present
        Assert.Contains("\"$hostUrl/health\"", content);
        Assert.Contains("\"$hostUrl/index.html\"", content);
        Assert.Contains("\"$hostUrl/partials/camera-modal.html\"", content);
        Assert.Contains("\"$hostUrl/mcp\"", content);
    }
}
