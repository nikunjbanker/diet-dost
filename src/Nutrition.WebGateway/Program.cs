using Microsoft.EntityFrameworkCore;
using Nutrition.Application.Agents;
using Nutrition.Application.Common;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Domain.Model.Progress;
using Nutrition.Infrastructure.AI;
using Nutrition.Infrastructure.Persistence;

using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using Nutrition.WebGateway.Middleware;

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
        tracing.AddSource(NutritionTelemetry.ServiceName)
               .AddAspNetCoreInstrumentation(options =>
               {
                   options.RecordException = true;
               })
               .AddHttpClientInstrumentation(options =>
               {
                   options.RecordException = true;
               });
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
    var initLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialization");
    try
    {
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

        // Safe SQLite schema migration: query PRAGMA table_info before adding columns to avoid duplicate column errors
        async Task EnsureColumnExistsAsync(string tableName, string columnName, string columnDefinition)
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            // Verify table exists before attempting column inspection/alteration
            var tableExists = false;
            using (var checkTableCmd = connection.CreateCommand())
            {
                checkTableCmd.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{tableName}';";
                var count = Convert.ToInt64(await checkTableCmd.ExecuteScalarAsync());
                tableExists = count > 0;
            }

            if (!tableExists)
            {
                return;
            }

            var columnExists = false;
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
            {
#pragma warning disable EF1002
                await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDefinition};");
#pragma warning restore EF1002
            }
        }

        await EnsureColumnExistsAsync("Meals", "TotalCalories", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync("Meals", "TotalProteinGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync("Meals", "TotalCarbsGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync("Meals", "TotalFatGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync("Meals", "TotalFiberGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync("Meals", "TotalSugarGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync("Meals", "TotalSodiumMg", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync("Meals", "AiFeedbackRating", "TEXT NULL");
        await EnsureColumnExistsAsync("Meals", "AiFeedbackRemarks", "TEXT NULL");
        await EnsureColumnExistsAsync("FoodItems", "OriginalDetection", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnExistsAsync("FoodItems", "FiberGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync("FoodItems", "SugarGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync("Ledgers", "TargetSugarGrams", "REAL NOT NULL DEFAULT 25.0");
        await EnsureColumnExistsAsync("Ledgers", "ConsumedSugarGrams", "REAL NOT NULL DEFAULT 0.0");
        await EnsureColumnExistsAsync("Profiles", "Timezone", "TEXT NOT NULL DEFAULT 'Asia/Kolkata'");

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
            );
            CREATE INDEX IF NOT EXISTS ""IX_ProgressPhotos_UserId_CapturedAtUtc"" ON ""ProgressPhotos"" (""UserId"", ""CapturedAtUtc"");
            CREATE INDEX IF NOT EXISTS ""IX_ProgressPhotos_UserId_PhotoType"" ON ""ProgressPhotos"" (""UserId"", ""PhotoType"");

            CREATE TABLE IF NOT EXISTS ""AiFeedbacks"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_AiFeedbacks"" PRIMARY KEY,
                ""UserId"" TEXT NOT NULL,
                ""MealLogId"" TEXT NULL,
                ""DishName"" TEXT NOT NULL,
                ""DetectedByModel"" TEXT NOT NULL,
                ""ConfidenceScore"" REAL NOT NULL,
                ""Rating"" TEXT NOT NULL,
                ""Remarks"" TEXT NULL,
                ""IdentifiedItemsSummary"" TEXT NULL,
                ""RetrainingTriggered"" INTEGER NOT NULL,
                ""RetrainingOutcome"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ""IX_AiFeedbacks_UserId_CreatedAtUtc"" ON ""AiFeedbacks"" (""UserId"", ""CreatedAtUtc"");
            CREATE INDEX IF NOT EXISTS ""IX_AiFeedbacks_Rating"" ON ""AiFeedbacks"" (""Rating"");
        ");

        initLogger.LogInformation("SQLite database schema verified and initialized successfully with 0 errors.");
    }
    catch (Exception ex)
    {
        initLogger.LogError(ex, "Critical database initialization failure during startup. ErrorType: {ErrorType}, Message: {ErrorMessage}", ex.GetType().Name, ex.Message);
        throw;
    }

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
            Timezone = "Asia/Kolkata",
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
        var userTz = ClinicalDietitianService.GetUserTimeZoneInfo(defaultProfile.Timezone);
        await dietitian.GetOrCreateDailyLedgerAsync(defaultProfile.Id, DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTz)));
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

    if (!await db.Meals.AnyAsync())
    {
        var now = DateTime.UtcNow;
        var sampleMeals = new List<MealLog>
        {
            new MealLog
            {
                UserId = "user-default",
                LoggedAt = now.AddHours(-3),
                MealType = MealType.Lunch,
                DishName = "North Indian Thali (Phulkas, Dal & Bhindi Masala)",
                PhotoUri = "/uploads/meals/sample_thali.jpg",
                OverallConfidenceScore = 0.96,
                IsVerifiedByUser = true,
                DietitianAdvice = "Balanced meal with adequate protein and high-fiber okra. Great portion control under 550 kcal.",
                Items = new List<FoodItemRecord>
                {
                    new() { Name = "Whole Wheat Roti", HindiOrRegionalName = "Phulka / Roti", EstimatedPortion = "2 Phulkas", Quantity = 2, Grams = 60, Calories = 70, ProteinGrams = 2.2, CarbsGrams = 15.0, FatGrams = 0.4, FiberGrams = 2.2, SugarGrams = 0.25, SodiumMg = 5 },
                    new() { Name = "Yellow Moong Dal Tadka", HindiOrRegionalName = "Moong Dal Fry", EstimatedPortion = "1 Katori", Quantity = 1, Grams = 150, Calories = 135, ProteinGrams = 7.2, CarbsGrams = 19.0, FatGrams = 4.5, FiberGrams = 5.2, SugarGrams = 0.8, SodiumMg = 320 },
                    new() { Name = "Bhindi Masala (Okra Subzi)", HindiOrRegionalName = "Bhindi ki Sabzi", EstimatedPortion = "1 Katori", Quantity = 1, Grams = 120, Calories = 115, ProteinGrams = 2.8, CarbsGrams = 9.5, FatGrams = 7.2, FiberGrams = 4.8, SugarGrams = 1.5, SodiumMg = 180 },
                    new() { Name = "Green Salad (Cucumber & Tomato)", HindiOrRegionalName = "Kachumber Salad", EstimatedPortion = "1 Plate", Quantity = 1, Grams = 100, Calories = 30, ProteinGrams = 1.0, CarbsGrams = 6.0, FatGrams = 0.2, FiberGrams = 0.8, SugarGrams = 5.9, SodiumMg = 20 }
                }
            },
            new MealLog
            {
                UserId = "user-default",
                LoggedAt = now.AddHours(-7),
                MealType = MealType.Breakfast,
                DishName = "Kanda Poha with Roasted Peanuts",
                OverallConfidenceScore = 0.92,
                IsVerifiedByUser = true,
                DietitianAdvice = "Wholesome complex carbs. Peanuts provide good monounsaturated fats.",
                Items = new List<FoodItemRecord>
                {
                    new() { Name = "Kanda Poha", HindiOrRegionalName = "Poha", EstimatedPortion = "1 Plate (150g)", Quantity = 1, Grams = 150, Calories = 220, ProteinGrams = 4.5, CarbsGrams = 38.0, FatGrams = 5.5, FiberGrams = 3.2, SugarGrams = 1.8, SodiumMg = 260 },
                    new() { Name = "Roasted Peanuts", HindiOrRegionalName = "Moongphali", EstimatedPortion = "1 Tbsp (15g)", Quantity = 1, Grams = 15, Calories = 85, ProteinGrams = 3.8, CarbsGrams = 2.4, FatGrams = 7.2, FiberGrams = 1.2, SugarGrams = 0.6, SodiumMg = 10 },
                    new() { Name = "Masala Chai (Low Sugar)", HindiOrRegionalName = "Chai", EstimatedPortion = "1 Cup (120ml)", Quantity = 1, Grams = 120, Calories = 65, ProteinGrams = 2.2, CarbsGrams = 8.5, FatGrams = 2.4, FiberGrams = 0.0, SugarGrams = 5.0, SodiumMg = 40 }
                }
            },
            new MealLog
            {
                UserId = "user-default",
                LoggedAt = now.AddDays(-1).Date.AddHours(20),
                MealType = MealType.Dinner,
                DishName = "Palak Paneer with Phulkas",
                OverallConfidenceScore = 0.95,
                IsVerifiedByUser = true,
                DietitianAdvice = "High biological value protein from paneer. Low carb dinner supports optimal insulin sensitivity.",
                Items = new List<FoodItemRecord>
                {
                    new() { Name = "Palak Paneer", HindiOrRegionalName = "Palak Paneer", EstimatedPortion = "1 Katori (150g)", Quantity = 1, Grams = 150, Calories = 220, ProteinGrams = 12.0, CarbsGrams = 8.5, FatGrams = 15.5, FiberGrams = 4.5, SugarGrams = 2.4, SodiumMg = 380 },
                    new() { Name = "Whole Wheat Roti", HindiOrRegionalName = "Phulka", EstimatedPortion = "2 Phulkas", Quantity = 2, Grams = 60, Calories = 70, ProteinGrams = 2.2, CarbsGrams = 15.0, FatGrams = 0.4, FiberGrams = 2.2, SugarGrams = 0.25, SodiumMg = 5 }
                }
            },
            new MealLog
            {
                UserId = "user-default",
                LoggedAt = now.AddDays(-1).Date.AddHours(13),
                MealType = MealType.Lunch,
                DishName = "Rajma Chawal with Kachumber Salad",
                OverallConfidenceScore = 0.94,
                IsVerifiedByUser = true,
                DietitianAdvice = "Classic complementary protein combination. Good prebiotic fiber from red kidney beans.",
                Items = new List<FoodItemRecord>
                {
                    new() { Name = "Rajma Masala", HindiOrRegionalName = "Rajma Gravy", EstimatedPortion = "1 Bowl (200g)", Quantity = 1, Grams = 200, Calories = 210, ProteinGrams = 9.5, CarbsGrams = 32.0, FatGrams = 5.0, FiberGrams = 7.5, SugarGrams = 2.8, SodiumMg = 410 },
                    new() { Name = "Steamed Basmati Rice", HindiOrRegionalName = "Chawal", EstimatedPortion = "1 Cup (150g)", Quantity = 1, Grams = 150, Calories = 195, ProteinGrams = 4.0, CarbsGrams = 42.0, FatGrams = 0.5, FiberGrams = 0.8, SugarGrams = 0.1, SodiumMg = 2 }
                }
            },
            new MealLog
            {
                UserId = "user-default",
                LoggedAt = now.AddDays(-2).Date.AddHours(20).AddMinutes(15),
                MealType = MealType.Dinner,
                DishName = "Moong Dal Khichdi with Curd",
                OverallConfidenceScore = 0.97,
                IsVerifiedByUser = true,
                DietitianAdvice = "Gentle on digestion and gut-friendly with probiotic curd.",
                Items = new List<FoodItemRecord>
                {
                    new() { Name = "Moong Dal Khichdi", HindiOrRegionalName = "Khichdi", EstimatedPortion = "1.5 Bowl (250g)", Quantity = 1, Grams = 250, Calories = 270, ProteinGrams = 9.8, CarbsGrams = 45.0, FatGrams = 5.5, FiberGrams = 5.0, SugarGrams = 1.2, SodiumMg = 340 },
                    new() { Name = "Plain Cow Milk Curd / Dahi", HindiOrRegionalName = "Dahi", EstimatedPortion = "1 Katori (100g)", Quantity = 1, Grams = 100, Calories = 60, ProteinGrams = 3.5, CarbsGrams = 4.5, FatGrams = 3.2, FiberGrams = 0.0, SugarGrams = 4.2, SodiumMg = 38 }
                }
            },
            new MealLog
            {
                UserId = "user-default",
                LoggedAt = now.AddDays(-3).Date.AddHours(17),
                MealType = MealType.Snack,
                DishName = "Roasted Makhana & Green Tea",
                OverallConfidenceScore = 0.95,
                IsVerifiedByUser = true,
                DietitianAdvice = "Low glycemic load evening snack packed with antioxidants and magnesium.",
                Items = new List<FoodItemRecord>
                {
                    new() { Name = "Roasted Foxnuts (Makhana)", HindiOrRegionalName = "Phool Makhana", EstimatedPortion = "1 Bowl (30g)", Quantity = 1, Grams = 30, Calories = 105, ProteinGrams = 3.0, CarbsGrams = 20.0, FatGrams = 1.8, FiberGrams = 2.4, SugarGrams = 0.2, SodiumMg = 85 }
                }
            }
        };

        foreach (var meal in sampleMeals)
        {
            meal.RecalculateTotals();
        }

        db.Meals.AddRange(sampleMeals);
        await db.SaveChangesAsync();
    }
}

app.UseMiddleware<HttpPayloadTelemetryMiddleware>();

app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
