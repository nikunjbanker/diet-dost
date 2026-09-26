/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Domain.Model.Meal;

public class UserCorrectionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string OriginalDetectedItem { get; set; } = string.Empty;
    public string CorrectedItemName { get; set; } = string.Empty;
    public string HindiOrRegionalName { get; set; } = string.Empty;
    public string EstimatedPortion { get; set; } = "1 Katori";
    public double Calories { get; set; }
    public double ProteinGrams { get; set; }
    public double CarbsGrams { get; set; }
    public double FatGrams { get; set; }
    public string MealType { get; set; } = "Lunch";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int FrequencyCount { get; set; } = 1;
}
