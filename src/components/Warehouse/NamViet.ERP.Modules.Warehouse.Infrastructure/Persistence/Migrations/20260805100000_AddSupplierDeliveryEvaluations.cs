using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260805100000_AddSupplierDeliveryEvaluations")]
public class AddSupplierDeliveryEvaluations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS warehouse.supplier_delivery_evaluations (
                "Id" serial PRIMARY KEY,
                "SupplierId" integer NOT NULL,
                "SupplierNameSnapshot" character varying(255),
                "PoCode" character varying(50) NOT NULL,
                "ImportTicketCode" character varying(50) NOT NULL,
                "ReceivedAt" timestamp with time zone NOT NULL,
                "DueAt" timestamp with time zone NOT NULL,
                "Status" character varying(20) NOT NULL,
                "Score" numeric(5,2),
                "Grade" character varying(10),
                "OnTimeDeliveryRate" numeric(5,2),
                "DefectRate" numeric(5,2),
                "WarrantyCompletionRate" numeric(5,2),
                "Note" character varying(2000),
                "EvaluatedById" uuid,
                "EvaluatedByName" character varying(200),
                "EvaluatedAt" date,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT (NOW()),
                "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (NOW()),
                "DeletedAt" timestamp with time zone,
                CONSTRAINT "FK_supplier_delivery_evaluations_suppliers_SupplierId"
                    FOREIGN KEY ("SupplierId") REFERENCES warehouse.suppliers ("Id") ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_supplier_delivery_evaluations_ImportTicketCode"
                ON warehouse.supplier_delivery_evaluations ("ImportTicketCode")
                WHERE "DeletedAt" IS NULL;

            CREATE INDEX IF NOT EXISTS "IX_supplier_delivery_evaluations_PoCode"
                ON warehouse.supplier_delivery_evaluations ("PoCode");

            CREATE INDEX IF NOT EXISTS "IX_supplier_delivery_evaluations_Status_DueAt"
                ON warehouse.supplier_delivery_evaluations ("Status", "DueAt");

            CREATE INDEX IF NOT EXISTS "IX_supplier_delivery_evaluations_SupplierId"
                ON warehouse.supplier_delivery_evaluations ("SupplierId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS warehouse.supplier_delivery_evaluations;
            """);
    }
}
