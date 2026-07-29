namespace NamViet.ERP.SharedKernel.Authorization;

public static class RolePermissionTemplates
{
    public const string AdminRole = "Admin";
    public const string PmRole = "PM";
    public const string HrRole = "HR";
    public const string WarehouseRole = "Warehouse_Keeper";
    public const string StaffRole = "Staff";
    public const string BodRole = "BOD";
    public const string AccountantRole = "Accountant";
    public const string ProcurementRole = "Procurement";

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
        "hrm.recruitment.plans.view",
        "hrm.recruitment.plans.create",
        "hrm.recruitment.plans.update",
        "hrm.recruitment.plans.delete",
        "hrm.recruitment.costs.view",
        "hrm.recruitment.costs.create",
        "hrm.recruitment.costs.update",
        "hrm.recruitment.costs.delete",
        "hrm.recruitment.reports.view",
        "hrm.recruitment.reports.export",
        "hrm.birthdays.view",
        "hrm.insurance.view",
        "hrm.insurance.manage",
        "hrm.leaves.view",
        "hrm.leaves.create",
        "hrm.leaves.manage",
        "hrm.leaves.approve",
    ];

    /** Xem toàn bộ phân hệ kho — không CUD. */
    private static readonly string[] WmsViewOnlyPermissions =
    [
        "wms.dashboard.view",
        "wms.stock.view:all",
        "wms.projects.view:all",
        "wms.materials.view:all",
        "wms.imports.view:all",
        "wms.exports.view:all",
        "wms.transfers.view:all",
        "wms.export_returns.view:all",
        "wms.defects.view:all",
        "wms.stocktakes.view:all",
        "wms.stocktakes.settings.view",
        "wms.suppliers.view:all",
        "wms.categories.view:all",
        "wms.material_units.view:all",
        "wms.reports.view:all",
        "wms.transactions.view:all",
        "wms.prices.view",
        "wms.settings.view",
        "wms.warehouses.view:all",
        "wms.conversions.view:all",
        "wms.lending_logs.view:all",
    ];

    /** Xem mua hàng — không CUD. */
    private static readonly string[] PurchasingViewOnlyPermissions =
    [
        "purchasing.dashboard.view",
        "purchasing.suppliers.view",
        "purchasing.plans.view",
        "purchasing.orders.view",
        "purchasing.payments.view",
        "purchasing.deliveries.view",
        "purchasing.documents.view",
        "purchasing.settings.view",
        "purchasing.arrival_plans.view",
    ];

    private static readonly string[] PurchasingWritePermissions =
    [
        "purchasing.dashboard.view",
        "purchasing.suppliers.view",
        "purchasing.plans.view",
        "purchasing.plans.create",
        "purchasing.plans.update",
        "purchasing.plans.import",
        "purchasing.arrival_plans.view",
        "purchasing.orders.view",
        "purchasing.orders.create",
        "purchasing.orders.update",
        "purchasing.orders.approve",
        "purchasing.orders.unapprove",
        "purchasing.orders.place",
        "purchasing.deliveries.view",
        "purchasing.payments.view",
        "purchasing.payments.create",
        "purchasing.payments.submit",
        "purchasing.payments.pay",
        "purchasing.payments.cancel",
        "purchasing.documents.view",
        "purchasing.settings.view",
        "wms.suppliers.view:all",
        "wms.projects.view:all",
    ];

    private static readonly string[] PmPermissions =
    [
        ..CorePermissions,
        ..WmsViewOnlyPermissions,
        "wms.projects.create",
        "wms.projects.update",
        "wms.imports.approve",
        "wms.exports.approve",
        "wms.transfers.approve",
        "wms.export_returns.approve",
        "wms.defects.approve",
        "wms.reports.export",
        "wms.transactions.approve",
        "hrm.leaves.view",
        "hrm.leaves.create",
    ];

    private static readonly string[] WarehousePermissions =
    [
        ..CorePermissions,
        "wms.dashboard.view",
        "wms.stock.view:all",
        "wms.projects.view:all",
        "wms.projects.create",
        "wms.projects.update",
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
        "wms.defects.view:all",
        "wms.defects.create",
        "wms.defects.approve",
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
        "wms.reports.view:all",
        "wms.reports.export",
        "wms.transactions.view:all",
        "wms.transactions.create",
        "wms.settings.view",
        "wms.settings.update",
        "wms.warehouses.view:all",
        "wms.warehouses.create",
        "wms.warehouses.update",
        "wms.warehouses.delete",
        "wms.conversions.view:all",
        "wms.conversions.create",
        "wms.lending_logs.view:all",
        "wms.lending_logs.create",
        "wms.lending_logs.update",
        "wms.lending_logs.delete",
        // Không có wms.prices.view — ẩn giá
        "purchasing.arrival_plans.view",
        "hrm.leaves.view",
        "hrm.leaves.create",
    ];

    private static readonly string[] StaffPermissions =
    [
        "core.dashboard.view",
        "core.profile.view",
        "hrm.employees.view:own",
        "wms.dashboard.view",
        "wms.stock.view:all",
        "wms.transactions.view:all",
        "hrm.leaves.view",
        "hrm.leaves.create",
    ];

    private static readonly string[] BodPermissions =
    [
        ..CorePermissions,
        "hrm.leaves.view",
        "hrm.leaves.approve",
        "wms.transactions.view:all",
        "wms.transactions.approve",
        "wms.prices.view",
    ];

    /** Kế toán: chỉ xem mua hàng + kho (có giá). */
    private static readonly string[] AccountantPermissions =
    [
        ..CorePermissions,
        "hrm.leaves.view",
        "hrm.leaves.create",
        ..PurchasingViewOnlyPermissions,
        ..WmsViewOnlyPermissions,
    ];

    /** Cung ứng: toàn quyền mua hàng; kho chỉ xem (có giá). */
    private static readonly string[] ProcurementPermissions =
    [
        ..CorePermissions,
        "hrm.leaves.view",
        "hrm.leaves.create",
        ..PurchasingWritePermissions,
        ..WmsViewOnlyPermissions,
    ];

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ByIdentityRoleName { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [AdminRole] = PermissionCatalog.All.ToList(),
            [PmRole] = PmPermissions,
            [HrRole] = CorePermissions.Concat(HrmPermissions).ToList(),
            [WarehouseRole] = WarehousePermissions,
            [StaffRole] = StaffPermissions,
            [BodRole] = BodPermissions,
            [AccountantRole] = AccountantPermissions,
            [ProcurementRole] = ProcurementPermissions,
        };

    public static IReadOnlyList<string> ForIdentityRole(string? roleName) =>
        roleName is not null && ByIdentityRoleName.TryGetValue(roleName, out var permissions)
            ? permissions
            : [];

    /** Roles whose permission claims should be replaced to match templates (not only additive). */
    public static IReadOnlyList<string> RolesToResync { get; } =
    [
        WarehouseRole,
        AccountantRole,
        ProcurementRole,
        PmRole,
    ];
}
