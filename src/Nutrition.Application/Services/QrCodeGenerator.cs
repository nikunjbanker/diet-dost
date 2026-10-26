/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Drawing;
using System.Text;
using Nutrition.Application.Common.Interfaces;
using QRCoder;

namespace Nutrition.Application.Services;

/// <summary>
/// High-performance, standards-compliant ISO/IEC 18004 QR Code generator using QRCoder.
/// Implements full Galois Field GF(256) Reed-Solomon error correction and optimal masking.
/// Generates vector SVGs scannable natively by Google Lens, Android Camera, and iOS Camera apps.
/// Authoritatively renders QR codes on the server to prevent client-side DOM tampering.
/// </summary>
public sealed class QrCodeGenerator : IQrCodeGenerator
{
    public string GenerateSvg(string content, int size = 240, int margin = 4)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        using var generator = new QRCodeGenerator();
        using var qrCodeData = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        var qrCode = new SvgQRCode(qrCodeData);

        var svg = qrCode.GetGraphic(
            viewBox: new Size(size, size),
            darkColorHex: "#000000",
            lightColorHex: "#FFFFFF",
            drawQuietZones: true,
            sizingMode: SvgQRCode.SizingMode.WidthHeightAttribute
        );

        if (!svg.Contains("shape-rendering=\"crispEdges\""))
        {
            svg = svg.Replace("<svg ", "<svg shape-rendering=\"crispEdges\" role=\"img\" aria-label=\"QR Code\" ");
        }

        return svg;
    }

    public string GenerateDataUri(string content, int size = 240, int margin = 4)
    {
        var svg = GenerateSvg(content, size, margin);
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        return $"data:image/svg+xml;base64,{base64}";
    }
}
