namespace NamViet.ERP.Api.Application.Services.Wms;

public interface IWmsCatalogQueryService
{
    Task<decimal> GetLatestPriceAsync(string materialCode, CancellationToken ct = default);
    Task<decimal> GetLatestPriceForSupplierAsync(string materialCode, int supplierId, CancellationToken ct = default);
    Task<(int totalStock, decimal totalValue)> GetWarehouseTotalsAsync(string warehouseId, CancellationToken ct = default);
    Task<decimal> GetTotalInventoryValueAsync(CancellationToken ct = default);
}
