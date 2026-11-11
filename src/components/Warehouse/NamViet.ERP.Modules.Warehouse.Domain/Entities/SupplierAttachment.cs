using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Tài liệu hồ sơ đính kèm của nhà cung cấp (GPKD, Profile, Bảng giá, ...).</summary>
public class SupplierAttachment : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int SupplierId { get; set; }
    /// <summary>business_license | company_profile | catalog_price_list | other</summary>
    public string DocType { get; set; } = "other";
    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? UploadedByName { get; set; }

    public Supplier? Supplier { get; set; }
}

public static class SupplierAttachmentDocTypes
{
    public const string BusinessLicense = "business_license";
    public const string CompanyProfile = "company_profile";
    public const string CatalogPriceList = "catalog_price_list";
    public const string Other = "other";

    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        BusinessLicense, CompanyProfile, CatalogPriceList, Other
    };

    public static string ToClientLabel(string? docType) => (docType ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        BusinessLicense => "GPKD / ĐKKD doanh nghiệp",
        CompanyProfile => "Hồ sơ năng lực (Profile)",
        CatalogPriceList => "Catalog & Bảng giá sản phẩm",
        _ => "Tài liệu khác"
    };
}
