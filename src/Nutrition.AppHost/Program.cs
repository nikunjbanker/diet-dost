/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.AppHost.Extensions;

// .NET Aspire Distributed Application Host (NET 11 RC / Aspire.AppHost.Sdk 13.5.4)
// Coordinates distributed components, environment forwarding, local developer telemetry,
// and Azure Container Apps / Azure Storage infrastructure provisioning.
var builder = DistributedApplication.CreateBuilder(args);

// 1. Declare Azure Container Apps Environment deployment target
var acaEnv = builder.AddAzureContainerAppEnvironment("cae-dietdost");

// 2. Declare Azure Storage for persistent SQLite SMB file share
var storage = builder.AddAzureStorage("dietdost-storage");

// 3. Register Web Gateway (Linear-style PWA, API endpoints & AI Vision subsystem)
builder.AddWebGateway();

builder.Build().Run();

