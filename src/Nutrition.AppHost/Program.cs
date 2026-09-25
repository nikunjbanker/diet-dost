using Nutrition.AppHost.Extensions;

// .NET Aspire Distributed Application Host (NET 11 RC / Aspire.AppHost.Sdk 13.5.4)
// Coordinates distributed components, environment forwarding, and local developer telemetry.
var builder = DistributedApplication.CreateBuilder(args);

// Register Web Gateway (Linear-style PWA, API endpoints & AI Vision subsystem)
builder.AddWebGateway();

builder.Build().Run();
