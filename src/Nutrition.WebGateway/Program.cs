using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Nutrition.Application.Agents;
using Nutrition.Application.Common;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Domain.Model.Progress;
using Nutrition.Infrastructure.AI;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Security;

using Polly;
using Polly.RateLimiting;
using System.Threading.RateLimiting;

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
builder.Services.AddSecurityInfrastructure();
builder.Services.AddScoped<ClinicalDietitianService>();

// AI Agent Infrastructure (Microsoft Agent Framework + Google AI Gemini)
builder.Services.AddHttpClient<MicrosoftAgentFoodVisionService>(client =>
{
    // Vision analysis with large images over Gemini can take 30-90s.
    // We allow 120s total so all model fallbacks can complete before timeout.
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddScoped<IFoodVisionAgent, MicrosoftAgentFoodVisionService>();

// Authentication & Dual Scheme Security: JWT Bearer + Cookie Session (OWASP A02, A07)
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "DietDostGateway";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "DietDostClient";
var jwtKey = builder.Configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? JwtTokenService.DefaultDevKey;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "SmartScheme";
    options.DefaultChallengeScheme = "SmartScheme";
})
.AddPolicyScheme("SmartScheme", "JWT Bearer or Cookie Authentication", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }
        return CookieAuthenticationDefaults.AuthenticationScheme;
    };
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception is SecurityTokenExpiredException)
            {
                context.Response.Headers.Append("Token-Expired", "true");
            }
            return Task.CompletedTask;
        }
    };
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = "DietDost.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;

    // Return 401/403 for API endpoints rather than redirecting to HTML login
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole(UserRole.Admin.ToString(), UserRole.SuperAdmin.ToString()));
    options.AddPolicy("RequireSuperAdmin", policy => policy.RequireRole(UserRole.SuperAdmin.ToString()));
    options.AddPolicy("RequireActiveUser", policy => policy.RequireAuthenticatedUser());
});

// Per-IP Partitioned Rate Limiter for Brute-Force Defense (OWASP A04)
// Each unique client IP gets an independent sliding window: max 5 attempts per 15 minutes.
// This replaces the previous global singleton which incorrectly shared one counter across all IPs.
var authPartitionedRateLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    RateLimitPartition.GetSlidingWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString()
                      ?? context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                      ?? "unknown",
        factory: _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            SegmentsPerWindow = 3,
            QueueLimit = 0
        }));

builder.Services.AddSingleton(authPartitionedRateLimiter);

// Keep ResiliencePipeline registered so PollyRateLimitingTests & any DI consumers resolve correctly.
// The middleware now uses the partitioned limiter above instead of this singleton.
var legacyPollyPipeline = new ResiliencePipelineBuilder()
    .AddRateLimiter(new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
    {
        PermitLimit = 5,
        Window = TimeSpan.FromMinutes(15),
        SegmentsPerWindow = 3,
        QueueLimit = 0
    }))
    .Build();
builder.Services.AddSingleton(legacyPollyPipeline);

// CORS policy — dev-mode permissive (same-origin in prod via static file hosting)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
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

            CREATE TABLE IF NOT EXISTS ""Users"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_Users"" PRIMARY KEY,
                ""Email"" TEXT NOT NULL,
                ""NormalizedEmail"" TEXT NOT NULL,
                ""MobileNumber"" TEXT NOT NULL,
                ""NormalizedMobileNumber"" TEXT NOT NULL,
                ""PasswordHash"" TEXT NOT NULL,
                ""SecurityStamp"" TEXT NOT NULL,
                ""Role"" INTEGER NOT NULL,
                ""Tier"" INTEGER NOT NULL,
                ""IsEmailVerified"" INTEGER NOT NULL,
                ""IsMobileVerified"" INTEGER NOT NULL,
                ""IsActive"" INTEGER NOT NULL,
                ""TermsAcceptedAtUtc"" TEXT NULL,
                ""TermsVersionAccepted"" TEXT NULL,
                ""HealthConsentAcceptedAtUtc"" TEXT NULL,
                ""HealthConsentVersionAccepted"" TEXT NULL,
                ""ConsentIpAddress"" TEXT NULL,
                ""ConsentUserAgent"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL,
                ""LastLoginAtUtc"" TEXT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_NormalizedEmail"" ON ""Users"" (""NormalizedEmail"");
            CREATE INDEX IF NOT EXISTS ""IX_Users_NormalizedMobileNumber"" ON ""Users"" (""NormalizedMobileNumber"");
            CREATE INDEX IF NOT EXISTS ""IX_Users_Role"" ON ""Users"" (""Role"");
            CREATE INDEX IF NOT EXISTS ""IX_Users_Tier"" ON ""Users"" (""Tier"");

            CREATE TABLE IF NOT EXISTS ""VerificationOtps"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_VerificationOtps"" PRIMARY KEY,
                ""UserId"" TEXT NOT NULL,
                ""Target"" TEXT NOT NULL,
                ""OtpCodeHash"" TEXT NOT NULL,
                ""Channel"" INTEGER NOT NULL,
                ""ExpiresAtUtc"" TEXT NOT NULL,
                ""AttemptCount"" INTEGER NOT NULL,
                ""IsUsed"" INTEGER NOT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ""IX_VerificationOtps_UserId_Target"" ON ""VerificationOtps"" (""UserId"", ""Target"");
            CREATE INDEX IF NOT EXISTS ""IX_VerificationOtps_Target_Channel_IsUsed"" ON ""VerificationOtps"" (""Target"", ""Channel"", ""IsUsed"");
            CREATE INDEX IF NOT EXISTS ""IX_VerificationOtps_ExpiresAtUtc"" ON ""VerificationOtps"" (""ExpiresAtUtc"");

            CREATE TABLE IF NOT EXISTS ""TierConfigurations"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_TierConfigurations"" PRIMARY KEY,
                ""Tier"" INTEGER NOT NULL,
                ""DailyAiDetectionLimit"" INTEGER NOT NULL,
                ""AllowPhotoCompare"" INTEGER NOT NULL,
                ""AllowDataExport"" INTEGER NOT NULL,
                ""AnalyticsHistoryDays"" INTEGER NOT NULL,
                ""Description"" TEXT NOT NULL,
                ""UpdatedAtUtc"" TEXT NOT NULL,
                ""UpdatedByUserId"" TEXT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_TierConfigurations_Tier"" ON ""TierConfigurations"" (""Tier"");

            CREATE TABLE IF NOT EXISTS ""AiUsageLogs"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_AiUsageLogs"" PRIMARY KEY,
                ""UserId"" TEXT NOT NULL,
                ""OperationType"" INTEGER NOT NULL,
                ""ModelId"" TEXT NOT NULL,
                ""EstimatedTokensUsed"" INTEGER NOT NULL,
                ""LatencyMs"" REAL NOT NULL,
                ""IsSuccess"" INTEGER NOT NULL,
                ""ErrorReason"" TEXT NULL,
                ""TimestampUtc"" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ""IX_AiUsageLogs_UserId_TimestampUtc"" ON ""AiUsageLogs"" (""UserId"", ""TimestampUtc"");
            CREATE INDEX IF NOT EXISTS ""IX_AiUsageLogs_UserId_OperationType"" ON ""AiUsageLogs"" (""UserId"", ""OperationType"");
        ");

        // Seed default Tier Configurations
        if (!await db.TierConfigurations.AnyAsync())
        {
            await db.TierConfigurations.AddRangeAsync(TierFeatureConfiguration.GetDefaultConfigurations());
            await db.SaveChangesAsync();
            initLogger.LogInformation("Default tier configurations seeded successfully.");
        }

        // Initialize / Seed SuperAdmin User
        var superAdminEmail = builder.Configuration["Auth:SuperAdminEmail"]
            ?? Environment.GetEnvironmentVariable("SUPER_ADMIN_EMAIL")
            ?? "admin@dietdost.app";
        var normalizedSuperAdminEmail = ApplicationUser.NormalizeEmailAddress(superAdminEmail);

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var superAdminUser = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedSuperAdminEmail);
        if (superAdminUser == null)
        {
            superAdminUser = new ApplicationUser
            {
                Id = "user-superadmin",
                Email = superAdminEmail,
                NormalizedEmail = normalizedSuperAdminEmail,
                MobileNumber = "+919999999999",
                NormalizedMobileNumber = ApplicationUser.NormalizePhoneNumber("+919999999999"),
                PasswordHash = passwordHasher.HashPassword("SuperAdmin@DietDost2026!"),
                SecurityStamp = Guid.NewGuid().ToString("N"),
                Role = UserRole.SuperAdmin,
                Tier = UserTier.SuperAdmin,
                IsEmailVerified = true,
                IsMobileVerified = true,
                IsActive = true,
                TermsAcceptedAtUtc = DateTime.UtcNow,
                TermsVersionAccepted = builder.Configuration["Auth:TermsVersion"] ?? "v1.0-202609",
                HealthConsentAcceptedAtUtc = DateTime.UtcNow,
                HealthConsentVersionAccepted = builder.Configuration["Auth:HealthConsentVersion"] ?? "v1.0-202609",
                ConsentIpAddress = "127.0.0.1",
                ConsentUserAgent = "SystemBootstrap",
                CreatedAtUtc = DateTime.UtcNow
            };
            await db.Users.AddAsync(superAdminUser);
            await db.SaveChangesAsync();
            initLogger.LogInformation("SuperAdmin account provisioned successfully.");
        }

        // Migrate pre-existing 'user-default' sample records to the SuperAdmin user
        await db.Database.ExecuteSqlRawAsync(@"
            UPDATE ""Profiles"" SET ""Id"" = {0} WHERE ""Id"" = 'user-default';
            UPDATE ""Meals"" SET ""UserId"" = {0} WHERE ""UserId"" = 'user-default';
            UPDATE ""Ledgers"" SET ""UserId"" = {0} WHERE ""UserId"" = 'user-default';
            UPDATE ""ProgressPhotos"" SET ""UserId"" = {0} WHERE ""UserId"" = 'user-default';
            UPDATE ""Corrections"" SET ""UserId"" = {0} WHERE ""UserId"" = 'user-default';
            UPDATE ""AiFeedbacks"" SET ""UserId"" = {0} WHERE ""UserId"" = 'user-default';
        ", superAdminUser.Id);

        initLogger.LogInformation("SQLite database schema verified and initialized successfully with 0 errors.");
    }
    catch (Exception ex)
    {
        initLogger.LogError(ex, "Critical database initialization failure during startup. ErrorType: {ErrorType}, Message: {ErrorMessage}", ex.GetType().Name, ex.Message);
        throw;
    }

    var activeSuperAdmin = await db.Users.FirstAsync(u => u.Role == UserRole.SuperAdmin);

    if (!await db.Profiles.AnyAsync())
    {
        var defaultProfile = new UserProfile
        {
            Id = activeSuperAdmin.Id,
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
                UserId = activeSuperAdmin.Id,
                CapturedAtUtc = baselineDate,
                WeightKg = 85.0,
                PhotoType = ProgressPhotoType.Face,
                PhotoUri = "/uploads/progress/face_baseline.svg",
                IsBaseline = true,
                Notes = "Day 1 Baseline photo before commencing ICMR-NIN deficit protocol."
            },
            new()
            {
                UserId = activeSuperAdmin.Id,
                CapturedAtUtc = currentDate,
                WeightKg = 81.5,
                PhotoType = ProgressPhotoType.Face,
                PhotoUri = "/uploads/progress/face_current.svg",
                IsBaseline = false,
                Notes = "Day 30 check-in: Noticeable jawline definition and facial slimming."
            },
            new()
            {
                UserId = activeSuperAdmin.Id,
                CapturedAtUtc = baselineDate,
                WeightKg = 85.0,
                PhotoType = ProgressPhotoType.FullBodyFront,
                PhotoUri = "/uploads/progress/body_baseline.svg",
                IsBaseline = true,
                Notes = "Day 1 Full Body Front View (Starting Waist: 38 inches)."
            },
            new()
            {
                UserId = activeSuperAdmin.Id,
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
                UserId = activeSuperAdmin.Id,
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
                UserId = activeSuperAdmin.Id,
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
                UserId = activeSuperAdmin.Id,
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
                UserId = activeSuperAdmin.Id,
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
                UserId = activeSuperAdmin.Id,
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
                UserId = activeSuperAdmin.Id,
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

app.UseAuthentication();
app.UseAuthorization();

// Per-IP Rate Limiter Middleware for Auth & Sensitive Endpoints (OWASP A04)
// Each client IP has an independent 5-attempts-per-15-minutes sliding window.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/auth/login") ||
        context.Request.Path.StartsWithSegments("/api/auth/register") ||
        context.Request.Path.StartsWithSegments("/api/auth/verify-otp") ||
        context.Request.Path.StartsWithSegments("/api/auth/resend-otp") ||
        context.Request.Path.StartsWithSegments("/api/auth/token"))
    {
        var rateLimiter = context.RequestServices
            .GetRequiredService<PartitionedRateLimiter<HttpContext>>();
        using var lease = await rateLimiter.AcquireAsync(context, permitCount: 1);
        if (!lease.IsAcquired)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                "{\"error\":\"TooManyRequests\",\"message\":\"Too many authentication attempts from your IP. Please wait before retrying.\"}");
            return;
        }
    }

    await next();
});

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
