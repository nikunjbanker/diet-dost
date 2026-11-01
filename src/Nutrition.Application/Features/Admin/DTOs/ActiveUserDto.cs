/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Admin.DTOs;

/// <summary>
/// Data Transfer Object representing an active application user.
/// Returned exclusively to Admin and SuperAdmin users.
/// </summary>
public record ActiveUserDto(
    string Id,
    string Email,
    UserRole Role,
    UserTier Tier,
    DateTime? LastActiveUtc,
    bool IsCurrentlyOnline
);
