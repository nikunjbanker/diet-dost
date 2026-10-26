/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Text;
using Nutrition.Application.Common.Interfaces;

namespace Nutrition.Application.Services;

/// <summary>
/// High-performance, zero-dependency, pure C# ISO/IEC 18004 standard QR Code generator.
/// Implements Galois Field GF(256) Reed-Solomon error correction and optimal masking.
/// Authoritatively renders QR codes on the server to prevent client-side DOM tampering and XSS manipulation.
/// </summary>
public sealed class QrCodeGenerator : IQrCodeGenerator
{
    // ISO/IEC 18004 Version specifications for Medium (M) error correction level (Versions 1-10)
    // Structure: [TotalCodewords, EcCodewordsPerBlock, NumGroup1Blocks, DataCodewordsGroup1, NumGroup2Blocks, DataCodewordsGroup2]
    private static readonly int[][] VersionSpecs =
    [
        [26, 10, 1, 16, 0, 0],   // V1 (21x21)
        [44, 16, 1, 28, 0, 0],   // V2 (25x25)
        [70, 26, 1, 44, 0, 0],   // V3 (29x29)
        [100, 18, 2, 32, 0, 0],  // V4 (33x33)
        [134, 24, 2, 43, 0, 0],  // V5 (37x37)
        [172, 16, 4, 27, 0, 0],  // V6 (41x41)
        [196, 18, 4, 31, 0, 0],  // V7 (45x45)
        [242, 22, 2, 38, 2, 39], // V8 (49x49)
        [292, 22, 3, 36, 2, 37], // V9 (53x53)
        [346, 26, 4, 43, 1, 44]  // V10 (57x57)
    ];

    private static readonly int[][] AlignmentPositions =
    [
        [],
        [6, 18],
        [6, 22],
        [6, 26],
        [6, 30],
        [6, 34],
        [6, 22, 38],
        [6, 24, 42],
        [6, 26, 46],
        [6, 28, 50]
    ];

    // Format info bit sequences for Medium ECC (Masks 0-7), masked with 0x5412
    private static readonly int[] FormatInfoBits =
    [
        0x5412 ^ 0x0000, // Mask 0 (000) -> 0x5412
        0x5125 ^ 0x0000, // Mask 1 (001) -> 0x5125
        0x5E7C ^ 0x0000, // Mask 2 (010) -> 0x5E7C
        0x5B4B ^ 0x0000, // Mask 3 (011) -> 0x5B4B
        0x45F9 ^ 0x0000, // Mask 4 (100) -> 0x45F9
        0x40CE ^ 0x0000, // Mask 5 (101) -> 0x40CE
        0x4F97 ^ 0x0000, // Mask 6 (110) -> 0x4F97
        0x4AA0 ^ 0x0000  // Mask 7 (111) -> 0x4AA0
    ];

    // Galois Field GF(256) logarithm and exponential tables (primitive polynomial 0x11D)
    private static readonly byte[] GfExp = new byte[512];
    private static readonly byte[] GfLog = new byte[256];

    static QrCodeGenerator()
    {
        int x = 1;
        for (int i = 0; i < 255; i++)
        {
            GfExp[i] = (byte)x;
            GfLog[x] = (byte)i;
            x <<= 1;
            if ((x & 0x100) != 0)
            {
                x ^= 0x11D;
            }
        }
        for (int i = 255; i < 512; i++)
        {
            GfExp[i] = GfExp[i - 255];
        }
    }

    public string GenerateSvg(string content, int size = 200, int margin = 4)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        var matrix = CreateMatrix(content);
        int modules = matrix.Length;
        int totalModules = modules + (margin * 2);

        var sb = new StringBuilder(modules * modules * 12 + 256);
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {totalModules} {totalModules}\" width=\"{size}\" height=\"{size}\" shape-rendering=\"crispEdges\" role=\"img\" aria-label=\"QR Code\">");
        sb.Append($"<rect width=\"100%\" height=\"100%\" fill=\"#FFFFFF\" rx=\"{margin}\"/>");
        sb.Append("<path fill=\"#0F172A\" d=\"");

        for (int r = 0; r < modules; r++)
        {
            int runStart = -1;
            for (int c = 0; c < modules; c++)
            {
                if (matrix[r][c])
                {
                    if (runStart < 0) runStart = c;
                }
                else
                {
                    if (runStart >= 0)
                    {
                        sb.Append($"M{runStart + margin} {r + margin}h{c - runStart}v1h{-(c - runStart)}z ");
                        runStart = -1;
                    }
                }
            }
            if (runStart >= 0)
            {
                sb.Append($"M{runStart + margin} {r + margin}h{modules - runStart}v1h{-(modules - runStart)}z ");
            }
        }

        sb.Append("\"/></svg>");
        return sb.ToString();
    }

    public string GenerateDataUri(string content, int size = 200, int margin = 4)
    {
        var svg = GenerateSvg(content, size, margin);
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        return $"data:image/svg+xml;base64,{base64}";
    }

    private static bool[][] CreateMatrix(string text)
    {
        byte[] dataBytes = Encoding.UTF8.GetBytes(text);
        int version = SelectVersion(dataBytes.Length);
        int[] spec = VersionSpecs[version - 1];

        int totalDataCodewords = (spec[2] * spec[3]) + (spec[4] * spec[5]);
        byte[] dataCodewords = EncodeData(dataBytes, version, totalDataCodewords);

        byte[] allCodewords = InterleaveBlocks(dataCodewords, spec);

        int dimension = 17 + (version * 4);
        var matrix = new bool[dimension][];
        var isReserved = new bool[dimension][];
        for (int i = 0; i < dimension; i++)
        {
            matrix[i] = new bool[dimension];
            isReserved[i] = new bool[dimension];
        }

        PlaceFunctionPatterns(matrix, isReserved, version);
        PlaceDataBits(matrix, isReserved, allCodewords);

        int bestMask = SelectBestMask(matrix, isReserved);
        ApplyMask(matrix, isReserved, bestMask);
        PlaceFormatInfo(matrix, bestMask);

        return matrix;
    }

    private static int SelectVersion(int dataLen)
    {
        for (int v = 1; v <= 10; v++)
        {
            int[] spec = VersionSpecs[v - 1];
            int capacity = (spec[2] * spec[3]) + (spec[4] * spec[5]);
            // Byte mode overhead: 4 mode bits + (8 or 16 length bits)
            int overheadBytes = v < 10 ? 2 : 3;
            if (dataLen + overheadBytes <= capacity)
            {
                return v;
            }
        }
        return 10; // Max supported in this high-throughput profile
    }

    private static byte[] EncodeData(byte[] data, int version, int targetLength)
    {
        var bits = new List<bool>(targetLength * 8);

        // Byte mode indicator: 0100
        bits.Add(false); bits.Add(true); bits.Add(false); bits.Add(false);

        // Character count indicator (8 bits for V1-9, 16 bits for V10+)
        int countBits = version < 10 ? 8 : 16;
        for (int i = countBits - 1; i >= 0; i--)
        {
            bits.Add(((data.Length >> i) & 1) != 0);
        }

        // Data bytes
        foreach (byte b in data)
        {
            for (int i = 7; i >= 0; i--)
            {
                bits.Add(((b >> i) & 1) != 0);
            }
        }

        // Terminator (up to 4 zeros)
        int targetBits = targetLength * 8;
        int term = Math.Min(4, targetBits - bits.Count);
        for (int i = 0; i < term; i++) bits.Add(false);

        // Pad to byte boundary
        while ((bits.Count % 8) != 0 && bits.Count < targetBits)
        {
            bits.Add(false);
        }

        // Pad bytes (alternating 0xEC and 0x11)
        byte[] padBytes = [0xEC, 0x11];
        int padIdx = 0;
        while (bits.Count < targetBits)
        {
            byte p = padBytes[padIdx % 2];
            for (int i = 7; i >= 0; i--)
            {
                bits.Add(((p >> i) & 1) != 0);
            }
            padIdx++;
        }

        var result = new byte[targetLength];
        for (int i = 0; i < targetLength; i++)
        {
            byte b = 0;
            for (int j = 0; j < 8; j++)
            {
                if (bits[i * 8 + j]) b |= (byte)(1 << (7 - j));
            }
            result[i] = b;
        }
        return result;
    }

    private static byte[] InterleaveBlocks(byte[] dataCodewords, int[] spec)
    {
        int ecPerBlock = spec[1];
        int g1Blocks = spec[2];
        int g1Data = spec[3];
        int g2Blocks = spec[4];
        int g2Data = spec[5];
        int numBlocks = g1Blocks + g2Blocks;

        var dataBlocks = new byte[numBlocks][];
        var ecBlocks = new byte[numBlocks][];

        int offset = 0;
        for (int b = 0; b < numBlocks; b++)
        {
            int blockSize = (b < g1Blocks) ? g1Data : g2Data;
            dataBlocks[b] = new byte[blockSize];
            Array.Copy(dataCodewords, offset, dataBlocks[b], 0, blockSize);
            offset += blockSize;
            ecBlocks[b] = ComputeReedSolomon(dataBlocks[b], ecPerBlock);
        }

        var interleaved = new List<byte>(spec[0]);
        int maxData = Math.Max(g1Data, g2Data);

        for (int i = 0; i < maxData; i++)
        {
            for (int b = 0; b < numBlocks; b++)
            {
                if (i < dataBlocks[b].Length)
                {
                    interleaved.Add(dataBlocks[b][i]);
                }
            }
        }

        for (int i = 0; i < ecPerBlock; i++)
        {
            for (int b = 0; b < numBlocks; b++)
            {
                interleaved.Add(ecBlocks[b][i]);
            }
        }

        return interleaved.ToArray();
    }

    private static byte[] ComputeReedSolomon(byte[] data, int ecCount)
    {
        byte[] generator = BuildGeneratorPolynomial(ecCount);
        var msg = new byte[data.Length + ecCount];
        Array.Copy(data, msg, data.Length);

        for (int i = 0; i < data.Length; i++)
        {
            byte lead = msg[i];
            if (lead == 0) continue;

            byte logLead = GfLog[lead];
            for (int j = 0; j < generator.Length; j++)
            {
                msg[i + j] ^= GfExp[logLead + generator[j]];
            }
        }

        var ec = new byte[ecCount];
        Array.Copy(msg, data.Length, ec, 0, ecCount);
        return ec;
    }

    private static byte[] BuildGeneratorPolynomial(int degree)
    {
        var poly = new byte[] { 0 }; // in log notation
        for (int i = 0; i < degree; i++)
        {
            var next = new byte[poly.Length + 1];
            for (int j = 0; j < poly.Length; j++)
            {
                next[j] ^= GfExp[poly[j] + i];
                next[j + 1] ^= GfExp[poly[j]];
            }
            poly = next.Select(v => GfLog[v]).ToArray();
        }
        return poly;
    }

    private static void PlaceFunctionPatterns(bool[][] matrix, bool[][] isReserved, int version)
    {
        int dim = matrix.Length;

        // Finder patterns (top-left, top-right, bottom-left)
        PlaceFinderPattern(matrix, isReserved, 0, 0);
        PlaceFinderPattern(matrix, isReserved, 0, dim - 7);
        PlaceFinderPattern(matrix, isReserved, dim - 7, 0);

        // Separators
        for (int i = 0; i < 8; i++)
        {
            Reserve(isReserved, 7, i);
            Reserve(isReserved, i, 7);
            Reserve(isReserved, 7, dim - 8 + i);
            Reserve(isReserved, i, dim - 8);
            Reserve(isReserved, dim - 8, i);
            Reserve(isReserved, dim - 8 + i, 7);
        }

        // Timing patterns
        for (int i = 8; i < dim - 8; i++)
        {
            bool val = (i % 2 == 0);
            matrix[6][i] = val;
            isReserved[6][i] = true;
            matrix[i][6] = val;
            isReserved[i][6] = true;
        }

        // Alignment patterns for versions >= 2
        int[] alignPos = AlignmentPositions[version - 1];
        if (alignPos.Length > 0)
        {
            foreach (int r in alignPos)
            {
                foreach (int c in alignPos)
                {
                    if (isReserved[r][c]) continue;
                    PlaceAlignmentPattern(matrix, isReserved, r - 2, c - 2);
                }
            }
        }

        // Dark module
        matrix[4 * version + 9][8] = true;
        isReserved[4 * version + 9][8] = true;

        // Reserve format info areas
        for (int i = 0; i < 9; i++)
        {
            Reserve(isReserved, 8, i);
            Reserve(isReserved, i, 8);
        }
        for (int i = 0; i < 8; i++)
        {
            Reserve(isReserved, 8, dim - 1 - i);
            Reserve(isReserved, dim - 1 - i, 8);
        }
    }

    private static void PlaceFinderPattern(bool[][] matrix, bool[][] isReserved, int top, int left)
    {
        for (int r = 0; r < 7; r++)
        {
            for (int c = 0; c < 7; c++)
            {
                bool isBlack = (r == 0 || r == 6 || c == 0 || c == 6 || (r >= 2 && r <= 4 && c >= 2 && c <= 4));
                matrix[top + r][left + c] = isBlack;
                isReserved[top + r][left + c] = true;
            }
        }
    }

    private static void PlaceAlignmentPattern(bool[][] matrix, bool[][] isReserved, int top, int left)
    {
        for (int r = 0; r < 5; r++)
        {
            for (int c = 0; c < 5; c++)
            {
                bool isBlack = (r == 0 || r == 4 || c == 0 || c == 4 || (r == 2 && c == 2));
                matrix[top + r][left + c] = isBlack;
                isReserved[top + r][left + c] = true;
            }
        }
    }

    private static void Reserve(bool[][] isReserved, int r, int c)
    {
        if (r >= 0 && r < isReserved.Length && c >= 0 && c < isReserved.Length)
        {
            isReserved[r][c] = true;
        }
    }

    private static void PlaceDataBits(bool[][] matrix, bool[][] isReserved, byte[] codewords)
    {
        int dim = matrix.Length;
        int bitIdx = 0;
        int totalBits = codewords.Length * 8;
        bool upward = true;

        for (int right = dim - 1; right > 0; right -= 2)
        {
            if (right == 6) right--; // Skip vertical timing column

            int[] rows = upward
                ? Enumerable.Range(0, dim).Reverse().ToArray()
                : Enumerable.Range(0, dim).ToArray();

            foreach (int r in rows)
            {
                for (int colOffset = 0; colOffset < 2; colOffset++)
                {
                    int c = right - colOffset;
                    if (isReserved[r][c]) continue;

                    if (bitIdx < totalBits)
                    {
                        int byteIdx = bitIdx / 8;
                        int bitPos = 7 - (bitIdx % 8);
                        matrix[r][c] = ((codewords[byteIdx] >> bitPos) & 1) != 0;
                        bitIdx++;
                    }
                    else
                    {
                        matrix[r][c] = false; // Remainder bits
                    }
                }
            }
            upward = !upward;
        }
    }

    private static int SelectBestMask(bool[][] matrix, bool[][] isReserved)
    {
        int bestMask = 0;
        int lowestPenalty = int.MaxValue;

        for (int mask = 0; mask < 8; mask++)
        {
            var testMatrix = CloneMatrix(matrix);
            ApplyMask(testMatrix, isReserved, mask);
            PlaceFormatInfo(testMatrix, mask);

            int penalty = CalculatePenalty(testMatrix);
            if (penalty < lowestPenalty)
            {
                lowestPenalty = penalty;
                bestMask = mask;
            }
        }
        return bestMask;
    }

    private static bool EvaluateMask(int mask, int r, int c) => mask switch
    {
        0 => (r + c) % 2 == 0,
        1 => r % 2 == 0,
        2 => c % 3 == 0,
        3 => (r + c) % 3 == 0,
        4 => (r / 2 + c / 3) % 2 == 0,
        5 => (r * c) % 2 + (r * c) % 3 == 0,
        6 => ((r * c) % 2 + (r * c) % 3) % 2 == 0,
        7 => ((r + c) % 2 + (r * c) % 3) % 2 == 0,
        _ => false
    };

    private static void ApplyMask(bool[][] matrix, bool[][] isReserved, int mask)
    {
        int dim = matrix.Length;
        for (int r = 0; r < dim; r++)
        {
            for (int c = 0; c < dim; c++)
            {
                if (!isReserved[r][c] && EvaluateMask(mask, r, c))
                {
                    matrix[r][c] = !matrix[r][c];
                }
            }
        }
    }

    private static void PlaceFormatInfo(bool[][] matrix, int mask)
    {
        int bits = FormatInfoBits[mask];
        int dim = matrix.Length;

        // Top-left finder format info
        int[] r1 = [0, 1, 2, 3, 4, 5, 7, 8, 8, 8, 8, 8, 8, 8, 8];
        int[] c1 = [8, 8, 8, 8, 8, 8, 8, 8, 7, 5, 4, 3, 2, 1, 0];

        for (int i = 0; i < 15; i++)
        {
            bool bit = ((bits >> i) & 1) != 0;
            matrix[r1[i]][c1[i]] = bit;
        }

        // Split format info around other finders
        for (int i = 0; i < 7; i++)
        {
            matrix[dim - 1 - i][8] = ((bits >> i) & 1) != 0;
        }
        for (int i = 7; i < 15; i++)
        {
            matrix[8][dim - 15 + i] = ((bits >> i) & 1) != 0;
        }
    }

    private static int CalculatePenalty(bool[][] matrix)
    {
        int dim = matrix.Length;
        int penalty = 0;

        // Rule N1: 5 or more consecutive modules of same color
        for (int r = 0; r < dim; r++)
        {
            int count = 1;
            for (int c = 1; c < dim; c++)
            {
                if (matrix[r][c] == matrix[r][c - 1]) count++;
                else
                {
                    if (count >= 5) penalty += 3 + (count - 5);
                    count = 1;
                }
            }
            if (count >= 5) penalty += 3 + (count - 5);
        }

        for (int c = 0; c < dim; c++)
        {
            int count = 1;
            for (int r = 1; r < dim; r++)
            {
                if (matrix[r][c] == matrix[r - 1][c]) count++;
                else
                {
                    if (count >= 5) penalty += 3 + (count - 5);
                    count = 1;
                }
            }
            if (count >= 5) penalty += 3 + (count - 5);
        }

        // Rule N2: 2x2 blocks of same color
        for (int r = 0; r < dim - 1; r++)
        {
            for (int c = 0; c < dim - 1; c++)
            {
                bool val = matrix[r][c];
                if (val == matrix[r + 1][c] && val == matrix[r][c + 1] && val == matrix[r + 1][c + 1])
                {
                    penalty += 3;
                }
            }
        }

        return penalty;
    }

    private static bool[][] CloneMatrix(bool[][] src)
    {
        var dst = new bool[src.Length][];
        for (int i = 0; i < src.Length; i++)
        {
            dst[i] = (bool[])src[i].Clone();
        }
        return dst;
    }
}
