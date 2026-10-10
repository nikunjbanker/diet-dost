/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Nutrition.WebGateway.Middleware;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class SecurityEnvironmentAndHeaderTests
{
    private class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Nutrition.WebGateway";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public async Task HostGating_Production_PermitsDevDietDostIn()
    {
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var middleware = new HostGatingMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }, env);

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("dev.diet-dost.in");
        context.Request.Path = "/api/test";

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task HostGating_Production_PermitsDietDostIn()
    {
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var middleware = new HostGatingMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }, env);

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("diet-dost.in");
        context.Request.Path = "/api/test";

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("evil.attacker.com")]
    [InlineData("app-dietdost-web.azurecontainerapps.io")]
    [InlineData("unauthorized.diet-dost.in")]
    [InlineData("192.168.1.100")]
    public async Task HostGating_Production_BlocksUnauthorizedHost_With403Forbidden(string unauthorizedHost)
    {
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var middleware = new HostGatingMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }, env);

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(unauthorizedHost);
        context.Request.Path = "/api/test";
        var memStream = new MemoryStream();
        context.Response.Body = memStream;

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        memStream.Position = 0;
        using var reader = new StreamReader(memStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        Assert.Contains("ForbiddenHost", body);
        Assert.Contains("dev.diet-dost.in", body);
    }

    [Fact]
    public async Task HostGating_Production_AllowsHealthProbes_FromAnyHost()
    {
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var middleware = new HostGatingMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }, env);

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("10.0.0.5");
        context.Request.Path = "/health";

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task HostGating_Development_AllowsAnyHost()
    {
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };
        var middleware = new HostGatingMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }, env);

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("any-host.local");
        context.Request.Path = "/api/test";

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task SecurityHeaders_AppendsAllOwaspTop10Headers()
    {
        var middleware = new SecurityHeadersMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        await middleware.InvokeAsync(context);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"]);
        Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"]);
        Assert.Contains("geolocation=()", context.Response.Headers["Permissions-Policy"].ToString());
        Assert.Contains("default-src 'self'", context.Response.Headers["Content-Security-Policy"].ToString());
        Assert.Contains("dev.diet-dost.in", context.Response.Headers["Content-Security-Policy"].ToString());
    }

    [Fact]
    public void DeploymentWorkflows_MustBeStrictlyRestrictedToNikunjBanker()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var appWorkflowPath = Path.Combine(repoRoot, ".github", "workflows", "azure-app-deploy.yml");
        var infraWorkflowPath = Path.Combine(repoRoot, ".github", "workflows", "azure-infra-deploy.yml");

        Assert.True(File.Exists(appWorkflowPath), $"Expected workflow file at {appWorkflowPath}");
        Assert.True(File.Exists(infraWorkflowPath), $"Expected workflow file at {infraWorkflowPath}");

        var appContent = File.ReadAllText(appWorkflowPath);
        var infraContent = File.ReadAllText(infraWorkflowPath);

        Assert.Contains("if: github.actor == 'nikunjbanker'", appContent);
        Assert.Contains("if: github.actor == 'nikunjbanker'", infraContent);
    }

    [Fact]
    public void CodeOwners_MustStrictlyProtectWorkflowsDirectory()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var codeownersPath = Path.Combine(repoRoot, ".github", "CODEOWNERS");

        Assert.True(File.Exists(codeownersPath), $"Expected CODEOWNERS file at {codeownersPath}");

        var codeownersContent = File.ReadAllText(codeownersPath);

        Assert.Contains("/.github/   @nikunjbanker", codeownersContent);
        Assert.Contains("/.github/workflows/                             @nikunjbanker", codeownersContent);
        Assert.Contains("/.github/workflows/azure-app-deploy.yml         @nikunjbanker", codeownersContent);
        Assert.Contains("/.github/workflows/azure-infra-deploy.yml        @nikunjbanker", codeownersContent);
        Assert.Contains("/.github/workflows/security-scan.yml             @nikunjbanker", codeownersContent);
        Assert.Contains("* @nikunjbanker", codeownersContent);
    }

    [Fact]
    public void SecretAndEnvironmentVariableGovernance_MustBeStrictlyRestrictedToNikunjBanker()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var appWorkflowPath = Path.Combine(repoRoot, ".github", "workflows", "azure-app-deploy.yml");
        var infraWorkflowPath = Path.Combine(repoRoot, ".github", "workflows", "azure-infra-deploy.yml");
        var agentsMdPath = Path.Combine(repoRoot, "AGENTS.md");

        Assert.True(File.Exists(appWorkflowPath), $"Expected workflow file at {appWorkflowPath}");
        Assert.True(File.Exists(infraWorkflowPath), $"Expected workflow file at {infraWorkflowPath}");
        Assert.True(File.Exists(agentsMdPath), $"Expected AGENTS.md at {agentsMdPath}");

        var appContent = File.ReadAllText(appWorkflowPath);
        var infraContent = File.ReadAllText(infraWorkflowPath);
        var agentsContent = File.ReadAllText(agentsMdPath);

        // Authoritative job-level actor guard asserting @nikunjbanker across all deployment pipelines
        Assert.Contains("if: github.actor == 'nikunjbanker'", appContent);
        Assert.Contains("if: github.actor == 'nikunjbanker'", infraContent);

        // AGENTS.md rule 17 governance assertion
        Assert.Contains("Mandatory Secret & Environment Variable Governance Rule (Sole Authority: @nikunjbanker)", agentsContent);
        Assert.Contains("Strictly and exclusively `@nikunjbanker`", agentsContent);
    }

    [Fact]
    public void DeploymentWorkflows_MustGateOnSecurityScan_AndPinActionCommitHashes()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var appWorkflowPath = Path.Combine(repoRoot, ".github", "workflows", "azure-app-deploy.yml");
        var infraWorkflowPath = Path.Combine(repoRoot, ".github", "workflows", "azure-infra-deploy.yml");
        var secScanWorkflowPath = Path.Combine(repoRoot, ".github", "workflows", "security-scan.yml");

        Assert.True(File.Exists(appWorkflowPath), $"Expected workflow file at {appWorkflowPath}");
        Assert.True(File.Exists(infraWorkflowPath), $"Expected workflow file at {infraWorkflowPath}");
        Assert.True(File.Exists(secScanWorkflowPath), $"Expected workflow file at {secScanWorkflowPath}");

        var appContent = File.ReadAllText(appWorkflowPath);
        var infraContent = File.ReadAllText(infraWorkflowPath);
        var secScanContent = File.ReadAllText(secScanWorkflowPath);

        // Pre-deployment and pre-provisioning security-scan gating assertion
        Assert.Contains("uses: ./.github/workflows/security-scan.yml", appContent);
        Assert.Contains("needs: pre-deployment-security-scan", appContent);
        Assert.Contains("uses: ./.github/workflows/security-scan.yml", infraContent);
        Assert.Contains("needs: pre-provisioning-security-scan", infraContent);

        // Security gate summary PR comment assertion
        Assert.Contains("gh pr comment", secScanContent);
        Assert.Contains("pull-requests: write", secScanContent);

        // Supply chain security assertion: All actions in security-scan must be pinned to 40-character commit SHAs
        Assert.Contains("actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683", secScanContent);
        Assert.Contains("actions/setup-dotnet@87b7050bc53ea08284295505d98d2aa94301e852", secScanContent);
        Assert.Contains("github/codeql-action/upload-sarif@cf12ceefebab2c867b9c35d7f907e059681d37ab", secScanContent);
        Assert.Contains("TRIVY_VERSION=", secScanContent);
        Assert.Contains("trivy fs", secScanContent);
        Assert.Contains("bridgecrewio/checkov-action@b406dfe80a3d33640d5b5deb4341f2475cc016a0", secScanContent);
        Assert.Contains("Execute SecurityCodeScan Analysis (All Projects)", secScanContent);

        // AGENTS.md Rule 18 governance assertion
        var agentsMdPath = Path.Combine(repoRoot, "AGENTS.md");
        Assert.True(File.Exists(agentsMdPath));
        var agentsContent = File.ReadAllText(agentsMdPath);
        Assert.Contains("Immutable Security-Scan Pipeline & Mandatory Pre-Execution Security Gating Rule", agentsContent);
        Assert.Contains("PERMANENT AND IMMUTABLE", agentsContent);
    }
}

