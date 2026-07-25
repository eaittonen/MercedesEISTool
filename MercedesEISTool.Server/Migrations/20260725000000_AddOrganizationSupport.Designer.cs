using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MercedesEISTool.Server.Data;

namespace MercedesEISTool.Server.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260725000000_AddOrganizationSupport")]
public partial class AddOrganizationSupport : Migration
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.8");
    }
}
