using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence.Seeding;

#nullable disable

namespace NamViet.ERP.Modules.Identity.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seed purchasing.warranties.view|create|update onto role templates.
/// </summary>
[DbContext(typeof(IdentityDbContext))]
[Migration("20260810101000_SeedPurchaseWarrantyPermissions")]
public class SeedPurchaseWarrantyPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(RolePermissionSeedSql.BuildResyncSelectedRolesSql());
        migrationBuilder.Sql(RolePermissionSeedSql.BuildUpSql());
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Additive permission seed — keep claims.
    }
}
