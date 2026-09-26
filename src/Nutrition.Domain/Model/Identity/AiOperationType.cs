/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Domain.Model.Identity;

/// <summary>
/// Identifies the category of AI multimodal or LLM operations tracked in audit logs.
/// </summary>
public enum AiOperationType
{
    PhotoDetection = 0,
    TextDetection = 1,
    ProgressCompare = 2
}
