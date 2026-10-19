using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence.Seeding;

#nullable disable

namespace NamViet.ERP.Modules.Identity.Infrastructure.Persistence.Migrations;

/// <summary>
/// Resync roles: thủ kho bỏ create phiếu nhập; cung ứng được create/update phiếu nhập trên PO.
/// </summary>
[DbContext(typeof(IdentityDbContext))]
[Migration("20260803143000_ResyncImportWorkflowPermissions")]
public class ResyncImportWorkflowPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(RolePermissionSeedSql.BuildResyncSelectedRolesSql());
        migrationBuilder.Sql(RolePermissionSeedSql.BuildUpSql());
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Permission template migrations are additive.
    }
}
