using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

/// <summary>
/// DDL-only: bảng đính kèm tài liệu PO (hợp đồng, hóa đơn, CO/CQ, BB giao hàng, …).
/// </summary>
[DbContext(typeof(WarehouseDbContext))]
[Migration("20260727091000_AddPurchaseOrderAttachments")]
public class AddPurchaseOrderAttachments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS warehouse.purchase_order_attachments (
                "Id" uuid NOT NULL,
                "PoCode" character varying(50) NOT NULL,
                "DocType" character varying(20) NOT NULL,
                "FileName" character varying(255) NOT NULL,
                "StorageKey" character varying(500) NOT NULL,
                "UploadedAt" timestamp with time zone NOT NULL,
                "UploadedByName" character varying(200) NULL,
                CONSTRAINT "PK_purchase_order_attachments" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_purchase_order_attachments_purchase_orders_PoCode"
                    FOREIGN KEY ("PoCode") REFERENCES warehouse.purchase_orders ("Code") ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS "IX_purchase_order_attachments_PoCode"
                ON warehouse.purchase_order_attachments ("PoCode");

            CREATE INDEX IF NOT EXISTS "IX_purchase_order_attachments_PoCode_DocType"
                ON warehouse.purchase_order_attachments ("PoCode", "DocType");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS warehouse.purchase_order_attachments;
            """);
    }
}
