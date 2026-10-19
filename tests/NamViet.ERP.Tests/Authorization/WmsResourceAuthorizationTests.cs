using System.Reflection;
using NamViet.ERP.Api.Controllers;
using NamViet.ERP.Api.Controllers.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.SharedKernel.Authorization;

namespace NamViet.ERP.Tests.Authorization;

public class WmsResourceAuthorizationTests
{
    public static TheoryData<Type, string, string> ProtectedActions => new()
    {
        { typeof(SettingsController), nameof(SettingsController.Get), "wms.settings.view" },
        { typeof(SettingsController), nameof(SettingsController.Put), "wms.settings.update" },
        { typeof(WarehousesController), nameof(WarehousesController.List), "wms.warehouses.view:all" },
        { typeof(WarehousesController), nameof(WarehousesController.GetById), "wms.warehouses.view:all" },
        { typeof(WarehousesController), nameof(WarehousesController.Create), "wms.warehouses.create" },
        { typeof(WarehousesController), nameof(WarehousesController.Update), "wms.warehouses.update" },
        { typeof(WarehousesController), nameof(WarehousesController.Delete), "wms.warehouses.delete" },
    };

    [Theory]
    [MemberData(nameof(ProtectedActions))]
    public void Action_requires_catalogued_permission(Type controller, string action, string permission)
    {
        var method = controller.GetMethod(action, BindingFlags.Instance | BindingFlags.Public);
        var attribute = method?.GetCustomAttribute<RequirePermissionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(permission, attribute.Permission);
        Assert.True(PermissionCatalog.IsValid(permission));
    }

    [Fact]
    public void Warehouse_keeper_receives_new_resource_permissions()
    {
        var permissions = RolePermissionTemplates.ForIdentityRole(RolePermissionTemplates.WarehouseRole);

        foreach (var permission in ProtectedActions.Select(row => (string)row[2]).Distinct())
            Assert.Contains(permission, permissions);
    }
}
