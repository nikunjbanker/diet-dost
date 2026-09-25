using System.Data;
using Microsoft.EntityFrameworkCore;
using Nutrition.Application.Common;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Domain.Model.Progress;
using Nutrition.Domain.Model.Security;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Security;

namespace Nutrition.WebGateway.Extensions;

/// <summary>
/// Encapsulates SQLite database schema verification, PRAGMA migrations,
/// and deterministic seeding of tier policies and demo accounts.
/// </summary>
public static class DatabaseInitializationExtensions
{

    public static async Task InitializeAndSeedDatabaseAsync(this IApplicationBuilder app, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configuration);

        using var scope = app.ApplicationServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialization");

        try
        {
            await EnsureDatabaseCreatedAndMigratedAsync(db);
            await SeedAppSecretsAsync(db, configuration, logger);
            await SeedTierConfigurationsAsync(db, logger);
            await SeedDemoUsersAsync(db, scope.ServiceProvider, configuration, logger);
            await MigrateLegacyDataAsync(db, logger);

            logger.LogInformation("SQLite database schema verified and initialized successfully with 0 errors.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Critical database initialization failure during startup. ErrorType: {ErrorType}, Message: {ErrorMessage}", ex.GetType().Name, ex.Message);
            throw;
        }
    }

    private static async Task EnsureDatabaseCreatedAndMigratedAsync(DietTrackerDbContext db)
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

        await EnsureColumnExistsAsync(db, "Meals", "TotalCalories", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "TotalProteinGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "TotalCarbsGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "TotalFatGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "TotalFiberGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "TotalSugarGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "TotalSodiumMg", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Meals", "AiFeedbackRating", "TEXT NULL");
        await EnsureColumnExistsAsync(db, "Meals", "AiFeedbackRemarks", "TEXT NULL");
        await EnsureColumnExistsAsync(db, "FoodItems", "OriginalDetection", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnExistsAsync(db, "FoodItems", "FiberGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "FoodItems", "SugarGrams", "REAL NOT NULL DEFAULT 0");
        await EnsureColumnExistsAsync(db, "Ledgers", "TargetSugarGrams", "REAL NOT NULL DEFAULT 25.0");
        await EnsureColumnExistsAsync(db, "Ledgers", "ConsumedSugarGrams", "REAL NOT NULL DEFAULT 0.0");
        await EnsureColumnExistsAsync(db, "Profiles", "Timezone", "TEXT NOT NULL DEFAULT 'Asia/Kolkata'");

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

            CREATE TABLE IF NOT EXISTS ""AppSecrets"" (
                ""Key"" TEXT NOT NULL CONSTRAINT ""PK_AppSecrets"" PRIMARY KEY,
                ""Value"" TEXT NOT NULL,
                ""Description"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL,
                ""UpdatedAtUtc"" TEXT NOT NULL
            );
        ");
    }

    private static async Task EnsureColumnExistsAsync(DietTrackerDbContext db, string tableName, string columnName, string columnDefinition)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

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

    private static async Task SeedAppSecretsAsync(DietTrackerDbContext db, IConfiguration configuration, ILogger logger)
    {
        var defaultSecrets = new List<(string Key, string FallbackValue, string Description)>
        {
            ("Jwt:Key", "DietDost_SecretKey_For_Jwt_HMAC_SHA256_Authentication_2026_Minimum32BytesRequired!", "Cryptographic signing key for JWT HMAC-SHA256 tokens"),
            ("Auth:DemoPassword", "DietDost@Demo2026!", "Deterministic password for seeded demo tier accounts"),
            ("AI:GoogleAI:ApiKey", string.Empty, "Google Gemini Vision API Key"),
            ("AI:AzureOpenAI:ApiKey", string.Empty, "Azure OpenAI API Key")
        };

        foreach (var (key, fallbackValue, description) in defaultSecrets)
        {
            var existing = await db.AppSecrets.FirstOrDefaultAsync(s => s.Key == key);
            if (existing == null)
            {
                var configuredValue = configuration[key];
                var finalValue = !string.IsNullOrWhiteSpace(configuredValue) ? configuredValue : fallbackValue;

                await db.AppSecrets.AddAsync(new AppSecret
                {
                    Key = key,
                    Value = finalValue,
                    Description = description,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
                logger.LogInformation("Seeded database secret into AppSecrets table: {Key}", key);
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedTierConfigurationsAsync(DietTrackerDbContext db, ILogger logger)
    {
        if (!await db.TierConfigurations.AnyAsync())
        {
            await db.TierConfigurations.AddRangeAsync(TierFeatureConfiguration.GetDefaultConfigurations());
            await db.SaveChangesAsync();
            logger.LogInformation("Default tier configurations seeded successfully.");
        }
    }

    private static async Task SeedDemoUsersAsync(
        DietTrackerDbContext db,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger logger)
    {
        var passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher>();
        var dietitian = serviceProvider.GetRequiredService<ClinicalDietitianService>();

        var configuredSuperAdminEmail = configuration["Auth:SuperAdminEmail"]?.Trim();
        var primarySuperAdminEmail = !string.IsNullOrWhiteSpace(configuredSuperAdminEmail)
            ? configuredSuperAdminEmail
            : "superadmin@dietdost.app";

        var demoSpecs = new List<DemoUserSpec>
        {
            new(
                Id: "user-free",
                Email: "free@dietdost.app",
                Name: "Free Tier User (Demo)",
                Mobile: "+919876500001",
                Role: UserRole.User,
                Tier: UserTier.Free,
                Cuisine: "North Indian",
                Conditions: new List<string>(),
                Medications: new List<MedicationEntry>()
            ),
            new(
                Id: "user-basic",
                Email: "basic@dietdost.app",
                Name: "Basic Tier User (Demo)",
                Mobile: "+919876500002",
                Role: UserRole.User,
                Tier: UserTier.Basic,
                Cuisine: "South Indian",
                Conditions: new List<string> { "Hypertension" },
                Medications: new List<MedicationEntry>
                {
                    new() { DrugName = "Telmisartan 40mg", Dosage = "40mg", Frequency = "Morning" }
                }
            ),
            new(
                Id: "user-premium",
                Email: "premium@dietdost.app",
                Name: "Premium Tier User (Demo)",
                Mobile: "+919876500003",
                Role: UserRole.User,
                Tier: UserTier.Premium,
                Cuisine: "Gujarati",
                Conditions: new List<string> { "Pre-Diabetes" },
                Medications: new List<MedicationEntry>
                {
                    new() { DrugName = "Metformin 500mg", Dosage = "500mg", Frequency = "With Dinner" }
                }
            ),
            new(
                Id: "user-admin",
                Email: "admin.demo@dietdost.app",
                Name: "Admin Tier User (Demo)",
                Mobile: "+919876500004",
                Role: UserRole.Admin,
                Tier: UserTier.Premium,
                Cuisine: "Maharashtrian",
                Conditions: new List<string>(),
                Medications: new List<MedicationEntry>()
            ),
            new(
                Id: "user-superadmin",
                Email: primarySuperAdminEmail,
                Name: "SuperAdmin Tier User (Demo)",
                Mobile: "+919999999999",
                Role: UserRole.SuperAdmin,
                Tier: UserTier.SuperAdmin,
                Cuisine: "North Indian",
                Conditions: new List<string> { "Pre-Diabetes" },
                Medications: new List<MedicationEntry>
                {
                    new() { DrugName = "Metformin 500mg", Dosage = "500mg", Frequency = "With Dinner" }
                }
            )
        };

        if (!string.Equals(primarySuperAdminEmail, "admin@dietdost.app", StringComparison.OrdinalIgnoreCase))
        {
            demoSpecs.Add(new(
                Id: "user-superadmin-alias",
                Email: "admin@dietdost.app",
                Name: "SuperAdmin Tier User (Alias)",
                Mobile: "+919999999998",
                Role: UserRole.SuperAdmin,
                Tier: UserTier.SuperAdmin,
                Cuisine: "North Indian",
                Conditions: new List<string> { "Pre-Diabetes" },
                Medications: new List<MedicationEntry>
                {
                    new() { DrugName = "Metformin 500mg", Dosage = "500mg", Frequency = "With Dinner" }
                }
            ));
        }

        var demoPassword = configuration["Auth:DemoPassword"]
            ?? (await db.AppSecrets.Where(s => s.Key == "Auth:DemoPassword").Select(s => s.Value).FirstOrDefaultAsync())
            ?? "DietDost@Demo2026!";

        var userTz = ClinicalDietitianService.GetUserTimeZoneInfo("Asia/Kolkata");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTz));

        foreach (var spec in demoSpecs)
        {
            var normEmail = ApplicationUser.NormalizeEmailAddress(spec.Email);
            var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normEmail);
            if (user == null)
            {
                user = await db.Users.FirstOrDefaultAsync(u => u.Id == spec.Id);
                if (user != null)
                {
                    user.Email = spec.Email;
                    user.NormalizedEmail = normEmail;
                    user.PasswordHash = passwordHasher.HashPassword(demoPassword);
                    user.Role = spec.Role;
                    user.Tier = spec.Tier;
                    user.IsEmailVerified = true;
                    user.IsMobileVerified = true;
                    user.IsActive = true;
                    await db.SaveChangesAsync();
                    logger.LogInformation("Updated demo user email to match configuration: {Email} ({Tier}, {Role})", spec.Email, spec.Tier, spec.Role);
                }
                else
                {
                    user = new ApplicationUser
                    {
                        Id = spec.Id,
                        Email = spec.Email,
                        NormalizedEmail = normEmail,
                        MobileNumber = spec.Mobile,
                        NormalizedMobileNumber = ApplicationUser.NormalizePhoneNumber(spec.Mobile),
                        PasswordHash = passwordHasher.HashPassword(demoPassword),
                        SecurityStamp = Guid.NewGuid().ToString("N"),
                        Role = spec.Role,
                        Tier = spec.Tier,
                        IsEmailVerified = true,
                        IsMobileVerified = true,
                        IsActive = true,
                        TermsAcceptedAtUtc = DateTime.UtcNow,
                        TermsVersionAccepted = configuration["Auth:TermsVersion"] ?? "v1.0-202609",
                        HealthConsentAcceptedAtUtc = DateTime.UtcNow,
                        HealthConsentVersionAccepted = configuration["Auth:HealthConsentVersion"] ?? "v1.0-202609",
                        ConsentIpAddress = "127.0.0.1",
                        ConsentUserAgent = "SystemBootstrap",
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    await db.Users.AddAsync(user);
                    await db.SaveChangesAsync();
                    logger.LogInformation("Provisioned demo user: {Email} ({Tier}, {Role})", spec.Email, spec.Tier, spec.Role);
                }
            }
            else
            {
                user.PasswordHash = passwordHasher.HashPassword(demoPassword);
                user.Role = spec.Role;
                user.Tier = spec.Tier;
                user.IsEmailVerified = true;
                user.IsMobileVerified = true;
                user.IsActive = true;
                await db.SaveChangesAsync();
            }

            var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == user.Id);
            if (profile == null)
            {
                profile = new UserProfile
                {
                    Id = user.Id,
                    Name = spec.Name,
                    Sex = BiologicalSex.Male,
                    Age = 32,
                    HeightCm = 175,
                    CurrentWeightKg = 80,
                    TargetWeightKg = 72,
                    DesiredPaceKgPerWeek = 0.5,
                    ActivityLevel = ActivityLevel.Sedentary,
                    DietaryPreference = DietaryPreference.LactoVeg,
                    RegionalCuisine = spec.Cuisine,
                    Timezone = "Asia/Kolkata",
                    DiagnosedConditions = spec.Conditions,
                    Medications = spec.Medications
                };
                await db.Profiles.AddAsync(profile);
                await db.SaveChangesAsync();
            }
            else
            {
                profile.Name = spec.Name;
                profile.RegionalCuisine = spec.Cuisine;
                await db.SaveChangesAsync();
            }

            await dietitian.GetOrCreateDailyLedgerAsync(user.Id, today);

            // Seed sample progress comparison photos if none exist
            if (!await db.ProgressPhotos.AnyAsync(p => p.UserId == user.Id))
            {
                var baselineDate = DateTime.UtcNow.AddDays(-30);
                var currentDate = DateTime.UtcNow;

                var samplePhotos = new List<ProgressPhoto>
                {
                    new()
                    {
                        UserId = user.Id,
                        CapturedAtUtc = baselineDate,
                        WeightKg = 85.0,
                        PhotoType = ProgressPhotoType.Face,
                        PhotoUri = "/uploads/progress/face_baseline.svg",
                        IsBaseline = true,
                        Notes = "Day 1 Baseline photo before commencing ICMR-NIN protocol."
                    },
                    new()
                    {
                        UserId = user.Id,
                        CapturedAtUtc = currentDate,
                        WeightKg = 81.5,
                        PhotoType = ProgressPhotoType.Face,
                        PhotoUri = "/uploads/progress/face_current.svg",
                        IsBaseline = false,
                        Notes = "Day 30 check-in: Noticeable jawline definition and facial slimming."
                    },
                    new()
                    {
                        UserId = user.Id,
                        CapturedAtUtc = baselineDate,
                        WeightKg = 85.0,
                        PhotoType = ProgressPhotoType.FullBodyFront,
                        PhotoUri = "/uploads/progress/body_baseline.svg",
                        IsBaseline = true,
                        Notes = "Day 1 Full Body Front View."
                    },
                    new()
                    {
                        UserId = user.Id,
                        CapturedAtUtc = currentDate,
                        WeightKg = 81.5,
                        PhotoType = ProgressPhotoType.FullBodyFront,
                        PhotoUri = "/uploads/progress/body_current.svg",
                        IsBaseline = false,
                        Notes = "Day 30 Full Body Front View: Down 3.5 kg."
                    }
                };
                await db.ProgressPhotos.AddRangeAsync(samplePhotos);
                await db.SaveChangesAsync();
            }

            // Seed representative Indian meals for rich dashboard view
            if (!await db.Meals.AnyAsync(m => m.UserId == user.Id))
            {
                var now = DateTime.UtcNow;
                var sampleMeals = new List<MealLog>
                {
                    new()
                    {
                        UserId = user.Id,
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
                    new()
                    {
                        UserId = user.Id,
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
                    new()
                    {
                        UserId = user.Id,
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
                    new()
                    {
                        UserId = user.Id,
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
    }

    private static async Task MigrateLegacyDataAsync(DietTrackerDbContext db, ILogger logger)
    {
        var activeSuperAdmin = await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.SuperAdmin);
        if (activeSuperAdmin != null)
        {
            await db.Database.ExecuteSqlRawAsync(@"
                UPDATE ""Profiles"" SET ""Id"" = {0} WHERE ""Id"" = 'user-default';
                UPDATE ""Meals"" SET ""UserId"" = {0} WHERE ""UserId"" = 'user-default';
                UPDATE ""Ledgers"" SET ""UserId"" = {0} WHERE ""UserId"" = 'user-default';
                UPDATE ""ProgressPhotos"" SET ""UserId"" = {0} WHERE ""UserId"" = 'user-default';
                UPDATE ""Corrections"" SET ""UserId"" = {0} WHERE ""UserId"" = 'user-default';
                UPDATE ""AiFeedbacks"" SET ""UserId"" = {0} WHERE ""UserId"" = 'user-default';
            ", activeSuperAdmin.Id);
        }
    }

    private sealed record DemoUserSpec(
        string Id,
        string Email,
        string Name,
        string Mobile,
        UserRole Role,
        UserTier Tier,
        string Cuisine,
        List<string> Conditions,
        List<MedicationEntry> Medications);
}
