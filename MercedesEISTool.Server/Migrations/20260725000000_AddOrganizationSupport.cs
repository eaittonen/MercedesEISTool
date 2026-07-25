using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

#nullable disable

namespace MercedesEISTool.Server.Migrations;

public partial class AddOrganizationSupport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS Organizations (
                Id TEXT NOT NULL CONSTRAINT PK_Organizations PRIMARY KEY,
                Name TEXT NOT NULL,
                ContactEmail TEXT NULL,
                Country TEXT NULL,
                IsActive INTEGER NOT NULL,
                LicenseType TEXT NULL,
                LicenseExpirationUtc TEXT NULL,
                MaxUsers INTEGER NOT NULL,
                CreatedUtc TEXT NOT NULL,
                UpdatedUtc TEXT NOT NULL
            );
        ");

        migrationBuilder.Sql(@"
            INSERT INTO Organizations (Id, Name, ContactEmail, Country, IsActive, LicenseType, LicenseExpirationUtc, MaxUsers, CreatedUtc, UpdatedUtc)
            SELECT 'default-org', 'Default Organization', 'admin@example.local', 'Finland', 1, 'Standard', NULL, 4, datetime('now'), datetime('now')
            WHERE NOT EXISTS (SELECT 1 FROM Organizations);
        ");

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS AspNetRoles (
                Id TEXT NOT NULL CONSTRAINT PK_AspNetRoles PRIMARY KEY,
                Name TEXT NULL,
                NormalizedName TEXT NULL,
                ConcurrencyStamp TEXT NULL
            );
        ");

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS AspNetRoleClaims (
                Id INTEGER NOT NULL CONSTRAINT PK_AspNetRoleClaims PRIMARY KEY AUTOINCREMENT,
                RoleId TEXT NOT NULL,
                ClaimType TEXT NULL,
                ClaimValue TEXT NULL
            );
        ");

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS AspNetUserClaims (
                Id INTEGER NOT NULL CONSTRAINT PK_AspNetUserClaims PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                ClaimType TEXT NULL,
                ClaimValue TEXT NULL
            );
        ");

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS AspNetUserLogins (
                LoginProvider TEXT NOT NULL,
                ProviderKey TEXT NOT NULL,
                ProviderDisplayName TEXT NULL,
                UserId TEXT NOT NULL,
                CONSTRAINT PK_AspNetUserLogins PRIMARY KEY (LoginProvider, ProviderKey)
            );
        ");

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS AspNetUserRoles (
                UserId TEXT NOT NULL,
                RoleId TEXT NOT NULL,
                CONSTRAINT PK_AspNetUserRoles PRIMARY KEY (UserId, RoleId)
            );
        ");

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS AspNetUserTokens (
                UserId TEXT NOT NULL,
                LoginProvider TEXT NOT NULL,
                Name TEXT NOT NULL,
                Value TEXT NULL,
                CONSTRAINT PK_AspNetUserTokens PRIMARY KEY (UserId, LoginProvider, Name)
            );
        ");

        var connectionString = ResolveConnectionString();
        var existingColumns = LoadExistingColumns(connectionString);
        var hasUsersTable = TableExists(connectionString, "AspNetUsers");
        var requiredColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Id",
            "UserName",
            "NormalizedUserName",
            "Email",
            "NormalizedEmail",
            "EmailConfirmed",
            "PasswordHash",
            "SecurityStamp",
            "ConcurrencyStamp",
            "PhoneNumber",
            "PhoneNumberConfirmed",
            "TwoFactorEnabled",
            "LockoutEnd",
            "LockoutEnabled",
            "AccessFailedCount",
            "DisplayName",
            "CreatedAtUtc",
            "IsEnabled",
            "LastLoginAtUtc",
            "OrganizationId",
            "MustChangePassword"
        };

        var shouldRebuildUsers = !hasUsersTable || requiredColumns.Any(column => !existingColumns.Contains(column));

        if (!hasUsersTable)
        {
            migrationBuilder.Sql(@"
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
                    OrganizationId TEXT NULL,
                    MustChangePassword INTEGER NOT NULL,
                    CONSTRAINT FK_AspNetUsers_Organizations_OrganizationId FOREIGN KEY (OrganizationId)
                        REFERENCES Organizations (Id) ON DELETE RESTRICT
                );
            ");
        }
        else if (shouldRebuildUsers)
        {
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS AspNetUsers_new;
                DROP TABLE IF EXISTS AspNetUsers_legacy;
            ");

            var createTableSql = @"
                CREATE TABLE AspNetUsers_new (
                    Id TEXT NOT NULL CONSTRAINT PK_AspNetUsers_new PRIMARY KEY,
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
                    OrganizationId TEXT NULL,
                    MustChangePassword INTEGER NOT NULL,
                    CONSTRAINT FK_AspNetUsers_Organizations_OrganizationId FOREIGN KEY (OrganizationId)
                        REFERENCES Organizations (Id) ON DELETE RESTRICT
                );
            ";
            migrationBuilder.Sql(createTableSql);

            var sourceColumns = new[]
            {
                "Id",
                "UserName",
                "NormalizedUserName",
                "Email",
                "NormalizedEmail",
                "EmailConfirmed",
                "PasswordHash",
                "SecurityStamp",
                "ConcurrencyStamp",
                "PhoneNumber",
                "PhoneNumberConfirmed",
                "TwoFactorEnabled",
                "LockoutEnd",
                "LockoutEnabled",
                "AccessFailedCount",
                "DisplayName",
                "CreatedAtUtc",
                "IsEnabled",
                "LastLoginAtUtc",
                "OrganizationId",
                "MustChangePassword"
            };

            var selectExpressions = new List<string>();
            foreach (var columnName in sourceColumns)
            {
                selectExpressions.Add(GetColumnExpression(columnName, existingColumns));
            }

            var insertSql = $@"
                INSERT INTO AspNetUsers_new ({string.Join(", ", sourceColumns)})
                SELECT {string.Join(", ", selectExpressions)}
                FROM AspNetUsers;
            ";

            migrationBuilder.Sql(insertSql);

            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS AspNetUsers;
                ALTER TABLE AspNetUsers_new RENAME TO AspNetUsers;
            ");
        }
        else
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS AspNetUsers (
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
                    OrganizationId TEXT NULL,
                    MustChangePassword INTEGER NOT NULL
                );
            ");
        }

        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS IX_AspNetUsers_OrganizationId ON AspNetUsers (OrganizationId);
            CREATE INDEX IF NOT EXISTS IX_AspNetUsers_NormalizedUserName ON AspNetUsers (NormalizedUserName);
            CREATE INDEX IF NOT EXISTS IX_AspNetUsers_NormalizedEmail ON AspNetUsers (NormalizedEmail);
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DROP TABLE IF EXISTS AspNetUsers;
        ");

        migrationBuilder.Sql(@"
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
        ");

        migrationBuilder.DropTable(
            name: "Organizations");
    }

    private static HashSet<string> LoadExistingColumns(string connectionString)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return columns;
        }

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info('AspNetUsers')";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static bool TableExists(string connectionString, string tableName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=@tableName";
        command.Parameters.AddWithValue("@tableName", tableName);
        using var reader = command.ExecuteReader();
        return reader.Read();
    }

    private static string ResolveConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionString");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddJsonFile("appsettings.Production.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    private static string GetColumnExpression(string columnName, HashSet<string> existingColumns)
    {
        if (!existingColumns.Contains(columnName))
        {
            return columnName switch
            {
                "DisplayName" => "NULL",
                "CreatedAtUtc" => "datetime('now')",
                "IsEnabled" => "1",
                "LastLoginAtUtc" => "NULL",
                "OrganizationId" => "'default-org'",
                "MustChangePassword" => "0",
                _ => "NULL"
            };
        }

        return columnName switch
        {
            "EmailConfirmed" => "EmailConfirmed",
            "PhoneNumberConfirmed" => "PhoneNumberConfirmed",
            "TwoFactorEnabled" => "TwoFactorEnabled",
            "LockoutEnabled" => "LockoutEnabled",
            "AccessFailedCount" => "AccessFailedCount",
            "LockoutEnd" => "LockoutEnd",
            _ => columnName
        };
    }
}
