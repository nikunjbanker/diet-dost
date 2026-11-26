/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.ComponentModel.DataAnnotations;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Application.Features.Profile.DTOs;

public sealed record MedicationEntryDto(
    string DrugName,
    string Dosage = "",
    string Frequency = "",
    string? TimingNotes = null
);

public sealed class SaveProfileRequestDto
{
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;
    public BiologicalSex Sex { get; set; }
    public int Age { get; set; }
    public double HeightCm { get; set; }
    public double CurrentWeightKg { get; set; }
    public double TargetWeightKg { get; set; }
    public double DesiredPaceKgPerWeek { get; set; } = 0.5;
    public ActivityLevel ActivityLevel { get; set; } = ActivityLevel.Sedentary;
    public DietaryPreference DietaryPreference { get; set; } = DietaryPreference.LactoVeg;
    [MaxLength(100)]
    public string RegionalCuisine { get; set; } = "North Indian";
    [MaxLength(30)]
    public List<string> DiagnosedConditions { get; set; } = new();
    [MaxLength(30)]
    public List<MedicationEntryDto> Medications { get; set; } = new();
    [MaxLength(100)]
    public string Timezone { get; set; } = "Asia/Kolkata";
}

public sealed record UserProfileDto(
    string Id,
    string Name,
    BiologicalSex Sex,
    int Age,
    double HeightCm,
    double CurrentWeightKg,
    double TargetWeightKg,
    double DesiredPaceKgPerWeek,
    ActivityLevel ActivityLevel,
    DietaryPreference DietaryPreference,
    string RegionalCuisine,
    List<string> DiagnosedConditions,
    List<MedicationEntryDto> Medications,
    string Timezone,
    bool IsOnboarded,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public record ProfileResponseDto(
    UserProfileDto Profile,
    BmrTdeeResult? Budget,
    MacroDistribution? Macros,
    string? Message = null
);

public static class ProfileDtoMapper
{
    public static UserProfileDto ToDto(this UserProfile entity) => new(
        Id: entity.Id,
        Name: entity.Name,
        Sex: entity.Sex,
        Age: entity.Age,
        HeightCm: entity.HeightCm,
        CurrentWeightKg: entity.CurrentWeightKg,
        TargetWeightKg: entity.TargetWeightKg,
        DesiredPaceKgPerWeek: entity.DesiredPaceKgPerWeek,
        ActivityLevel: entity.ActivityLevel,
        DietaryPreference: entity.DietaryPreference,
        RegionalCuisine: entity.RegionalCuisine,
        DiagnosedConditions: entity.DiagnosedConditions.ToList(),
        Medications: entity.Medications.Select(m => new MedicationEntryDto(m.DrugName, m.Dosage, m.Frequency, m.TimingNotes)).ToList(),
        Timezone: entity.Timezone,
        IsOnboarded: entity.IsOnboarded,
        CreatedAtUtc: entity.CreatedAtUtc,
        UpdatedAtUtc: entity.UpdatedAtUtc
    );

    public static UserProfile ToDomainEntity(this SaveProfileRequestDto dto, string userId) => new()
    {
        Id = userId,
        Name = dto.Name?.Trim() ?? string.Empty,
        Sex = dto.Sex,
        Age = dto.Age,
        HeightCm = dto.HeightCm,
        CurrentWeightKg = dto.CurrentWeightKg,
        TargetWeightKg = dto.TargetWeightKg,
        DesiredPaceKgPerWeek = dto.DesiredPaceKgPerWeek,
        ActivityLevel = dto.ActivityLevel,
        DietaryPreference = dto.DietaryPreference,
        RegionalCuisine = string.IsNullOrWhiteSpace(dto.RegionalCuisine) ? "North Indian" : dto.RegionalCuisine.Trim(),
        DiagnosedConditions = dto.DiagnosedConditions?.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList() ?? new List<string>(),
        Medications = dto.Medications?.Select(m => new MedicationEntry
        {
            DrugName = m.DrugName?.Trim() ?? string.Empty,
            Dosage = m.Dosage?.Trim() ?? string.Empty,
            Frequency = m.Frequency?.Trim() ?? string.Empty,
            TimingNotes = m.TimingNotes?.Trim()
        }).ToList() ?? new List<MedicationEntry>(),
        Timezone = string.IsNullOrWhiteSpace(dto.Timezone) ? "Asia/Kolkata" : dto.Timezone.Trim()
    };
}
