/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Xunit;

namespace Nutrition.EvalHarness.Tests;

/// <summary>
/// Validates that Bicep Infrastructure-as-Code (IaC) definitions align with
/// application configuration keys, Options pattern models, and architectural invariants.
/// </summary>
public class BicepIaCConfigurationContractTests
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
    public void AppBicep_Declares_RequiredConfigurationEnvironmentVariables()
    {
        var repoRoot = GetRepoRoot();
        var appBicepPath = Path.Combine(repoRoot, "infra", "app.bicep");
        Assert.True(File.Exists(appBicepPath), $"app.bicep must exist at {appBicepPath}");

        var bicepContent = File.ReadAllText(appBicepPath);

        // Required configuration keys matched to Options pattern
        Assert.Contains("Database__Provider", bicepContent);
        Assert.Contains("ConnectionStrings__DefaultConnection", bicepContent);
        Assert.Contains("Security__AllowDemoUsers", bicepContent);
        Assert.Contains("AI__Google__ApiKey", bicepContent);
        Assert.Contains("Storage__WebRootPath", bicepContent);
        Assert.Contains("ASPNETCORE_URLS", bicepContent);
    }

    [Fact]
    public void AppBicep_Declares_HealthCheck_Probes_For_ACA()
    {
        var repoRoot = GetRepoRoot();
        var appBicepPath = Path.Combine(repoRoot, "infra", "app.bicep");
        var bicepContent = File.ReadAllText(appBicepPath);

        // Probes for liveness and readiness
        Assert.Contains("type: 'Liveness'", bicepContent);
        Assert.Contains("path: '/healthz'", bicepContent);
        Assert.Contains("type: 'Readiness'", bicepContent);
        Assert.Contains("path: '/ready'", bicepContent);
        Assert.Contains("port: 8080", bicepContent);
    }

    [Fact]
    public void AppBicep_Enforces_SingleReplica_And_PersistentMountPath()
    {
        var repoRoot = GetRepoRoot();
        var appBicepPath = Path.Combine(repoRoot, "infra", "app.bicep");
        var bicepContent = File.ReadAllText(appBicepPath);

        // SQLite Golden Rule 1: Single writer invariant
        Assert.Contains("minReplicas: 1", bicepContent);
        Assert.Contains("maxReplicas: 1", bicepContent);

        // Volume mount invariant: /app/data
        Assert.Contains("mountPath: '/app/data'", bicepContent);
        Assert.Contains("Data Source=/app/data/diet_dost.db", bicepContent);
    }

    [Fact]
    public void InfraBicep_Enforces_KeyVault_PurgeProtection()
    {
        var repoRoot = GetRepoRoot();
        var infraBicepPath = Path.Combine(repoRoot, "infra", "infra.bicep");
        Assert.True(File.Exists(infraBicepPath), $"infra.bicep must exist at {infraBicepPath}");

        var bicepContent = File.ReadAllText(infraBicepPath);

        // Sole Authority Security Invariant: Key Vault Purge Protection
        Assert.Contains("enablePurgeProtection: true", bicepContent);
    }

    [Fact]
    public void InfraBicep_Enforces_KeyVault_FirewallRules_And_SubnetServiceEndpoint()
    {
        var repoRoot = GetRepoRoot();
        var infraBicepPath = Path.Combine(repoRoot, "infra", "infra.bicep");
        var bicepContent = File.ReadAllText(infraBicepPath);

        // Checkov CKV_AZURE_109 resolution: Service endpoint in delegated VNet subnet
        Assert.Contains("service: 'Microsoft.KeyVault'", bicepContent);

        // Checkov CKV_AZURE_109 resolution: Key Vault firewall defaultAction Deny + VNet rule
        Assert.Contains("defaultAction: 'Deny'", bicepContent);
        Assert.Contains("bypass: 'AzureServices'", bicepContent);
        Assert.Contains("virtualNetworkRules:", bicepContent);
    }

    [Fact]
    public void Checkov_Configuration_Enforces_SoftFailFalse_And_DevTierExclusions()
    {
        var repoRoot = GetRepoRoot();
        var checkovPath = Path.Combine(repoRoot, ".checkov.yaml");
        Assert.True(File.Exists(checkovPath), $".checkov.yaml must exist at {checkovPath}");

        var checkovContent = File.ReadAllText(checkovPath);
        Assert.Contains("soft-fail: false", checkovContent);
        Assert.Contains("CKV_AZURE_43", checkovContent);
        Assert.Contains("CKV_AZURE_139", checkovContent);
        Assert.Contains("CKV_AZURE_163", checkovContent);
        Assert.Contains("CKV_AZURE_166", checkovContent);
        Assert.Contains("CKV_AZURE_189", checkovContent);
    }
}
