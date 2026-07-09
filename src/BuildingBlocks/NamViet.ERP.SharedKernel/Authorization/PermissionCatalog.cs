namespace NamViet.ERP.SharedKernel.Authorization;

public static class PermissionCatalog
{
    public const string ClaimType = "permission";

    public static IReadOnlyList<PermissionDefinition> Definitions { get; } =
    [
        // Core
        new("core.dashboard.view", "Core", "Dashboard", "Xem dashboard tổng"),
        new("core.profile.view", "Core", "Profile", "Xem hồ sơ cá nhân"),

        // Admin
        new("admin.users.view:all", "Admin", "Users", "Xem danh sách user"),
        new("admin.users.create", "Admin", "Users", "Tạo user"),
        new("admin.users.update", "Admin", "Users", "Sửa user / gán role"),
        new("admin.users.lock", "Admin", "Users", "Khóa / mở khóa tài khoản"),
        new("admin.roles.view:all", "Admin", "Roles", "Xem danh sách role"),
        new("admin.roles.create", "Admin", "Roles", "Tạo role"),
        new("admin.roles.update", "Admin", "Roles", "Sửa role"),
        new("admin.roles.delete", "Admin", "Roles", "Xóa role"),
        new("admin.roles.permissions.update", "Admin", "Roles", "Sửa ma trận quyền role"),

        // HRM
        new("hrm.employees.view:all", "HRM", "Employees", "Xem tất cả nhân viên"),
        new("hrm.employees.view:own", "HRM", "Employees", "Xem hồ sơ liên quan bản thân"),
        new("hrm.employees.create", "HRM", "Employees", "Tạo nhân viên"),
        new("hrm.employees.update", "HRM", "Employees", "Sửa nhân viên"),
        new("hrm.members.settings.view", "HRM", "Settings", "Xem thiết lập HRM"),
        new("hrm.members.settings.update", "HRM", "Settings", "Sửa thiết lập HRM"),
        new("hrm.candidates.view:all", "HRM", "Candidates", "Xem ứng viên"),
        new("hrm.candidates.create", "HRM", "Candidates", "Tạo ứng viên"),
        new("hrm.candidates.update", "HRM", "Candidates", "Sửa ứng viên"),
        new("hrm.candidates.delete", "HRM", "Candidates", "Xóa ứng viên"),
        new("hrm.recruitment.positions.view", "HRM", "Recruitment", "Xem vị trí tuyển dụng"),
        new("hrm.recruitment.positions.create", "HRM", "Recruitment", "Tạo vị trí tuyển dụng"),
        new("hrm.recruitment.positions.update", "HRM", "Recruitment", "Sửa vị trí tuyển dụng"),
        new("hrm.recruitment.positions.delete", "HRM", "Recruitment", "Xóa vị trí tuyển dụng"),
        new("hrm.birthdays.view", "HRM", "Birthdays", "Xem sinh nhật (dashboard)"),

        // WMS
        new("wms.dashboard.view", "WMS", "Dashboard", "Xem dashboard kho"),
        new("wms.stock.view:all", "WMS", "Stock", "Xem tồn kho"),
        new("wms.projects.view:all", "WMS", "Projects", "Xem dự án"),
        new("wms.projects.create", "WMS", "Projects", "Tạo dự án"),
        new("wms.projects.update", "WMS", "Projects", "Sửa dự án"),
        new("wms.materials.view:all", "WMS", "Materials", "Xem vật tư"),
        new("wms.materials.create", "WMS", "Materials", "Tạo vật tư"),
        new("wms.materials.update", "WMS", "Materials", "Sửa vật tư"),
        new("wms.materials.delete", "WMS", "Materials", "Xóa vật tư"),
        new("wms.imports.view:all", "WMS", "Imports", "Xem phiếu nhập"),
        new("wms.imports.create", "WMS", "Imports", "Tạo phiếu nhập"),
        new("wms.imports.update", "WMS", "Imports", "Sửa phiếu nhập"),
        new("wms.imports.approve", "WMS", "Imports", "Duyệt phiếu nhập"),
        new("wms.exports.view:all", "WMS", "Exports", "Xem phiếu xuất"),
        new("wms.exports.create", "WMS", "Exports", "Tạo phiếu xuất"),
        new("wms.exports.update", "WMS", "Exports", "Sửa phiếu xuất"),
        new("wms.exports.approve", "WMS", "Exports", "Duyệt phiếu xuất"),
        new("wms.transfers.view:all", "WMS", "Transfers", "Xem phiếu chuyển kho"),
        new("wms.transfers.create", "WMS", "Transfers", "Tạo chuyển kho"),
        new("wms.transfers.approve", "WMS", "Transfers", "Duyệt chuyển kho"),
        new("wms.export_returns.view:all", "WMS", "ExportReturns", "Xem phiếu hoàn trả vật tư"),
        new("wms.export_returns.create", "WMS", "ExportReturns", "Tạo phiếu hoàn trả vật tư"),
        new("wms.export_returns.approve", "WMS", "ExportReturns", "Duyệt phiếu hoàn trả vật tư"),
        new("wms.stocktakes.view:all", "WMS", "Stocktakes", "Xem kiểm kê"),
        new("wms.stocktakes.create", "WMS", "Stocktakes", "Tạo kiểm kê"),
        new("wms.stocktakes.approve", "WMS", "Stocktakes", "Duyệt / chốt kiểm kê"),
        new("wms.stocktakes.settings.view", "WMS", "Stocktakes", "Xem thiết lập kiểm kê"),
        new("wms.stocktakes.settings.update", "WMS", "Stocktakes", "Sửa thiết lập kiểm kê"),
        new("wms.suppliers.view:all", "WMS", "Suppliers", "Xem nhà cung cấp"),
        new("wms.suppliers.create", "WMS", "Suppliers", "Tạo nhà cung cấp"),
        new("wms.suppliers.update", "WMS", "Suppliers", "Sửa nhà cung cấp"),
        new("wms.suppliers.delete", "WMS", "Suppliers", "Xóa nhà cung cấp"),
        new("wms.categories.view:all", "WMS", "Categories", "Xem nhóm vật tư"),
        new("wms.categories.create", "WMS", "Categories", "Tạo nhóm vật tư"),
        new("wms.categories.update", "WMS", "Categories", "Sửa nhóm vật tư"),
        new("wms.categories.delete", "WMS", "Categories", "Xóa nhóm vật tư"),
        new("wms.material_units.view:all", "WMS", "MaterialUnits", "Xem đơn vị tính"),
        new("wms.material_units.create", "WMS", "MaterialUnits", "Tạo đơn vị tính"),
        new("wms.material_units.update", "WMS", "MaterialUnits", "Sửa đơn vị tính"),
        new("wms.material_units.delete", "WMS", "MaterialUnits", "Xóa đơn vị tính"),
        new("wms.price_books.view:all", "WMS", "PriceBooks", "Xem bảng giá NCC"),
        new("wms.price_books.create", "WMS", "PriceBooks", "Tạo bảng giá NCC"),
        new("wms.price_books.update", "WMS", "PriceBooks", "Sửa bảng giá NCC"),
        new("wms.price_books.delete", "WMS", "PriceBooks", "Xóa bảng giá NCC"),
        new("wms.reports.view:all", "WMS", "Reports", "Xem báo cáo kho"),
        new("wms.reports.export", "WMS", "Reports", "Xuất báo cáo kho"),
    ];

    public static IReadOnlySet<string> All { get; } =
        Definitions.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);

    public static bool IsValid(string permission) => All.Contains(permission);
}

public sealed record PermissionDefinition(
    string Key,
    string Module,
    string Resource,
    string Label);
