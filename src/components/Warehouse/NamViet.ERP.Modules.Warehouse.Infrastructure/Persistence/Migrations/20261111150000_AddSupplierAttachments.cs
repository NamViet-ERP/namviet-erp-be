using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20261111150000_AddSupplierAttachments")]
public partial class AddSupplierAttachments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS warehouse.supplier_attachments (
                "Id" uuid NOT NULL PRIMARY KEY,
                "SupplierId" integer NOT NULL,
                "DocType" character varying(50) NOT NULL,
                "FileName" character varying(255) NOT NULL,
                "StorageKey" character varying(500) NOT NULL,
                "FileSizeBytes" bigint NOT NULL DEFAULT 0,
                "ContentType" character varying(100) NOT NULL DEFAULT 'application/octet-stream',
                "UploadedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
                "UploadedByName" character varying(200) NULL,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
                "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
                "DeletedAt" timestamp with time zone NULL,
                CONSTRAINT "FK_supplier_attachments_suppliers_SupplierId" FOREIGN KEY ("SupplierId") 
                    REFERENCES warehouse.suppliers ("Id") ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS "IX_supplier_attachments_SupplierId" 
            ON warehouse.supplier_attachments ("SupplierId");

            CREATE INDEX IF NOT EXISTS "IX_supplier_attachments_SupplierId_DocType" 
            ON warehouse.supplier_attachments ("SupplierId", "DocType");
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS warehouse.supplier_attachments;
        """);
    }
}
