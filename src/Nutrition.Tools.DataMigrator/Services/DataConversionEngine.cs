/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Globalization;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

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
        "Items",
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

        if (columnName.Equals("Status", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToLowerInvariant() switch
            {
                "pending" => 0,
                "scanned" => 1,
                "approved" => 2,
                "rejected" => 3,
                "consumed" => 4,
                "expired" => 5,
                _ => null
            };
        }

        if (columnName.Equals("OperationType", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToLowerInvariant() switch
            {
                "photodetection" => 0,
                "textdetection" => 1,
                "progresscompare" => 2,
                "selflearning" => 3,
                _ => null
            };
        }

        if (columnName.Equals("Channel", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToLowerInvariant() switch
            {
                "email" => 0,
                "sms" => 1,
                _ => null
            };
        }

        if (columnName.Equals("Sex", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToLowerInvariant() switch
            {
                "male" => 0,
                "female" => 1,
                _ => null
            };
        }

        if (columnName.Equals("ActivityLevel", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToLowerInvariant() switch
            {
                "sedentary" => 0,
                "light" => 1,
                "moderate" => 2,
                "active" => 3,
                "veryactive" => 4,
                _ => null
            };
        }

        if (columnName.Equals("DietaryPreference", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToLowerInvariant() switch
            {
                "lactoveg" => 0,
                "ovovegetarian" => 1,
                "lactoovovegetarian" => 2,
                "vegan" => 3,
                "nonveg" => 4,
                "eggitarian" => 5,
                "jain" => 6,
                _ => null
            };
        }

        if (columnName.Equals("DetectionMode", StringComparison.OrdinalIgnoreCase) ||
            columnName.Equals("AiDetectionMode", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToLowerInvariant() switch
            {
                "auto" => 0,
                "gemini" => 1,
                "foundry" => 2,
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

    /// <summary>
    /// Converts a dictionary of tabular relational column values into a native BSON document
    /// for insertion/upsert into Azure DocumentDB collections.
    /// Maps primary key 'Id' (or 'Key' for AppSecret) to '_id'.
    /// Deserializes JSON array strings into native BsonArray documents for richer querying.
    /// </summary>
    public BsonDocument ConvertToBsonDocument(
        string tableName,
        Dictionary<string, object?> row,
        string rowKey,
        TableConversionStats stats)
    {
        var doc = new BsonDocument();

        // Determine primary key name for this table
        var isAppSecret = tableName.Equals("AppSecrets", StringComparison.OrdinalIgnoreCase);
        var pkName = isAppSecret ? "Key" : "Id";

        foreach (var (colName, colVal) in row)
        {
            var isPk = colName.Equals(pkName, StringComparison.OrdinalIgnoreCase);
            var targetPropName = isPk ? "_id" : colName;

            if (colVal == null || colVal == DBNull.Value)
            {
                doc[targetPropName] = BsonNull.Value;
                continue;
            }

            // 1. JSON Array / Document expansion
            if (KnownJsonColumns.Contains(colName))
            {
                var jsonStr = colVal.ToString();
                if (!string.IsNullOrWhiteSpace(jsonStr))
                {
                    var trimmed = jsonStr.Trim();
                    if (trimmed.StartsWith('['))
                    {
                        try
                        {
                            var wrapped = BsonDocument.Parse($"{{\"_items\": {trimmed}}}");
                            doc[targetPropName] = wrapped["_items"].AsBsonArray;
                            continue;
                        }
                        catch
                        {
                            // fallback below
                        }
                    }
                    else if (trimmed.StartsWith('{'))
                    {
                        try
                        {
                            var parsedBson = BsonDocument.Parse(trimmed);
                            doc[targetPropName] = parsedBson;
                            continue;
                        }
                        catch
                        {
                            // fallback below
                        }
                    }
                }
                var normalizedJson = ConvertToJson(colVal, tableName, colName, rowKey, stats);
                try
                {
                    var trimmedNorm = normalizedJson.Trim();
                    if (trimmedNorm.StartsWith('['))
                    {
                        var wrapped = BsonDocument.Parse($"{{\"_items\": {trimmedNorm}}}");
                        doc[targetPropName] = wrapped["_items"].AsBsonArray;
                    }
                    else if (trimmedNorm.StartsWith('{'))
                    {
                        doc[targetPropName] = BsonDocument.Parse(trimmedNorm);
                    }
                    else
                    {
                        doc[targetPropName] = new BsonString(normalizedJson);
                    }
                }
                catch
                {
                    doc[targetPropName] = new BsonString(normalizedJson);
                }
                continue;
            }

            // 2. Boolean Conversion
            if (colVal is bool b)
            {
                doc[targetPropName] = BsonBoolean.Create(b);
                continue;
            }
            if (colName.StartsWith("Is", StringComparison.OrdinalIgnoreCase) ||
                colName.StartsWith("Has", StringComparison.OrdinalIgnoreCase) ||
                colName.StartsWith("Allow", StringComparison.OrdinalIgnoreCase))
            {
                var convertedBool = ConvertToBoolean(colVal, tableName, colName, rowKey, stats);
                doc[targetPropName] = BsonBoolean.Create(convertedBool);
                continue;
            }

            // 3. DateTime / DateOnly Conversion
            if (colVal is DateOnly dOnly)
            {
                var pureDate = DateTime.SpecifyKind(dOnly.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
                doc[targetPropName] = new BsonDateTime(pureDate);
                continue;
            }
            if (colVal is DateTime dt)
            {
                if (colName.Equals("Date", StringComparison.OrdinalIgnoreCase))
                {
                    var pureDate = DateTime.SpecifyKind(dt.Date, DateTimeKind.Utc);
                    doc[targetPropName] = new BsonDateTime(pureDate);
                    continue;
                }
                var utcDt = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
                doc[targetPropName] = new BsonDateTime(utcDt);
                continue;
            }
            if (colName.EndsWith("Utc", StringComparison.OrdinalIgnoreCase) ||
                colName.EndsWith("Date", StringComparison.OrdinalIgnoreCase) ||
                colName.EndsWith("At", StringComparison.OrdinalIgnoreCase))
            {
                var convertedDt = ConvertToDateTime(colVal, tableName, colName, isNullable: true, rowKey, stats);
                if (convertedDt is DateTime validDt)
                {
                    if (colName.Equals("Date", StringComparison.OrdinalIgnoreCase))
                    {
                        var pureDate = DateTime.SpecifyKind(validDt.Date, DateTimeKind.Utc);
                        doc[targetPropName] = new BsonDateTime(pureDate);
                        continue;
                    }
                    var utcDt = validDt.Kind == DateTimeKind.Utc ? validDt : validDt.ToUniversalTime();
                    doc[targetPropName] = new BsonDateTime(utcDt);
                    continue;
                }
                doc[targetPropName] = BsonNull.Value;
                continue;
            }

            // 4. Numeric Floats / Doubles
            if (colVal is double d)
            {
                doc[targetPropName] = double.IsNaN(d) || double.IsInfinity(d) ? BsonDouble.Create(0.0) : BsonDouble.Create(d);
                continue;
            }
            if (colVal is float f)
            {
                doc[targetPropName] = float.IsNaN(f) || float.IsInfinity(f) ? BsonDouble.Create(0.0) : BsonDouble.Create(f);
                continue;
            }
            if (colVal is decimal dec)
            {
                doc[targetPropName] = BsonDouble.Create((double)dec);
                continue;
            }

            // 5. Integers
            if (colVal is int iVal)
            {
                doc[targetPropName] = BsonInt32.Create(iVal);
                continue;
            }
            if (colVal is long lVal)
            {
                doc[targetPropName] = BsonInt64.Create(lVal);
                continue;
            }
            if (colVal is short sVal)
            {
                doc[targetPropName] = BsonInt32.Create(sVal);
                continue;
            }
            if (colVal is byte byteVal)
            {
                doc[targetPropName] = BsonInt32.Create(byteVal);
                continue;
            }

            // Fallback: String representation
            doc[targetPropName] = new BsonString(colVal.ToString() ?? string.Empty);
        }

        return doc;
    }

    /// <summary>
    /// Converts a native BSON document extracted from Azure DocumentDB back into a tabular
    /// relational dictionary suitable for writing into SQLite or Azure SQL.
    /// Maps '_id' back to 'Id' (or 'Key' for AppSecret) and serializes BSON arrays back to JSON strings.
    /// </summary>
    public Dictionary<string, object?> ConvertFromBsonDocument(string tableName, BsonDocument doc)
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var isAppSecret = tableName.Equals("AppSecrets", StringComparison.OrdinalIgnoreCase);
        var targetPkName = isAppSecret ? "Key" : "Id";

        foreach (var element in doc.Elements)
        {
            var colName = element.Name.Equals("_id", StringComparison.OrdinalIgnoreCase)
                ? targetPkName
                : element.Name;

            var val = element.Value;
            if (val.IsBsonNull)
            {
                row[colName] = null;
            }
            else if (val.IsBsonDateTime)
            {
                row[colName] = val.ToUniversalTime();
            }
            else if (val.IsBoolean)
            {
                row[colName] = val.AsBoolean;
            }
            else if (val.IsInt32)
            {
                row[colName] = val.AsInt32;
            }
            else if (val.IsInt64)
            {
                row[colName] = val.AsInt64;
            }
            else if (val.IsDouble)
            {
                row[colName] = val.AsDouble;
            }
            else if (val.IsBsonArray || val.IsBsonDocument)
            {
                row[colName] = val.ToJson();
            }
            else
            {
                row[colName] = val.AsString;
            }
        }

        return row;
    }
}
