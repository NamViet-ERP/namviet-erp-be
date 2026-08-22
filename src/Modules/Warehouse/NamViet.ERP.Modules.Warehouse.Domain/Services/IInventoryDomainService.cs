using NamViet.ERP.Modules.Warehouse.Domain.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Services;

public record ReservationResult(decimal ReservedFromStock, decimal Shortfall);

/// <summary>Phiếu HL nguồn tồn kho, trạng thái chờ duyệt - giữ chỗ khả dụng kho dự án.</summary>
public readonly record struct PendingStockDefectHold(string ProjectCode, string MaterialCode, decimal Quantity);

public interface IInventoryDomainService
{
    /// <summary>
    /// Tồn khả dụng kho dự án = tồn thực tế − xuất/chuyển dự kiến − phiếu HL tồn kho chờ duyệt.
    /// </summary>
    Task<decimal> GetProjectAvailableQtyAsync(
        string projectCode,
        string materialCode,
        string? excludeStockDefectTicketCode = null,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, decimal>> GetPendingStockDefectQtyByMaterialAsync(
        string projectCode,
        string? excludeStockDefectTicketCode = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<PendingStockDefectHold>> ListPendingStockDefectHoldsAsync(
        CancellationToken ct = default);

    Task<ReservationResult> CreateReservationAsync(
        string warehouseId,
        string projectCode,
        string materialCode,
        decimal requestedQty,
        string sourceTicketCode,
        CancellationToken ct = default);

    Task FulfillReservationsAsync(string sourceTicketCode, CancellationToken ct = default);
    Task CancelReservationsAsync(string sourceTicketCode, CancellationToken ct = default);

    /// <summary>Ghi dự kiến nhập vào tồn dự án (phiếu nháp/chờ duyệt) — không giữ kho tổng.</summary>
    Task CreateProjectImportPlannedAsync(WarehouseTransaction transaction, CancellationToken ct = default);
    Task CancelProjectImportPlannedAsync(WarehouseTransaction transaction, CancellationToken ct = default);

    /// <summary>Ghi dự kiến xuất từ tồn dự án (phiếu nháp/chờ duyệt).</summary>
    Task CreateProjectExportPlannedAsync(WarehouseTransaction transaction, CancellationToken ct = default);
    Task CancelProjectExportPlannedAsync(WarehouseTransaction transaction, CancellationToken ct = default);

    Task ApplyApprovedImportAsync(WarehouseTransaction transaction, CancellationToken ct = default);
    Task ApplyApprovedExportAsync(WarehouseTransaction transaction, CancellationToken ct = default);

    /// <summary>Ghi dự kiến hoàn trả về kho dự án (phiếu nháp/chờ duyệt).</summary>
    Task CreateProjectExportReturnPlannedAsync(WarehouseTransaction transaction, CancellationToken ct = default);
    Task CancelProjectExportReturnPlannedAsync(WarehouseTransaction transaction, CancellationToken ct = default);
    /// <summary>Hoàn trả vật tư về kho dự án khi duyệt phiếu hoàn trả theo phiếu xuất.</summary>
    Task ApplyApprovedExportReturnAsync(WarehouseTransaction transaction, CancellationToken ct = default);
    Task ApplyDirectIssueAsync(WarehouseTransaction importTransaction, CancellationToken ct = default);
    Task ApplyTransferAsync(WarehouseTransaction transaction, CancellationToken ct = default);
    /// <summary>Hoàn trả từ kho dự án về kho tổng khi duyệt phiếu chuyển.</summary>
    Task ApplyReturnToGeneralAsync(WarehouseTransaction transaction, CancellationToken ct = default);
    Task ApplyCrossProjectTransferAsync(WarehouseTransaction transaction, CancellationToken ct = default);
    Task ApplyProjectCloseTransferAsync(string projectCode, CancellationToken ct = default);

    /// <summary>Điều chỉnh tồn dự án sau kiểm kê (+ diff / − |diff|).</summary>
    Task ApplyStocktakeAdjustmentAsync(
        string projectCode, string materialCode, decimal diffQty, CancellationToken ct = default);

    Task<decimal> GetAvailableQtyAsync(string warehouseId, string materialCode, CancellationToken ct = default);
    Task<string> GetGeneralWarehouseIdAsync(CancellationToken ct = default);
}
