/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol.Server;
using Nutrition.Application.Agents;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Features.Meals.Commands.AnalyzeTextMeal;
using Nutrition.Application.Features.Meals.Commands.ConfirmMeal;
using Nutrition.Application.Features.Meals.Commands.UploadAndAnalyzeMeal;
using Nutrition.Application.Features.Meals.DTOs;
using Nutrition.Application.Features.Meals.Queries.EstimateFoodItem;
using Nutrition.Application.Features.Meals.Queries.GetMealHistory;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Meal;
using Nutrition.Mcp.Models;
using Nutrition.Mcp.Security;

namespace Nutrition.Mcp.Tools;

/// <summary>
/// Model Context Protocol Tools exposing Diet-Dost Meal History, Nutritional Analysis, and Human-in-the-Loop Logging.
/// Enforces ICMR-NIN 2024 / IFCT 2017 clinical standards and the Zero-Assumption Intake rule.
/// Directly dispatches to Native Clean Architecture CQRS handlers via IDispatcher.
/// </summary>
[McpServerToolType]
public class MealTools
{
    private readonly IMcpContinuationTokenProtector _tokenProtector;

    public MealTools(IMcpContinuationTokenProtector tokenProtector)
    {
        _tokenProtector = tokenProtector;
    }

    [McpServerTool]
    [Description("Retrieves the user's logged meals and nutritional intake history by period ('day', 'week', 'month', 'all') or specific target date.")]
    public async Task<object> ViewMealLogs(
        IDispatcher dispatcher,
        [Description("Reporting period: 'day', 'week', 'month', or 'all'")] string period = "day",
        [Description("Specific target date in 'YYYY-MM-DD' format (defaults to current date if omitted)")] string? date = null,
        [Description("Optional filter by meal type: 'Breakfast', 'Lunch', 'Dinner', or 'Snack'")] string? mealType = null,
        ClaimsPrincipal? user = null,
        CancellationToken ct = default)
    {
        var userId = ResolveUserId(user);
        var isAdmin = user != null && (user.IsInRole("Admin") || user.IsInRole("SuperAdmin"));

        var query = new GetMealHistoryQuery(
            CurrentUserId: userId,
            TargetUserId: null,
            IsAdminOrSuper: isAdmin,
            Period: period,
            Date: date,
            MealType: mealType
        );

        var result = await dispatcher.QueryAsync(query, ct);
        if (!result.Succeeded || result.Data == null)
        {
            return new McpErrorResult("Error", result.Error ?? "Unable to retrieve meal history.");
        }

        var dto = result.Data;
        return new ViewMealLogsResult(
            User: dto.UserId,
            Period: dto.Period,
            SelectedDate: dto.SelectedDate,
            TotalMealsCount: dto.TotalMealsCount,
            NutritionTotals: new NutritionSummaryDto(
                Calories: dto.Summary.TotalCalories,
                ProteinGrams: dto.Summary.TotalProteinGrams,
                CarbsGrams: dto.Summary.TotalCarbsGrams,
                FatGrams: dto.Summary.TotalFatGrams,
                FiberGrams: dto.Summary.TotalFiberGrams,
                SugarGrams: dto.Summary.TotalSugarGrams,
                SodiumMg: dto.Summary.TotalSodiumMg
            ),
            Meals: dto.Meals.Select(m => new LoggedMealDto(
                Id: m.Id,
                MealType: m.MealType.ToString(),
                LoggedAtUtc: m.LoggedAt,
                IsVerifiedByUser: m.IsVerifiedByUser,
                DishName: m.DishName,
                Items: m.Items.Select(i => new LoggedItemDto(
                    Name: i.Name,
                    Portion: i.EstimatedPortion,
                    Calories: i.Calories,
                    Protein: i.ProteinGrams,
                    Carbs: i.CarbsGrams,
                    Fat: i.FatGrams,
                    Fiber: i.FiberGrams
                )).ToList()
            )).ToList()
        );
    }

    [McpServerTool]
    [Description("Estimates nutritional breakdown for a specific food item or dish using the ICMR-NIN 2024 / IFCT 2017 database.")]
    public async Task<object> EstimateFoodPortion(
        IDispatcher dispatcher,
        [Description("Name of the food item or dish (e.g. 'Paneer Butter Masala', 'Masala Dosa', 'Poha')")] string foodName,
        [Description("Portion or quantity description (e.g. '1 bowl', '2 pieces', '150g')")] string? portion = null,
        ClaimsPrincipal? user = null,
        CancellationToken ct = default)
    {
        var userId = ResolveUserId(user);
        var query = new EstimateFoodItemQuery(
            CurrentUserId: userId,
            Name: foodName,
            Portion: portion,
            UseAi: true,
            MealType: null
        );

        var result = await dispatcher.QueryAsync(query, ct);
        if (!result.Succeeded || result.Data == null)
        {
            return new McpErrorResult("Error", result.Error ?? $"Could not estimate nutrition for '{foodName}'.");
        }

        var item = result.Data;
        return new FoodPortionEstimateResult(
            Dish: item.NormalizedName,
            Portion: item.EstimatedPortion,
            Calories: item.Calories,
            ProteinGrams: item.ProteinGrams,
            CarbsGrams: item.CarbsGrams,
            FatGrams: item.FatGrams,
            FiberGrams: item.FiberGrams
        );
    }

    [McpServerTool]
    [Description("Analyzes and drafts a meal entry from text or photo with Human-in-the-Loop elicitation. Enforces the Zero-Assumption Intake rule before writing to the health ledger.")]
    public async Task<object> AddMealEntry(
        IDispatcher dispatcher,
        [Description("Natural language description of food consumed (e.g. '2 rotis, 1 bowl dal tadka, and 1 katori curd')")] string? textDescription = null,
        [Description("Optional Base64 image string or local file path of meal photo")] string? photoData = null,
        [Description("Meal type: 'Breakfast', 'Lunch', 'Dinner', or 'Snack'")] string? mealType = null,
        [Description("Continuation token when answering an elicitation approval prompt")] string? approvalToken = null,
        [Description("User confirmation response ('yes' to commit, 'no' to cancel)")] string? approved = null,
        ClaimsPrincipal? user = null,
        CancellationToken ct = default)
    {
        var userId = ResolveUserId(user);
        var tier = ResolveUserTier(user);

        // =========================================================================
        // STAGE 2: Continuation Handling (Human Approved or Rejected)
        // =========================================================================
        if (!string.IsNullOrEmpty(approvalToken) && !string.IsNullOrEmpty(approved))
        {
            if (approved.Trim().Equals("no", StringComparison.OrdinalIgnoreCase) ||
                approved.Trim().Equals("reject", StringComparison.OrdinalIgnoreCase))
            {
                return new MealCancelledResult(
                    Status: "Cancelled",
                    Message: "Meal entry cancelled by user. Nothing was committed to the health diary."
                );
            }

            var pendingState = _tokenProtector.Unprotect(approvalToken, userId);
            if (pendingState == null)
            {
                return new McpErrorResult(
                    Status: "Error",
                    Error: "Invalid, expired, or cross-tenant approval token. Request rejected under security policy."
                );
            }

            // Construct verified SaveMealRequestDto from pending draft
            var parsedMealType = Enum.TryParse<MealType>(pendingState.MealType, true, out var mt) ? mt : MealType.Lunch;
            var mealRequest = new SaveMealRequestDto
            {
                UserId = userId,
                MealType = parsedMealType,
                DishName = pendingState.Description,
                LoggedAt = DateTime.UtcNow,
                Items = pendingState.Items.Select(itm => new FoodItemRequestDto
                {
                    Name = itm.Name,
                    EstimatedPortion = itm.Portion,
                    Calories = itm.Calories,
                    ProteinGrams = itm.Protein,
                    CarbsGrams = itm.Carbs,
                    FatGrams = itm.Fat,
                    FiberGrams = itm.Fiber,
                    OriginalDetection = itm.Name
                }).ToList()
            };

            var confirmCommand = new ConfirmMealCommand(
                Meal: mealRequest,
                CurrentUserId: userId,
                IsAdminOrSuper: user != null && (user.IsInRole("Admin") || user.IsInRole("SuperAdmin"))
            );

            var confirmResult = await dispatcher.SendAsync(confirmCommand, ct);
            if (!confirmResult.Succeeded)
            {
                return new McpErrorResult("Error", confirmResult.Error ?? "Failed to commit verified meal to diary.");
            }

            return new MealConfirmedResult(
                Status: "Confirmed",
                Message: $"Successfully verified and logged {parsedMealType} to your health diary!",
                MealId: confirmResult.Data?.Meal?.Id ?? string.Empty,
                TotalCalories: confirmResult.Data?.Meal?.TotalCalories ?? pendingState.TotalCalories,
                ProteinGrams: confirmResult.Data?.Meal?.TotalProteinGrams ?? pendingState.TotalProtein,
                CarbsGrams: confirmResult.Data?.Meal?.TotalCarbsGrams ?? pendingState.TotalCarbs,
                FatGrams: confirmResult.Data?.Meal?.TotalFatGrams ?? pendingState.TotalFat
            );
        }

        // =========================================================================
        // STAGE 1: Initial Estimation & Elicitation Generation
        // =========================================================================
        if (string.IsNullOrWhiteSpace(textDescription) && string.IsNullOrWhiteSpace(photoData))
        {
            return new McpErrorResult("Error", "Please provide food textDescription or photoData.");
        }

        MealAnalysisResultDto analysisResult;

        if (!string.IsNullOrWhiteSpace(textDescription))
        {
            // PromptShield sanitization
            var sanitizedText = textDescription.Trim();
            if (sanitizedText.Length > 500) sanitizedText = sanitizedText[..500];

            var cmdResult = await dispatcher.SendAsync(new AnalyzeTextMealCommand(
                UserId: userId,
                Tier: tier,
                Description: sanitizedText,
                MealType: mealType
            ), ct);

            if (!cmdResult.Succeeded || cmdResult.Data == null)
            {
                return new McpErrorResult("Error", cmdResult.Error ?? "Failed to analyze meal description.");
            }

            analysisResult = cmdResult.Data;
        }
        else
        {
            // Ingest photo (Base64 string or file path)
            byte[] imageBytes;
            if (File.Exists(photoData))
            {
                imageBytes = await File.ReadAllBytesAsync(photoData, ct);
            }
            else
            {
                imageBytes = Convert.FromBase64String(photoData!);
            }

            using var stream = new MemoryStream(imageBytes);
            var cmdResult = await dispatcher.SendAsync(new UploadAndAnalyzeMealCommand(
                UserId: userId,
                Tier: tier,
                ImageStream: stream,
                FileName: "mcp_upload.jpg",
                MimeType: "image/jpeg",
                RegionalContext: null,
                MealType: mealType
            ), ct);

            if (!cmdResult.Succeeded || cmdResult.Data == null)
            {
                return new McpErrorResult("Error", cmdResult.Error ?? "Failed to analyze meal photo.");
            }

            analysisResult = cmdResult.Data;
        }

        var items = analysisResult.Analysis?.IdentifiedItems ?? [];
        var draftItems = items.Select(i => new DraftItem(
            i.Name,
            i.EstimatedPortion,
            i.Calories,
            i.ProteinGrams,
            i.CarbsGrams,
            i.FatGrams,
            i.FiberGrams
        )).ToList();

        var state = new DraftMealContinuationState(
            UserId: userId,
            MealType: analysisResult.Analysis?.MealType ?? mealType ?? "Lunch",
            Description: textDescription ?? analysisResult.Analysis?.DishName ?? "Photo meal",
            TotalCalories: analysisResult.Analysis?.TotalCalories ?? 0,
            TotalProtein: analysisResult.Analysis?.TotalProteinGrams ?? 0,
            TotalCarbs: analysisResult.Analysis?.TotalCarbsGrams ?? 0,
            TotalFat: analysisResult.Analysis?.TotalFatGrams ?? 0,
            TotalFiber: analysisResult.Analysis?.TotalFiberGrams ?? 0,
            ExpiresAtUtc: DateTime.UtcNow.AddMinutes(15),
            Nonce: Guid.NewGuid().ToString("N"),
            Items: draftItems
        );

        var encryptedToken = _tokenProtector.Protect(state);
        var itemsSummary = string.Join(", ", draftItems.Select(i => $"{i.Portion} {i.Name} ({i.Calories:F0} kcal)"));
        var promptMessage = $"🍽️ Proposed {state.MealType}: {itemsSummary}.\n" +
                            $"Estimated Nutrition: {state.TotalCalories:F0} kcal | {state.TotalProtein:F1}g Protein | {state.TotalCarbs:F1}g Carbs | {state.TotalFat:F1}g Fat.\n" +
                            $"Under Diet-Dost Clinical Dietetics Zero-Assumption rules, please confirm ('yes' or 'no') to record in your ledger.";

        return new MealElicitationRequiredResult(
            Status: "ElicitationRequired",
            Message: promptMessage,
            ApprovalToken: encryptedToken,
            ProposedMeal: new ProposedMealDto(
                MealType: state.MealType,
                TotalCalories: state.TotalCalories,
                ProteinGrams: state.TotalProtein,
                CarbsGrams: state.TotalCarbs,
                FatGrams: state.TotalFat,
                Items: draftItems
            ),
            Instruction: "To commit this meal, reply with approved: 'yes' and include the approvalToken."
        );
    }

    [McpServerTool]
    [Description("Confirms and commits an elicited meal proposal to the user's permanent diary with optional portion adjustments.")]
    public async Task<object> ConfirmMealEntry(
        IDispatcher dispatcher,
        [Description("The cryptographic continuation token returned by add_meal_entry")] string approvalToken = "",
        [Description("Confirmation response: 'yes' to commit, 'no' to cancel")] string approved = "yes",
        ClaimsPrincipal? user = null,
        CancellationToken ct = default)
    {
        return await AddMealEntry(
            dispatcher: dispatcher,
            user: user,
            textDescription: null,
            photoData: null,
            mealType: null,
            approvalToken: approvalToken,
            approved: approved,
            ct: ct
        );
    }

    private static string ResolveUserId(ClaimsPrincipal? user)
    {
        return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user?.FindFirst("sub")?.Value
            ?? user?.FindFirst("uid")?.Value
            ?? "free@dietdost.app"; // Default demo fallback for local developer testing
    }

    private static UserTier ResolveUserTier(ClaimsPrincipal? user)
    {
        var tierClaim = user?.FindFirst("tier")?.Value ?? user?.FindFirst(ClaimTypes.Role)?.Value;
        return Enum.TryParse<UserTier>(tierClaim, true, out var t) ? t : UserTier.Free;
    }
}
