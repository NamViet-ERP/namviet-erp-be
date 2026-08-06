using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence.Seeding;
using NamViet.ERP.SharedKernel.Authorization;

#nullable disable

namespace NamViet.ERP.Modules.Identity.Infrastructure.Persistence.Migrations;

/// <summary>
/// Duyệt 2 cấp KHMH/phát sinh: permissions + role Trưởng cung ứng / Sếp.
/// </summary>
[DbContext(typeof(IdentityDbContext))]
[Migration("20260805081000_PurchasePlanApprovalPermissions")]
public class PurchasePlanApprovalPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            INSERT INTO identity.roles ("Id", "Name", "NormalizedName", "ConcurrencyStamp", "Description", "Module", "IsActive")
            SELECT gen_random_uuid(), '{RolePermissionTemplates.ProcurementLeadRole}', '{RolePermissionTemplates.ProcurementLeadRole.ToUpperInvariant()}', gen_random_uuid()::text, 'Trưởng cung ứng — duyệt cấp 1', 'Purchasing', TRUE
            WHERE NOT EXISTS (SELECT 1 FROM identity.roles WHERE "NormalizedName" = '{RolePermissionTemplates.ProcurementLeadRole.ToUpperInvariant()}');

            INSERT INTO identity.roles ("Id", "Name", "NormalizedName", "ConcurrencyStamp", "Description", "Module", "IsActive")
            SELECT gen_random_uuid(), '{RolePermissionTemplates.ProcurementBossRole}', '{RolePermissionTemplates.ProcurementBossRole.ToUpperInvariant()}', gen_random_uuid()::text, 'Sếp cung ứng — duyệt cấp 2', 'Purchasing', TRUE
            WHERE NOT EXISTS (SELECT 1 FROM identity.roles WHERE "NormalizedName" = '{RolePermissionTemplates.ProcurementBossRole.ToUpperInvariant()}');
            """);

        migrationBuilder.Sql(RolePermissionSeedSql.BuildResyncSelectedRolesSql());
        migrationBuilder.Sql(RolePermissionSeedSql.BuildUpSql());
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Additive permission seed — keep roles.
    }
}
