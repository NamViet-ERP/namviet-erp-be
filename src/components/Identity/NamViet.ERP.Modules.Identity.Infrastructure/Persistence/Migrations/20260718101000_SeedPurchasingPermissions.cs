using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence.Seeding;

#nullable disable

namespace NamViet.ERP.Modules.Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260718101000_SeedPurchasingPermissions")]
public class SeedPurchasingPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RolePermissionSeedSql.BuildUpSql());

    protected override void Down(MigrationBuilder _) { }
}
