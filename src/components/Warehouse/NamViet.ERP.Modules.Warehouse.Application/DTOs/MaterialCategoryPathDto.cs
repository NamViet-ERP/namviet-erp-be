namespace NamViet.ERP.Modules.Warehouse.Application.DTOs;

/// <summary>5 cấp cây vật tư theo CÂY VẬT TƯ.xlsx: Danh mục → Nhóm → Loại → Phân loại → Kiểu.</summary>
public record MaterialCategoryPathDto(
    string? DanhMuc,
    string? NhomVatTu,
    string? LoaiVatTu,
    string? PhanLoai,
    string? Kieu);
