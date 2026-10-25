/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Text.RegularExpressions;

namespace Nutrition.Application.Common.Security;

/// <summary>
/// Security utility for path traversal protection and URI normalization.
/// Guarantees that uploaded media paths are strictly stored as root-relative paths
/// and that user directory identifiers are safe from path traversal attacks.
/// </summary>
public static partial class PhotoPathSanitizer
{
    [GeneratedRegex(@"[^a-zA-Z0-9_\-]")]
    private static partial Regex SafeDirectoryCharsRegex();

    /// <summary>
    /// Sanitizes a user ID for safe use as a directory name, preventing directory traversal.
    /// Only allows alphanumeric characters, underscores, and hyphens.
    /// </summary>
    public static string SanitizeUserId(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return "default";
        }

        var cleaned = SafeDirectoryCharsRegex().Replace(userId.Trim(), "");
        return string.IsNullOrWhiteSpace(cleaned) ? "default" : cleaned;
    }

    /// <summary>
    /// Normalizes a photo URI or URL to a root-relative path (e.g. /uploads/users/... or /uploads/meals/...).
    /// Strips external host, protocol, query parameters, and directory traversal tokens (..).
    /// </summary>
    public static string NormalizeToRelativePath(string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            return string.Empty;
        }

        var trimmed = pathOrUrl.Trim();

        // If it's an absolute URL (e.g. http://localhost:5240/uploads/... or https://diet-dost.in/uploads/...)
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            trimmed = uri.AbsolutePath;
        }

        // Prevent path traversal
        trimmed = trimmed.Replace("..", "").Replace('\\', '/');

        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        return trimmed;
    }
}
