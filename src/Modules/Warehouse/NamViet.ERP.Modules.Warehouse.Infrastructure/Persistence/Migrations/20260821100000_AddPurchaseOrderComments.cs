using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

/// <summary>
/// DDL-only: bảng bình luận đa người dùng trên đơn mua hàng.
/// </summary>
[DbContext(typeof(WarehouseDbContext))]
[Migration("20260821100000_AddPurchaseOrderComments")]
public class AddPurchaseOrderComments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "purchase_order_comments",
            schema: "warehouse",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PoCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                AuthorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_purchase_order_comments", x => x.Id);
                table.ForeignKey(
                    name: "FK_purchase_order_comments_purchase_orders_PoCode",
                    column: x => x.PoCode,
                    principalSchema: "warehouse",
                    principalTable: "purchase_orders",
                    principalColumn: "Code",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_purchase_order_comments_PoCode",
            schema: "warehouse",
            table: "purchase_order_comments",
            column: "PoCode");

        migrationBuilder.CreateIndex(
            name: "IX_purchase_order_comments_PoCode_CreatedAt",
            schema: "warehouse",
            table: "purchase_order_comments",
            columns: new[] { "PoCode", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "purchase_order_comments",
            schema: "warehouse");
    }
}
