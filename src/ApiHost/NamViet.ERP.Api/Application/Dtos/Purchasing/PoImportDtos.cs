namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

public record PoImportLinePreviewDto(
    int PoItemId,
    string Name,
    string? MaterialCode,
    string Unit,
    decimal Ordered,
    decimal Received,
    decimal ReservedPending,
    decimal Available,
    decimal UnitPrice,
    /// <summary>Tồn khả dụng kho trung tâm (tồn thực tế − giữ chỗ).</summary>
    decimal GeneralAvailableQty = 0);

public record PoImportPreviewDto(
    string PoCode,
    string PlanCode,
    string ProjectCode,
    string SupplierName,
    IReadOnlyList<PoImportLinePreviewDto> Lines);

public record CreateImportFromPoLineRequest(int PoItemId, decimal Qty);

public record CreateImportFromPoRequest(
    IReadOnlyList<CreateImportFromPoLineRequest>? Items = null,
    bool IsDirectProcurement = false,
    bool IssueFromGeneralWarehouse = false);
