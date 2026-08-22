using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260818140000_AddProjectConstructionTasks")]
public class AddProjectConstructionTasks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "project_construction_tasks",
            schema: "warehouse",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                PlannedStart = table.Column<DateOnly>(type: "date", nullable: false),
                PlannedEnd = table.Column<DateOnly>(type: "date", nullable: false),
                ActualEnd = table.Column<DateOnly>(type: "date", nullable: true),
                PercentComplete = table.Column<int>(type: "integer", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_project_construction_tasks", x => x.Id);
                table.ForeignKey(
                    name: "FK_project_construction_tasks_projects_ProjectCode",
                    column: x => x.ProjectCode,
                    principalSchema: "warehouse",
                    principalTable: "projects",
                    principalColumn: "ProjectCode",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_project_construction_tasks_ProjectCode",
            schema: "warehouse",
            table: "project_construction_tasks",
            column: "ProjectCode");

        migrationBuilder.CreateIndex(
            name: "IX_project_construction_tasks_ProjectCode_SortOrder",
            schema: "warehouse",
            table: "project_construction_tasks",
            columns: new[] { "ProjectCode", "SortOrder" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "project_construction_tasks",
            schema: "warehouse");
    }
}
