using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercedesEISTool.Server.Migrations;

public partial class AddOrganizationSupport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Organizations",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                ContactEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Country = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                LicenseType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                LicenseExpirationUtc = table.Column<string>(type: "TEXT", nullable: true),
                MaxUsers = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<string>(type: "TEXT", nullable: false),
                UpdatedUtc = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Organizations", x => x.Id);
            });

        migrationBuilder.Sql(@"
            INSERT INTO Organizations (Id, Name, ContactEmail, Country, IsActive, LicenseType, LicenseExpirationUtc, MaxUsers, CreatedUtc, UpdatedUtc)
            SELECT 'default-org', 'Default Organization', 'admin@example.local', 'Finland', 1, 'Standard', datetime('now'), 4, datetime('now'), datetime('now')
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
                MustChangePassword INTEGER NOT NULL
            );
        ");

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS AspNetUsers_legacy (
                Id TEXT NOT NULL CONSTRAINT PK_AspNetUsers_legacy PRIMARY KEY,
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

        migrationBuilder.Sql(@"
            INSERT INTO AspNetUsers_legacy (
                Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
                PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed,
                TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount, DisplayName,
                CreatedAtUtc, IsEnabled, LastLoginAtUtc, MustChangePassword)
            SELECT
                Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
                PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed,
                TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount, DisplayName,
                CreatedAtUtc, IsEnabled, LastLoginAtUtc, MustChangePassword
            FROM AspNetUsers;
        ");

        migrationBuilder.Sql(@"
            DROP TABLE AspNetUsers;
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
                OrganizationId TEXT NULL,
                MustChangePassword INTEGER NOT NULL,
                CONSTRAINT FK_AspNetUsers_Organizations_OrganizationId FOREIGN KEY (OrganizationId)
                    REFERENCES Organizations (Id) ON DELETE RESTRICT
            );
        ");

        migrationBuilder.Sql(@"
            INSERT INTO AspNetUsers (
                Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
                PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed,
                TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount, DisplayName,
                CreatedAtUtc, IsEnabled, LastLoginAtUtc, OrganizationId, MustChangePassword)
            SELECT
                Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
                PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed,
                TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount, DisplayName,
                CreatedAtUtc, IsEnabled, LastLoginAtUtc, 'default-org', MustChangePassword
            FROM AspNetUsers_legacy;
        ");

        migrationBuilder.Sql(@"
            DROP TABLE AspNetUsers_legacy;
        ");

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
}
