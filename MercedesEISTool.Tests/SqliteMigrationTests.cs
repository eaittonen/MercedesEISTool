using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MercedesEISTool.Server.Data;
using MercedesEISTool.Server.Models;

namespace MercedesEISTool.Tests;

public class SqliteMigrationTests
{
    [Fact]
    public void Migration_CreatesOrganizationAndRebuildsUsers_ForFreshDatabase()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"fresh-{Guid.NewGuid():N}.sqlite");
        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            using (var context = new ApplicationDbContext(options))
            {
                context.Database.Migrate();
            }

            using (var context = new ApplicationDbContext(options))
            {
                Assert.True(context.Organizations.Any());
                Assert.Equal(1, context.Organizations.Count());

                var columns = GetTableColumns(context, "AspNetUsers");
                Assert.Contains("OrganizationId", columns);
            }
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public void Migration_PreservesExistingUsersAndIdentityData_ForLegacyDatabase()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"legacy-{Guid.NewGuid():N}.sqlite");
        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = @"
                CREATE TABLE AspNetUsers (
                    Id TEXT NOT NULL CONSTRAINT PK_AspNetUsers PRIMARY KEY,
                    UserName TEXT NULL,
                    NormalizedUserName TEXT NULL,
                    Email TEXT NULL,
                    NormalizedEmail TEXT NULL,
                    EmailConfirmed INTEGER NOT NULL,
                    PasswordHash TEXT NULL,
                    SecurityStamp TEXT NULL,
                    ConcurrencyStamp TEXT NULL,
                    PhoneNumber TEXT NULL,
                    PhoneNumberConfirmed INTEGER NOT NULL,
                    TwoFactorEnabled INTEGER NOT NULL,
                    LockoutEnd TEXT NULL,
                    LockoutEnabled INTEGER NOT NULL,
                    AccessFailedCount INTEGER NOT NULL,
                    DisplayName TEXT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    IsEnabled INTEGER NOT NULL,
                    LastLoginAtUtc TEXT NULL,
                    MustChangePassword INTEGER NOT NULL
                );

                INSERT INTO AspNetUsers (
                    Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
                    PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed,
                    TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount, DisplayName,
                    CreatedAtUtc, IsEnabled, LastLoginAtUtc, MustChangePassword)
                VALUES (
                    'user-1', 'legacy-user', 'LEGACY-USER', 'legacy@example.com', 'LEGACY@EXAMPLE.COM', 1,
                    'hashed-password', 'security-stamp', 'concurrency-stamp', '+123456789', 1,
                    0, NULL, 1, 0, 'Legacy User',
                    '2024-01-01T00:00:00Z', 1, '2024-02-01T00:00:00Z', 1);
            ";
            createCommand.ExecuteNonQuery();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            using (var context = new ApplicationDbContext(options))
            {
                context.Database.Migrate();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var columns = GetTableColumns(context, "AspNetUsers");
                Assert.Contains("OrganizationId", columns);

                var user = context.Users.Single(u => u.Id == "user-1");
                Assert.Equal("legacy-user", user.UserName);
                Assert.Equal("hashed-password", user.PasswordHash);
                Assert.Equal("security-stamp", user.SecurityStamp);
                Assert.Equal("concurrency-stamp", user.ConcurrencyStamp);
                Assert.Equal("Legacy User", user.DisplayName);
                Assert.NotNull(user.OrganizationId);
                Assert.Equal("default-org", user.OrganizationId);
            }
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public void Migration_HandlesMissingNewerColumns_ForPartialLegacyDatabase()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"partial-{Guid.NewGuid():N}.sqlite");
        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = @"
                CREATE TABLE AspNetUsers (
                    Id TEXT NOT NULL CONSTRAINT PK_AspNetUsers PRIMARY KEY,
                    UserName TEXT NULL,
                    NormalizedUserName TEXT NULL,
                    Email TEXT NULL,
                    NormalizedEmail TEXT NULL,
                    EmailConfirmed INTEGER NOT NULL,
                    PasswordHash TEXT NULL,
                    SecurityStamp TEXT NULL,
                    ConcurrencyStamp TEXT NULL,
                    PhoneNumber TEXT NULL,
                    PhoneNumberConfirmed INTEGER NOT NULL,
                    TwoFactorEnabled INTEGER NOT NULL,
                    LockoutEnd TEXT NULL,
                    LockoutEnabled INTEGER NOT NULL,
                    AccessFailedCount INTEGER NOT NULL
                );

                INSERT INTO AspNetUsers (
                    Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
                    PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed,
                    TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount)
                VALUES (
                    'user-2', 'partial-user', 'PARTIAL-USER', 'partial@example.com', 'PARTIAL@EXAMPLE.COM', 1,
                    'hashed-password-2', 'security-stamp-2', 'concurrency-stamp-2', '+111111111', 1,
                    0, NULL, 1, 0);
            ";
            createCommand.ExecuteNonQuery();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            using (var context = new ApplicationDbContext(options))
            {
                context.Database.Migrate();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var columns = GetTableColumns(context, "AspNetUsers");
                Assert.Contains("OrganizationId", columns);
                var user = context.Users.Single(u => u.Id == "user-2");
                Assert.Equal("partial-user", user.UserName);
                Assert.Equal("hashed-password-2", user.PasswordHash);
                Assert.NotNull(user.OrganizationId);
                Assert.Equal("default-org", user.OrganizationId);
            }
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    private static List<string> GetTableColumns(DbContext context, string tableName)
    {
        var connection = context.Database.GetDbConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info('{tableName}')";
        using var reader = command.ExecuteReader();

        var columns = new List<string>();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }
}
