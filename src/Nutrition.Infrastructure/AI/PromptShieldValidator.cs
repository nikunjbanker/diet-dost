/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Text.RegularExpressions;
using Nutrition.Application.Agents;

namespace Nutrition.Infrastructure.AI;

/// <summary>
/// Result of a content safety and prompt shield evaluation.
/// </summary>
public sealed record PromptShieldResult(
    bool IsSafe,
    string? ViolationCategory = null,
    string? ViolationReason = null,
    string SanitizedInput = ""
);

/// <summary>
/// Enterprise AI Prompt Shield & Content Safety Pre-Validator.
/// Enforces zero-tolerance defense against:
/// 1. Harmful, violent, weapon, and self-harm content
/// 2. Sexually explicit, adult, and vulgar content
/// 3. Communal, religious hate, casteist, and sectarian incitement
/// 4. Prompt injection, jailbreak attempts, and system override tokens
/// 5. Adversarial data poisoning in self-learning / feedback retraining
/// </summary>
public static class PromptShieldValidator
{
    // -------------------------------------------------------------------------
    // Pattern 1: Harmful, Violent, Weapons & Self-Harm Content
    // -------------------------------------------------------------------------
    private static readonly Regex ViolentAndHarmfulPattern = new(
        @"\b(kill|murder|suicide|self-harm|cut\s+myself|bomb|explosive|weapon|gun|knife|stab|assault|behead|terrorist|terrorism|poison\s+someone|hang\s+myself|shoot\s+up|strangle|arson)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // -------------------------------------------------------------------------
    // Pattern 2: Sexually Explicit & Adult Content
    // -------------------------------------------------------------------------
    private static readonly Regex SexualAndExplicitPattern = new(
        @"\b(porn|pornography|sex|sexual|erotic|nude|nudity|penis|vagina|boobs|blowjob|anal|incest|orgasm|masturbat|fetish)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // -------------------------------------------------------------------------
    // Pattern 3: Communal, Religious Hate & Sectarian Incitement
    // -------------------------------------------------------------------------
    private static readonly Regex CommunalAndHatePattern = new(
        @"\b(communal\s+riot|genocide|kill\s+all\s+(hindu|muslim|sikh|christian|jew|dalit)|kafir\s+must\s+die|jihad\s+against|casteist|untouchable|infidel\s+scum|religious\s+hatred|ethnic\s+cleansing)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // -------------------------------------------------------------------------
    // Pattern 4: Prompt Injection, Jailbreaks & System Override Tokens
    // -------------------------------------------------------------------------
    private static readonly Regex PromptInjectionPattern = new(
        @"(ignore\s+(all\s+)?(previous\s+)?(instructions|prompts|rules|guidelines)|" +
        @"disregard\s+(all\s+)?(previous\s+)?(instructions|guidelines)|" +
        @"you\s+are\s+now\s+(DAN|unfiltered|jailbroken|an\s+unrestricted)|" +
        @"system\s+override|" +
        @"bypass\s+(safety|content\s+filter|guardrails)|" +
        @"reveal\s+(your\s+)?(system\s+prompt|instructions|api\s*key|secret)|" +
        @"output\s+(your\s+)?(initial\s+prompt|hidden\s+prompt)|" +
        @"forget\s+everything\s+you\s+were\s+told|" +
        @"pretend\s+you\s+have\s+no\s+rules|" +
        @"developer\s+mode\s+enabled)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Validates raw natural language user text against all content safety categories
    /// and prompt injection shields.
    /// </summary>
    public static PromptShieldResult ValidateInput(string? input, int maxLength = 1000)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new PromptShieldResult(IsSafe: true, SanitizedInput: string.Empty);
        }

        var trimmed = input.Trim();

        // 1. Prompt Injection & Jailbreak Defense
        if (PromptInjectionPattern.IsMatch(trimmed))
        {
            return new PromptShieldResult(
                IsSafe: false,
                ViolationCategory: "PromptInjection",
                ViolationReason: "Input contains prompt injection or system override tokens rejected by Prompt Shield.",
                SanitizedInput: string.Empty);
        }

        // 2. Harmful & Violent Content
        if (ViolentAndHarmfulPattern.IsMatch(trimmed))
        {
            return new PromptShieldResult(
                IsSafe: false,
                ViolationCategory: "ViolenceAndHarm",
                ViolationReason: "Input contains violent, harmful, or self-harm content rejected by Content Safety.",
                SanitizedInput: string.Empty);
        }

        // 3. Sexually Explicit Content
        if (SexualAndExplicitPattern.IsMatch(trimmed))
        {
            return new PromptShieldResult(
                IsSafe: false,
                ViolationCategory: "SexualAndExplicit",
                ViolationReason: "Input contains sexually explicit or adult content rejected by Content Safety.",
                SanitizedInput: string.Empty);
        }

        // 4. Communal & Religious Hate Speech
        if (CommunalAndHatePattern.IsMatch(trimmed))
        {
            return new PromptShieldResult(
                IsSafe: false,
                ViolationCategory: "CommunalHate",
                ViolationReason: "Input contains communal, hate speech, or sectarian incitement rejected by Content Safety.",
                SanitizedInput: string.Empty);
        }

        // 5. Sanitize and clamp length
        var clamped = trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
        var sanitized = clamped
            .Replace("[USER_MEAL_INTAKE_DATA]", "", StringComparison.OrdinalIgnoreCase)
            .Replace("[/USER_MEAL_INTAKE_DATA]", "", StringComparison.OrdinalIgnoreCase)
            .Replace("```", "")
            .Trim();

        return new PromptShieldResult(
            IsSafe: true,
            SanitizedInput: sanitized);
    }

    /// <summary>
    /// Validates feedback retraining input to defend continuous self-learning
    /// against adversarial data poisoning or abusive memory injection.
    /// </summary>
    public static PromptShieldResult ValidateFeedback(string dishName, string? remarks)
    {
        var dishCheck = ValidateInput(dishName, maxLength: 100);
        if (!dishCheck.IsSafe)
        {
            return dishCheck;
        }

        if (!string.IsNullOrWhiteSpace(remarks))
        {
            var remarksCheck = ValidateInput(remarks, maxLength: 500);
            if (!remarksCheck.IsSafe)
            {
                return remarksCheck;
            }
        }

        // Verify dishName looks like a food term rather than code, tags, or numbers
        var cleanDish = dishName.Trim();
        if (cleanDish.Contains('<') || cleanDish.Contains('>') || cleanDish.Contains('{') || cleanDish.Contains('}'))
        {
            return new PromptShieldResult(
                IsSafe: false,
                ViolationCategory: "DataPoisoning",
                ViolationReason: "Dish name contains invalid markup or code rejected by Self-Learning Guardrail.",
                SanitizedInput: string.Empty);
        }

        return new PromptShieldResult(IsSafe: true, SanitizedInput: cleanDish);
    }

    /// <summary>
    /// Constructs a standardized safe response for content safety rejections.
    /// </summary>
    public static IndianMealAnalysisResult CreateSafetyViolationResult(string? violationReason, string? mealType = null)
    {
        var safeMealType = string.IsNullOrWhiteSpace(mealType) ? "Meal" : mealType;
        return new IndianMealAnalysisResult
        {
            MealType = safeMealType,
            DishName = "Content Safety Policy Rejection",
            OverallConfidenceScore = 0.0,
            IdentifiedItems = new List<IndianMealItemDto>(),
            TotalCalories = 0,
            TotalProteinGrams = 0,
            TotalCarbsGrams = 0,
            TotalFatGrams = 0,
            WhoComplianceFlags = new List<string>
            {
                "Content Safety Guardrail Activated: Input violated safety policy."
            },
            DietitianAdvice = $"Request could not be processed: {violationReason ?? "Input violates Diet Dost content safety standards (no harmful, violent, sexual, communal, or prompt-override content permitted)."}"
        };
    }
}
