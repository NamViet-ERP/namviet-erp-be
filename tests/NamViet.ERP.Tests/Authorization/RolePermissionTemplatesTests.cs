using NamViet.ERP.SharedKernel.Authorization;

namespace NamViet.ERP.Tests.Authorization;

public class RolePermissionTemplatesTests
{
    [Fact]
    public void BodRole_contains_expected_permissions_and_no_admin_rights()
    {
        var permissions = RolePermissionTemplates.ForIdentityRole(RolePermissionTemplates.BodRole);
        var permSet = permissions.ToHashSet(StringComparer.Ordinal);

        // No duplicates
        Assert.Equal(permissions.Count, permSet.Count);

        // All valid in catalog
        foreach (var perm in permissions)
        {
            Assert.True(PermissionCatalog.IsValid(perm), $"Permission '{perm}' is not defined in PermissionCatalog");
        }

        // Core permissions
        Assert.Contains("core.dashboard.view", permSet);
        Assert.Contains("core.profile.view", permSet);

        // WMS & Purchasing & Price view
        Assert.Contains("wms.stock.view:all", permSet);
        Assert.Contains("wms.projects.view:all", permSet);
        Assert.Contains("wms.prices.view", permSet);
        Assert.Contains("purchasing.plans.view", permSet);
        Assert.Contains("purchasing.orders.view", permSet);
        Assert.Contains("purchasing.pricebook.view", permSet);

        // HRM view
        Assert.Contains("hrm.employees.view:all", permSet);
        Assert.Contains("hrm.leaves.view", permSet);
        Assert.Contains("hrm.attendance.view", permSet);
        Assert.Contains("hrm.kpi.view", permSet);
        Assert.Contains("hrm.recruitment.costs.view", permSet);
        Assert.Contains("hrm.recruitment.reports.view", permSet);

        // High-level executive approvals
        Assert.Contains("purchasing.plans.approve_l2", permSet);
        Assert.Contains("purchasing.plans.reject", permSet);
        Assert.Contains("purchasing.estimates.approve_l2", permSet);
        Assert.Contains("wms.suppliers.approve", permSet);
        Assert.Contains("purchasing.orders.approve", permSet);
        Assert.Contains("purchasing.payments.pay", permSet);
        Assert.Contains("hrm.leaves.approve", permSet);
        Assert.Contains("wms.transactions.approve", permSet);

        // STRICTLY NO IT admin permissions
        Assert.DoesNotContain("admin.users.create", permSet);
        Assert.DoesNotContain("admin.users.update", permSet);
        Assert.DoesNotContain("admin.users.lock", permSet);
        Assert.DoesNotContain("admin.roles.create", permSet);
        Assert.DoesNotContain("admin.roles.update", permSet);
        Assert.DoesNotContain("admin.roles.delete", permSet);
        Assert.DoesNotContain("admin.roles.permissions.update", permSet);
    }

    [Fact]
    public void DepartmentManagerRole_contains_expected_permissions_and_no_l2_rights()
    {
        var permissions = RolePermissionTemplates.ForIdentityRole(RolePermissionTemplates.DepartmentManagerRole);
        var permSet = permissions.ToHashSet(StringComparer.Ordinal);

        // No duplicates
        Assert.Equal(permissions.Count, permSet.Count);

        // All valid in catalog
        foreach (var perm in permissions)
        {
            Assert.True(PermissionCatalog.IsValid(perm), $"Permission '{perm}' is not defined in PermissionCatalog");
        }

        // Core permissions
        Assert.Contains("core.dashboard.view", permSet);
        Assert.Contains("core.profile.view", permSet);

        // WMS & Purchasing view
        Assert.Contains("wms.stock.view:all", permSet);
        Assert.Contains("purchasing.plans.view", permSet);

        // Department manager specific
        Assert.Contains("hrm.employees.view:all", permSet);
        Assert.Contains("hrm.leaves.view", permSet);
        Assert.Contains("hrm.leaves.create", permSet);
        Assert.Contains("hrm.leaves.approve", permSet);
        Assert.Contains("purchasing.plans.approve_l1", permSet);
        Assert.Contains("purchasing.plans.reject", permSet);
        Assert.Contains("purchasing.estimates.approve_l1", permSet);
        Assert.Contains("wms.projects.create", permSet);
        Assert.Contains("wms.projects.update", permSet);

        // STRICTLY NO L2 executive approvals
        Assert.DoesNotContain("purchasing.plans.approve_l2", permSet);
        Assert.DoesNotContain("purchasing.estimates.approve_l2", permSet);
        Assert.DoesNotContain("purchasing.orders.approve", permSet);
        Assert.DoesNotContain("purchasing.payments.pay", permSet);
        Assert.DoesNotContain("wms.suppliers.approve", permSet);

        // STRICTLY NO IT admin permissions
        Assert.DoesNotContain("admin.users.create", permSet);
        Assert.DoesNotContain("admin.roles.permissions.update", permSet);
    }

    [Fact]
    public void RolesToResync_contains_bod_and_department_manager()
    {
        Assert.Contains(RolePermissionTemplates.BodRole, RolePermissionTemplates.RolesToResync);
        Assert.Contains(RolePermissionTemplates.DepartmentManagerRole, RolePermissionTemplates.RolesToResync);
    }

    [Fact]
    public void ByIdentityRoleName_has_registered_roles()
    {
        Assert.True(RolePermissionTemplates.ByIdentityRoleName.ContainsKey(RolePermissionTemplates.BodRole));
        Assert.True(RolePermissionTemplates.ByIdentityRoleName.ContainsKey(RolePermissionTemplates.DepartmentManagerRole));
    }
}
