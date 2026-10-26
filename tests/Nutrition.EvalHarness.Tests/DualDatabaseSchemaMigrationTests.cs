/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Domain.Model.Identity;
using Nutrition.Infrastructure.Persistence;
using Nutrition.WebGateway.Extensions;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

/// <summary>
/// Comprehensive contract & integration tests verifying dual-database (SQLite & Azure SQL / SQL Server)
/// schema initialization, incremental table reconciliation, and zero Error 207 failures during deployment.
/// </summary>
public class DualDatabaseSchemaMigrationTests
{
    private const string LocalDbMaster = "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Database=master;TrustServerCertificate=true;";

    [Fact]
    public async Task EnsureDatabaseCreatedAndMigrated_Sqlite_ReconcilesLegacyUsersTableAndQrSessions()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"dietdost_legacy_test_{Guid.NewGuid():N}.db");
        try
        {
            // Step 1: Create an existing/legacy SQLite DB with older schema (before PR #110)
            using (var conn = new SqliteConnection($"Data Source={tempDbPath}"))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE ""Meals"" (
                        ""Id"" TEXT NOT NULL PRIMARY KEY,
                        ""UserId"" TEXT NOT NULL
                    );
                    CREATE TABLE ""Users"" (
                        ""Id"" TEXT NOT NULL PRIMARY KEY,
                        ""Email"" TEXT NOT NULL,
                        ""NormalizedEmail"" TEXT NOT NULL,
                        ""MobileNumber"" TEXT NOT NULL,
                        ""NormalizedMobileNumber"" TEXT NOT NULL,
                        ""PasswordHash"" TEXT NOT NULL,
                        ""SecurityStamp"" TEXT NOT NULL,
                        ""Role"" INTEGER NOT NULL,
                        ""Tier"" INTEGER NOT NULL,
                        ""IsEmailVerified"" INTEGER NOT NULL,
                        ""IsMobileVerified"" INTEGER NOT NULL,
                        ""IsActive"" INTEGER NOT NULL,
                        ""CreatedAtUtc"" TEXT NOT NULL
                    );
                    INSERT INTO ""Users"" (""Id"", ""Email"", ""NormalizedEmail"", ""MobileNumber"", ""NormalizedMobileNumber"", ""PasswordHash"", ""SecurityStamp"", ""Role"", ""Tier"", ""IsEmailVerified"", ""IsMobileVerified"", ""IsActive"", ""CreatedAtUtc"")
                    VALUES ('user-legacy-1', 'legacy@dietdost.app', 'LEGACY@DIETDOST.APP', '+919876543210', '+919876543210', 'hash', 'stamp', 0, 0, 1, 1, 1, '2026-01-01');
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            // Step 2: Run migration on this existing SQLite database
            var options = new DbContextOptionsBuilder<DietTrackerDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .Options;

            using (var db = new DietTrackerDbContext(options))
            {
                await DatabaseInitializationExtensions.EnsureDatabaseCreatedAndMigratedAsync(db, NullLogger.Instance);

                // Step 3: Querying Users must succeed without column missing errors
                var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == "LEGACY@DIETDOST.APP");
                Assert.NotNull(user);
                Assert.False(user.IsAnonymous);
                Assert.Null(user.DisplayName);
                Assert.Null(user.RecoveryLookupId);

                // Step 4: QrSessions table must exist and accept new sessions
                var session = QrLoginSession.CreateNew(
                    challengeNonce: "test-nonce-123",
                    browserSecretHash: "test-hash-456",
                    confirmationCode: "42",
                    confirmationGlyph: "🟢 Emerald Falcon");

                db.QrSessions.Add(session);
                await db.SaveChangesAsync();

                var fetchedSession = await db.QrSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
                Assert.NotNull(fetchedSession);
                Assert.Equal("42", fetchedSession.ConfirmationCode);
            }
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task EnsureSqlServerDatabaseMigrated_SqlServer_ReconcilesLegacyUsersTableAndQrSessions()
    {
        var dbName = $"DietDost_MigrationTest_{Guid.NewGuid():N}";
        var connStr = $"Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Database={dbName};TrustServerCertificate=true;";

        try
        {
            // Step 1: Create isolated test database on LocalDB
            using (var masterConn = new SqlConnection(LocalDbMaster))
            {
                await masterConn.OpenAsync();
                using var cmd = masterConn.CreateCommand();
                cmd.CommandText = $"CREATE DATABASE [{dbName}];";
                await cmd.ExecuteNonQueryAsync();
            }

            // Step 2: Seed older schema (simulating pre-existing Azure SQL database before PR #110)
            using (var testConn = new SqlConnection(connStr))
            {
                await testConn.OpenAsync();
                using var cmd = testConn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE [Meals] (
                        [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                        [UserId] NVARCHAR(450) NOT NULL
                    );
                    CREATE TABLE [Users] (
                        [Id] NVARCHAR(450) NOT NULL CONSTRAINT [PK_Users] PRIMARY KEY,
                        [Email] NVARCHAR(256) NOT NULL,
                        [NormalizedEmail] NVARCHAR(256) NOT NULL,
                        [MobileNumber] NVARCHAR(50) NOT NULL,
                        [NormalizedMobileNumber] NVARCHAR(50) NOT NULL,
                        [PasswordHash] NVARCHAR(MAX) NOT NULL,
                        [SecurityStamp] NVARCHAR(MAX) NOT NULL,
                        [Role] INT NOT NULL,
                        [Tier] INT NOT NULL,
                        [IsEmailVerified] BIT NOT NULL,
                        [IsMobileVerified] BIT NOT NULL,
                        [IsActive] BIT NOT NULL,
                        [CreatedAtUtc] DATETIME2 NOT NULL
                    );
                    INSERT INTO [Users] ([Id], [Email], [NormalizedEmail], [MobileNumber], [NormalizedMobileNumber], [PasswordHash], [SecurityStamp], [Role], [Tier], [IsEmailVerified], [IsMobileVerified], [IsActive], [CreatedAtUtc])
                    VALUES ('superadmin-root', 'superadmin@dietdost.app', 'SUPERADMIN@DIETDOST.APP', '+919999999990', '+919999999990', 'hash', 'stamp', 3, 3, 1, 1, 1, SYSUTCDATETIME());
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            // Step 3: Run migration on this existing SQL Server database
            var options = new DbContextOptionsBuilder<DietTrackerDbContext>()
                .UseSqlServer(connStr)
                .Options;

            using (var db = new DietTrackerDbContext(options))
            {
                await DatabaseInitializationExtensions.EnsureSqlServerDatabaseMigratedAsync(db, NullLogger.Instance);

                // Step 4: Querying Users must NOT throw Error 207 ("Invalid column name 'IsAnonymous'...")
                var superAdmin = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == "SUPERADMIN@DIETDOST.APP");
                Assert.NotNull(superAdmin);
                Assert.False(superAdmin.IsAnonymous);
                Assert.Null(superAdmin.DisplayName);
                Assert.Null(superAdmin.RecoveryLookupId);

                // Step 5: QrSessions table must exist and accept new sessions
                var session = QrLoginSession.CreateNew(
                    challengeNonce: "sql-nonce-789",
                    browserSecretHash: "sql-hash-012",
                    confirmationCode: "88",
                    confirmationGlyph: "🔥 Crimson Tiger");

                db.QrSessions.Add(session);
                await db.SaveChangesAsync();

                var fetchedSession = await db.QrSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
                Assert.NotNull(fetchedSession);
                Assert.Equal("88", fetchedSession.ConfirmationCode);
            }
        }
        finally
        {
            // Cleanup database on LocalDB
            try
            {
                using var masterConn = new SqlConnection(LocalDbMaster);
                await masterConn.OpenAsync();
                using var cmd = masterConn.CreateCommand();
                cmd.CommandText = $@"
                    IF EXISTS (SELECT name FROM sys.databases WHERE name = N'{dbName}')
                    BEGIN
                        ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        DROP DATABASE [{dbName}];
                    END";
                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
