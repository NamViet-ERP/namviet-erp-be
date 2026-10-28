using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

public partial class AddGanttSnapshots2 : Migration
{
    // Repair the original empty-snapshot scaffold: all other tables were already created
    // by preceding migrations. Keep this migration ID so deployed databases do not rerun it.
    protected override void Up(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.CreateTable(
                name: "ProjectGanttSnapshots",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectCode = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PlanPct = table.Column<double>(type: "double precision", nullable: false),
                    ActualPct = table.Column<double>(type: "double precision", nullable: false),
                    ItemCount = table.Column<int>(type: "integer", nullable: false),
                    ProjectCode1 = table.Column<string>(type: "character varying(50)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectGanttSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectGanttSnapshotItems",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<string>(type: "text", nullable: false),
                    TaskName = table.Column<string>(type: "text", nullable: false),
                    PlanPct = table.Column<double>(type: "double precision", nullable: false),
                    ActualPct = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectGanttSnapshotItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectGanttSnapshotItems_SnapshotId",
                schema: "warehouse",
                table: "ProjectGanttSnapshotItems",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectGanttSnapshots_ProjectCode1",
                schema: "warehouse",
                table: "ProjectGanttSnapshots",
                column: "ProjectCode1");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ProjectGanttSnapshotItems", schema: "warehouse");
        migrationBuilder.DropTable(name: "ProjectGanttSnapshots", schema: "warehouse");
    }
}
