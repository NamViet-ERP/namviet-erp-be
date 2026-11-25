namespace NamViet.ERP.Modules.Warehouse.Domain;

public static class WmsSettingsKeys
{
    public const string GeneralWarehouseId = "general_warehouse_id";

    /// <summary>Thiết lập module NCC — lưu JSON một dòng, đọc theo default trong code nếu chưa có.</summary>
    public const string SupplierModule = "supplier_module";

    /// <summary>Cấu hình hệ số markup chào thầu của sổ giá (Boss Markup Settings).</summary>
    public const string PriceBookMarkup = "pricebook_markup_settings";
}

public static class WmsWarehouseIds
{
    public const string General = "wh-general";
}

public static class MaterialCategoryCodes
{
    public const string Uncategorized = "UNCATEGORIZED";
}
