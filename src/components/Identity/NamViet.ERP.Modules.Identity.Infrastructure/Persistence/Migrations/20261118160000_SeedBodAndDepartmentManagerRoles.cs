using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence.Seeding;
using NamViet.ERP.SharedKernel.Authorization;

#nullable disable

namespace NamViet.ERP.Modules.Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261118160000_SeedBodAndDepartmentManagerRoles")]
public class SeedBodAndDepartmentManagerRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            INSERT INTO identity.roles ("Id", "Name", "NormalizedName", "ConcurrencyStamp", "Description", "Module", "IsActive")
            SELECT gen_random_uuid(), '{RolePermissionTemplates.BodRole}', '{RolePermissionTemplates.BodRole.ToUpperInvariant()}', gen_random_uuid()::text, 'Ban Giám Đốc (Giám đốc)', 'General', TRUE
            WHERE NOT EXISTS (SELECT 1 FROM identity.roles WHERE "NormalizedName" = '{RolePermissionTemplates.BodRole.ToUpperInvariant()}');

            INSERT INTO identity.roles ("Id", "Name", "NormalizedName", "ConcurrencyStamp", "Description", "Module", "IsActive")
            SELECT gen_random_uuid(), '{RolePermissionTemplates.DepartmentManagerRole}', '{RolePermissionTemplates.DepartmentManagerRole.ToUpperInvariant()}', gen_random_uuid()::text, 'Trưởng phòng', 'General', TRUE
            WHERE NOT EXISTS (SELECT 1 FROM identity.roles WHERE "NormalizedName" = '{RolePermissionTemplates.DepartmentManagerRole.ToUpperInvariant()}');
            """);

        migrationBuilder.Sql(RolePermissionSeedSql.BuildResyncSelectedRolesSql());
        migrationBuilder.Sql(RolePermissionSeedSql.BuildUpSql());
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Additive role seed — keep roles.
    }
}
