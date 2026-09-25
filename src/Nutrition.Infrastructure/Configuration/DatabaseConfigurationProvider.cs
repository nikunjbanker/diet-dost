using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Nutrition.Infrastructure.Configuration;

/// <summary>
/// Custom ConfigurationProvider that loads application secrets from the database table 'AppSecrets'
/// directly into the ASP.NET Core IConfiguration hierarchy at application startup.
/// </summary>
public class DatabaseConfigurationProvider : ConfigurationProvider
{
    private readonly string _connectionString;

    public DatabaseConfigurationProvider(string connectionString)
    {
        _connectionString = string.IsNullOrWhiteSpace(connectionString)
            ? "Data Source=diettracker.db"
            : connectionString;
    }

    public override void Load()
    {
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            // 1. Ensure table exists
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS ""AppSecrets"" (
                        ""Key"" TEXT NOT NULL CONSTRAINT ""PK_AppSecrets"" PRIMARY KEY,
                        ""Value"" TEXT NOT NULL,
                        ""Description"" TEXT NULL,
                        ""CreatedAtUtc"" TEXT NOT NULL,
                        ""UpdatedAtUtc"" TEXT NOT NULL
                    );";
                cmd.ExecuteNonQuery();
            }

            // 2. Ensure initial default secrets are seeded if not present
            SeedInitialSecretIfNotExists(
                conn,
                "Jwt:Key",
                "DietDost_SecretKey_For_Jwt_HMAC_SHA256_Authentication_2026_Minimum32BytesRequired!",
                "Cryptographic signing key for JWT HMAC-SHA256 tokens");

            SeedInitialSecretIfNotExists(
                conn,
                "Auth:DemoPassword",
                "DietDost@Demo2026!",
                "Deterministic password for seeded demo tier accounts");

            // 3. Load all secrets into Data dictionary
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"SELECT ""Key"", ""Value"" FROM ""AppSecrets"";";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var key = reader.GetString(0);
                    var val = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    Data[key] = val;
                }
            }
        }
        catch
        {
            // If database is not ready or during tooling/design-time scenarios, silently proceed
        }
    }

    private static void SeedInitialSecretIfNotExists(SqliteConnection conn, string key, string value, string description)
    {
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"SELECT COUNT(*) FROM ""AppSecrets"" WHERE ""Key"" = $key;";
        checkCmd.Parameters.AddWithValue("$key", key);
        var count = Convert.ToInt64(checkCmd.ExecuteScalar());
        if (count == 0)
        {
            using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO ""AppSecrets"" (""Key"", ""Value"", ""Description"", ""CreatedAtUtc"", ""UpdatedAtUtc"")
                VALUES ($key, $val, $desc, $created, $updated);";
            insertCmd.Parameters.AddWithValue("$key", key);
            insertCmd.Parameters.AddWithValue("$val", value);
            insertCmd.Parameters.AddWithValue("$desc", description);
            insertCmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
            insertCmd.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O"));
            insertCmd.ExecuteNonQuery();
        }
    }
}

public class DatabaseConfigurationSource : IConfigurationSource
{
    private readonly string _connectionString;

    public DatabaseConfigurationSource(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DatabaseConfigurationProvider(_connectionString);
    }
}

public static class DatabaseConfigurationExtensions
{
    public static IConfigurationBuilder AddDatabaseSecrets(this IConfigurationBuilder builder, string connectionString)
    {
        return builder.Add(new DatabaseConfigurationSource(connectionString));
    }
}
