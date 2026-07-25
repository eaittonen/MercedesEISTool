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

        migrationBuilder.AddColumn<string>(
            name: "OrganizationId",
            table: "AspNetUsers",
            type: "TEXT",
            nullable: true);

        migrationBuilder.Sql(@"
            UPDATE AspNetUsers
            SET OrganizationId = 'default-org'
            WHERE OrganizationId IS NULL OR trim(OrganizationId) = '';
        ");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUsers_OrganizationId",
            table: "AspNetUsers",
            column: "OrganizationId");

        migrationBuilder.AddForeignKey(
            name: "FK_AspNetUsers_Organizations_OrganizationId",
            table: "AspNetUsers",
            column: "OrganizationId",
            principalTable: "Organizations",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AspNetUsers_Organizations_OrganizationId",
            table: "AspNetUsers");

        migrationBuilder.DropIndex(
            name: "IX_AspNetUsers_OrganizationId",
            table: "AspNetUsers");

        migrationBuilder.DropColumn(
            name: "OrganizationId",
            table: "AspNetUsers");

        migrationBuilder.DropTable(
            name: "Organizations");
    }
}
