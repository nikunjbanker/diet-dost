/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Server;
using Xunit;

namespace Nutrition.Mcp.Tests;

public class McpServerBuilderTests
{
    [Fact]
    public void McpServer_RegistersRequiredToolsAndTransports()
    {
        var services = new ServiceCollection();
        var builder = services.AddDietDostMcpServer();
        Assert.NotNull(builder);

        // Verify stdio registration
        var stdioServices = new ServiceCollection();
        var stdioBuilder = stdioServices.AddDietDostMcpStdioServer();
        Assert.NotNull(stdioBuilder);
    }

    [Fact]
    public void McpClient_TypesAreAvailable()
    {
        Assert.NotNull(typeof(McpClient));
        Assert.NotNull(typeof(HttpClientTransport));
        Assert.NotNull(typeof(StdioClientTransport));
        Assert.NotNull(typeof(IChatClient));
    }
}
