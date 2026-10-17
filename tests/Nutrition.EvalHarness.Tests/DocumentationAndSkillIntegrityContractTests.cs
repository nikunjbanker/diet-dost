/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Text.RegularExpressions;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

/// <summary>
/// Automated Architectural & Contract Tests enforcing Living Documentation & Skill Integrity.
/// Programmatically validates that all local file links in SDDs, CFTs, and Agent Skills resolve
/// to actual existing files on disk, preventing dead documentation links and silent path drift.
/// </summary>
public partial class DocumentationAndSkillIntegrityContractTests
{
    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"(?<!dev/|prod/|staging/|\{env\}/|<env>/)infra/(?:infra|app)\.bicep")]
    private static partial Regex LegacyBicepRegex();

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
    public void All_Markdown_FileLinks_In_SDDs_And_Skills_MustResolveToExistingFiles()
    {
        var repoRoot = GetRepoRoot();
        var searchDirs = new[]
        {
            Path.Combine(repoRoot, "docs", "sdd"),
            Path.Combine(repoRoot, "docs", "cft"),
            Path.Combine(repoRoot, ".agents", "skills"),
            Path.Combine(repoRoot, ".agents", "rules")
        };

        var markdownLinkRegex = MarkdownLinkRegex();
        var deadLinks = new List<string>();

        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;

            var mdFiles = Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories);
            foreach (var mdFile in mdFiles)
            {
                // Skip git worktrees or build artifacts if any
                if (mdFile.Contains(".kilo") || mdFile.Contains("bin") || mdFile.Contains("obj")) continue;

                var content = File.ReadAllText(mdFile);
                var matches = markdownLinkRegex.Matches(content);

                foreach (Match match in matches)
                {
                    var target = match.Groups[2].Value.Trim();

                    // Skip external web links, mailto, anchor-only links
                    if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                        target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                        target.StartsWith('#'))
                    {
                        continue;
                    }

                    // Strip anchors like #section
                    var targetPath = target.Split('#')[0].Trim();
                    if (string.IsNullOrEmpty(targetPath)) continue;

                    string resolvedPath;
                    if (targetPath.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                    {
                        var uriPath = Uri.UnescapeDataString(targetPath.Substring(8).Replace('/', Path.DirectorySeparatorChar));
                        var idx = uriPath.IndexOf("diet-dost" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                        {
                            var relFromRepo = uriPath.Substring(idx + 10);
                            resolvedPath = Path.GetFullPath(Path.Combine(repoRoot, relFromRepo));
                        }
                        else
                        {
                            resolvedPath = Path.GetFullPath(uriPath);
                        }
                    }
                    else
                    {
                        var docDir = Path.GetDirectoryName(mdFile)!;
                        resolvedPath = Path.GetFullPath(Path.Combine(docDir, targetPath.Replace('/', Path.DirectorySeparatorChar)));
                    }

                    if (!File.Exists(resolvedPath) && !Directory.Exists(resolvedPath))
                    {
                        var relDoc = Path.GetRelativePath(repoRoot, mdFile);
                        deadLinks.Add($"[{relDoc}] Target: '{target}' -> Resolved Path Not Found: '{resolvedPath}'");
                    }
                }
            }
        }

        Assert.True(
            deadLinks.Count == 0,
            $"Found {deadLinks.Count} broken/dead file link(s) in documentation or skills:\n" + string.Join("\n", deadLinks));
    }

    [Fact]
    public void No_Unscoped_Legacy_Bicep_Paths_Referenced_In_Living_Documentation()
    {
        var repoRoot = GetRepoRoot();
        var searchDirs = new[]
        {
            Path.Combine(repoRoot, "docs", "sdd"),
            Path.Combine(repoRoot, ".agents", "skills")
        };

        // Regex detecting bare 'infra/infra.bicep' or 'infra/app.bicep' (without environment segment)
        var legacyBicepRegex = LegacyBicepRegex();
        var violations = new List<string>();

        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;

            var mdFiles = Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories);
            foreach (var mdFile in mdFiles)
            {
                // Exclude historical improvement reports or changelogs documenting legacy issues
                var fileName = Path.GetFileName(mdFile);
                if (fileName.Contains("dev-ops_improvement_report") || fileName.Contains("archive")) continue;

                var content = File.ReadAllText(mdFile);
                var matches = legacyBicepRegex.Matches(content);
                if (matches.Count > 0)
                {
                    var relDoc = Path.GetRelativePath(repoRoot, mdFile);
                    violations.Add($"[{relDoc}] contains {matches.Count} un-scoped reference(s) to legacy 'infra/infra.bicep' or 'infra/app.bicep'. Must use 'infra/<env>/' or 'infra/{{env}}/'.");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Detected legacy un-scoped Bicep file paths in living documentation:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Infrastructure_Documentation_And_Skill_Must_Reflect_Current_Bicep_Structure()
    {
        var repoRoot = GetRepoRoot();
        var devInfraBicep = Path.Combine(repoRoot, "infra", "dev", "infra.bicep");
        var devAppBicep = Path.Combine(repoRoot, "infra", "dev", "app.bicep");
        var devMainBicep = Path.Combine(repoRoot, "infra", "dev", "main.bicep");

        Assert.True(File.Exists(devInfraBicep), $"Expected {devInfraBicep} to exist.");
        Assert.True(File.Exists(devAppBicep), $"Expected {devAppBicep} to exist.");
        Assert.True(File.Exists(devMainBicep), $"Expected {devMainBicep} to exist.");

        var sdd05 = Path.Combine(repoRoot, "docs", "sdd", "05_devops_and_infrastructure.md");
        Assert.True(File.Exists(sdd05), $"Expected SDD 05 to exist at {sdd05}");

        var sdd05Content = File.ReadAllText(sdd05);
        Assert.Contains("infra/<env>/infra.bicep", sdd05Content);
        Assert.Contains("snet-aca-infra-${environment}", sdd05Content);

        var azureSkill = Path.Combine(repoRoot, ".agents", "skills", "diet-dost-azure-deployment", "SKILL.md");
        Assert.True(File.Exists(azureSkill), $"Expected Azure deployment skill at {azureSkill}");

        var skillContent = File.ReadAllText(azureSkill);
        Assert.Contains("infra/<env>/infra.bicep", skillContent);
        Assert.Contains("snet-aca-infra-${environment}", skillContent);
    }
}
