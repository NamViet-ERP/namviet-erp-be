using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20261001120000_AddProjectConstructionTaskWbsFields")]
public class AddProjectConstructionTaskWbsFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.project_construction_tasks
                ADD COLUMN IF NOT EXISTS "WbsCode" character varying(50) DEFAULT '',
                ADD COLUMN IF NOT EXISTS "Level" integer DEFAULT 0,
                ADD COLUMN IF NOT EXISTS "ParentId" uuid NULL,
                ADD COLUMN IF NOT EXISTS "IsGroup" boolean DEFAULT false,
                ADD COLUMN IF NOT EXISTS "Department" character varying(255) NULL,
                ADD COLUMN IF NOT EXISTS "StatusLabel" character varying(100) NULL,
                ADD COLUMN IF NOT EXISTS "StatusTone" character varying(20) NULL,
                ADD COLUMN IF NOT EXISTS "Notes" text NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.project_construction_tasks
                DROP COLUMN IF EXISTS "WbsCode",
                DROP COLUMN IF EXISTS "Level",
                DROP COLUMN IF EXISTS "ParentId",
                DROP COLUMN IF EXISTS "IsGroup",
                DROP COLUMN IF EXISTS "Department",
                DROP COLUMN IF EXISTS "StatusLabel",
                DROP COLUMN IF EXISTS "StatusTone",
                DROP COLUMN IF EXISTS "Notes";
            """);
    }
}
