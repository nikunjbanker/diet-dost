/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;

namespace Nutrition.WebGateway.Hubs;

/// <summary>
/// ASP.NET Core SignalR Realtime Hub for In-App Notifications.
/// Enforces strict Origin checking (CSWSH defense) and role-gated group separation.
/// </summary>
public partial class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        // CSWSH Defense: Validate Origin header against Host if present
        var httpContext = Context.GetHttpContext();
        if (httpContext != null)
        {
            var origin = httpContext.Request.Headers.Origin.ToString();
            var host = httpContext.Request.Host.Value;
            if (!string.IsNullOrWhiteSpace(origin))
            {
                if (Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
                {
                    if (!string.Equals(originUri.Authority, host, StringComparison.OrdinalIgnoreCase) &&
                        !originUri.Host.EndsWith("localhost", StringComparison.OrdinalIgnoreCase) &&
                        !originUri.Host.EndsWith("127.0.0.1", StringComparison.OrdinalIgnoreCase) &&
                        !originUri.Host.EndsWith("diet-dost.in", StringComparison.OrdinalIgnoreCase) &&
                        !originUri.Host.EndsWith("dietdost.app", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("[SECURITY] Aborting SignalR connection due to mismatched Origin '{Origin}' vs Host '{Host}'", origin, host);
                        Context.Abort();
                        return;
                    }
                }
            }
        }

        // Automatic server-side group assignment based on validated claims
        if (Context.User != null && (Context.User.IsInRole("SuperAdmin") || Context.User.IsInRole("Admin")))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "SuperAdmins");
            _logger.LogInformation("[SIGNALR] Connected SuperAdmin {UserId} added to 'SuperAdmins' group.", Context.UserIdentifier);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Connects an unauthenticated applicant browser to listen for their account approval.
    /// Requires a valid 32-character hexadecimal registration ticket.
    /// </summary>
    public async Task JoinApplicantChannel(string registrationTicket)
    {
        if (string.IsNullOrWhiteSpace(registrationTicket) || !RegistrationTicketRegex().IsMatch(registrationTicket))
        {
            throw new HubException("Invalid registration ticket format.");
        }

        var groupName = $"applicant_{registrationTicket}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("[SIGNALR] Connection {ConnectionId} joined applicant channel group: {GroupName}", Context.ConnectionId, groupName);
    }

    [GeneratedRegex(@"^[a-fA-F0-9]{32}$")]
    private static partial Regex RegistrationTicketRegex();
}

