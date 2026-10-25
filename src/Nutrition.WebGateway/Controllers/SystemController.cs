/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.SystemWarmup.Queries.WarmupDatabase;

namespace Nutrition.WebGateway.Controllers;

/// <summary>
/// System health and database pre-warming controller.
/// Provides an anonymous, lightweight endpoint to wake serverless cloud databases (e.g. Azure SQL Serverless Free Tier)
/// upon application launch, auth gate interaction, or keep-alive pings before user operations execute.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public SystemController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>
    /// Lightweight database warm-up probe.
    /// Issues a non-blocking SELECT 1 / CanConnect probe to active persistence.
    /// </summary>
    [HttpGet("warmup")]
    [AllowAnonymous]
    public async Task<IActionResult> WarmupDatabase(CancellationToken ct)
    {
        var result = await _dispatcher.QueryAsync(new WarmupDatabaseQuery(), ct);
        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { message = result.Error, error = result.ErrorCode });
        }

        return Ok(result.Data);
    }
}
