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
                return (inputBytes, originalMimeType, 0, 0);
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
            // Graceful fallback on any decoding error
            return (inputBytes, originalMimeType, 0, 0);
        }
    }
}
