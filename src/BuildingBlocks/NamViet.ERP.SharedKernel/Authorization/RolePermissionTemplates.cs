namespace NamViet.ERP.SharedKernel.Authorization;

public static class RolePermissionTemplates
{
    public const string AdminRole = "Admin";
    public const string PmRole = "PM";
    public const string HrRole = "HR";
    public const string WarehouseRole = "Warehouse_Keeper";
    public const string StaffRole = "Staff";

    private static readonly string[] CorePermissions =
    [
        "core.dashboard.view",
        "core.profile.view",
    ];

    private static readonly string[] HrmPermissions =
    [
        "hrm.employees.view:all",
        "hrm.employees.view:own",
        "hrm.employees.create",
        "hrm.employees.update",
        "hrm.members.settings.view",
        "hrm.members.settings.update",
        "hrm.candidates.view:all",
        "hrm.candidates.create",
        "hrm.candidates.update",
        "hrm.candidates.delete",
        "hrm.recruitment.positions.view",
        "hrm.recruitment.positions.create",
        "hrm.recruitment.positions.update",
        "hrm.recruitment.positions.delete",
        "hrm.birthdays.view",
    ];

    private static readonly string[] PmPermissions =
    [
        ..CorePermissions,
        "wms.dashboard.view",
        "wms.stock.view:all",
        "wms.projects.view:all",
        "wms.projects.create",
        "wms.projects.update",
        "wms.materials.view:all",
        "wms.imports.view:all",
        "wms.imports.approve",
        "wms.exports.view:all",
        "wms.exports.approve",
        "wms.transfers.view:all",
        "wms.transfers.approve",
        "wms.export_returns.view:all",
        "wms.export_returns.approve",
        "wms.stocktakes.view:all",
        "wms.suppliers.view:all",
        "wms.categories.view:all",
        "wms.material_units.view:all",
        "wms.price_books.view:all",
        "wms.reports.view:all",
        "wms.reports.export",
    ];

    private static readonly string[] WarehousePermissions =
    [
        ..CorePermissions,
        "wms.dashboard.view",
        "wms.stock.view:all",
        "wms.projects.view:all",
        "wms.materials.view:all",
        "wms.materials.create",
        "wms.materials.update",
        "wms.imports.view:all",
        "wms.imports.create",
        "wms.imports.update",
        "wms.imports.approve",
        "wms.exports.view:all",
        "wms.exports.create",
        "wms.exports.update",
        "wms.exports.approve",
        "wms.transfers.view:all",
        "wms.transfers.create",
        "wms.transfers.approve",
        "wms.export_returns.view:all",
        "wms.export_returns.create",
        "wms.export_returns.approve",
        "wms.stocktakes.view:all",
        "wms.stocktakes.create",
        "wms.stocktakes.approve",
        "wms.stocktakes.settings.view",
        "wms.stocktakes.settings.update",
        "wms.suppliers.view:all",
        "wms.suppliers.create",
        "wms.suppliers.update",
        "wms.suppliers.delete",
        "wms.categories.view:all",
        "wms.categories.create",
        "wms.categories.update",
        "wms.categories.delete",
        "wms.material_units.view:all",
        "wms.material_units.create",
        "wms.material_units.update",
        "wms.material_units.delete",
        "wms.price_books.view:all",
        "wms.price_books.create",
        "wms.price_books.update",
        "wms.price_books.delete",
        "wms.reports.view:all",
        "wms.reports.export",
    ];

    private static readonly string[] StaffPermissions =
    [
        "core.dashboard.view",
        "core.profile.view",
        "hrm.employees.view:own",
        "wms.dashboard.view",
        "wms.stock.view:all",
    ];

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ByIdentityRoleName { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [AdminRole] = PermissionCatalog.All.ToList(),
            [PmRole] = PmPermissions,
            [HrRole] = CorePermissions.Concat(HrmPermissions).ToList(),
            [WarehouseRole] = WarehousePermissions,
            [StaffRole] = StaffPermissions,
        };

    public static IReadOnlyList<string> ForIdentityRole(string? roleName) =>
        roleName is not null && ByIdentityRoleName.TryGetValue(roleName, out var permissions)
            ? permissions
            : [];
}
