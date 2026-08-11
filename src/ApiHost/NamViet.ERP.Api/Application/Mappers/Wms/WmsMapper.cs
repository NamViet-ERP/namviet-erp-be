using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;

namespace NamViet.ERP.Api.Application.Mappers.Wms;

public static class WmsMapper
{
    public static string ToClientApprovalStatus(ApprovalStatus status) => status switch
    {
        ApprovalStatus.Approved => "approved",
        ApprovalStatus.Rejected => "rejected",
        ApprovalStatus.Draft => "draft",
        _ => "pending"
    };

    public static ApprovalStatus FromClientApprovalStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "approved" => ApprovalStatus.Approved,
        "rejected" => ApprovalStatus.Rejected,
        "draft" => ApprovalStatus.Draft,
        _ => ApprovalStatus.Pending
    };

    public static string ToClientKeeperApprovalStatus(WarehouseKeeperApprovalStatus status) => status switch
    {
        WarehouseKeeperApprovalStatus.Approved => "approved",
        WarehouseKeeperApprovalStatus.Rejected => "rejected",
        _ => "pending"
    };

    public static WarehouseKeeperApprovalStatus FromClientKeeperStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "approved" => WarehouseKeeperApprovalStatus.Approved,
        "rejected" => WarehouseKeeperApprovalStatus.Rejected,
        _ => WarehouseKeeperApprovalStatus.Pending
    };

    public static string ToClientRole(string role) => role switch
    {
        "Warehouse_Keeper" => "storekeeper",
        "Admin" => "admin",
        "Accountant" => "accountant",
        "Procurement" => "procurement",
        "PM" => "pm",
        "HR" => "hr",
        "Staff" => "staff",
        "Project" => "project",
        "Production" => "production",
        _ => role.ToLowerInvariant()
    };

    public static string ToClientRoleLabel(string role) => role switch
    {
        "Warehouse_Keeper" => "Thủ kho (Storekeeper)",
        "Admin" => "Quản trị viên",
        "Accountant" => "Kế toán trưởng",
        "Procurement" => "Phòng mua hàng",
        "PM" => "Quản lý dự án (PM)",
        "HR" => "Nhân sự (HR)",
        "Staff" => "Nhân viên",
        "Project" => "Phòng Dự án",
        "Production" => "Phòng Sản xuất",
        _ => role
    };

    public static string ToIdentityRole(string clientRole) => clientRole.ToLowerInvariant() switch
    {
        "admin" => "Admin",
        "storekeeper" => "Warehouse_Keeper",
        "accountant" => "Accountant",
        "procurement" => "Procurement",
        "pm" => "PM",
        "hr" => "HR",
        "staff" => "Staff",
        "project" => "Project",
        "production" => "Production",
        _ => clientRole
    };

    public static string FormatDate(DateTime dt) => dt.ToString("dd/MM/yyyy");

    public static string FormatDate(DateOnly d) => d.ToString("dd/MM/yyyy");

    public static string FormatCreatedAt(DateTime value) => value.ToString("o");

    public static string? FormatApprovedAt(DateTime? value) =>
        value.HasValue ? value.Value.ToString("o") : null;

    public static DateOnly ParseClientDate(string? value) =>
        DateOnly.TryParseExact(value, "dd/MM/yyyy", out var d) ? d
        : DateOnly.TryParse(value, out d) ? d
        : DateOnly.FromDateTime(DateTime.UtcNow);

    public static WarehouseDto ToWarehouse(WmsWarehouse w, int totalStock, decimal totalValue) =>
        new(w.Id, w.Code, w.Name, w.Group, w.Address, w.Branch, w.Manager, totalStock, totalValue, w.Status);

    public static ImportDto ToImport(WarehouseTransaction t)
    {
        var items = t.Items.Select(i => new ImportItemDto(
            i.MaterialCode, i.MaterialCode,
            TransactionItemHelper.ResolveName(i),
            i.Quantity, TransactionItemHelper.ResolveUnit(i), i.UnitPrice,
            i.SupplierName, i.SupplierId, i.Notes)).ToList();
        var attachments = t.Attachments
            .OrderByDescending(a => a.UploadedAt)
            .Select(a => new ImportAttachmentDto(
                a.Id,
                a.Kind,
                a.FileName,
                a.StorageKey,
                FormatCreatedAt(a.UploadedAt)))
            .ToList();
        var total = items.Sum(x => x.Qty * x.UnitPrice);
        return new ImportDto(
            t.Id.ToString(), t.TicketCode, FormatDate(t.CreatedAt),
            t.SupplierName ?? t.Supplier?.SupplierName ?? "",
            t.ReferenceCode ?? t.InvoiceNumber ?? "", total,
            ToClientApprovalStatus(t.ApprovalStatus),
            ToClientKeeperApprovalStatus(t.WarehouseApprovalStatus),
            t.ApprovedByName ?? "", FormatApprovedAt(t.ApprovedAt), t.WarehouseKeeper ?? "",
            items, t.CreatedByName, t.Notes ?? "",
            t.WarehouseId ?? "", t.IsDirectProcurement,
            t.BeneficiaryProjectCode,
            FormatCreatedAt(t.CreatedAt),
            t.PurchasePlanCode,
            t.ReceiptEvaluation,
            attachments);
    }

    public static ExportDto ToExport(
        WarehouseTransaction t, string? warehouseName = null, string? projectName = null) =>
        new(t.Id.ToString(), t.TicketCode, FormatDate(t.CreatedAt),
            t.SoCode ?? "", t.Customer ?? "",
            t.WarehouseId ?? "", warehouseName ?? projectName ?? "",
            t.Items.Select(i => new ExportItemDto(
                i.MaterialCode, i.MaterialCode,
                TransactionItemHelper.ResolveName(i),
                i.Quantity, TransactionItemHelper.ResolveUnit(i))).ToList(),
            ToClientApprovalStatus(t.ApprovalStatus),
            t.CreatedByName, t.Notes ?? "", t.ApprovedByName, FormatApprovedAt(t.ApprovedAt),
            t.ProjectCode ?? t.BeneficiaryProjectCode,
            projectName,
            FormatCreatedAt(t.CreatedAt));

    public static TransferDto ToTransfer(
        WarehouseTransaction t,
        string? fromName = null,
        string? toName = null,
        string? projectName = null,
        string? toProjectName = null) =>
        new(t.Id.ToString(), t.TicketCode, FormatDate(t.CreatedAt),
            t.FromWarehouseId ?? "", fromName ?? projectName ?? "",
            t.ToWarehouseId ?? "", toName ?? toProjectName ?? "",
            t.Items.Select(i => new TransferItemDto(
                i.MaterialCode, i.MaterialCode,
                TransactionItemHelper.ResolveName(i),
                i.Quantity, TransactionItemHelper.ResolveUnit(i),
                i.ConstructionPhase)).ToList(),
            ToClientApprovalStatus(t.ApprovalStatus),
            t.CreatedByName, t.Notes ?? "", t.ApprovedByName, FormatApprovedAt(t.ApprovedAt),
            t.ProjectCode,
            projectName,
            IsCrossProjectTransferEntity(t) ? t.BeneficiaryProjectCode : null,
            IsCrossProjectTransferEntity(t) ? toProjectName : null,
            FormatCreatedAt(t.CreatedAt));

    public static ExportReturnDto ToExportReturn(
        WarehouseTransaction t,
        string sourceExportId,
        string sourceExportCode,
        string? projectName = null,
        List<ImportAttachmentDto>? attachments = null) =>
        new(t.Id.ToString(), t.TicketCode, FormatDate(t.CreatedAt),
            sourceExportId, sourceExportCode,
            t.ProjectCode ?? t.BeneficiaryProjectCode,
            projectName,
            t.Items.Select(i => new ExportReturnItemDto(
                i.MaterialCode, i.MaterialCode,
                TransactionItemHelper.ResolveName(i),
                i.Quantity, TransactionItemHelper.ResolveUnit(i))).ToList(),
            ToClientApprovalStatus(t.ApprovalStatus),
            t.CreatedByName, t.Notes ?? "", t.ApprovedByName, FormatApprovedAt(t.ApprovedAt),
            FormatCreatedAt(t.CreatedAt),
            attachments);

    private static bool IsCrossProjectTransferEntity(WarehouseTransaction t) =>
        !string.IsNullOrWhiteSpace(t.ProjectCode)
        && !string.IsNullOrWhiteSpace(t.BeneficiaryProjectCode)
        && !string.Equals(t.ProjectCode, t.BeneficiaryProjectCode, StringComparison.OrdinalIgnoreCase);

    public static StocktakeDto ToStocktake(Stocktake s) =>
        new(s.Id, s.VoucherCode, FormatDate(s.VoucherDate), s.CreatedByName, s.Status,
            s.WarehouseId, s.WarehouseName, s.Condition, s.IsFrozen, s.Note,
            s.Items.Select(i => new StocktakeItemDto(
                i.MaterialCode, i.Sku, i.Name, i.SystemQty, i.ActualQty, i.Diff)).ToList());

    public static ConversionDto ToConversion(Conversion c) =>
        new(c.Id, c.VoucherCode, FormatDate(c.VoucherDate), c.FromWarehouseId, c.FromWarehouseName,
            c.FromType, c.ToWarehouseId, c.ToWarehouseName, c.ToType,
            c.Items.Select(i => new ConversionItemDto(
                i.MaterialCode, i.Sku, i.Name, i.Qty, i.Unit, i.Reason)).ToList(),
            c.Status, c.CreatedByName, c.ApprovedByName, c.Note);

    public static LendingLogDto ToLendingLog(ProjectMaterialLoan l) =>
        new(l.LoanCode, l.LoanCode, FormatDate(l.LoanDate),
            l.BorrowingProjectCode, l.LendingProjectCode, l.MaterialCode,
            l.LoanQty,
            l.Status switch
            {
                LoanStatus.Returned => "returned",
                LoanStatus.Borrowing => "borrowing",
                _ => "pending"
            },
            l.BorrowPmSigned, l.LendPmSigned, l.LoanReason ?? "",
            l.CreatedByName, FormatDate(l.CreatedAt));
}
