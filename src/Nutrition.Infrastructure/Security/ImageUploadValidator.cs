/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
namespace Nutrition.Infrastructure.Security;

public static class ImageUploadValidator
{
    public const long MaxSizeBytes = 8 * 1024 * 1024; // 8MB OWASP limit

    private static readonly byte[] JpegHeader = new byte[] { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] PngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
    private static readonly byte[] RiffHeader = new byte[] { 0x52, 0x49, 0x46, 0x46 }; // "RIFF"
    private static readonly byte[] WebpHeader = new byte[] { 0x57, 0x45, 0x42, 0x50 }; // "WEBP"

    /// <summary>
    /// Validates an uploaded image using its size and file signature.
    /// </summary>
    /// <param name="stream">Readable stream positioned at the beginning of the image.</param>
    /// <param name="length">Uploaded file length in bytes.</param>
    /// <returns>A validation result containing an error message or detected MIME type.</returns>
    public static (bool IsValid, string? ErrorMessage, string? MimeType) ValidateImage(Stream stream, long length)
    {
        if (length <= 0)
            return (false, "File is empty.", null);

        if (length > MaxSizeBytes)
            return (false, "File exceeds maximum permitted size of 8MB (OWASP A08).", null);

        if (!stream.CanRead)
            return (false, "Cannot read image stream.", null);

        byte[] header = new byte[12];
        var originalPos = stream.Position;
        int bytesRead = stream.Read(header, 0, header.Length);
        stream.Position = originalPos; // reset position

        if (bytesRead < 4)
            return (false, "Corrupted image file header.", null);

        // JPEG check
        if (header[0] == JpegHeader[0] && header[1] == JpegHeader[1] && header[2] == JpegHeader[2])
            return (true, null, "image/jpeg");

        // PNG check
        if (header.Take(4).SequenceEqual(PngHeader))
            return (true, null, "image/png");

        // WebP check (RIFF at 0..3 and WEBP at 8..11)
        if (bytesRead >= 12 &&
            header.Take(4).SequenceEqual(RiffHeader) &&
            header.Skip(8).Take(4).SequenceEqual(WebpHeader))
        {
            return (true, null, "image/webp");
        }

        return (false, "Invalid image format. Only authentic JPEG, PNG, and WebP files are accepted.", null);
    }
}
