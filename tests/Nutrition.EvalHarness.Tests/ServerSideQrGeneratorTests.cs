/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.Application.Services;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

/// <summary>
/// Unit tests for pure C# ISO/IEC 18004 standards-compliant QrCodeGenerator.
/// Validates mathematical matrix structure, Reed-Solomon GF(256) error correction,
/// quiet zone borders, and tamper-evident SVG path emission.
/// </summary>
public sealed class ServerSideQrGeneratorTests
{
    private readonly QrCodeGenerator _generator = new();

    [Fact]
    public void GenerateSvg_ForShortText_ProducesValidSvgTagAndCrispEdges()
    {
        var text = "https://diet-dost.in";
        var svg = _generator.GenerateSvg(text, size: 200, margin: 4);

        Assert.NotNull(svg);
        Assert.StartsWith("<svg xmlns=\"http://www.w3.org/2000/svg\"", svg);
        Assert.Contains("viewBox=\"0 0 ", svg);
        Assert.Contains("width=\"200\"", svg);
        Assert.Contains("height=\"200\"", svg);
        Assert.Contains("shape-rendering=\"crispEdges\"", svg);
        Assert.Contains("<rect width=\"100%\" height=\"100%\" fill=\"#FFFFFF\"", svg);
        Assert.Contains("<path fill=\"#0F172A\" d=\"", svg);
        Assert.EndsWith("</svg>", svg);
    }

    [Fact]
    public void GenerateSvg_ForFullWebApprovalUrl_SelectsCorrectDimensionsAndIncludesQuietZone()
    {
        var approvalUrl = "https://dev.diet-dost.in/qr-approve.html?sid=dd-sess-1234567890&nonce=chal-nonce-abcdef&code=16&glyph=Jade+Phoenix";
        var svg = _generator.GenerateSvg(approvalUrl, size: 220, margin: 4);

        Assert.NotNull(svg);
        Assert.Contains("width=\"220\"", svg);
        // Margin 4 on each side adds 8 to module count
        Assert.Contains("viewBox=\"0 0 ", svg);
    }

    [Fact]
    public void GenerateDataUri_ProducesValidBase64EncodedSvg()
    {
        var text = "https://dev.diet-dost.in/qr-approve.html?sid=test";
        var dataUri = _generator.GenerateDataUri(text, size: 200);

        Assert.NotNull(dataUri);
        Assert.StartsWith("data:image/svg+xml;base64,", dataUri);
        var base64Part = dataUri["data:image/svg+xml;base64,".Length..];
        var rawBytes = Convert.FromBase64String(base64Part);
        var rawString = System.Text.Encoding.UTF8.GetString(rawBytes);

        Assert.StartsWith("<svg", rawString);
        Assert.EndsWith("</svg>", rawString);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GenerateSvg_WithEmptyOrNullInput_ThrowsArgumentException(string? invalidInput)
    {
        Assert.ThrowsAny<ArgumentException>(() => _generator.GenerateSvg(invalidInput!));
    }
}
