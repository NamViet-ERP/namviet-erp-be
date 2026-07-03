using System.Text;
using NamViet.ERP.SharedKernel.Authorization;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Persistence.Seeding;

internal static class RolePermissionSeedSql
{
    public static string BuildUpSql()
    {
        var builder = new StringBuilder();

        builder.AppendLine("""
            INSERT INTO identity.roles ("Id", "Name", "NormalizedName", "ConcurrencyStamp", "Description", "Module", "IsActive")
            SELECT gen_random_uuid(), 'Staff', 'STAFF', gen_random_uuid()::text, 'Nhân viên', 'General', TRUE
            WHERE NOT EXISTS (SELECT 1 FROM identity.roles WHERE "NormalizedName" = 'STAFF');
            """);

        foreach (var (roleName, permissions) in RolePermissionTemplates.ByIdentityRoleName)
        {
            var normalized = roleName.ToUpperInvariant();
            var permissionArray = string.Join(", ", permissions.Select(p => $"'{p.Replace("'", "''")}'"));

            builder.AppendLine($"""
                INSERT INTO identity.role_claims ("RoleId", "ClaimType", "ClaimValue")
                SELECT r."Id", '{PermissionCatalog.ClaimType}', perm
                FROM identity.roles r
                CROSS JOIN unnest(ARRAY[{permissionArray}]::text[]) AS perm
                WHERE r."NormalizedName" = '{normalized}'
                  AND NOT EXISTS (
                    SELECT 1 FROM identity.role_claims rc
                    WHERE rc."RoleId" = r."Id"
                      AND rc."ClaimType" = '{PermissionCatalog.ClaimType}'
                      AND rc."ClaimValue" = perm
                  );
                """);
        }

        return builder.ToString();
    }

    public static string BuildDownSql() =>
        $"DELETE FROM identity.role_claims WHERE \"ClaimType\" = '{PermissionCatalog.ClaimType}';";
}
