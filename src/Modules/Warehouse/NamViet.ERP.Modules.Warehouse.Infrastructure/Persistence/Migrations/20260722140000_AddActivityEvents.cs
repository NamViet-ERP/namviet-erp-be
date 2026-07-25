using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260722140000_AddActivityEvents")]
public class AddActivityEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "activity_events",
            schema: "warehouse",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                ActorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                EntityCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                CategoryLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_activity_events", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_activity_events_Module_OccurredAt",
            schema: "warehouse",
            table: "activity_events",
            columns: new[] { "Module", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_activity_events_ActorUserId_OccurredAt",
            schema: "warehouse",
            table: "activity_events",
            columns: new[] { "ActorUserId", "OccurredAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "activity_events",
            schema: "warehouse");
    }
}
