/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Application.Common.Options;

/// <summary>
/// Strongly-typed Options for User Authentication, Registration gating, and DPDPA 2023 Consent Versions.
/// Bound to configuration section "Auth".
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public bool AllowRegistration { get; set; } = true;
    public string SuperAdminEmail { get; set; } = "superadmin@dietdost.app";
    public bool RequireMobileVerification { get; set; } = false;
    public string TermsVersion { get; set; } = "v1.0-202609";
    public string HealthConsentVersion { get; set; } = "v1.0-202609";
    public string DemoPassword { get; set; } = "DietDost@Demo2026!";
}
