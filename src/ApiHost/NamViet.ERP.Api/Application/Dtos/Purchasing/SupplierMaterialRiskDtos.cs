namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

public record SupplierMaterialRiskLookupRequest(
    IReadOnlyList<SupplierMaterialRiskPairRequest> Pairs);

public record SupplierMaterialRiskPairRequest(
    int? SupplierId,
    string? SupplierName,
    string MaterialCode);

public record SupplierMaterialRiskDto(
    int? SupplierId,
    string? SupplierName,
    string MaterialCode,
    int DefectCount3m,
    int WarrantyClaimCount,
    bool Warn,
    IReadOnlyList<string> Reasons);
