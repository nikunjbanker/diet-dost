/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Application.Features.Profile.DTOs;

public record ProfileResponseDto(
    UserProfile Profile,
    BmrTdeeResult Budget,
    MacroDistribution Macros,
    string? Message = null
);
