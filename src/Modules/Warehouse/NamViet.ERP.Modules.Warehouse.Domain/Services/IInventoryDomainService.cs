using NamViet.ERP.Modules.Warehouse.Domain.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Services;

public record ReservationResult(decimal ReservedFromStock, decimal Shortfall);

public interface IInventoryDomainService
{
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
