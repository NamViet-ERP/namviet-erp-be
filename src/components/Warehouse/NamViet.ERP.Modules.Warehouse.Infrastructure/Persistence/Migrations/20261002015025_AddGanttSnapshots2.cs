using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGanttSnapshots2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "warehouse");

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
                    CategoryLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "conversions",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VoucherCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VoucherDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FromWarehouseId = table.Column<string>(type: "text", nullable: false),
                    FromWarehouseName = table.Column<string>(type: "text", nullable: false),
                    FromType = table.Column<string>(type: "text", nullable: false),
                    ToWarehouseId = table.Column<string>(type: "text", nullable: false),
                    ToWarehouseName = table.Column<string>(type: "text", nullable: false),
                    ToType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedByName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
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
                name: "material_categories",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    ParentId = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_material_categories_material_categories_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "warehouse",
                        principalTable: "material_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "material_units",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_units", x => x.Id);
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
                    ClientProjectId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Stage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ScMembers = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.ProjectCode);
                });

            migrationBuilder.CreateTable(
                name: "purchase_estimates",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CurrentVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ShareToken = table.Column<Guid>(type: "uuid", nullable: true),
                    ShareTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PreparerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PreparerDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConvertedPlanCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_estimates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_item_estimate_prices",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaterialName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EstimatePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_item_estimate_prices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_rfq_quotes",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaterialName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    QuoteDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: true),
                    PaymentTerm = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PoRef = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_rfq_quotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_sell_prices",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaterialName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SellPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_sell_prices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_groups",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_groups", x => x.Id);
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
                    CreditLimit = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PaymentTerms = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Rating = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "D"),
                    ApprovalStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "new"),
                    IsBlacklisted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    BlacklistedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BlacklistedById = table.Column<Guid>(type: "uuid", nullable: true),
                    BlacklistedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BlacklistReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.Id);
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
                name: "materials",
                schema: "warehouse",
                columns: table => new
                {
                    MaterialCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaterialName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TechnicalSpec = table.Column<string>(type: "text", nullable: true),
                    GeneralTechnicalSpec = table.Column<string>(type: "text", nullable: true),
                    QualityStandard = table.Column<string>(type: "text", nullable: true),
                    CeilingPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    AbcClass = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    IsConsumable = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_materials", x => x.MaterialCode);
                    table.ForeignKey(
                        name: "FK_materials_material_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "warehouse",
                        principalTable: "material_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                    WbsCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsGroup = table.Column<bool>(type: "boolean", nullable: false),
                    Department = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    StatusLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StatusTone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "project_warehouse_keepers",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
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
                    table.ForeignKey(
                        name: "FK_ProjectGanttSnapshots_projects_ProjectCode1",
                        column: x => x.ProjectCode1,
                        principalSchema: "warehouse",
                        principalTable: "projects",
                        principalColumn: "ProjectCode");
                });

            migrationBuilder.CreateTable(
                name: "purchase_plans",
                schema: "warehouse",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    L1ApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    L1ApprovedByName = table.Column<string>(type: "text", nullable: true),
                    L1ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    L2ApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    L2ApprovedByName = table.Column<string>(type: "text", nullable: true),
                    L2ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedById = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectedByName = table.Column<string>(type: "text", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectReason = table.Column<string>(type: "text", nullable: true),
                    RemadeFromCode = table.Column<string>(type: "text", nullable: true),
                    RemadeToCode = table.Column<string>(type: "text", nullable: true),
                    BudgetTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ProposalTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PurchaseTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaidTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_plans", x => x.Code);
                    table.ForeignKey(
                        name: "FK_purchase_plans_projects_ProjectCode",
                        column: x => x.ProjectCode,
                        principalSchema: "warehouse",
                        principalTable: "projects",
                        principalColumn: "ProjectCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_estimate_approvals",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EstimateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActionById = table.Column<Guid>(type: "uuid", nullable: true),
                    ActionByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ActionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_estimate_approvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_estimate_approvals_purchase_estimates_EstimateId",
                        column: x => x.EstimateId,
                        principalSchema: "warehouse",
                        principalTable: "purchase_estimates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_estimate_proposals",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EstimateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Spec = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_estimate_proposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_estimate_proposals_purchase_estimates_EstimateId",
                        column: x => x.EstimateId,
                        principalSchema: "warehouse",
                        principalTable: "purchase_estimates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_estimate_versions",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EstimateId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_estimate_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_estimate_versions_purchase_estimates_EstimateId",
                        column: x => x.EstimateId,
                        principalSchema: "warehouse",
                        principalTable: "purchase_estimates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_contacts",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    FullName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Position = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_contacts_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "warehouse",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_delivery_evaluations",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    SupplierNameSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PoCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ImportTicketCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quality = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    Delivery = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    Price = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    Service = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    Compliance = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    Score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    Grade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EvaluatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    EvaluatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EvaluatedAt = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_delivery_evaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_delivery_evaluations_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "warehouse",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_evaluations",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    SupplierNameSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Period = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    Grade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    EvaluatedAt = table.Column<DateOnly>(type: "date", nullable: false),
                    CriteriaScores = table.Column<Dictionary<string, int>>(type: "jsonb", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EvaluatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    EvaluatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_evaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_evaluations_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "warehouse",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_group_members",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierGroupId = table.Column<int>(type: "integer", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_group_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_group_members_supplier_groups_SupplierGroupId",
                        column: x => x.SupplierGroupId,
                        principalSchema: "warehouse",
                        principalTable: "supplier_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_supplier_group_members_suppliers_SupplierId",
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    WarehouseId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FromWarehouseId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ToWarehouseId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PurchasePlanCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SoCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Customer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    WarehouseKeeper = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    WarehouseApprovalStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    IsDirectProcurement = table.Column<bool>(type: "boolean", nullable: false),
                    BeneficiaryProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ReceiptEvaluation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ShortageReport = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LinkedDefectTicketCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LinkedTicketCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
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
                    table.ForeignKey(
                        name: "FK_warehouse_transactions_warehouse_transactions_LinkedTicketC~",
                        column: x => x.LinkedTicketCode,
                        principalSchema: "warehouse",
                        principalTable: "warehouse_transactions",
                        principalColumn: "TicketCode",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "stocktakes",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VoucherCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VoucherDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
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
                name: "conversion_items",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConversionId = table.Column<string>(type: "character varying(50)", nullable: false),
                    MaterialCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.ForeignKey(
                        name: "FK_conversion_items_materials_MaterialCode",
                        column: x => x.MaterialCode,
                        principalSchema: "warehouse",
                        principalTable: "materials",
                        principalColumn: "MaterialCode",
                        onDelete: ReferentialAction.Restrict);
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
                    ReservedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    MinStockQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
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
                    BorrowPmSigned = table.Column<bool>(type: "boolean", nullable: false),
                    LendPmSigned = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByName = table.Column<string>(type: "text", nullable: true),
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
                name: "warehouse_inventory",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WarehouseId = table.Column<string>(type: "character varying(50)", nullable: false),
                    MaterialCode = table.Column<string>(type: "character varying(100)", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
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
                    table.ForeignKey(
                        name: "FK_ProjectGanttSnapshotItems_ProjectGanttSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalSchema: "warehouse",
                        principalTable: "ProjectGanttSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                schema: "warehouse",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PlanCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OrderPlacementStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DeliveryStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PaymentStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PaymentWarning = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SupplierPaymentTermsDays = table.Column<int>(type: "integer", nullable: false),
                    SupplierNameSnapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PaymentDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PlacedById = table.Column<Guid>(type: "uuid", nullable: true),
                    PlacedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PlacedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.Code);
                    table.ForeignKey(
                        name: "FK_purchase_orders_projects_ProjectCode",
                        column: x => x.ProjectCode,
                        principalSchema: "warehouse",
                        principalTable: "projects",
                        principalColumn: "ProjectCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_purchase_plans_PlanCode",
                        column: x => x.PlanCode,
                        principalSchema: "warehouse",
                        principalTable: "purchase_plans",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "warehouse",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "purchase_plan_lines",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    SystemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MaterialCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProductName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Spec = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    KgPerUnit = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    LineNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PurchaseStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StatusNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BudgetUnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    BudgetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ProposalKgPerUnit = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ProposalUnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ProposalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ProposalSupplierName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ProposalNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ProposalStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequiredDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OrderedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpectedDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualReceiptDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DeliveryNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PurchaseKgPerUnit = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    PurchaseUnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PurchaseAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PurchaseSupplierName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PurchaseNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SelectedUnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    SelectedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    SelectedSupplierName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SelectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OrderedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReceivedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DefectQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_plan_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_plan_lines_purchase_plans_PlanCode",
                        column: x => x.PlanCode,
                        principalSchema: "warehouse",
                        principalTable: "purchase_plans",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_estimate_lines",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaterialName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OldQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    NewQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    EstimateUnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_estimate_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_estimate_lines_purchase_estimate_versions_VersionId",
                        column: x => x.VersionId,
                        principalSchema: "warehouse",
                        principalTable: "purchase_estimate_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_reservations",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WarehouseId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaterialCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceTicketCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReservedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ExternalQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    FulfilledQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FulfilledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_materials_MaterialCode",
                        column: x => x.MaterialCode,
                        principalSchema: "warehouse",
                        principalTable: "materials",
                        principalColumn: "MaterialCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_projects_ProjectCode",
                        column: x => x.ProjectCode,
                        principalSchema: "warehouse",
                        principalTable: "projects",
                        principalColumn: "ProjectCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_warehouse_transactions_SourceTicketC~",
                        column: x => x.SourceTicketCode,
                        principalSchema: "warehouse",
                        principalTable: "warehouse_transactions",
                        principalColumn: "TicketCode",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "warehouse",
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_transaction_attachments",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketCode = table.Column<string>(type: "character varying(50)", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_transaction_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warehouse_transaction_attachments_warehouse_transactions_Ti~",
                        column: x => x.TicketCode,
                        principalSchema: "warehouse",
                        principalTable: "warehouse_transactions",
                        principalColumn: "TicketCode",
                        onDelete: ReferentialAction.Cascade);
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
                    MaterialNameSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UnitSnapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PlannedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AcceptedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    DefectQty = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ShortageQty = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    DefectLineReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ShortageLineReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ConstructionPhase = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
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
                        name: "FK_warehouse_transaction_items_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "warehouse",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_warehouse_transaction_items_warehouse_transactions_TicketCo~",
                        column: x => x.TicketCode,
                        principalSchema: "warehouse",
                        principalTable: "warehouse_transactions",
                        principalColumn: "TicketCode",
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
                    MaterialCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SystemQty = table.Column<decimal>(type: "numeric", nullable: false),
                    ActualQty = table.Column<decimal>(type: "numeric", nullable: false),
                    Diff = table.Column<decimal>(type: "numeric", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stocktake_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stocktake_items_materials_MaterialCode",
                        column: x => x.MaterialCode,
                        principalSchema: "warehouse",
                        principalTable: "materials",
                        principalColumn: "MaterialCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stocktake_items_stocktakes_StocktakeId",
                        column: x => x.StocktakeId,
                        principalSchema: "warehouse",
                        principalTable: "stocktakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_contracts",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ContractNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PoCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SignedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UploadedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_contracts_purchase_orders_PoCode",
                        column: x => x.PoCode,
                        principalSchema: "warehouse",
                        principalTable: "purchase_orders",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_purchase_contracts_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "warehouse",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "purchase_defects",
                schema: "warehouse",
                columns: table => new
                {
                    TicketCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PoCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ImportTicketCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PlanCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CauseSource = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    SupplierFeedback = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    HandlingMethod = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ProcessedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedByName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedByName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_defects", x => x.TicketCode);
                    table.ForeignKey(
                        name: "FK_purchase_defects_purchase_orders_PoCode",
                        column: x => x.PoCode,
                        principalSchema: "warehouse",
                        principalTable: "purchase_orders",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_attachments",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PoCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DocType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploadedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_order_attachments_purchase_orders_PoCode",
                        column: x => x.PoCode,
                        principalSchema: "warehouse",
                        principalTable: "purchase_orders",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "purchase_payments",
                schema: "warehouse",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PoCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StageNumber = table.Column<int>(type: "integer", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    SupplierNameSnapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    InvoiceNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PaidById = table.Column<Guid>(type: "uuid", nullable: true),
                    PaidByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_payments", x => x.Code);
                    table.ForeignKey(
                        name: "FK_purchase_payments_purchase_orders_PoCode",
                        column: x => x.PoCode,
                        principalSchema: "warehouse",
                        principalTable: "purchase_orders",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_payments_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "warehouse",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_items",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PoCode = table.Column<string>(type: "character varying(50)", nullable: false),
                    PlanLineId = table.Column<int>(type: "integer", nullable: true),
                    MaterialCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Spec = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EstPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PoPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Ordered = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Received = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Defected = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExpectedDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    WarrantyMonths = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    WarrantyAnchorType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "delivery")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_order_items_materials_MaterialCode",
                        column: x => x.MaterialCode,
                        principalSchema: "warehouse",
                        principalTable: "materials",
                        principalColumn: "MaterialCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_items_purchase_orders_PoCode",
                        column: x => x.PoCode,
                        principalSchema: "warehouse",
                        principalTable: "purchase_orders",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_purchase_order_items_purchase_plan_lines_PlanLineId",
                        column: x => x.PlanLineId,
                        principalSchema: "warehouse",
                        principalTable: "purchase_plan_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_plan_line_quotes",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanLineId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    KgPerUnit = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_plan_line_quotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_plan_line_quotes_purchase_plan_lines_PlanLineId",
                        column: x => x.PlanLineId,
                        principalSchema: "warehouse",
                        principalTable: "purchase_plan_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_defect_attachments",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_defect_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_defect_attachments_purchase_defects_TicketCode",
                        column: x => x.TicketCode,
                        principalSchema: "warehouse",
                        principalTable: "purchase_defects",
                        principalColumn: "TicketCode",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_defect_items",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PoItemId = table.Column<int>(type: "integer", nullable: false),
                    MaterialCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LineReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_defect_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_defect_items_purchase_defects_TicketCode",
                        column: x => x.TicketCode,
                        principalSchema: "warehouse",
                        principalTable: "purchase_defects",
                        principalColumn: "TicketCode",
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

            migrationBuilder.CreateTable(
                name: "purchase_warranties",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PoCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PoItemId = table.Column<int>(type: "integer", nullable: false),
                    ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PlanCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    MaterialCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MaterialName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    WarrantyMonths = table.Column<int>(type: "integer", nullable: false),
                    AnchorType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AcceptanceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    WarrantyEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ImportTicketCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_warranties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_warranties_purchase_order_items_PoItemId",
                        column: x => x.PoItemId,
                        principalSchema: "warehouse",
                        principalTable: "purchase_order_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_purchase_warranties_purchase_orders_PoCode",
                        column: x => x.PoCode,
                        principalSchema: "warehouse",
                        principalTable: "purchase_orders",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_warranty_claims",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    WarrantyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "warranty"),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DefectType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ServiceRating = table.Column<int>(type: "integer", nullable: true),
                    ServiceNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_warranty_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_warranty_claims_purchase_warranties_WarrantyId",
                        column: x => x.WarrantyId,
                        principalSchema: "warehouse",
                        principalTable: "purchase_warranties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_ActorUserId_OccurredAt",
                schema: "warehouse",
                table: "activity_events",
                columns: new[] { "ActorUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_Module_OccurredAt",
                schema: "warehouse",
                table: "activity_events",
                columns: new[] { "Module", "OccurredAt" });

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
                name: "IX_conversion_items_MaterialCode",
                schema: "warehouse",
                table: "conversion_items",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_MaterialCode",
                schema: "warehouse",
                table: "inventory_reservations",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_ProjectCode_MaterialCode",
                schema: "warehouse",
                table: "inventory_reservations",
                columns: new[] { "ProjectCode", "MaterialCode" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_SourceTicketCode",
                schema: "warehouse",
                table: "inventory_reservations",
                column: "SourceTicketCode");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_WarehouseId_MaterialCode_Status",
                schema: "warehouse",
                table: "inventory_reservations",
                columns: new[] { "WarehouseId", "MaterialCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_material_categories_Code",
                schema: "warehouse",
                table: "material_categories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_categories_ParentId",
                schema: "warehouse",
                table: "material_categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_material_units_Name",
                schema: "warehouse",
                table: "material_units",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_materials_CategoryId",
                schema: "warehouse",
                table: "materials",
                column: "CategoryId");

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
                name: "IX_project_construction_tasks_ProjectCode",
                schema: "warehouse",
                table: "project_construction_tasks",
                column: "ProjectCode");

            migrationBuilder.CreateIndex(
                name: "IX_project_construction_tasks_ProjectCode_SortOrder",
                schema: "warehouse",
                table: "project_construction_tasks",
                columns: new[] { "ProjectCode", "SortOrder" });

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

            migrationBuilder.CreateIndex(
                name: "IX_purchase_contracts_Code",
                schema: "warehouse",
                table: "purchase_contracts",
                column: "Code",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_contracts_PoCode",
                schema: "warehouse",
                table: "purchase_contracts",
                column: "PoCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_contracts_PoCode_StorageKey",
                schema: "warehouse",
                table: "purchase_contracts",
                columns: new[] { "PoCode", "StorageKey" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL AND \"PoCode\" IS NOT NULL AND \"StorageKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_contracts_ProjectCode",
                schema: "warehouse",
                table: "purchase_contracts",
                column: "ProjectCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_contracts_Status",
                schema: "warehouse",
                table: "purchase_contracts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_contracts_SupplierId",
                schema: "warehouse",
                table: "purchase_contracts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_defect_attachments_TicketCode",
                schema: "warehouse",
                table: "purchase_defect_attachments",
                column: "TicketCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_defect_items_PoItemId",
                schema: "warehouse",
                table: "purchase_defect_items",
                column: "PoItemId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_defect_items_TicketCode",
                schema: "warehouse",
                table: "purchase_defect_items",
                column: "TicketCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_defects_Id",
                schema: "warehouse",
                table: "purchase_defects",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_defects_ImportTicketCode",
                schema: "warehouse",
                table: "purchase_defects",
                column: "ImportTicketCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_defects_PoCode",
                schema: "warehouse",
                table: "purchase_defects",
                column: "PoCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_defects_SourceType",
                schema: "warehouse",
                table: "purchase_defects",
                column: "SourceType");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_defects_Status",
                schema: "warehouse",
                table: "purchase_defects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_estimate_approvals_EstimateId_Level",
                schema: "warehouse",
                table: "purchase_estimate_approvals",
                columns: new[] { "EstimateId", "Level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_estimate_lines_VersionId_MaterialCode",
                schema: "warehouse",
                table: "purchase_estimate_lines",
                columns: new[] { "VersionId", "MaterialCode" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_estimate_proposals_EstimateId",
                schema: "warehouse",
                table: "purchase_estimate_proposals",
                column: "EstimateId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_estimate_versions_EstimateId_VersionNumber",
                schema: "warehouse",
                table: "purchase_estimate_versions",
                columns: new[] { "EstimateId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_estimates_Code",
                schema: "warehouse",
                table: "purchase_estimates",
                column: "Code",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_estimates_ProjectCode",
                schema: "warehouse",
                table: "purchase_estimates",
                column: "ProjectCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_estimates_ShareToken",
                schema: "warehouse",
                table: "purchase_estimates",
                column: "ShareToken",
                unique: true,
                filter: "\"ShareToken\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_item_estimate_prices_MaterialCode_ProjectCode_Effe~",
                schema: "warehouse",
                table: "purchase_item_estimate_prices",
                columns: new[] { "MaterialCode", "ProjectCode", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_attachments_PoCode",
                schema: "warehouse",
                table: "purchase_order_attachments",
                column: "PoCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_attachments_PoCode_DocType",
                schema: "warehouse",
                table: "purchase_order_attachments",
                columns: new[] { "PoCode", "DocType" });

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

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_items_MaterialCode",
                schema: "warehouse",
                table: "purchase_order_items",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_items_PlanLineId",
                schema: "warehouse",
                table: "purchase_order_items",
                column: "PlanLineId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_items_PoCode",
                schema: "warehouse",
                table: "purchase_order_items",
                column: "PoCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_DeliveryStatus",
                schema: "warehouse",
                table: "purchase_orders",
                column: "DeliveryStatus");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_OrderPlacementStatus",
                schema: "warehouse",
                table: "purchase_orders",
                column: "OrderPlacementStatus");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_PaymentStatus",
                schema: "warehouse",
                table: "purchase_orders",
                column: "PaymentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_PlanCode",
                schema: "warehouse",
                table: "purchase_orders",
                column: "PlanCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_ProjectCode",
                schema: "warehouse",
                table: "purchase_orders",
                column: "ProjectCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_SupplierId",
                schema: "warehouse",
                table: "purchase_orders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_payments_DueDate",
                schema: "warehouse",
                table: "purchase_payments",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_payments_PoCode",
                schema: "warehouse",
                table: "purchase_payments",
                column: "PoCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_payments_PoCode_StageNumber",
                schema: "warehouse",
                table: "purchase_payments",
                columns: new[] { "PoCode", "StageNumber" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL AND lower(\"Status\") <> 'cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_payments_Status",
                schema: "warehouse",
                table: "purchase_payments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_payments_SupplierId",
                schema: "warehouse",
                table: "purchase_payments",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_plan_line_quotes_PlanLineId_SortOrder",
                schema: "warehouse",
                table: "purchase_plan_line_quotes",
                columns: new[] { "PlanLineId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_plan_lines_MaterialCode",
                schema: "warehouse",
                table: "purchase_plan_lines",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_plan_lines_PlanCode_LineNo",
                schema: "warehouse",
                table: "purchase_plan_lines",
                columns: new[] { "PlanCode", "LineNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_plans_CreatedAt",
                schema: "warehouse",
                table: "purchase_plans",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_plans_ProjectCode",
                schema: "warehouse",
                table: "purchase_plans",
                column: "ProjectCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_plans_Status",
                schema: "warehouse",
                table: "purchase_plans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_rfq_quotes_MaterialCode",
                schema: "warehouse",
                table: "purchase_rfq_quotes",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_rfq_quotes_QuoteDate",
                schema: "warehouse",
                table: "purchase_rfq_quotes",
                column: "QuoteDate");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_rfq_quotes_SupplierId",
                schema: "warehouse",
                table: "purchase_rfq_quotes",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_sell_prices_MaterialCode_ProjectCode_EffectiveDate",
                schema: "warehouse",
                table: "purchase_sell_prices",
                columns: new[] { "MaterialCode", "ProjectCode", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_warranties_Code",
                schema: "warehouse",
                table: "purchase_warranties",
                column: "Code",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_warranties_MaterialCode",
                schema: "warehouse",
                table: "purchase_warranties",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_warranties_PoCode",
                schema: "warehouse",
                table: "purchase_warranties",
                column: "PoCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_warranties_PoItemId",
                schema: "warehouse",
                table: "purchase_warranties",
                column: "PoItemId",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_warranties_ProjectCode",
                schema: "warehouse",
                table: "purchase_warranties",
                column: "ProjectCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_warranties_SupplierId",
                schema: "warehouse",
                table: "purchase_warranties",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_warranty_claims_Kind",
                schema: "warehouse",
                table: "purchase_warranty_claims",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_warranty_claims_TicketCode",
                schema: "warehouse",
                table: "purchase_warranty_claims",
                column: "TicketCode",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_warranty_claims_WarrantyId",
                schema: "warehouse",
                table: "purchase_warranty_claims",
                column: "WarrantyId");

            migrationBuilder.CreateIndex(
                name: "IX_stocktake_items_MaterialCode",
                schema: "warehouse",
                table: "stocktake_items",
                column: "MaterialCode");

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
                name: "IX_supplier_contacts_SupplierId",
                schema: "warehouse",
                table: "supplier_contacts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_delivery_evaluations_ImportTicketCode",
                schema: "warehouse",
                table: "supplier_delivery_evaluations",
                column: "ImportTicketCode",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_delivery_evaluations_PoCode",
                schema: "warehouse",
                table: "supplier_delivery_evaluations",
                column: "PoCode");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_delivery_evaluations_Status_DueAt",
                schema: "warehouse",
                table: "supplier_delivery_evaluations",
                columns: new[] { "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_delivery_evaluations_SupplierId",
                schema: "warehouse",
                table: "supplier_delivery_evaluations",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_evaluations_EvaluatedAt",
                schema: "warehouse",
                table: "supplier_evaluations",
                column: "EvaluatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_evaluations_SupplierId_Period",
                schema: "warehouse",
                table: "supplier_evaluations",
                columns: new[] { "SupplierId", "Period" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_group_members_SupplierGroupId_SupplierId",
                schema: "warehouse",
                table: "supplier_group_members",
                columns: new[] { "SupplierGroupId", "SupplierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_group_members_SupplierId",
                schema: "warehouse",
                table: "supplier_group_members",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_groups_Name",
                schema: "warehouse",
                table: "supplier_groups",
                column: "Name",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_ApprovalStatus",
                schema: "warehouse",
                table: "suppliers",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_IsBlacklisted",
                schema: "warehouse",
                table: "suppliers",
                column: "IsBlacklisted");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_SupplierCode",
                schema: "warehouse",
                table: "suppliers",
                column: "SupplierCode",
                unique: true);

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
                name: "IX_warehouse_transaction_attachments_TicketCode",
                schema: "warehouse",
                table: "warehouse_transaction_attachments",
                column: "TicketCode");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_transaction_items_MaterialCode",
                schema: "warehouse",
                table: "warehouse_transaction_items",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_transaction_items_SupplierId",
                schema: "warehouse",
                table: "warehouse_transaction_items",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_transaction_items_TicketCode",
                schema: "warehouse",
                table: "warehouse_transaction_items",
                column: "TicketCode");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_transactions_Id",
                schema: "warehouse",
                table: "warehouse_transactions",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_transactions_LinkedTicketCode",
                schema: "warehouse",
                table: "warehouse_transactions",
                column: "LinkedTicketCode");

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
                name: "activity_events",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "consumable_quotas",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "conversion_items",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "inventory_reservations",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "material_units",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "product_barcodes",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "project_boms",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "project_construction_tasks",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "project_inventory",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "project_material_loans",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "project_warehouse_keepers",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "ProjectGanttSnapshotItems",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_contracts",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_defect_attachments",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_defect_items",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_estimate_approvals",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_estimate_lines",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_estimate_proposals",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_item_estimate_prices",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_order_attachments",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_order_comments",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_order_installment_documents",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_payments",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_plan_line_quotes",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_rfq_quotes",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_sell_prices",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_warranty_claims",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "stocktake_items",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "supplier_contacts",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "supplier_delivery_evaluations",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "supplier_evaluations",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "supplier_group_members",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "warehouse_inventory",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "warehouse_transaction_attachments",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "warehouse_transaction_items",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "wms_settings",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "conversions",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "ProjectGanttSnapshots",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_defects",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_estimate_versions",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_order_installments",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_warranties",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "stocktakes",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "supplier_groups",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "warehouse_transactions",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_estimates",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_order_items",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "warehouses",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "materials",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_orders",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_plan_lines",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "material_categories",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "suppliers",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "purchase_plans",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "projects",
                schema: "warehouse");
        }
    }
}
