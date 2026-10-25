/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Globalization;
using System.Text.Json;

namespace Nutrition.Tools.DataMigrator.Services;

public record ConversionEvent(
    string TableName,
    string RowKey,
    string ColumnName,
    string ConversionType,
    string OriginalValue,
    string ResolvedValue,
    string Details
);

public class TableConversionStats
{
    public string TableName { get; init; } = string.Empty;
    public int TotalRowsRead { get; set; }
    public int DateTimeConversions { get; set; }
    public int BooleanConversions { get; set; }
    public int JsonConversions { get; set; }
    public int NumericConversions { get; set; }
    public int StringTruncations { get; set; }
    public int TotalConversions => DateTimeConversions + BooleanConversions + JsonConversions + NumericConversions + StringTruncations;
    public List<ConversionEvent> SampleConversions { get; } = new();

    public void RecordEvent(ConversionEvent ev)
    {
        if (SampleConversions.Count < 20)
        {
            SampleConversions.Add(ev);
        }
    }
}

public class DataConversionEngine
{
    private static readonly HashSet<string> KnownJsonColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "DiagnosedConditions",
        "Medications",
        "WhoComplianceFlags",
        "MedicationWarnings",
        "EarnedBadges"
    };

#pragma warning disable CA1822 // ConvertValue is designed as an instance method for dependency injection and testing
    public object? ConvertValue(
        string tableName,
        string columnName,
        string targetDataType,
        bool isNullable,
        int? maxLength,
        object? sourceValue,
        string rowKey,
        TableConversionStats stats)
    {
        if (sourceValue == null || sourceValue == DBNull.Value)
        {
            if (!isNullable)
            {
                // Fallback for non-nullable columns with NULL in SQLite
                return ProvideNonNullableFallback(tableName, columnName, targetDataType, rowKey, stats);
            }
            return DBNull.Value;
        }

        var normalizedType = targetDataType.ToLowerInvariant();

        // 1. Boolean / BIT Conversion
        if (normalizedType.Contains("bit") || columnName.StartsWith("Is", StringComparison.OrdinalIgnoreCase) || columnName.StartsWith("Has", StringComparison.OrdinalIgnoreCase) || columnName.StartsWith("Allow", StringComparison.OrdinalIgnoreCase))
        {
            return ConvertToBoolean(sourceValue, tableName, columnName, rowKey, stats);
        }

        // 2. DateTime / DATETIME2 Conversion
        if (normalizedType.Contains("date") || normalizedType.Contains("time") || columnName.EndsWith("Utc", StringComparison.OrdinalIgnoreCase) || columnName.EndsWith("Date", StringComparison.OrdinalIgnoreCase) || columnName.EndsWith("At", StringComparison.OrdinalIgnoreCase))
        {
            return ConvertToDateTime(sourceValue, tableName, columnName, isNullable, rowKey, stats);
        }

        // 3. JSON Collections
        if (KnownJsonColumns.Contains(columnName))
        {
            return ConvertToJson(sourceValue, tableName, columnName, rowKey, stats);
        }

        // 4. Numeric Floats / Real / Decimals
        if (normalizedType.Contains("float") || normalizedType.Contains("real") || normalizedType.Contains("decimal") || normalizedType.Contains("double"))
        {
            return ConvertToFloat(sourceValue, tableName, columnName, isNullable, rowKey, stats);
        }

        // 5. Integers (int, bigint, smallint, tinyint)
        if (normalizedType.Contains("int"))
        {
            return ConvertToInteger(sourceValue, tableName, columnName, isNullable, rowKey, stats);
        }

        // 6. Strings (nvarchar, varchar, text)
        return ConvertToString(sourceValue, tableName, columnName, maxLength, rowKey, stats);
    }

    private static bool ConvertToBoolean(object sourceValue, string tableName, string columnName, string rowKey, TableConversionStats stats)
    {
        if (sourceValue is bool b)
        {
            return b;
        }

        bool result;
        var originalStr = sourceValue.ToString() ?? "";

        if (sourceValue is int or long or short or byte)
        {
            var num = Convert.ToInt64(sourceValue, CultureInfo.InvariantCulture);
            result = num != 0;
            stats.BooleanConversions++;
            stats.RecordEvent(new(tableName, rowKey, columnName, "IntegerToBit", originalStr, result ? "1" : "0", $"Mapped integer {num} to boolean {result}"));
            return result;
        }

        var trimmed = originalStr.Trim();
        if (trimmed == "1" || string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "yes", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
        }
        else
        {
            result = false;
        }

        stats.BooleanConversions++;
        stats.RecordEvent(new(tableName, rowKey, columnName, "StringToBit", originalStr, result ? "1" : "0", $"Parsed string '{originalStr}' to boolean {result}"));
        return result;
    }

    private static object ConvertToDateTime(object sourceValue, string tableName, string columnName, bool isNullable, string rowKey, TableConversionStats stats)
    {
        if (sourceValue is DateTime dt)
        {
            var utcDt = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
            return ClampDateTime(utcDt);
        }

        var originalStr = sourceValue.ToString()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(originalStr))
        {
            if (isNullable)
            {
                return DBNull.Value;
            }
            var fallback = DateTime.UtcNow;
            stats.DateTimeConversions++;
            stats.RecordEvent(new(tableName, rowKey, columnName, "EmptyToUtcFallback", "<empty>", fallback.ToString("o"), "Empty non-nullable timestamp replaced with UtcNow"));
            return fallback;
        }

        // Check if numeric unix timestamp
        if (long.TryParse(originalStr, CultureInfo.InvariantCulture, out var unixTime))
        {
            DateTime converted;
            if (unixTime > 1000000000000L) // milliseconds
            {
                converted = DateTimeOffset.FromUnixTimeMilliseconds(unixTime).UtcDateTime;
            }
            else if (unixTime > 100000000L) // seconds
            {
                converted = DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
            }
            else
            {
                converted = DateTime.UtcNow;
            }

            stats.DateTimeConversions++;
            stats.RecordEvent(new(tableName, rowKey, columnName, "EpochToDateTime", originalStr, converted.ToString("o"), "Unix epoch converted to UTC DateTime"));
            return ClampDateTime(converted);
        }

        // Parse ISO 8601 or standard date string formats
        string[] formats =
        {
            "yyyy-MM-dd HH:mm:ss.FFFFFFF",
            "yyyy-MM-dd HH:mm:ss.FFF",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.FFFFFFFZ",
            "yyyy-MM-ddTHH:mm:ss.FFFZ",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
            "yyyy-MM-ddTHH:mm:ss.FFF",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-dd",
            "o",
            "s"
        };

        if (DateTime.TryParseExact(originalStr, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            var utcParsed = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            stats.DateTimeConversions++;
            stats.RecordEvent(new(tableName, rowKey, columnName, "NormalizedToUtc", originalStr, utcParsed.ToString("o"), "Normalized timestamp to universal UTC DateTime"));
            return ClampDateTime(utcParsed);
        }

        if (DateTime.TryParse(originalStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var fallbackParsed))
        {
            var utcFallback = DateTime.SpecifyKind(fallbackParsed, DateTimeKind.Utc);
            stats.DateTimeConversions++;
            stats.RecordEvent(new(tableName, rowKey, columnName, "FuzzyParsedToUtc", originalStr, utcFallback.ToString("o"), "Fuzzy parsed timestamp to universal UTC DateTime"));
            return ClampDateTime(utcFallback);
        }

        if (isNullable)
        {
            stats.DateTimeConversions++;
            stats.RecordEvent(new(tableName, rowKey, columnName, "UnparseableToNull", originalStr, "NULL", "Unparseable timestamp set to NULL on nullable column"));
            return DBNull.Value;
        }

        var defaultUtc = DateTime.UtcNow;
        stats.DateTimeConversions++;
        stats.RecordEvent(new(tableName, rowKey, columnName, "UnparseableToUtcNow", originalStr, defaultUtc.ToString("o"), "Unparseable non-nullable timestamp set to UtcNow"));
        return defaultUtc;
    }

    private static DateTime ClampDateTime(DateTime dt)
    {
        // Safe boundaries for SQL Server DATETIME2 / DATETIME
        var min = new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var max = new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        if (dt < min) return min;
        if (dt > max) return max;
        return dt;
    }

    private static string ConvertToJson(object sourceValue, string tableName, string columnName, string rowKey, TableConversionStats stats)
    {
        var rawStr = sourceValue.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(rawStr))
        {
            stats.JsonConversions++;
            stats.RecordEvent(new(tableName, rowKey, columnName, "EmptyJsonToEmptyArray", "<empty>", "[]", "Initialized empty JSON collection to '[]'"));
            return "[]";
        }

        try
        {
            using var doc = JsonDocument.Parse(rawStr);
            return rawStr; // Valid JSON
        }
        catch (JsonException ex)
        {
            // If it's a comma-separated list, wrap it into JSON array
            if (!rawStr.StartsWith('[') && !rawStr.StartsWith('{') && rawStr.Contains(','))
            {
                var items = rawStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var json = JsonSerializer.Serialize(items);
                stats.JsonConversions++;
                stats.RecordEvent(new(tableName, rowKey, columnName, "CsvToJsonArray", rawStr, json, "Converted comma-separated string to valid JSON array"));
                return json;
            }

            // Fallback to empty array
            stats.JsonConversions++;
            stats.RecordEvent(new(tableName, rowKey, columnName, "CorruptedJsonRepair", rawStr, "[]", $"Repaired corrupted JSON ({ex.Message}) to '[]'"));
            return "[]";
        }
    }

    private static object ConvertToFloat(object sourceValue, string tableName, string columnName, bool isNullable, string rowKey, TableConversionStats stats)
    {
        if (sourceValue is double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                stats.NumericConversions++;
                stats.RecordEvent(new(tableName, rowKey, columnName, "NaNToZero", d.ToString(), "0.0", "Replaced NaN/Infinity float with 0.0"));
                return 0.0;
            }
            return d;
        }

        if (sourceValue is float f)
        {
            if (float.IsNaN(f) || float.IsInfinity(f))
            {
                stats.NumericConversions++;
                stats.RecordEvent(new(tableName, rowKey, columnName, "NaNToZero", f.ToString(), "0.0", "Replaced NaN/Infinity float with 0.0"));
                return 0.0;
            }
            return (double)f;
        }

        var str = sourceValue.ToString()?.Trim() ?? "";
        if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            if (double.IsNaN(parsed) || double.IsInfinity(parsed))
            {
                stats.NumericConversions++;
                stats.RecordEvent(new(tableName, rowKey, columnName, "NaNToZero", str, "0.0", "Replaced NaN/Infinity float with 0.0"));
                return 0.0;
            }
            return parsed;
        }

        if (isNullable)
        {
            return DBNull.Value;
        }

        stats.NumericConversions++;
        stats.RecordEvent(new(tableName, rowKey, columnName, "UnparseableFloatToZero", str, "0.0", "Replaced non-numeric float with 0.0"));
        return 0.0;
    }

    private static object ConvertToInteger(object sourceValue, string tableName, string columnName, bool isNullable, string rowKey, TableConversionStats stats)
    {
        if (sourceValue is int i) return i;
        if (sourceValue is long l) return (int)l;
        if (sourceValue is short s) return (int)s;
        if (sourceValue is byte b) return (int)b;

        var str = sourceValue.ToString()?.Trim() ?? "";
        if (int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
        {
            return parsedInt;
        }

        // Could be enum name (e.g. "User", "Admin", "SuperAdmin", "Free", "Basic", "Premium")
        var enumVal = ResolveKnownEnum(columnName, str);
        if (enumVal.HasValue)
        {
            stats.NumericConversions++;
            stats.RecordEvent(new(tableName, rowKey, columnName, "EnumNameToCode", str, enumVal.Value.ToString(), $"Resolved enum string '{str}' to code {enumVal.Value}"));
            return enumVal.Value;
        }

        if (isNullable)
        {
            return DBNull.Value;
        }

        stats.NumericConversions++;
        stats.RecordEvent(new(tableName, rowKey, columnName, "UnparseableIntToZero", str, "0", "Replaced non-numeric integer with 0"));
        return 0;
    }

    private static int? ResolveKnownEnum(string columnName, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (columnName.Equals("Role", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToLowerInvariant() switch
            {
                "user" => 0,
                "admin" => 1,
                "superadmin" => 2,
                _ => null
            };
        }

        if (columnName.Equals("Tier", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToLowerInvariant() switch
            {
                "free" => 0,
                "basic" => 1,
                "premium" => 2,
                "superadmin" => 3,
                _ => null
            };
        }

        if (columnName.Equals("MealType", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToLowerInvariant() switch
            {
                "breakfast" => 0,
                "morningtea" => 1,
                "lunch" => 2,
                "eveningtea" => 3,
                "dinner" => 4,
                "snack" => 5,
                _ => null
            };
        }

        if (columnName.Equals("PhotoType", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToLowerInvariant() switch
            {
                "face" => 0,
                "fullbodyfront" => 1,
                "fullbodyside" => 2,
                _ => null
            };
        }

        return null;
    }

    private static string ConvertToString(object sourceValue, string tableName, string columnName, int? maxLength, string rowKey, TableConversionStats stats)
    {
        var raw = sourceValue.ToString() ?? "";
        // Strip null characters
        if (raw.Contains('\0'))
        {
            raw = raw.Replace("\0", "");
            stats.StringTruncations++;
            stats.RecordEvent(new(tableName, rowKey, columnName, "NullByteStrip", "<raw>", raw, "Stripped null-byte characters from string"));
        }

        if (maxLength.HasValue && maxLength.Value > 0 && raw.Length > maxLength.Value)
        {
            var truncated = raw[..maxLength.Value];
            stats.StringTruncations++;
            stats.RecordEvent(new(tableName, rowKey, columnName, "StringTruncated", raw[..Math.Min(raw.Length, 30)] + "...", truncated[..Math.Min(truncated.Length, 30)] + "...", $"Truncated string from {raw.Length} to {maxLength.Value} characters"));
            return truncated;
        }

        return raw;
    }

    private static object ProvideNonNullableFallback(string tableName, string columnName, string targetDataType, string rowKey, TableConversionStats stats)
    {
        var normalized = targetDataType.ToLowerInvariant();
        if (normalized.Contains("bit"))
        {
            stats.BooleanConversions++;
            stats.RecordEvent(new(tableName, rowKey, columnName, "NullToBitFalse", "NULL", "0", "Non-nullable boolean defaulted to false"));
            return false;
        }
        if (normalized.Contains("date") || normalized.Contains("time"))
        {
            var now = DateTime.UtcNow;
            stats.DateTimeConversions++;
            stats.RecordEvent(new(tableName, rowKey, columnName, "NullToUtcNow", "NULL", now.ToString("o"), "Non-nullable timestamp defaulted to UtcNow"));
            return now;
        }
        if (normalized.Contains("int"))
        {
            stats.NumericConversions++;
            stats.RecordEvent(new(tableName, rowKey, columnName, "NullToIntZero", "NULL", "0", "Non-nullable integer defaulted to 0"));
            return 0;
        }
        if (normalized.Contains("float") || normalized.Contains("real") || normalized.Contains("decimal"))
        {
            stats.NumericConversions++;
            stats.RecordEvent(new(tableName, rowKey, columnName, "NullToFloatZero", "NULL", "0.0", "Non-nullable float defaulted to 0.0"));
            return 0.0;
        }

        // Strings
        if (KnownJsonColumns.Contains(columnName))
        {
            stats.JsonConversions++;
            stats.RecordEvent(new(tableName, rowKey, columnName, "NullToJsonArray", "NULL", "[]", "Non-nullable JSON collection defaulted to '[]'"));
            return "[]";
        }

        return string.Empty;
    }
}
