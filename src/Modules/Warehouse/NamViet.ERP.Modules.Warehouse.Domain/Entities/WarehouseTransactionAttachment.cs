namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class WarehouseTransactionAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TicketCode { get; set; } = string.Empty;
    public string Kind { get; set; } = "document";
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public WarehouseTransaction? Transaction { get; set; }
}
