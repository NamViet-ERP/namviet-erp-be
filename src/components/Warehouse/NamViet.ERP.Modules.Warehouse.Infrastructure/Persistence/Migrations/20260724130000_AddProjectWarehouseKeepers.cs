using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260724130000_AddProjectWarehouseKeepers")]
public class AddProjectWarehouseKeepers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "project_warehouse_keepers",
            schema: "warehouse",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                DisplayName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_project_warehouse_keepers", x => x.Id);
                table.ForeignKey(
                    name: "FK_project_warehouse_keepers_projects_ProjectCode",
                    column: x => x.ProjectCode,
                    principalSchema: "warehouse",
                    principalTable: "projects",
                    principalColumn: "ProjectCode",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_project_warehouse_keepers_ProjectCode_UserId",
            schema: "warehouse",
            table: "project_warehouse_keepers",
            columns: new[] { "ProjectCode", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_project_warehouse_keepers_UserId",
            schema: "warehouse",
            table: "project_warehouse_keepers",
            column: "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "project_warehouse_keepers",
            schema: "warehouse");
    }
}
