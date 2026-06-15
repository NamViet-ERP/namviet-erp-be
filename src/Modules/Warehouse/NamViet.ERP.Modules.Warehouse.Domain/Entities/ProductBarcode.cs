namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class ProductBarcode
{
    public int Id { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string NumericSku { get; set; } = string.Empty;

    public Material Material { get; set; } = null!;
}
