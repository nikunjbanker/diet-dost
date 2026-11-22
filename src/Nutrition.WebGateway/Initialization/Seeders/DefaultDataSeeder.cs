/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Configuration;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Options;
using Nutrition.Application.Services;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Domain.Model.Progress;
using Nutrition.Domain.Model.Security;
using Nutrition.Infrastructure.Security;
using Nutrition.WebGateway.Initialization.Abstractions;

namespace Nutrition.WebGateway.Initialization.Seeders;

/// <summary>
/// Authoritative unified data seeder operating across repository abstractions (IRepository and IUnitOfWork).
/// Eliminates code duplication across SQLite, Azure SQL, and Azure DocumentDB.
/// Seeds tier configurations, root SuperAdmin, 5-tier demo accounts, Indian diet sample meals,
/// progress photos, and anonymous recovery-enabled guest accounts.
/// </summary>
public sealed class DefaultDataSeeder : IDatabaseSeeder
{
    private readonly IRepository<TierFeatureConfiguration> _tierRepo;
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IRepository<UserProfile> _profileRepo;
    private readonly IRepository<MealLog> _mealRepo;
    private readonly IRepository<ProgressPhoto> _photoRepo;
    private readonly IRepository<AppSecret> _secretRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ClinicalDietitianService _dietitian;
    private readonly IConfiguration _configuration;
    private readonly IAppEnvironment _appEnvironment;
    private readonly ILogger<DefaultDataSeeder> _logger;

    public int Order => 20;

    public DefaultDataSeeder(
        IRepository<TierFeatureConfiguration> tierRepo,
        IRepository<ApplicationUser> userRepo,
        IRepository<UserProfile> profileRepo,
        IRepository<MealLog> mealRepo,
        IRepository<ProgressPhoto> photoRepo,
        IRepository<AppSecret> secretRepo,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ClinicalDietitianService dietitian,
        IConfiguration configuration,
        IAppEnvironment appEnvironment,
        ILogger<DefaultDataSeeder> logger)
    {
        _tierRepo = tierRepo ?? throw new ArgumentNullException(nameof(tierRepo));
        _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
        _profileRepo = profileRepo ?? throw new ArgumentNullException(nameof(profileRepo));
        _mealRepo = mealRepo ?? throw new ArgumentNullException(nameof(mealRepo));
        _photoRepo = photoRepo ?? throw new ArgumentNullException(nameof(photoRepo));
        _secretRepo = secretRepo ?? throw new ArgumentNullException(nameof(secretRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _dietitian = dietitian ?? throw new ArgumentNullException(nameof(dietitian));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _appEnvironment = appEnvironment ?? throw new ArgumentNullException(nameof(appEnvironment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedTierConfigurationsAsync(cancellationToken);
        await EnsureSuperAdminAccountAsync(cancellationToken);

        if (_appEnvironment.AllowsDemoUsers)
        {
            await SeedDemoUsersAsync(cancellationToken);
        }
        else
        {
            await DeactivateDemoUsersInReleaseModeAsync(cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedTierConfigurationsAsync(CancellationToken cancellationToken)
    {
        var existingCount = await _tierRepo.CountAsync(_ => true, cancellationToken);
        if (existingCount == 0)
        {
            foreach (var config in TierFeatureConfiguration.GetDefaultConfigurations())
            {
                await _tierRepo.AddAsync(config, cancellationToken);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Default tier configurations seeded successfully.");
            }
        }
    }

    private async Task EnsureSuperAdminAccountAsync(CancellationToken cancellationToken)
    {
        var authOptions = _configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        var primarySuperAdminEmail = !string.IsNullOrWhiteSpace(authOptions.SuperAdminEmail)
            ? authOptions.SuperAdminEmail.Trim()
            : (!string.IsNullOrWhiteSpace(_configuration["SUPER_ADMIN_EMAIL"])
                ? _configuration["SUPER_ADMIN_EMAIL"]!.Trim()
                : "nikunj_banker@hotmail.com");

        var superAdminPassword = !string.IsNullOrWhiteSpace(authOptions.SuperAdminPassword)
            ? authOptions.SuperAdminPassword
            : (!string.IsNullOrWhiteSpace(_configuration["SUPER_ADMIN_PASSWORD"])
                ? _configuration["SUPER_ADMIN_PASSWORD"]!
                : (!string.IsNullOrWhiteSpace(authOptions.DemoPassword)
                    ? authOptions.DemoPassword
                    : "DietDost@Demo2026!"));

        var normalizedEmail = ApplicationUser.NormalizeEmailAddress(primarySuperAdminEmail);
        var superAdmin = await _userRepo.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (superAdmin == null)
        {
            superAdmin = await _userRepo.FirstOrDefaultAsync(u => u.Id == "superadmin-root", cancellationToken);
        }

        if (superAdmin == null)
        {
            var deletedUsers = await _userRepo.FindDeletedAsync(u => u.NormalizedEmail == normalizedEmail || u.Id == "superadmin-root", cancellationToken);
            superAdmin = deletedUsers.FirstOrDefault();
        }

        if (superAdmin == null)
        {
            superAdmin = new ApplicationUser
            {
                Id = "superadmin-root",
                Email = primarySuperAdminEmail,
                NormalizedEmail = normalizedEmail,
                MobileNumber = "+919999999990",
                NormalizedMobileNumber = ApplicationUser.NormalizePhoneNumber("+919999999990"),
                PasswordHash = _passwordHasher.HashPassword(superAdminPassword),
                SecurityStamp = Guid.NewGuid().ToString("N"),
                Role = UserRole.SuperAdmin,
                Tier = UserTier.SuperAdmin,
                IsEmailVerified = true,
                IsMobileVerified = true,
                IsActive = true,
                IsOnboarded = true,
                TermsAcceptedAtUtc = DateTime.UtcNow,
                TermsVersionAccepted = !string.IsNullOrWhiteSpace(authOptions.TermsVersion) ? authOptions.TermsVersion : "v1.0-202609",
                HealthConsentAcceptedAtUtc = DateTime.UtcNow,
                HealthConsentVersionAccepted = !string.IsNullOrWhiteSpace(authOptions.HealthConsentVersion) ? authOptions.HealthConsentVersion : "v1.0-202609",
                ConsentIpAddress = "127.0.0.1",
                ConsentUserAgent = "SystemBootstrap",
                CreatedAtUtc = DateTime.UtcNow
            };
            await _userRepo.AddAsync(superAdmin, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("[SECURITY] Autonomous bootstrap completed: Created root SuperAdmin account for {Email} ({Tier}, {Role}).", superAdmin.Email, superAdmin.Tier, superAdmin.Role);
            }
        }
        else
        {
            superAdmin.Email = primarySuperAdminEmail;
            superAdmin.NormalizedEmail = normalizedEmail;
            superAdmin.Role = UserRole.SuperAdmin;
            superAdmin.Tier = UserTier.SuperAdmin;
            superAdmin.IsEmailVerified = true;
            superAdmin.IsMobileVerified = true;
            superAdmin.MobileNumber = "+919999999990";
            superAdmin.NormalizedMobileNumber = ApplicationUser.NormalizePhoneNumber("+919999999990");
            superAdmin.IsActive = true;
            superAdmin.IsOnboarded = true;
            superAdmin.IsDeleted = false;
            superAdmin.DeletedAtUtc = null;
            if (string.IsNullOrWhiteSpace(superAdmin.PasswordHash))
            {
                superAdmin.PasswordHash = _passwordHasher.HashPassword(superAdminPassword);
            }
            await _userRepo.UpdateAsync(superAdmin, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("[SECURITY] Autonomous synchronization completed: Reconciled root SuperAdmin account for {Email} ({Tier}, {Role}).", superAdmin.Email, superAdmin.Tier, superAdmin.Role);
            }
        }

        var profile = await _profileRepo.FirstOrDefaultAsync(p => p.Id == superAdmin.Id, cancellationToken);
        if (profile == null)
        {
            var deletedProfiles = await _profileRepo.FindDeletedAsync(p => p.Id == superAdmin.Id, cancellationToken);
            profile = deletedProfiles.FirstOrDefault();
        }

        if (profile == null)
        {
            profile = new UserProfile
            {
                Id = superAdmin.Id,
                Name = "Nikunj Banker (SuperAdmin)",
                Sex = BiologicalSex.Male,
                Age = 35,
                HeightCm = 175,
                CurrentWeightKg = 75,
                TargetWeightKg = 70,
                DesiredPaceKgPerWeek = 0.5,
                ActivityLevel = ActivityLevel.Moderate,
                DietaryPreference = DietaryPreference.LactoVeg,
                RegionalCuisine = "North Indian",
                Timezone = "Asia/Kolkata",
                IsOnboarded = true
            };
            await _profileRepo.AddAsync(profile, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else
        {
            profile.IsDeleted = false;
            profile.DeletedAtUtc = null;
            profile.IsOnboarded = true;
            await _profileRepo.UpdateAsync(profile, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var userTz = ClinicalDietitianService.GetUserTimeZoneInfo("Asia/Kolkata");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTz));
        await _dietitian.GetOrCreateDailyLedgerAsync(superAdmin.Id, today);
    }

    private async Task SeedDemoUsersAsync(CancellationToken cancellationToken)
    {
        var authOptions = _configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        var primarySuperAdminEmail = !string.IsNullOrWhiteSpace(authOptions.SuperAdminEmail)
            ? authOptions.SuperAdminEmail.Trim()
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
            )
        };

        if (_appEnvironment.AllowsAdminDemoUsers)
        {
            demoSpecs.Add(new(
                Id: "user-admin",
                Email: "admin.demo@dietdost.app",
                Name: "Admin Tier User (Demo)",
                Mobile: "+919876500004",
                Role: UserRole.Admin,
                Tier: UserTier.Premium,
                Cuisine: "Maharashtrian",
                Conditions: new List<string>(),
                Medications: new List<MedicationEntry>()
            ));

            if (!string.Equals(primarySuperAdminEmail, "superadmin@dietdost.app", StringComparison.OrdinalIgnoreCase))
            {
                demoSpecs.Add(new(
                    Id: "user-superadmin-demo",
                    Email: "superadmin@dietdost.app",
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
                ));
            }
        }
        else
        {
            _logger.LogInformation("[SECURITY] Released environment detected: Admin demo user seeding is suppressed. Only Free, Basic, and Premium demo users are seeded.");
            await DeactivatePrivilegedDemoUsersAsync(cancellationToken);
        }

        var demoPasswordSecret = await _secretRepo.FirstOrDefaultAsync(s => s.Key == "Auth:DemoPassword", cancellationToken);
        var demoPassword = !string.IsNullOrWhiteSpace(authOptions.DemoPassword)
            ? authOptions.DemoPassword
            : demoPasswordSecret?.Value ?? "DietDost@Demo2026!";

        var userTz = ClinicalDietitianService.GetUserTimeZoneInfo("Asia/Kolkata");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTz));

        foreach (var spec in demoSpecs)
        {
            var normEmail = ApplicationUser.NormalizeEmailAddress(spec.Email);
            var user = await _userRepo.FirstOrDefaultAsync(u => u.NormalizedEmail == normEmail, cancellationToken);

            if (user == null)
            {
                user = await _userRepo.FirstOrDefaultAsync(u => u.Id == spec.Id, cancellationToken);
            }

            if (user == null)
            {
                var deletedUsers = await _userRepo.FindDeletedAsync(u => u.NormalizedEmail == normEmail || u.Id == spec.Id, cancellationToken);
                user = deletedUsers.FirstOrDefault();
            }

            if (user == null)
            {
                user = new ApplicationUser
                {
                    Id = spec.Id,
                    Email = spec.Email,
                    NormalizedEmail = normEmail,
                    MobileNumber = spec.Mobile,
                    NormalizedMobileNumber = ApplicationUser.NormalizePhoneNumber(spec.Mobile),
                    PasswordHash = _passwordHasher.HashPassword(demoPassword),
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    Role = spec.Role,
                    Tier = spec.Tier,
                    IsEmailVerified = true,
                    IsMobileVerified = true,
                    IsActive = true,
                    IsOnboarded = true,
                    TermsAcceptedAtUtc = DateTime.UtcNow,
                    TermsVersionAccepted = !string.IsNullOrWhiteSpace(authOptions.TermsVersion) ? authOptions.TermsVersion : "v1.0-202609",
                    HealthConsentAcceptedAtUtc = DateTime.UtcNow,
                    HealthConsentVersionAccepted = !string.IsNullOrWhiteSpace(authOptions.HealthConsentVersion) ? authOptions.HealthConsentVersion : "v1.0-202609",
                    ConsentIpAddress = "127.0.0.1",
                    ConsentUserAgent = "SystemBootstrap",
                    CreatedAtUtc = DateTime.UtcNow
                };
                await _userRepo.AddAsync(user, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Provisioned demo user: {Email} ({Tier}, {Role})", spec.Email, spec.Tier, spec.Role);
                }
            }
            else
            {
                user.Email = spec.Email;
                user.NormalizedEmail = normEmail;
                user.PasswordHash = _passwordHasher.HashPassword(demoPassword);
                user.Role = spec.Role;
                user.Tier = spec.Tier;
                user.IsEmailVerified = true;
                user.IsMobileVerified = true;
                user.IsActive = true;
                user.IsOnboarded = true;
                user.IsDeleted = false;
                user.DeletedAtUtc = null;
                await _userRepo.UpdateAsync(user, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var profile = await _profileRepo.FirstOrDefaultAsync(p => p.Id == user.Id, cancellationToken);
            if (profile == null)
            {
                var deletedProfiles = await _profileRepo.FindDeletedAsync(p => p.Id == user.Id, cancellationToken);
                profile = deletedProfiles.FirstOrDefault();
            }

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
                    Medications = spec.Medications,
                    IsOnboarded = true
                };
                await _profileRepo.AddAsync(profile, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            else
            {
                profile.IsDeleted = false;
                profile.DeletedAtUtc = null;
                profile.IsOnboarded = true;
                profile.Name = spec.Name;
                profile.RegionalCuisine = spec.Cuisine;
                await _profileRepo.UpdateAsync(profile, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            await _dietitian.GetOrCreateDailyLedgerAsync(user.Id, today);

            // Seed sample progress photos
            var existingPhotosCount = await _photoRepo.CountAsync(p => p.UserId == user.Id, cancellationToken);
            if (existingPhotosCount == 0)
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

                foreach (var photo in samplePhotos)
                {
                    await _photoRepo.AddAsync(photo, cancellationToken);
                }
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // Seed sample meals
            var existingMealsCount = await _mealRepo.CountAsync(m => m.UserId == user.Id, cancellationToken);
            if (existingMealsCount == 0)
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
                        PhotoUri = "/uploads/meals/sample_lunch_thali.jpg",
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
                    await _mealRepo.AddAsync(meal, cancellationToken);
                }
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // Seed demo anonymous zero-PII user (usr_87c369c1e3f8 / "Nikunj QR")
        var demoMnemonic = "suspect vintage animal morning require leader genre youth very home trust jump";
        Bip39Mnemonic.ValidateMnemonic(demoMnemonic, out var masterEntropy);
        var derivedLookupId = RecoveryKit.DeriveLookupId(masterEntropy);
        var derivedSaltBytes = Convert.FromHexString(string.Concat("a1b2c3d4", "e5f67890", "12345678", "9abcdef0"));
        var derivedHashHex = RecoveryKit.ComputeKeyHash(masterEntropy, derivedSaltBytes);
        var derivedSaltHex = Convert.ToHexString(derivedSaltBytes).ToLowerInvariant();

        var existingAnon = await _userRepo.FirstOrDefaultAsync(u => u.Id == "usr_87c369c1e3f8" || u.RecoveryLookupId == derivedLookupId, cancellationToken);
        if (existingAnon == null)
        {
            var deletedAnons = await _userRepo.FindDeletedAsync(u => u.Id == "usr_87c369c1e3f8" || u.RecoveryLookupId == derivedLookupId, cancellationToken);
            existingAnon = deletedAnons.FirstOrDefault();
        }

        if (existingAnon == null)
        {
            var anonUser = new ApplicationUser
            {
                Id = "usr_87c369c1e3f8",
                Email = "usr_87c369c1e3f8@dietdost.local",
                NormalizedEmail = "USR_87C369C1E3F8@DIETDOST.LOCAL",
                DisplayName = "Nikunj QR",
                MobileNumber = "ANONYMOUS",
                NormalizedMobileNumber = "ANONYMOUS",
                IsAnonymous = true,
                Role = UserRole.User,
                Tier = UserTier.Free,
                IsEmailVerified = true,
                IsMobileVerified = false,
                PasswordHash = "ANONYMOUS_NO_PASSWORD",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAtUtc = DateTime.UtcNow,
                TermsAcceptedAtUtc = DateTime.UtcNow,
                TermsVersionAccepted = "v1.0-202609",
                HealthConsentAcceptedAtUtc = DateTime.UtcNow,
                HealthConsentVersionAccepted = "v1.0-202609",
                ConsentIpAddress = "127.0.0.1",
                ConsentUserAgent = "DietDost-Seed/1.0",
                RecoveryLookupId = derivedLookupId,
                RecoverySalt = derivedSaltHex,
                RecoveryKeyHash = derivedHashHex,
                RecoveryKeyCreatedAtUtc = DateTime.UtcNow,
                IsOnboarded = true
            };
            await _userRepo.AddAsync(anonUser, cancellationToken);

            var anonProfile = await _profileRepo.FirstOrDefaultAsync(p => p.Id == anonUser.Id, cancellationToken);
            if (anonProfile == null)
            {
                var deletedProfiles = await _profileRepo.FindDeletedAsync(p => p.Id == anonUser.Id, cancellationToken);
                anonProfile = deletedProfiles.FirstOrDefault();
            }

            if (anonProfile == null)
            {
                anonProfile = new UserProfile
                {
                    Id = anonUser.Id,
                    Name = "Nikunj QR",
                    Sex = BiologicalSex.Male,
                    Age = 35,
                    HeightCm = 175,
                    CurrentWeightKg = 78.0,
                    TargetWeightKg = 72.0,
                    DesiredPaceKgPerWeek = 0.5,
                    ActivityLevel = ActivityLevel.Sedentary,
                    DietaryPreference = DietaryPreference.LactoVeg,
                    RegionalCuisine = "Gujarati",
                    Timezone = "Asia/Kolkata",
                    DiagnosedConditions = new List<string>(),
                    Medications = new List<MedicationEntry>(),
                    IsOnboarded = true
                };
                await _profileRepo.AddAsync(anonProfile, cancellationToken);
            }
            else
            {
                anonProfile.IsDeleted = false;
                anonProfile.DeletedAtUtc = null;
                anonProfile.Name = "Nikunj QR";
                anonProfile.IsOnboarded = true;
                await _profileRepo.UpdateAsync(anonProfile, cancellationToken);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("[SEED] Seeded demo zero-PII user 'usr_87c369c1e3f8' ('Nikunj QR') with permanent recovery kit.");
            }
        }
        else
        {
            if (existingAnon.IsDeleted)
            {
                existingAnon.IsDeleted = false;
                existingAnon.DeletedAtUtc = null;
                existingAnon.IsActive = true;
                existingAnon.IsOnboarded = true;
            }

            if (existingAnon.RecoveryLookupId != derivedLookupId ||
                existingAnon.RecoveryKeyHash != derivedHashHex ||
                existingAnon.RecoverySalt != derivedSaltHex)
            {
                existingAnon.RecoveryLookupId = derivedLookupId;
                existingAnon.RecoveryKeyHash = derivedHashHex;
                existingAnon.RecoverySalt = derivedSaltHex;
                existingAnon.RecoveryKeyCreatedAtUtc = DateTime.UtcNow;
            }

            var anonProfile = await _profileRepo.FirstOrDefaultAsync(p => p.Id == existingAnon.Id, cancellationToken);
            if (anonProfile == null)
            {
                var deletedProfiles = await _profileRepo.FindDeletedAsync(p => p.Id == existingAnon.Id, cancellationToken);
                anonProfile = deletedProfiles.FirstOrDefault();
            }

            if (anonProfile == null)
            {
                anonProfile = new UserProfile
                {
                    Id = existingAnon.Id,
                    Name = "Nikunj QR",
                    Sex = BiologicalSex.Male,
                    Age = 35,
                    HeightCm = 175,
                    CurrentWeightKg = 78.0,
                    TargetWeightKg = 72.0,
                    DesiredPaceKgPerWeek = 0.5,
                    ActivityLevel = ActivityLevel.Sedentary,
                    DietaryPreference = DietaryPreference.LactoVeg,
                    RegionalCuisine = "Gujarati",
                    Timezone = "Asia/Kolkata",
                    DiagnosedConditions = new List<string>(),
                    Medications = new List<MedicationEntry>(),
                    IsOnboarded = true
                };
                await _profileRepo.AddAsync(anonProfile, cancellationToken);
            }
            else
            {
                anonProfile.IsDeleted = false;
                anonProfile.DeletedAtUtc = null;
                anonProfile.Name = "Nikunj QR";
                anonProfile.IsOnboarded = true;
                await _profileRepo.UpdateAsync(anonProfile, cancellationToken);
            }

            await _userRepo.UpdateAsync(existingAnon, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Ensure anonymous user has sample Poha breakfast
        var anonMealsCount = await _mealRepo.CountAsync(m => m.UserId == "usr_87c369c1e3f8", cancellationToken);
        if (anonMealsCount == 0)
        {
            var anonBreakfast = new MealLog
            {
                UserId = "usr_87c369c1e3f8",
                LoggedAt = DateTime.UtcNow.Date.AddHours(8).AddMinutes(30),
                MealType = MealType.Breakfast,
                DishName = "Kanda Poha with Peanuts",
                PhotoUri = "/uploads/meals/sample_poha.jpg",
                OverallConfidenceScore = 0.94,
                IsVerifiedByUser = true,
                DietitianAdvice = "Balanced breakfast with complex carbohydrates, healthy groundnut fats, and fresh coriander.",
                Items = new List<FoodItemRecord>
                {
                    new()
                    {
                        Name = "Flattened Rice (Poha)",
                        HindiOrRegionalName = "Poha",
                        EstimatedPortion = "1 plate (150g)",
                        Quantity = 1,
                        Grams = 150,
                        Calories = 220,
                        ProteinGrams = 4.2,
                        CarbsGrams = 38.0,
                        FatGrams = 5.5,
                        FiberGrams = 3.1,
                        SugarGrams = 1.8,
                        SodiumMg = 260
                    },
                    new()
                    {
                        Name = "Roasted Peanuts",
                        HindiOrRegionalName = "Moongphali",
                        EstimatedPortion = "1 Tbsp (15g)",
                        Quantity = 1,
                        Grams = 15,
                        Calories = 85,
                        ProteinGrams = 3.8,
                        CarbsGrams = 2.4,
                        FatGrams = 7.2,
                        FiberGrams = 1.2,
                        SugarGrams = 0.6,
                        SodiumMg = 10
                    }
                }
            };
            anonBreakfast.RecalculateTotals();
            await _mealRepo.AddAsync(anonBreakfast, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task DeactivateDemoUsersInReleaseModeAsync(CancellationToken cancellationToken)
    {
        var allUsers = await _userRepo.GetAllAsync(cancellationToken);
        var demoUsers = allUsers
            .Where(u => ApplicationUser.DemoEmails.Contains(u.Email) ||
                        u.Id.StartsWith("user-free") ||
                        u.Id.StartsWith("user-basic") ||
                        u.Id.StartsWith("user-premium") ||
                        u.Id.StartsWith("user-admin") ||
                        u.Id.StartsWith("user-superadmin"))
            .ToList();

        if (demoUsers.Count > 0)
        {
            foreach (var user in demoUsers)
            {
                user.IsActive = false;
                user.SecurityStamp = Guid.NewGuid().ToString("N");
                await _userRepo.UpdateAsync(user, cancellationToken);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("[SECURITY] Deactivated {Count} demo user account(s) and revoked security stamps to guarantee zero data breach exposure in Release mode.", demoUsers.Count);
        }
    }

    private async Task DeactivatePrivilegedDemoUsersAsync(CancellationToken cancellationToken)
    {
        var allUsers = await _userRepo.GetAllAsync(cancellationToken);
        var privilegedDemoUsers = allUsers
            .Where(u => ApplicationUser.PrivilegedDemoEmails.Contains(u.Email) ||
                        u.Id.StartsWith("user-admin"))
            .ToList();

        if (privilegedDemoUsers.Count > 0)
        {
            foreach (var user in privilegedDemoUsers)
            {
                user.IsActive = false;
                user.SecurityStamp = Guid.NewGuid().ToString("N");
                await _userRepo.UpdateAsync(user, cancellationToken);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("[SECURITY] Deactivated {Count} privileged demo user account(s) (Admin/SuperAdmin) in released environment.", privilegedDemoUsers.Count);
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
