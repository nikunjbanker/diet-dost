using Microsoft.EntityFrameworkCore;
using Nutrition.Application.Agents;
using Nutrition.Application.Common;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Profile;
using Nutrition.Domain.Model.Progress;
using Nutrition.Infrastructure.AI;
using Nutrition.Infrastructure.Persistence;

using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry for Aspire Dashboard observability (logs, traces, metrics)
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddRuntimeInstrumentation();
    })
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation();
    });

if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Services.AddOpenTelemetry().UseOtlpExporter();
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Storage Infrastructure (Swappable SQLite V1 per SDD section 3.1)
builder.Services.AddStorageInfrastructure(builder.Configuration);
builder.Services.AddScoped<ClinicalDietitianService>();

// AI Agent Infrastructure (Microsoft Agent Framework + Google AI Gemini)
builder.Services.AddHttpClient<MicrosoftAgentFoodVisionService>();
builder.Services.AddScoped<IFoodVisionAgent, MicrosoftAgentFoodVisionService>();

// CORS for local development & PWA
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Ensure SQLite database is created and seed initial profile
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS ""Corrections"" (
            ""Id"" TEXT NOT NULL CONSTRAINT ""PK_Corrections"" PRIMARY KEY,
            ""UserId"" TEXT NOT NULL,
            ""OriginalDetectedItem"" TEXT NOT NULL,
            ""CorrectedItemName"" TEXT NOT NULL,
            ""HindiOrRegionalName"" TEXT NOT NULL,
            ""EstimatedPortion"" TEXT NOT NULL,
            ""Calories"" REAL NOT NULL,
            ""ProteinGrams"" REAL NOT NULL,
            ""CarbsGrams"" REAL NOT NULL,
            ""FatGrams"" REAL NOT NULL,
            ""MealType"" TEXT NOT NULL,
            ""CreatedAtUtc"" TEXT NOT NULL,
            ""FrequencyCount"" INTEGER NOT NULL
        );");

    string[] alterColumns = new[]
    {
        "ALTER TABLE \"Meals\" ADD COLUMN \"TotalCalories\" REAL NOT NULL DEFAULT 0;",
        "ALTER TABLE \"Meals\" ADD COLUMN \"TotalProteinGrams\" REAL NOT NULL DEFAULT 0;",
        "ALTER TABLE \"Meals\" ADD COLUMN \"TotalCarbsGrams\" REAL NOT NULL DEFAULT 0;",
        "ALTER TABLE \"Meals\" ADD COLUMN \"TotalFatGrams\" REAL NOT NULL DEFAULT 0;",
        "ALTER TABLE \"Meals\" ADD COLUMN \"TotalFiberGrams\" REAL NOT NULL DEFAULT 0;",
        "ALTER TABLE \"Meals\" ADD COLUMN \"TotalSodiumMg\" REAL NOT NULL DEFAULT 0;",
        "ALTER TABLE \"FoodItems\" ADD COLUMN \"OriginalDetection\" TEXT NOT NULL DEFAULT '';"
    };
    foreach (var sql in alterColumns)
    {
        try { await db.Database.ExecuteSqlRawAsync(sql); } catch { }
    }

    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS ""ProgressPhotos"" (
            ""Id"" TEXT NOT NULL CONSTRAINT ""PK_ProgressPhotos"" PRIMARY KEY,
            ""UserId"" TEXT NOT NULL,
            ""CapturedAtUtc"" TEXT NOT NULL,
            ""WeightKg"" REAL NOT NULL,
            ""PhotoType"" INTEGER NOT NULL,
            ""PhotoUri"" TEXT NOT NULL,
            ""IsBaseline"" INTEGER NOT NULL,
            ""Notes"" TEXT NULL
        );");

    if (!await db.Profiles.AnyAsync())
    {
        var defaultProfile = new UserProfile
        {
            Id = "user-default",
            Name = "Aarav Sharma",
            Sex = BiologicalSex.Male,
            Age = 32,
            HeightCm = 175,
            CurrentWeightKg = 82,
            TargetWeightKg = 72,
            DesiredPaceKgPerWeek = 0.5,
            ActivityLevel = ActivityLevel.Sedentary,
            DietaryPreference = DietaryPreference.LactoVeg,
            RegionalCuisine = "North Indian",
            DiagnosedConditions = new() { "Pre-Diabetes" },
            Medications = new()
            {
                new MedicationEntry { DrugName = "Metformin 500mg", Dosage = "500mg", Frequency = "With Dinner" }
            }
        };
        await db.Profiles.AddAsync(defaultProfile);
        await db.SaveChangesAsync();

        // Also pre-seed today's ledger
        var dietitian = scope.ServiceProvider.GetRequiredService<ClinicalDietitianService>();
        await dietitian.GetOrCreateDailyLedgerAsync(defaultProfile.Id, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    if (!await db.ProgressPhotos.AnyAsync())
    {
        var baselineDate = DateTime.UtcNow.AddDays(-30);
        var currentDate = DateTime.UtcNow;

        var samplePhotos = new List<ProgressPhoto>
        {
            new()
            {
                UserId = "user-default",
                CapturedAtUtc = baselineDate,
                WeightKg = 85.0,
                PhotoType = ProgressPhotoType.Face,
                PhotoUri = "/uploads/progress/face_baseline.svg",
                IsBaseline = true,
                Notes = "Day 1 Baseline photo before commencing ICMR-NIN deficit protocol."
            },
            new()
            {
                UserId = "user-default",
                CapturedAtUtc = currentDate,
                WeightKg = 81.5,
                PhotoType = ProgressPhotoType.Face,
                PhotoUri = "/uploads/progress/face_current.svg",
                IsBaseline = false,
                Notes = "Day 30 check-in: Noticeable jawline definition and facial slimming."
            },
            new()
            {
                UserId = "user-default",
                CapturedAtUtc = baselineDate,
                WeightKg = 85.0,
                PhotoType = ProgressPhotoType.FullBodyFront,
                PhotoUri = "/uploads/progress/body_baseline.svg",
                IsBaseline = true,
                Notes = "Day 1 Full Body Front View (Starting Waist: 38 inches)."
            },
            new()
            {
                UserId = "user-default",
                CapturedAtUtc = currentDate,
                WeightKg = 81.5,
                PhotoType = ProgressPhotoType.FullBodyFront,
                PhotoUri = "/uploads/progress/body_current.svg",
                IsBaseline = false,
                Notes = "Day 30 Full Body Front View: Down 3.5 kg, waist trimmer by 2.5 inches."
            }
        };
        await db.ProgressPhotos.AddRangeAsync(samplePhotos);
        await db.SaveChangesAsync();
    }
}

app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
