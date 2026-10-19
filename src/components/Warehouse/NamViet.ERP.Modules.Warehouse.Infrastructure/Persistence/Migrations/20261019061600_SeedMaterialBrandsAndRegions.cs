using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(WarehouseDbContext))]
    [Migration("20261019061600_SeedMaterialBrandsAndRegions")]
    public partial class SeedMaterialBrandsAndRegions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO warehouse.material_regions (""Name"", ""Description"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
VALUES 
    ('G7', 'Khối các nước công nghiệp tiên tiến (Mỹ, Đức, Nhật Bản, Anh, Pháp, Ý, Canada)', 1, true, NOW()),
    ('EU', 'Liên minh Châu Âu (EU)', 2, true, NOW()),
    ('ĐNA', 'Đông Nam Á (Việt Nam, Thái Lan, Malaysia, Singapore, Indonesia...)', 3, true, NOW()),
    ('Châu Á', 'Các nước Châu Á khác (Hàn Quốc, Trung Quốc, Đài Loan, Ấn Độ...)', 4, true, NOW()),
    ('Nhóm 1', 'Nhóm tiêu chuẩn 1', 5, true, NOW()),
    ('Khác', 'Các quốc gia và vùng lãnh thổ khác', 6, true, NOW())
ON CONFLICT (""Name"") DO NOTHING;

INSERT INTO warehouse.material_brands (""Name"", ""Country"", ""Description"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
VALUES
    ('Hòa Phát', 'Việt Nam', 'Tập đoàn Thép Hòa Phát', 1, true, NOW()),
    ('Posco', 'Hàn Quốc', 'Tập đoàn thép Posco', 2, true, NOW()),
    ('Wilo', 'Đức', 'Hãng bơm công nghiệp Wilo', 3, true, NOW()),
    ('Ebara', 'Nhật Bản', 'Hãng bơm & thiết bị Ebara', 4, true, NOW()),
    ('System Fan', 'Việt Nam', 'Hệ thống quạt & thông gió System Fan', 5, true, NOW()),
    ('SeAH', 'Hàn Quốc', 'Thép ống & kết cấu SeAH', 6, true, NOW()),
    ('Hyundai', 'Hàn Quốc', 'Tập đoàn công nghiệp Hyundai', 7, true, NOW()),
    ('Schneider', 'Pháp', 'Thiết bị điện & tự động hóa Schneider Electric', 8, true, NOW()),
    ('Mitsubishi', 'Nhật Bản', 'Thiết bị điện & động cơ Mitsubishi', 9, true, NOW()),
    ('Siemens', 'Đức', 'Thiết bị công nghiệp & tự động hóa Siemens', 10, true, NOW()),
    ('Grundfos', 'Đan Mạch', 'Hãng máy bơm Grundfos', 11, true, NOW()),
    ('Teco', 'Đài Loan', 'Động cơ điện & thiết bị Teco', 12, true, NOW()),
    ('Tsurumi', 'Nhật Bản', 'Bơm chìm & bơm công nghiệp Tsurumi', 13, true, NOW())
ON CONFLICT (""Name"") DO NOTHING;

UPDATE warehouse.material_origins SET ""Region"" = 'ĐNA' WHERE ""Name"" = 'Việt Nam' AND (""Region"" IS NULL OR ""Region"" = '');
UPDATE warehouse.material_origins SET ""Region"" = 'G7' WHERE ""Name"" IN ('Đức', 'Nhật Bản', 'Mỹ', 'Anh', 'Ý', 'Pháp', 'Canada') AND (""Region"" IS NULL OR ""Region"" = '');
UPDATE warehouse.material_origins SET ""Region"" = 'Châu Á' WHERE ""Name"" IN ('Hàn Quốc', 'Trung Quốc', 'Đài Loan', 'Ấn Độ') AND (""Region"" IS NULL OR ""Region"" = '');
UPDATE warehouse.material_origins SET ""Region"" = 'ĐNA' WHERE ""Name"" IN ('Thái Lan', 'Malaysia', 'Singapore', 'Indonesia') AND (""Region"" IS NULL OR ""Region"" = '');
UPDATE warehouse.material_origins SET ""Region"" = 'EU' WHERE ""Name"" IN ('Thụy Điển', 'Thụy Sĩ', 'Tây Ban Nha') AND (""Region"" IS NULL OR ""Region"" = '');
UPDATE warehouse.material_origins SET ""Region"" = 'Khác' WHERE ""Name"" IN ('Nga', 'Thổ Nhĩ Kỳ') AND (""Region"" IS NULL OR ""Region"" = '');
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not delete master data on rollback
        }
    }
}
