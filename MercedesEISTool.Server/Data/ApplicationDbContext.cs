using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MercedesEISTool.Server.Models;

namespace MercedesEISTool.Server.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
        EnsureSchemaCompatibility();
    }

    public DbSet<Organization> Organizations { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Organization>(entity =>
        {
            entity.HasKey(organization => organization.Id);
            entity.Property(organization => organization.Name).HasMaxLength(200).IsRequired();
            entity.Property(organization => organization.ContactEmail).HasMaxLength(200);
            entity.Property(organization => organization.Country).HasMaxLength(100);
            entity.Property(organization => organization.LicenseType).HasMaxLength(50);
            entity.HasMany(organization => organization.Users)
                .WithOne(user => user.Organization)
                .HasForeignKey(user => user.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.DisplayName).HasMaxLength(200);
        });
    }

    private void EnsureSchemaCompatibility()
    {
        try
        {
            if (Database.GetDbConnection() is not SqliteConnection sqliteConnection)
            {
                return;
            }

            Environment.SetEnvironmentVariable("MercedesEISTool_ActiveConnectionString", sqliteConnection.ConnectionString);
            sqliteConnection.Open();
            using var columnCommand = sqliteConnection.CreateCommand();
            columnCommand.CommandText = "PRAGMA table_info('AspNetUsers')";
            using var reader = columnCommand.ExecuteReader();
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                columns.Add(reader.GetString(1));
            }

            AddMissingColumnIfNeeded(sqliteConnection, columns, "OrganizationId", "TEXT", "NULL");
            AddMissingColumnIfNeeded(sqliteConnection, columns, "MustChangePassword", "INTEGER", "0");
            AddMissingColumnIfNeeded(sqliteConnection, columns, "CreatedAtUtc", "TEXT", "datetime('now')");
            AddMissingColumnIfNeeded(sqliteConnection, columns, "IsEnabled", "INTEGER", "1");
            AddMissingColumnIfNeeded(sqliteConnection, columns, "LastLoginAtUtc", "TEXT", "NULL");
            AddMissingColumnIfNeeded(sqliteConnection, columns, "DisplayName", "TEXT", "''");
            AddMissingColumnIfNeeded(sqliteConnection, columns, "EmailConfirmed", "INTEGER", "0");
            AddMissingColumnIfNeeded(sqliteConnection, columns, "PhoneNumberConfirmed", "INTEGER", "0");
            AddMissingColumnIfNeeded(sqliteConnection, columns, "TwoFactorEnabled", "INTEGER", "0");
            AddMissingColumnIfNeeded(sqliteConnection, columns, "LockoutEnabled", "INTEGER", "0");
            AddMissingColumnIfNeeded(sqliteConnection, columns, "AccessFailedCount", "INTEGER", "0");

            using var updateCommand = sqliteConnection.CreateCommand();
            updateCommand.CommandText = @"
                UPDATE AspNetUsers
                SET UserName = COALESCE(UserName, ''),
                    NormalizedUserName = COALESCE(NormalizedUserName, ''),
                    Email = COALESCE(Email, ''),
                    NormalizedEmail = COALESCE(NormalizedEmail, ''),
                    DisplayName = COALESCE(DisplayName, ''),
                    SecurityStamp = COALESCE(SecurityStamp, ''),
                    ConcurrencyStamp = COALESCE(ConcurrencyStamp, ''),
                    PhoneNumber = COALESCE(PhoneNumber, ''),
                    EmailConfirmed = COALESCE(EmailConfirmed, 0),
                    PhoneNumberConfirmed = COALESCE(PhoneNumberConfirmed, 0),
                    TwoFactorEnabled = COALESCE(TwoFactorEnabled, 0),
                    LockoutEnabled = COALESCE(LockoutEnabled, 0),
                    AccessFailedCount = COALESCE(AccessFailedCount, 0),
                    IsEnabled = COALESCE(IsEnabled, 1),
                    MustChangePassword = COALESCE(MustChangePassword, 0),
                    OrganizationId = COALESCE(OrganizationId, 'default-org')
                WHERE UserName IS NULL
                   OR NormalizedUserName IS NULL
                   OR Email IS NULL
                   OR NormalizedEmail IS NULL
                   OR DisplayName IS NULL
                   OR SecurityStamp IS NULL
                   OR ConcurrencyStamp IS NULL
                   OR PhoneNumber IS NULL
                   OR EmailConfirmed IS NULL
                   OR PhoneNumberConfirmed IS NULL
                   OR TwoFactorEnabled IS NULL
                   OR LockoutEnabled IS NULL
                   OR AccessFailedCount IS NULL
                   OR IsEnabled IS NULL
                   OR MustChangePassword IS NULL
                   OR OrganizationId IS NULL;
            ";
            updateCommand.ExecuteNonQuery();
        }
        catch (Exception)
        {
            // The startup repair path already handles incompatible legacy schemas.
        }
        finally
        {
            try
            {
                Database.CloseConnection();
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    private static void AddMissingColumnIfNeeded(SqliteConnection connection, HashSet<string> columns, string columnName, string columnType, string defaultValue)
    {
        if (columns.Contains(columnName))
        {
            return;
        }

        using var addColumnCommand = connection.CreateCommand();
        addColumnCommand.CommandText = $"ALTER TABLE AspNetUsers ADD COLUMN {columnName} {columnType}";
        addColumnCommand.ExecuteNonQuery();

        if (!string.Equals(defaultValue, "NULL", StringComparison.OrdinalIgnoreCase))
        {
            using var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = $"UPDATE AspNetUsers SET {columnName} = {defaultValue} WHERE {columnName} IS NULL";
            updateCommand.ExecuteNonQuery();
        }
    }
}
