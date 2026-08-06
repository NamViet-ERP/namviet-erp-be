using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectClientAndScMembersAndPurchaseOrderInstallments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientName",
                schema: "warehouse",
                table: "projects",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScMembers",
                schema: "warehouse",
                table: "projects",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "purchase_order_installments",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PoCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StageNumber = table.Column<int>(type: "integer", nullable: false),
                    Percent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequiredDocTypes = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_installments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_order_installments_purchase_orders_PoCode",
                        column: x => x.PoCode,
                        principalSchema: "warehouse",
                        principalTable: "purchase_orders",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_installment_documents",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstallmentId = table.Column<int>(type: "integer", nullable: false),
                    DocType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploadedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_installment_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_order_installment_documents_purchase_order_install~",
                        column: x => x.InstallmentId,
                        principalSchema: "warehouse",
                        principalTable: "purchase_order_installments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_installment_documents_InstallmentId",
                schema: "warehouse",
                table: "purchase_order_installment_documents",
                column: "InstallmentId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_installment_documents_InstallmentId_DocType",
                schema: "warehouse",
                table: "purchase_order_installment_documents",
                columns: new[] { "InstallmentId", "DocType" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_installments_PoCode_StageNumber",
                schema: "warehouse",
                table: "purchase_order_installments",
                columns: new[] { "PoCode", "StageNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_order_installment_documents",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_order_installments",
                schema: "warehouse");

            migrationBuilder.DropColumn(
                name: "ClientName",
                schema: "warehouse",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "ScMembers",
                schema: "warehouse",
                table: "projects");
        }
    }
}
