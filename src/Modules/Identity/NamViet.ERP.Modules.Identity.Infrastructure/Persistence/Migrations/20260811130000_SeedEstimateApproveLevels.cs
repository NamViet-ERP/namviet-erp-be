using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence.Seeding;

#nullable disable

namespace NamViet.ERP.Modules.Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260811130000_SeedEstimateApproveLevels")]
public class SeedEstimateApproveLevels : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM identity.role_claims
            WHERE "ClaimType" = 'permission'
              AND "ClaimValue" = 'purchasing.estimates.approve';
            """);
        migrationBuilder.Sql(RolePermissionSeedSql.BuildResyncSelectedRolesSql());
        migrationBuilder.Sql(RolePermissionSeedSql.BuildUpSql());
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Additive permission seed — keep claims.
    }
}
