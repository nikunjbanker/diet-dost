/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Application.Common.Security;
using Nutrition.Domain.Common;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Domain.Model.Progress;
using Nutrition.Infrastructure.Persistence;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public sealed class SoftDeleteAndSuperAdminTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DietTrackerDbContext _dbContext;

    public SoftDeleteAndSuperAdminTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DietTrackerDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new DietTrackerDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void DomainEntities_Implement_ISoftDeletable_Interface()
    {
        Assert.True(typeof(ISoftDeletable).IsAssignableFrom(typeof(MealLog)));
        Assert.True(typeof(ISoftDeletable).IsAssignableFrom(typeof(ProgressPhoto)));
        Assert.True(typeof(ISoftDeletable).IsAssignableFrom(typeof(ApplicationUser)));
        Assert.True(typeof(ISoftDeletable).IsAssignableFrom(typeof(UserProfile)));
        Assert.True(typeof(ISoftDeletable).IsAssignableFrom(typeof(UserCorrectionRecord)));
        Assert.True(typeof(ISoftDeletable).IsAssignableFrom(typeof(DailyCalorieLedger)));
        Assert.True(typeof(ISoftDeletable).IsAssignableFrom(typeof(AiDetectionFeedbackRecord)));

        var meal = new MealLog();
        Assert.False(meal.IsDeleted);
        Assert.Null(meal.DeletedAtUtc);
    }

    [Fact]
    public async Task GlobalQueryFilter_ExcludesSoftDeletedMeals_ForStandardQueries()
    {
        var activeMeal = new MealLog
        {
            Id = "meal-active-1",
            UserId = "user-100",
            DishName = "Dal Tadka",
            IsDeleted = false
        };

        var deletedMeal = new MealLog
        {
            Id = "meal-deleted-2",
            UserId = "user-100",
            DishName = "Butter Naan",
            IsDeleted = true,
            DeletedAtUtc = DateTime.UtcNow
        };

        _dbContext.Meals.AddRange(activeMeal, deletedMeal);
        await _dbContext.SaveChangesAsync();

        // Standard EF Core query subject to Global Query Filter
        var meals = await _dbContext.Meals.ToListAsync();

        Assert.Single(meals);
        Assert.Equal("meal-active-1", meals[0].Id);
    }

    [Fact]
    public async Task SuperAdminMode_WithIgnoreQueryFilters_ReturnsDeletedMeals()
    {
        var activeMeal = new MealLog
        {
            Id = "meal-active-2",
            UserId = "user-200",
            DishName = "Poha",
            IsDeleted = false
        };

        var deletedMeal = new MealLog
        {
            Id = "meal-deleted-3",
            UserId = "user-200",
            DishName = "Jalebi",
            IsDeleted = true,
            DeletedAtUtc = DateTime.UtcNow
        };

        _dbContext.Meals.AddRange(activeMeal, deletedMeal);
        await _dbContext.SaveChangesAsync();

        // SuperAdmin Audit Mode bypasses query filters
        var allMeals = await _dbContext.Meals.IgnoreQueryFilters().ToListAsync();
        var deletedOnly = await _dbContext.Meals.IgnoreQueryFilters().Where(m => m.IsDeleted).ToListAsync();

        Assert.Equal(2, allMeals.Count);
        Assert.Single(deletedOnly);
        Assert.Equal("meal-deleted-3", deletedOnly[0].Id);
    }

    [Fact]
    public async Task EfRepository_GetByIdAsync_ReturnsNull_ForSoftDeletedEntity()
    {
        var deletedPhoto = new ProgressPhoto
        {
            Id = "photo-deleted-1",
            UserId = "user-300",
            PhotoUri = "/uploads/users/user-300/progress/test.jpg",
            IsDeleted = true,
            DeletedAtUtc = DateTime.UtcNow
        };

        _dbContext.ProgressPhotos.Add(deletedPhoto);
        await _dbContext.SaveChangesAsync();

        var repo = new EfRepository<ProgressPhoto>(_dbContext, NullLogger<EfRepository<ProgressPhoto>>.Instance);
        var fetched = await repo.GetByIdAsync("photo-deleted-1");

        Assert.Null(fetched);
    }

    [Theory]
    [InlineData("http://localhost:5240/uploads/users/usr1/meals/img.jpg", "/uploads/users/usr1/meals/img.jpg")]
    [InlineData("https://diet-dost.in/uploads/users/usr1/meals/img.jpg", "/uploads/users/usr1/meals/img.jpg")]
    [InlineData("/uploads/users/usr1/meals/img.jpg", "/uploads/users/usr1/meals/img.jpg")]
    [InlineData("uploads/users/usr1/meals/img.jpg", "/uploads/users/usr1/meals/img.jpg")]
    public void PhotoPathSanitizer_NormalizeToRelativePath_EnforcesRootRelativePath(string input, string expected)
    {
        var result = PhotoPathSanitizer.NormalizeToRelativePath(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("../../etc/passwd", "etcpasswd")]
    [InlineData("user/../admin", "useradmin")]
    [InlineData("usr_123-abc", "usr_123-abc")]
    [InlineData("   ", "default")]
    [InlineData(null, "default")]
    public void PhotoPathSanitizer_SanitizeUserId_PreventsPathTraversal(string? input, string expected)
    {
        var result = PhotoPathSanitizer.SanitizeUserId(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GetDeletedRecordsQueryHandler_ReturnsSoftDeletedItemsAcrossEntities()
    {
        var mealRepo = new EfRepository<MealLog>(_dbContext, NullLogger<EfRepository<MealLog>>.Instance);
        var photoRepo = new EfRepository<ProgressPhoto>(_dbContext, NullLogger<EfRepository<ProgressPhoto>>.Instance);
        var userRepo = new EfRepository<ApplicationUser>(_dbContext, NullLogger<EfRepository<ApplicationUser>>.Instance);

        var activeMeal = new MealLog { Id = "m-act", UserId = "u1", DishName = "Idli", IsDeleted = false };
        var delMeal = new MealLog { Id = "m-del", UserId = "u1", DishName = "Puri", IsDeleted = true, DeletedAtUtc = DateTime.UtcNow };

        var delPhoto = new ProgressPhoto { Id = "p-del", UserId = "u1", PhotoType = ProgressPhotoType.FullBodyFront, PhotoUri = "/uploads/users/u1/progress/p.jpg", IsDeleted = true, DeletedAtUtc = DateTime.UtcNow };

        _dbContext.Meals.AddRange(activeMeal, delMeal);
        _dbContext.ProgressPhotos.Add(delPhoto);
        await _dbContext.SaveChangesAsync();

        var handler = new Nutrition.Application.Features.Admin.Queries.GetDeletedRecords.GetDeletedRecordsQueryHandler(
            mealRepo, photoRepo, userRepo);

        var result = await handler.HandleAsync(new Nutrition.Application.Features.Admin.Queries.GetDeletedRecords.GetDeletedRecordsQuery("all", "u1"));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Contains(result.Data, r => r.Id == "m-del" && r.RecordType == "Meal");
        Assert.Contains(result.Data, r => r.Id == "p-del" && r.RecordType == "ProgressPhoto");
    }
}

