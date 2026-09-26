/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.AppHost.Extensions;

// .NET Aspire Distributed Application Host (NET 11 RC / Aspire.AppHost.Sdk 13.5.4)
// Coordinates distributed components, environment forwarding, and local developer telemetry.
var builder = DistributedApplication.CreateBuilder(args);

// Register Web Gateway (Linear-style PWA, API endpoints & AI Vision subsystem)
builder.AddWebGateway();

builder.Build().Run();
