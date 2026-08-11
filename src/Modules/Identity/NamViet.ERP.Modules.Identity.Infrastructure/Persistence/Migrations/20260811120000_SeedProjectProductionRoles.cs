using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence.Seeding;
using NamViet.ERP.SharedKernel.Authorization;

#nullable disable

namespace NamViet.ERP.Modules.Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260811120000_SeedProjectProductionRoles")]
public class SeedProjectProductionRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            INSERT INTO identity.roles ("Id", "Name", "NormalizedName", "ConcurrencyStamp", "Description", "Module", "IsActive")
            SELECT gen_random_uuid(), '{RolePermissionTemplates.ProjectRole}', '{RolePermissionTemplates.ProjectRole.ToUpperInvariant()}', gen_random_uuid()::text, 'Phòng Dự án', 'General', TRUE
            WHERE NOT EXISTS (SELECT 1 FROM identity.roles WHERE "NormalizedName" = '{RolePermissionTemplates.ProjectRole.ToUpperInvariant()}');

            INSERT INTO identity.roles ("Id", "Name", "NormalizedName", "ConcurrencyStamp", "Description", "Module", "IsActive")
            SELECT gen_random_uuid(), '{RolePermissionTemplates.ProductionRole}', '{RolePermissionTemplates.ProductionRole.ToUpperInvariant()}', gen_random_uuid()::text, 'Phòng Sản xuất', 'General', TRUE
            WHERE NOT EXISTS (SELECT 1 FROM identity.roles WHERE "NormalizedName" = '{RolePermissionTemplates.ProductionRole.ToUpperInvariant()}');
            """);

        migrationBuilder.Sql(RolePermissionSeedSql.BuildResyncSelectedRolesSql());
        migrationBuilder.Sql(RolePermissionSeedSql.BuildUpSql());
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Additive role seed — keep roles.
    }
}
