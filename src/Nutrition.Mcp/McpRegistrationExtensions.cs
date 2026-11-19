/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using Nutrition.Mcp.Security;
using Nutrition.Mcp.Tools;

namespace Nutrition.Mcp;

public static class McpRegistrationExtensions
{
    /// <summary>
    /// Registers Diet-Dost Model Context Protocol (MCP) services, security protectors, and clinical tools.
    /// </summary>
    public static IServiceCollection AddDietDostMcpServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDataProtection();
        services.AddHttpContextAccessor();
        services.AddTransient<ClaimsPrincipal>(sp =>
            sp.GetService<IHttpContextAccessor>()?.HttpContext?.User ?? new ClaimsPrincipal());

        // Register cryptographic token protector for stateless HITL elicitation
        services.AddScoped<IMcpContinuationTokenProtector, McpContinuationTokenProtector>();
        services.AddScoped<MealTools>();
        services.AddScoped<AnalyticsTools>();

        // Register official ASP.NET Core MCP Server with HTTP/SSE transport
        services.AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly(typeof(MealTools).Assembly);

        return services;
    }

    /// <summary>
    /// Registers Diet-Dost Model Context Protocol (MCP) services with Stdio transport for local CLI / Claude Desktop tools.
    /// </summary>
    public static IServiceCollection AddDietDostMcpStdioServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDataProtection();
        services.AddTransient<ClaimsPrincipal>(_ => new ClaimsPrincipal());
        services.AddScoped<IMcpContinuationTokenProtector, McpContinuationTokenProtector>();
        services.AddScoped<MealTools>();
        services.AddScoped<AnalyticsTools>();

        services.AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(MealTools).Assembly);

        return services;
    }

    /// <summary>
    /// Maps the Model Context Protocol endpoint (/mcp) to the ASP.NET Core routing pipeline.
    /// </summary>
    public static IEndpointConventionBuilder MapDietDostMcpEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints.MapMcp("/mcp");
    }
}
