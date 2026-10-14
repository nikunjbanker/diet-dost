/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.IO;
using SkiaSharp;

namespace Nutrition.Infrastructure.AI;

public static class ImageOptimizationHelper
{
    public const int DefaultMaxDimension = 1280;
    public const int DefaultQuality = 82; // 82% quality gives crisp food detail, avoids artifacts, while saving 70-85% file size
    public const int MaxSizeBytesForVision = 350 * 1024; // 350 KB

    /// <summary>
    /// Optimizes an image stream/bytes for AI Vision analysis.
    /// Preserves exact aspect ratio, downsamples if dimensions exceed maxDimension,
    /// and compresses to optimal quality JPEG (~82%).
    /// </summary>
    public static (byte[] Bytes, string MimeType, int Width, int Height) OptimizeForVision(
        byte[] inputBytes,
        string originalMimeType,
        int maxDimension = DefaultMaxDimension,
        int quality = DefaultQuality)
    {
        if (inputBytes == null || inputBytes.Length == 0)
        {
            return (inputBytes ?? Array.Empty<byte>(), originalMimeType, 0, 0);
        }

        try
        {
            using var inputStream = new MemoryStream(inputBytes);
            using var codec = SKCodec.Create(inputStream);
            if (codec == null)
            {
                var (hdrW, hdrH) = TryGetDimensionsFromHeader(inputBytes);
                return (inputBytes, originalMimeType, hdrW, hdrH);
            }

            int origW = codec.Info.Width;
            int origH = codec.Info.Height;

            // If already well within size and dimensions, keep as-is
            if (origW <= maxDimension && origH <= maxDimension && inputBytes.Length <= MaxSizeBytesForVision)
            {
                return (inputBytes, originalMimeType, origW, origH);
            }

            // Maintain exact aspect ratio
            int targetW = origW;
            int targetH = origH;
            if (origW > maxDimension || origH > maxDimension)
            {
                float ratio = Math.Min((float)maxDimension / origW, (float)maxDimension / origH);
                targetW = Math.Max(1, (int)Math.Round(origW * ratio));
                targetH = Math.Max(1, (int)Math.Round(origH * ratio));
            }

            using var originalBitmap = SKBitmap.Decode(codec);
            if (originalBitmap == null)
            {
                return (inputBytes, originalMimeType, origW, origH);
            }

            SKBitmap processedBitmap;
            bool ownsProcessedBitmap = false;

            if (targetW != origW || targetH != origH)
            {
                var resized = originalBitmap.Resize(new SKImageInfo(targetW, targetH), new SKSamplingOptions(SKCubicResampler.CatmullRom));
                if (resized != null)
                {
                    processedBitmap = resized;
                    ownsProcessedBitmap = true;
                }
                else
                {
                    processedBitmap = originalBitmap;
                    targetW = origW;
                    targetH = origH;
                }
            }
            else
            {
                processedBitmap = originalBitmap;
            }

            try
            {
                using var image = SKImage.FromBitmap(processedBitmap);
                using var encodedData = image.Encode(SKEncodedImageFormat.Jpeg, quality);

                if (encodedData != null && encodedData.Size > 0)
                {
                    byte[] optimized = encodedData.ToArray();
                    // Use optimized bytes if they reduce size or if dimensions changed
                    if (optimized.Length < inputBytes.Length || targetW != origW)
                    {
                        return (optimized, "image/jpeg", targetW, targetH);
                    }
                }
            }
            finally
            {
                if (ownsProcessedBitmap)
                {
                    processedBitmap.Dispose();
                }
            }

            return (inputBytes, originalMimeType, origW, origH);
        }
        catch
        {
            // Graceful fallback on any decoding error, preserving dimensions if available
            var (hdrW, hdrH) = TryGetDimensionsFromHeader(inputBytes);
            return (inputBytes, originalMimeType, hdrW, hdrH);
        }
    }

    /// <summary>
    /// Reads dimensions directly from raw JPEG or PNG file headers without native codec dependency.
    /// Guarantees dimension preservation even on bare-metal or minimal container environments.
    /// </summary>
    public static (int Width, int Height) TryGetDimensionsFromHeader(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 16) return (0, 0);

        // 1. JPEG (0xFF, 0xD8)
        if (bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            int i = 2;
            while (i < bytes.Length - 8)
            {
                if (bytes[i] != 0xFF) { i++; continue; }
                byte marker = bytes[i + 1];
                if (marker == 0xD9 || marker == 0xDA) break; // EOI or SOS
                // SOF0 .. SOF15 (excluding DHT 0xC4, JPG 0xC8, DAC 0xCC)
                if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                {
                    if (i + 8 < bytes.Length)
                    {
                        int height = (bytes[i + 5] << 8) | bytes[i + 6];
                        int width = (bytes[i + 7] << 8) | bytes[i + 8];
                        return (width, height);
                    }
                }
                int length = (bytes[i + 2] << 8) | bytes[i + 3];
                if (length < 2) break;
                i += 2 + length;
            }
        }

        // 2. PNG (8-byte signature: 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)
        if (bytes.Length >= 24 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            int width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
            int height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
            return (width, height);
        }

        return (0, 0);
    }
}
