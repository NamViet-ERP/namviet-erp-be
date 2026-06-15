using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WmsFacadeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BeneficiaryProjectCode",
                schema: "warehouse",
                table: "warehouse_transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Customer",
                schema: "warehouse",
                table: "warehouse_transactions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeStatus",
                schema: "warehouse",
                table: "warehouse_transactions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "pending");

            migrationBuilder.AddColumn<string>(
                name: "FromWarehouseId",
                schema: "warehouse",
                table: "warehouse_transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                schema: "warehouse",
                table: "warehouse_transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Backfill unique IDs for existing rows before creating unique index
            migrationBuilder.Sql("""
                UPDATE warehouse.warehouse_transactions
                SET "Id" = gen_random_uuid()
                WHERE "Id" = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.AddColumn<bool>(
                name: "IsDirectProcurement",
                schema: "warehouse",
                table: "warehouse_transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceCode",
                schema: "warehouse",
                table: "warehouse_transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoCode",
                schema: "warehouse",
                table: "warehouse_transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierName",
                schema: "warehouse",
                table: "warehouse_transactions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToWarehouseId",
                schema: "warehouse",
                table: "warehouse_transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarehouseApprovalStatus",
                schema: "warehouse",
                table: "warehouse_transactions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarehouseId",
                schema: "warehouse",
                table: "warehouse_transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarehouseKeeper",
                schema: "warehouse",
                table: "warehouse_transactions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeId",
                schema: "warehouse",
                table: "projects",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                schema: "warehouse",
                table: "projects",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BorrowPmSigned",
                schema: "warehouse",
                table: "project_material_loans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByName",
                schema: "warehouse",
                table: "project_material_loans",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LendPmSigned",
                schema: "warehouse",
                table: "project_material_loans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "consumable_quotas",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectCode = table.Column<string>(type: "text", nullable: false),
                    MaterialCode = table.Column<string>(type: "text", nullable: false),
                    QuotaQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IssuedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ProjectCode1 = table.Column<string>(type: "character varying(50)", nullable: true),
                    MaterialCode1 = table.Column<string>(type: "character varying(100)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consumable_quotas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consumable_quotas_materials_MaterialCode1",
                        column: x => x.MaterialCode1,
                        principalSchema: "warehouse",
                        principalTable: "materials",
                        principalColumn: "MaterialCode");
                    table.ForeignKey(
                        name: "FK_consumable_quotas_projects_ProjectCode1",
                        column: x => x.ProjectCode1,
                        principalSchema: "warehouse",
                        principalTable: "projects",
                        principalColumn: "ProjectCode");
                });

            migrationBuilder.CreateTable(
                name: "conversions",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VoucherCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Date = table.Column<string>(type: "text", nullable: false),
                    FromWarehouseId = table.Column<string>(type: "text", nullable: false),
                    FromWarehouseName = table.Column<string>(type: "text", nullable: false),
                    FromType = table.Column<string>(type: "text", nullable: false),
                    ToWarehouseId = table.Column<string>(type: "text", nullable: false),
                    ToWarehouseName = table.Column<string>(type: "text", nullable: false),
                    ToType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    ApprovedBy = table.Column<string>(type: "text", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "expected_imports",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Project = table.Column<string>(type: "text", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric", nullable: false),
                    Surplus = table.Column<decimal>(type: "numeric", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expected_imports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "misa_inventory_snapshots",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    WmsQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    MisaQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SnapshotAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_misa_inventory_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "product_barcodes",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaterialCode = table.Column<string>(type: "character varying(100)", nullable: false),
                    NumericSku = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_barcodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_barcodes_materials_MaterialCode",
                        column: x => x.MaterialCode,
                        principalSchema: "warehouse",
                        principalTable: "materials",
                        principalColumn: "MaterialCode",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_boms",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectCode = table.Column<string>(type: "text", nullable: false),
                    MaterialCode = table.Column<string>(type: "text", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ProjectCode1 = table.Column<string>(type: "character varying(50)", nullable: true),
                    MaterialCode1 = table.Column<string>(type: "character varying(100)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_boms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_boms_materials_MaterialCode1",
                        column: x => x.MaterialCode1,
                        principalSchema: "warehouse",
                        principalTable: "materials",
                        principalColumn: "MaterialCode");
                    table.ForeignKey(
                        name: "FK_project_boms_projects_ProjectCode1",
                        column: x => x.ProjectCode1,
                        principalSchema: "warehouse",
                        principalTable: "projects",
                        principalColumn: "ProjectCode");
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                schema: "warehouse",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Project = table.Column<string>(type: "text", nullable: false),
                    Supplier = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Group = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Branch = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Manager = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "wms_settings",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wms_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "conversion_items",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConversionId = table.Column<string>(type: "character varying(50)", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversion_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_conversion_items_conversions_ConversionId",
                        column: x => x.ConversionId,
                        principalSchema: "warehouse",
                        principalTable: "conversions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_items",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PoCode = table.Column<string>(type: "character varying(50)", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    EstPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    PoPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Ordered = table.Column<decimal>(type: "numeric", nullable: false),
                    Received = table.Column<decimal>(type: "numeric", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_order_items_purchase_orders_PoCode",
                        column: x => x.PoCode,
                        principalSchema: "warehouse",
                        principalTable: "purchase_orders",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "adjustments",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VoucherCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Date = table.Column<string>(type: "text", nullable: false),
                    StocktakeCode = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    Condition = table.Column<string>(type: "text", nullable: false),
                    WarehouseId = table.Column<string>(type: "character varying(50)", nullable: false),
                    WarehouseName = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    IsNew = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_adjustments_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "warehouse",
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stocktakes",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VoucherCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Date = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    WarehouseId = table.Column<string>(type: "character varying(50)", nullable: false),
                    WarehouseName = table.Column<string>(type: "text", nullable: false),
                    Condition = table.Column<string>(type: "text", nullable: false),
                    IsFrozen = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stocktakes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stocktakes_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "warehouse",
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_inventory",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WarehouseId = table.Column<string>(type: "character varying(50)", nullable: false),
                    MaterialCode = table.Column<string>(type: "character varying(100)", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_inventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warehouse_inventory_materials_MaterialCode",
                        column: x => x.MaterialCode,
                        principalSchema: "warehouse",
                        principalTable: "materials",
                        principalColumn: "MaterialCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warehouse_inventory_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "warehouse",
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "adjustment_items",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdjustmentId = table.Column<string>(type: "character varying(50)", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    AdjustQty = table.Column<decimal>(type: "numeric", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adjustment_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_adjustment_items_adjustments_AdjustmentId",
                        column: x => x.AdjustmentId,
                        principalSchema: "warehouse",
                        principalTable: "adjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stocktake_items",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StocktakeId = table.Column<string>(type: "character varying(50)", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SystemQty = table.Column<decimal>(type: "numeric", nullable: false),
                    ActualQty = table.Column<decimal>(type: "numeric", nullable: false),
                    Diff = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stocktake_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stocktake_items_stocktakes_StocktakeId",
                        column: x => x.StocktakeId,
                        principalSchema: "warehouse",
                        principalTable: "stocktakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_transactions_Id",
                schema: "warehouse",
                table: "warehouse_transactions",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_adjustment_items_AdjustmentId",
                schema: "warehouse",
                table: "adjustment_items",
                column: "AdjustmentId");

            migrationBuilder.CreateIndex(
                name: "IX_adjustments_VoucherCode",
                schema: "warehouse",
                table: "adjustments",
                column: "VoucherCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_adjustments_WarehouseId",
                schema: "warehouse",
                table: "adjustments",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_consumable_quotas_MaterialCode1",
                schema: "warehouse",
                table: "consumable_quotas",
                column: "MaterialCode1");

            migrationBuilder.CreateIndex(
                name: "IX_consumable_quotas_ProjectCode_MaterialCode",
                schema: "warehouse",
                table: "consumable_quotas",
                columns: new[] { "ProjectCode", "MaterialCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_consumable_quotas_ProjectCode1",
                schema: "warehouse",
                table: "consumable_quotas",
                column: "ProjectCode1");

            migrationBuilder.CreateIndex(
                name: "IX_conversion_items_ConversionId",
                schema: "warehouse",
                table: "conversion_items",
                column: "ConversionId");

            migrationBuilder.CreateIndex(
                name: "IX_product_barcodes_MaterialCode",
                schema: "warehouse",
                table: "product_barcodes",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_product_barcodes_NumericSku",
                schema: "warehouse",
                table: "product_barcodes",
                column: "NumericSku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_boms_MaterialCode1",
                schema: "warehouse",
                table: "project_boms",
                column: "MaterialCode1");

            migrationBuilder.CreateIndex(
                name: "IX_project_boms_ProjectCode_MaterialCode",
                schema: "warehouse",
                table: "project_boms",
                columns: new[] { "ProjectCode", "MaterialCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_boms_ProjectCode1",
                schema: "warehouse",
                table: "project_boms",
                column: "ProjectCode1");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_items_PoCode",
                schema: "warehouse",
                table: "purchase_order_items",
                column: "PoCode");

            migrationBuilder.CreateIndex(
                name: "IX_stocktake_items_StocktakeId",
                schema: "warehouse",
                table: "stocktake_items",
                column: "StocktakeId");

            migrationBuilder.CreateIndex(
                name: "IX_stocktakes_VoucherCode",
                schema: "warehouse",
                table: "stocktakes",
                column: "VoucherCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stocktakes_WarehouseId",
                schema: "warehouse",
                table: "stocktakes",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_inventory_MaterialCode",
                schema: "warehouse",
                table: "warehouse_inventory",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_inventory_WarehouseId_MaterialCode",
                schema: "warehouse",
                table: "warehouse_inventory",
                columns: new[] { "WarehouseId", "MaterialCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_Code",
                schema: "warehouse",
                table: "warehouses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wms_settings_Key",
                schema: "warehouse",
                table: "wms_settings",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adjustment_items",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "consumable_quotas",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "conversion_items",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "expected_imports",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "misa_inventory_snapshots",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "product_barcodes",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "project_boms",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_order_items",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "stocktake_items",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "warehouse_inventory",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "wms_settings",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "adjustments",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "conversions",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_orders",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "stocktakes",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "warehouses",
                schema: "warehouse");

            migrationBuilder.DropIndex(
                name: "IX_warehouse_transactions_Id",
                schema: "warehouse",
                table: "warehouse_transactions");

            migrationBuilder.DropColumn(
                name: "BeneficiaryProjectCode",
                schema: "warehouse",
                table: "warehouse_transactions");

            migrationBuilder.DropColumn(
                name: "Customer",
                schema: "warehouse",
                table: "warehouse_transactions");

            migrationBuilder.DropColumn(
                name: "FeStatus",
                schema: "warehouse",
                table: "warehouse_transactions");

            migrationBuilder.DropColumn(
                name: "FromWarehouseId",
                schema: "warehouse",
                table: "warehouse_transactions");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "warehouse",
                table: "warehouse_transactions");

            migrationBuilder.DropColumn(
                name: "IsDirectProcurement",
                schema: "warehouse",
                table: "warehouse_transactions");

            migrationBuilder.DropColumn(
                name: "ReferenceCode",
                schema: "warehouse",
                table: "warehouse_transactions");

            migrationBuilder.DropColumn(
                name: "SoCode",
                schema: "warehouse",
                table: "warehouse_transactions");

            migrationBuilder.DropColumn(
                name: "SupplierName",
                schema: "warehouse",
                table: "warehouse_transactions");

            migrationBuilder.DropColumn(
                name: "ToWarehouseId",
                schema: "warehouse",
                table: "warehouse_transactions");

            migrationBuilder.DropColumn(
                name: "WarehouseApprovalStatus",
                schema: "warehouse",
                table: "warehouse_transactions");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                schema: "warehouse",
                table: "warehouse_transactions");

            migrationBuilder.DropColumn(
                name: "WarehouseKeeper",
                schema: "warehouse",
                table: "warehouse_transactions");

            migrationBuilder.DropColumn(
                name: "FeId",
                schema: "warehouse",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "Stage",
                schema: "warehouse",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "BorrowPmSigned",
                schema: "warehouse",
                table: "project_material_loans");

            migrationBuilder.DropColumn(
                name: "CreatedByName",
                schema: "warehouse",
                table: "project_material_loans");

            migrationBuilder.DropColumn(
                name: "LendPmSigned",
                schema: "warehouse",
                table: "project_material_loans");
        }
    }
}
