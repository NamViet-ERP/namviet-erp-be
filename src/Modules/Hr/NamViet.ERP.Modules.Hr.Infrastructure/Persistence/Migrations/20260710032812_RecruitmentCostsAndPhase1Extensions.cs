using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NamViet.ERP.Modules.Hr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecruitmentCostsAndPhase1Extensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DeadlineDate",
                schema: "hr",
                table: "recruitment_plans",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OpenDate",
                schema: "hr",
                table: "recruitment_plans",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "recruitment_costs",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PositionName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Period = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: true),
                    PaymentYear = table.Column<int>(type: "integer", nullable: true),
                    PaymentMonth = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recruitment_costs", x => x.Id);
                });

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "hr",
                table: "candidates",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateTable(
                name: "recruitment_budgets",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    TotalAllocated = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recruitment_budgets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recruitment_budgets_Year",
                schema: "hr",
                table: "recruitment_budgets",
                column: "Year",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recruitment_budgets",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "recruitment_costs",
                schema: "hr");

            migrationBuilder.DropColumn(
                name: "DeadlineDate",
                schema: "hr",
                table: "recruitment_plans");

            migrationBuilder.DropColumn(
                name: "OpenDate",
                schema: "hr",
                table: "recruitment_plans");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "hr",
                table: "candidates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);
        }
    }
}
