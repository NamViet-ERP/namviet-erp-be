using NamViet.ERP.Modules.Warehouse.Domain.Entities;

namespace NamViet.ERP.Api.Application.Services.Wms;

internal static class ExportReturnLineHelper
{
    internal static string BuildKey(string materialCode, string? constructionPhase) =>
        $"{materialCode.Trim()}\u001f{(constructionPhase ?? "").Trim()}";

    internal static string ResolveMaterialCode(string? sku, string? productId) =>
        string.IsNullOrWhiteSpace(sku) ? (productId ?? "").Trim() : sku.Trim();

    internal static Dictionary<string, decimal> SumReturnedQtyByKey(
        IEnumerable<WarehouseTransaction> returnVouchers)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var voucher in returnVouchers)
        {
            foreach (var item in voucher.Items)
            {
                var key = BuildKey(item.MaterialCode, item.ConstructionPhase);
                result[key] = result.GetValueOrDefault(key) + item.Quantity;
            }
        }

        return result;
    }

    internal static List<ExportReturnableLineComputation> BuildReturnableLines(
        WarehouseTransaction export,
        Dictionary<string, decimal> returnedByKey)
    {
        var lines = new List<ExportReturnableLineComputation>();
        foreach (var item in export.Items)
        {
            var key = BuildKey(item.MaterialCode, item.ConstructionPhase);
            var exportedQty = item.Quantity;
            var returnedQty = returnedByKey.GetValueOrDefault(key);
            var remainingQty = exportedQty - returnedQty;
            if (remainingQty <= 0)
                continue;

            lines.Add(new ExportReturnableLineComputation(
                item.MaterialCode,
                item.ConstructionPhase,
                exportedQty,
                returnedQty,
                remainingQty));
        }

        return lines;
    }
}

internal sealed record ExportReturnableLineComputation(
    string MaterialCode,
    string? ConstructionPhase,
    decimal ExportedQty,
    decimal ReturnedQty,
    decimal RemainingQty);
