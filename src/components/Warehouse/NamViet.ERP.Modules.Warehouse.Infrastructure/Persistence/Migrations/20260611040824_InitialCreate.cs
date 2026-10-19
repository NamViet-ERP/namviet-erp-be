using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "warehouse");

            migrationBuilder.CreateTable(
                name: "materials",
                schema: "warehouse",
                columns: table => new
                {
                    MaterialCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaterialName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MaterialCategory = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TechnicalSpec = table.Column<string>(type: "text", nullable: true),
                    QualityStandard = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_materials", x => x.MaterialCode);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                schema: "warehouse",
                columns: table => new
                {
                    ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProjectName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProjectType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PmId = table.Column<Guid>(type: "uuid", nullable: false),
                    PmName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MaterialBudget = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.ProjectCode);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MaxDebtDays = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "A"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "project_inventory",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectCode = table.Column<string>(type: "character varying(50)", nullable: false),
                    MaterialCode = table.Column<string>(type: "character varying(100)", nullable: false),
                    PlannedImportQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    PlannedExportQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    ActualImportQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    ActualExportQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_inventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_inventory_materials_MaterialCode",
                        column: x => x.MaterialCode,
                        principalSchema: "warehouse",
                        principalTable: "materials",
                        principalColumn: "MaterialCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_inventory_projects_ProjectCode",
                        column: x => x.ProjectCode,
                        principalSchema: "warehouse",
                        principalTable: "projects",
                        principalColumn: "ProjectCode",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_material_loans",
                schema: "warehouse",
                columns: table => new
                {
                    LoanCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BorrowingProjectCode = table.Column<string>(type: "character varying(50)", nullable: false),
                    LendingProjectCode = table.Column<string>(type: "character varying(50)", nullable: false),
                    MaterialCode = table.Column<string>(type: "character varying(100)", nullable: false),
                    LoanQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LoanDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReturnTicketLink = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LoanReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_material_loans", x => x.LoanCode);
                    table.ForeignKey(
                        name: "FK_project_material_loans_materials_MaterialCode",
                        column: x => x.MaterialCode,
                        principalSchema: "warehouse",
                        principalTable: "materials",
                        principalColumn: "MaterialCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_material_loans_projects_BorrowingProjectCode",
                        column: x => x.BorrowingProjectCode,
                        principalSchema: "warehouse",
                        principalTable: "projects",
                        principalColumn: "ProjectCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_material_loans_projects_LendingProjectCode",
                        column: x => x.LendingProjectCode,
                        principalSchema: "warehouse",
                        principalTable: "projects",
                        principalColumn: "ProjectCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "price_book",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaterialCode = table.Column<string>(type: "character varying(100)", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PurchaseUrl = table.Column<string>(type: "text", nullable: true),
                    CatalogUrl = table.Column<string>(type: "text", nullable: true),
                    PriceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_book", x => x.Id);
                    table.ForeignKey(
                        name: "FK_price_book_materials_MaterialCode",
                        column: x => x.MaterialCode,
                        principalSchema: "warehouse",
                        principalTable: "materials",
                        principalColumn: "MaterialCode",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_price_book_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "warehouse",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_transactions",
                schema: "warehouse",
                columns: table => new
                {
                    TicketCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProjectCode = table.Column<string>(type: "character varying(50)", nullable: true),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DnttNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedByName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_transactions", x => x.TicketCode);
                    table.ForeignKey(
                        name: "FK_warehouse_transactions_projects_ProjectCode",
                        column: x => x.ProjectCode,
                        principalSchema: "warehouse",
                        principalTable: "projects",
                        principalColumn: "ProjectCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warehouse_transactions_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "warehouse",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_transaction_items",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketCode = table.Column<string>(type: "character varying(50)", nullable: false),
                    MaterialCode = table.Column<string>(type: "character varying(100)", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ConstructionPhase = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_transaction_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warehouse_transaction_items_materials_MaterialCode",
                        column: x => x.MaterialCode,
                        principalSchema: "warehouse",
                        principalTable: "materials",
                        principalColumn: "MaterialCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warehouse_transaction_items_warehouse_transactions_TicketCo~",
                        column: x => x.TicketCode,
                        principalSchema: "warehouse",
                        principalTable: "warehouse_transactions",
                        principalColumn: "TicketCode",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_price_book_MaterialCode",
                schema: "warehouse",
                table: "price_book",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_price_book_SupplierId",
                schema: "warehouse",
                table: "price_book",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_project_inventory_MaterialCode",
                schema: "warehouse",
                table: "project_inventory",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_project_inventory_ProjectCode_MaterialCode",
                schema: "warehouse",
                table: "project_inventory",
                columns: new[] { "ProjectCode", "MaterialCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_material_loans_BorrowingProjectCode",
                schema: "warehouse",
                table: "project_material_loans",
                column: "BorrowingProjectCode");

            migrationBuilder.CreateIndex(
                name: "IX_project_material_loans_LendingProjectCode",
                schema: "warehouse",
                table: "project_material_loans",
                column: "LendingProjectCode");

            migrationBuilder.CreateIndex(
                name: "IX_project_material_loans_MaterialCode",
                schema: "warehouse",
                table: "project_material_loans",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_SupplierCode",
                schema: "warehouse",
                table: "suppliers",
                column: "SupplierCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_transaction_items_MaterialCode",
                schema: "warehouse",
                table: "warehouse_transaction_items",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_transaction_items_TicketCode",
                schema: "warehouse",
                table: "warehouse_transaction_items",
                column: "TicketCode");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_transactions_ProjectCode",
                schema: "warehouse",
                table: "warehouse_transactions",
                column: "ProjectCode");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_transactions_SupplierId",
                schema: "warehouse",
                table: "warehouse_transactions",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "price_book",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "project_inventory",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "project_material_loans",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "warehouse_transaction_items",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "materials",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "warehouse_transactions",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "projects",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "suppliers",
                schema: "warehouse");
        }
    }
}
