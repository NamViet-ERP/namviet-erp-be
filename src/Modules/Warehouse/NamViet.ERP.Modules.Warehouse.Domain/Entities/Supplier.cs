using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class Supplier : BaseEntity
{
    public int Id { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public int MaxDebtDays { get; set; } = 30;
    public string? PaymentTerms { get; set; }

    /// <summary>
    /// Phân loại hồ sơ (A–H hoặc "—"). Chỉ cập nhật bởi recompute từ đánh giá đơn hàng — không nhận từ client.
    /// </summary>
    public string Rating { get; set; } = SupplierGrades.Unrated;

    public bool IsBlacklisted { get; set; }
    public DateTime? BlacklistedAt { get; set; }
    public Guid? BlacklistedById { get; set; }
    public string? BlacklistedByName { get; set; }
    public string? BlacklistReason { get; set; }

    public ICollection<WarehouseTransaction> Transactions { get; set; } = [];
    public ICollection<SupplierGroupMember> GroupMemberships { get; set; } = [];
    public ICollection<SupplierContact> Contacts { get; set; } = [];
    public ICollection<SupplierEvaluation> Evaluations { get; set; } = [];
    public ICollection<SupplierDeliveryEvaluation> DeliveryEvaluations { get; set; } = [];
}
