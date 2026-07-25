using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MercedesEISTool.Server.Models;

namespace MercedesEISTool.Server.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
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
}
