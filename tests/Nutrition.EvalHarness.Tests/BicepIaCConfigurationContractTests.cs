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

    private static string GetAppBicepPath(string environment = "dev")
    {
        var repoRoot = GetRepoRoot();
        return Path.Combine(repoRoot, "infra", environment, "app.bicep");
    }

    private static string GetInfraBicepPath(string environment = "dev")
    {
        var repoRoot = GetRepoRoot();
        return Path.Combine(repoRoot, "infra", environment, "infra.bicep");
    }

    private static string GetDocumentDbBicepPath(string environment = "dev")
    {
        var repoRoot = GetRepoRoot();
        return Path.Combine(repoRoot, "infra", environment, "documentdb.bicep");
    }

    [Fact]
    public void AppBicep_Declares_RequiredConfigurationEnvironmentVariables()
    {
        var appBicepPath = GetAppBicepPath("dev");
        Assert.True(File.Exists(appBicepPath), $"app.bicep must exist at {appBicepPath}");

        var bicepContent = File.ReadAllText(appBicepPath);

        // Required configuration keys matched to Options pattern
        Assert.Contains("Database__Provider", bicepContent);
        Assert.Contains("Database__JournalMode", bicepContent);
        Assert.Contains("ConnectionStrings__DefaultConnection", bicepContent);
        Assert.Contains("Security__AllowDemoUsers", bicepContent);
        Assert.Contains("AI__Google__ApiKey", bicepContent);
        Assert.Contains("Storage__WebRootPath", bicepContent);
        Assert.Contains("ASPNETCORE_URLS", bicepContent);
    }

    [Fact]
    public void AppBicep_Declares_HealthCheck_Probes_For_ACA()
    {
        var appBicepPath = GetAppBicepPath("dev");
        var bicepContent = File.ReadAllText(appBicepPath);

        // Probes for startup, liveness, and readiness
        Assert.Contains("type: 'Startup'", bicepContent);
        Assert.Contains("type: 'Liveness'", bicepContent);
        Assert.Contains("path: '/healthz'", bicepContent);
        Assert.Contains("type: 'Readiness'", bicepContent);
        Assert.Contains("path: '/ready'", bicepContent);
        Assert.Contains("port: 8080", bicepContent);
    }

    [Fact]
    public void AppBicep_Enforces_SingleReplica_And_PersistentMountPath()
    {
        var appBicepPath = GetAppBicepPath("dev");
        var bicepContent = File.ReadAllText(appBicepPath);

        // SQLite Golden Rule 1: Single writer invariant & Scale-to-zero dev free tier grant
        Assert.True(
            bicepContent.Contains("minReplicas: minReplicas") || bicepContent.Contains("minReplicas: 1"),
            "app.bicep must bind minReplicas to scale block");
        Assert.True(
            bicepContent.Contains("maxReplicas: maxReplicas") || bicepContent.Contains("maxReplicas: 1"),
            "app.bicep must bind maxReplicas to scale block");
        Assert.Contains("param maxReplicas int = 1", bicepContent);

        // Volume mount invariant: /app/data
        Assert.Contains("mountPath: '/app/data'", bicepContent);
        Assert.Contains("Data Source=/app/data/diet_dost.db", bicepContent);
    }

    [Fact]
    public void InfraBicep_Enforces_KeyVault_PurgeProtection()
    {
        var infraBicepPath = GetInfraBicepPath("dev");
        Assert.True(File.Exists(infraBicepPath), $"infra.bicep must exist at {infraBicepPath}");

        var bicepContent = File.ReadAllText(infraBicepPath);

        // Sole Authority Security Invariant: Key Vault Purge Protection
        Assert.Contains("enablePurgeProtection: true", bicepContent);
    }

    [Fact]
    public void InfraBicep_Enforces_KeyVault_FirewallRules_And_SubnetServiceEndpoint()
    {
        var infraBicepPath = GetInfraBicepPath("dev");
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

    [Fact]
    public void InfraBicep_Enforces_StorageAccount_Security_And_SubnetServiceEndpoint()
    {
        var infraBicepPath = GetInfraBicepPath("dev");
        var bicepContent = File.ReadAllText(infraBicepPath);

        // Storage account firewall invariants: zero public access + VNet service endpoint
        Assert.Contains("supportsHttpsTrafficOnly: true", bicepContent);
        Assert.Contains("allowBlobPublicAccess: false", bicepContent);
        Assert.Contains("defaultAction: 'Deny'", bicepContent);
        Assert.Contains("service: 'Microsoft.Storage'", bicepContent);

        // NSG rule permitting SMB outbound only to Storage service tag
        Assert.Contains("Allow-Storage-SMB-Outbound", bicepContent);
        Assert.Contains("destinationPortRange: '445'", bicepContent);
        Assert.Contains("destinationAddressPrefix: 'Storage'", bicepContent);

        // ACA environment storage mount invariant
        Assert.Contains("Microsoft.App/managedEnvironments/storages", bicepContent);
        Assert.Contains("accessMode: 'ReadWrite'", bicepContent);
    }

    [Fact]
    public void BicepIaC_Enforces_Environment_Isolation_And_Resource_Naming()
    {
        var infraBicepPath = GetInfraBicepPath("dev");
        var infraContent = File.ReadAllText(infraBicepPath);

        // Assert all resources utilize environment in name to guarantee zero conflict in prod
        Assert.Contains("param environment string = 'dev'", infraContent);
        Assert.Contains("snet-aca-infra", infraContent);
        Assert.Contains("stgdost${environment}", infraContent);
        Assert.Contains("crdost${environment}", infraContent);
        Assert.Contains("dietdost-data-${environment}", infraContent);
        Assert.Contains("kvdost${environment}", infraContent);
        Assert.Contains("vnet-dietdost-${environment}", infraContent);
        Assert.Contains("cae-dietdost-${environment}", infraContent);

        var appBicepPath = GetAppBicepPath("dev");
        var appContent = File.ReadAllText(appBicepPath);
        Assert.Contains("param environment string = 'dev'", appContent);
        Assert.Contains("app-dietdost-web-${environment}", appContent);
        Assert.Contains("${environment}.diet-dost.in", appContent);
    }

    [Fact]
    public void InfraBicep_Enforces_ApplicationInsights_DisableLocalAuth_And_MonitoringPublisherRole()
    {
        var infraBicepPath = GetInfraBicepPath("dev");
        var infraContent = File.ReadAllText(infraBicepPath);

        // Workspace-based Application Insights
        Assert.Contains("Microsoft.Insights/components@2020-02-02", infraContent);
        Assert.Contains("WorkspaceResourceId: logAnalytics.id", infraContent);

        // Security baseline: Local auth disabled (Checkov CKV_AZURE_224)
        Assert.Contains("DisableLocalAuth: true", infraContent);

        // Entra ID RBAC: Monitoring Metrics Publisher role (3913510d-42f4-4e42-8a64-420c390055eb)
        Assert.Contains("3913510d-42f4-4e42-8a64-420c390055eb", infraContent);
        Assert.Contains("appiMonitoringPublisherRole", infraContent);

        // Outputs
        Assert.Contains("output appInsightsName string = appInsights.name", infraContent);
        Assert.Contains("output appInsightsConnectionString string = appInsights.properties.ConnectionString", infraContent);
    }

    [Fact]
    public void AppBicep_Declares_ApplicationInsights_Configuration()
    {
        var appBicepPath = GetAppBicepPath("dev");
        var appContent = File.ReadAllText(appBicepPath);

        Assert.Contains("param appInsightsConnectionString string", appContent);
        Assert.Contains("app-insights-connection-string", appContent);
        Assert.Contains("APPLICATIONINSIGHTS_CONNECTION_STRING", appContent);
    }

    [Fact]
    public void DocumentDbBicep_Declares_FreeTier_Cluster_And_KeyVault_Secret()
    {
        var docDbBicepPath = GetDocumentDbBicepPath("dev");
        Assert.True(File.Exists(docDbBicepPath), $"documentdb.bicep must exist at {docDbBicepPath}");

        var bicepContent = File.ReadAllText(docDbBicepPath);

        // Azure DocumentDB (Mongo vCore Free Tier - 32 GB dedicated storage)
        Assert.Contains("Microsoft.DocumentDB/mongoClusters@2024-07-01", bicepContent);
        Assert.Contains("tier: 'Free'", bicepContent);
        Assert.Contains("sizeGb: 32", bicepContent);
        Assert.Contains("serverVersion: '6.0'", bicepContent);
        Assert.Contains("documentdb-connection-string", bicepContent);
    }

    [Fact]
    public void InfraBicep_Declares_DocumentDb_Outbound_NSG_Rules_And_Module()
    {
        var infraBicepPath = GetInfraBicepPath("dev");
        var infraContent = File.ReadAllText(infraBicepPath);

        // Outbound NSG rule for DocumentDB / AzureCosmosDB ports 10255 and 27017
        Assert.Contains("Allow-DocumentDB-Outbound", infraContent);
        Assert.Contains("'10255'", infraContent);
        Assert.Contains("'27017'", infraContent);
        Assert.Contains("destinationAddressPrefix: 'AzureCosmosDB'", infraContent);

        // DocumentDB module invocation and outputs
        Assert.Contains("module documentdb 'documentdb.bicep'", infraContent);
        Assert.Contains("output documentDbClusterFqdn string", infraContent);
    }

    [Fact]
    public void DocumentDbBicep_ZeroTrust_FirewallRules_Reject_Wildcard_By_Default_And_Allow_ContainerEgress_And_DeveloperIp()
    {
        var docDbBicepPath = GetDocumentDbBicepPath("dev");
        var bicepContent = File.ReadAllText(docDbBicepPath);

        // Zero-Trust Rule: AllowAzureServices (0.0.0.0) MUST NOT be unconditional
        Assert.DoesNotContain("name: 'AllowAzureServices'\n  properties:\n    startIpAddress: '0.0.0.0'", bicepContent.Replace("\r\n", "\n"));
        Assert.Contains("param allowAllAzureServices bool = false", bicepContent);
        Assert.Contains("resource allowAzureServices 'Microsoft.DocumentDB/mongoClusters/firewallRules@2024-07-01' = if (allowAllAzureServices)", bicepContent);

        // Dedicated Container App Environment egress IP rule
        Assert.Contains("param containerAppOutboundIp string = ''", bicepContent);
        Assert.Contains("resource allowContainerAppOutboundIp 'Microsoft.DocumentDB/mongoClusters/firewallRules@2024-07-01' = if (!empty(containerAppOutboundIp))", bicepContent);
        Assert.Contains("startIpAddress: containerAppOutboundIp", bicepContent);
        Assert.Contains("endIpAddress: containerAppOutboundIp", bicepContent);

        // Developer workstation client IP rule
        Assert.Contains("resource allowDeveloperIp 'Microsoft.DocumentDB/mongoClusters/firewallRules@2024-07-01' = if (!empty(allowedClientIp))", bicepContent);
        Assert.Contains("startIpAddress: allowedClientIp", bicepContent);
        Assert.Contains("endIpAddress: allowedClientIp", bicepContent);

        // Standard DocumentDB / MongoDB protocol port
        Assert.Contains("output databasePort int = 27017", bicepContent);
    }

    [Fact]
    public void DocumentDbBicep_Enforces_FreeTier_Sharding_And_HighAvailability_SchemaCompliance()
    {
        var docDbBicepPath = GetDocumentDbBicepPath("dev");
        var bicepContent = File.ReadAllText(docDbBicepPath);

        // Required ARM 2024-07-01 properties for mongoClusters
        Assert.Contains("tier: 'Free'", bicepContent);
        Assert.Contains("sizeGb: 32", bicepContent);
        Assert.Contains("sharding:", bicepContent);
        Assert.Contains("shardCount: 1", bicepContent);
        Assert.Contains("highAvailability:", bicepContent);
        Assert.Contains("targetMode: 'Disabled'", bicepContent);
    }

    [Fact]
    public void InfraBicep_Enforces_PortSegregation_Between_Sql_And_DocumentDb()
    {
        var infraBicepPath = GetInfraBicepPath("dev");
        var infraContent = File.ReadAllText(infraBicepPath);

        // Subnet service endpoints must declare Microsoft.Sql for Azure SQL virtual network rules
        Assert.Contains("service: 'Microsoft.Sql'", infraContent);

        // DocumentDB (vCore mongoClusters) does NOT use VNet service endpoints; it secures traffic
        // via dedicated container environment egress IP firewall rules (AllowContainerAppOutboundIp)
        // Assert that Microsoft.AzureCosmosDB is NOT declared in subnet service endpoints to avoid Cosmos DB conflation
        Assert.DoesNotContain("service: 'Microsoft.AzureCosmosDB'", infraContent);

        // SQL uses port 1433 and redirect 11000-11999; DocumentDB uses port 27017 and 10255
        Assert.Contains("'1433'", infraContent);
        Assert.Contains("'11000-11999'", infraContent);
        Assert.Contains("'27017'", infraContent);
        Assert.Contains("'10255'", infraContent);

        // DocumentDB module must receive ACA static outbound IP and enable Azure Services egress
        Assert.Contains("containerAppOutboundIp: acaEnvironment.properties.staticIp", infraContent);
        Assert.Contains("allowAllAzureServices: true", infraContent);

        // Disjoint port outputs
        Assert.Contains("output sqlPort int", infraContent);
        Assert.Contains("output documentDbPort int", infraContent);
    }

    [Fact]
    public void AppBicep_Declares_DocumentDb_Connection_Configuration()
    {
        var appBicepPath = GetAppBicepPath("dev");
        var appContent = File.ReadAllText(appBicepPath);

        Assert.Contains("param documentDbConnectionString string", appContent);
        Assert.Contains("documentdb-connection-string", appContent);
        Assert.Contains("ConnectionStrings__DocumentDbConnection", appContent);
    }
}
