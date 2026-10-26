/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Application.Common.Interfaces;

/// <summary>
/// Authoritative server-side ISO/IEC 18004 standard QR Code generator.
/// Eliminates client-side DOM tampering and XSS payload alterations.
/// </summary>
public interface IQrCodeGenerator
{
    /// <summary>
    /// Generates a standards-compliant ISO/IEC 18004 vector SVG string.
    /// </summary>
    /// <param name="content">The text or URL payload to encode.</param>
    /// <param name="size">Target viewBox pixel width and height.</param>
    /// <param name="margin">Quiet zone border width in modules (default 4 per ISO spec).</param>
    /// <returns>A standalone, tamper-evident SVG XML string.</returns>
    string GenerateSvg(string content, int size = 200, int margin = 4);

    /// <summary>
    /// Generates a data URI string (data:image/svg+xml;base64,...) for direct use in &lt;img src&gt;.
    /// </summary>
    string GenerateDataUri(string content, int size = 200, int margin = 4);
}
