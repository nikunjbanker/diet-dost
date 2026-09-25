using Nutrition.Infrastructure.Configuration;
using Nutrition.WebGateway.Extensions;

// ====================================================================================
// Diet-Dost Web Gateway — Program Entrypoint
// Architecture: Clean Architecture, Domain-Driven Design (DDD) & OWASP ASVS Governance
// Target: .NET 11 RC Standalone Aspire Host
// ====================================================================================

var builder = WebApplication.CreateBuilder(args);

// 0. Load Application Secrets from Database Table into IConfiguration Hierarchy
var dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=diettracker.db";
builder.Configuration.AddDatabaseSecrets(dbConnectionString);

// 1. Core Framework & Distributed Telemetry (Aspire / OpenTelemetry)
builder.Services.AddAppTelemetry(builder.Logging, builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 2. Application Domain Services & Storage Infrastructure
builder.Services.AddAppServices(builder.Configuration);

// 3. Security, Authentication & Rate Limiting (OWASP ASVS & A04)
builder.Services.AddAppSecurityAndAuth(builder.Configuration);
builder.Services.AddAppCors();
builder.Services.AddAppRateLimiting(builder.Environment);

var app = builder.Build();

// 4. Database Schema Verification, PRAGMA Migration & Demo Seeding
await app.InitializeAndSeedDatabaseAsync(builder.Configuration);

// 5. HTTP Middleware Pipeline
app.UseAppMiddlewarePipeline();

app.Run();
