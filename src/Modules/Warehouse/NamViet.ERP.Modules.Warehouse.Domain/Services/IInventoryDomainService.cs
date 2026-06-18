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

    Task ApplyApprovedImportAsync(WarehouseTransaction transaction, CancellationToken ct = default);
    Task ApplyApprovedExportAsync(WarehouseTransaction transaction, CancellationToken ct = default);
    Task ApplyDirectIssueAsync(WarehouseTransaction importTransaction, CancellationToken ct = default);
    Task ApplyTransferAsync(WarehouseTransaction transaction, CancellationToken ct = default);
    Task ApplyProjectCloseTransferAsync(string projectCode, CancellationToken ct = default);

    Task<decimal> GetAvailableQtyAsync(string warehouseId, string materialCode, CancellationToken ct = default);
    Task<string> GetGeneralWarehouseIdAsync(CancellationToken ct = default);
}
