using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WmsVoucherDateCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── adjustments ──────────────────────────────────────────────────
            migrationBuilder.AddColumn<DateOnly>(
                name: "VoucherDate",
                schema: "warehouse",
                table: "adjustments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "warehouse",
                table: "adjustments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByName",
                schema: "warehouse",
                table: "adjustments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE warehouse.adjustments
                SET "VoucherDate" = TO_DATE("Date", 'DD/MM/YYYY')
                WHERE "Date" ~ '^\d{2}/\d{2}/\d{4}$';

                UPDATE warehouse.adjustments
                SET "VoucherDate" = DATE '2026-01-01'
                WHERE "VoucherDate" IS NULL;

                UPDATE warehouse.adjustments
                SET "CreatedByName" = "CreatedBy",
                    "CreatedById" = '00000000-0000-0000-0000-000000000000'::uuid;
                """);

            migrationBuilder.DropColumn(
                name: "Date",
                schema: "warehouse",
                table: "adjustments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "warehouse",
                table: "adjustments");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "VoucherDate",
                schema: "warehouse",
                table: "adjustments",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedById",
                schema: "warehouse",
                table: "adjustments",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByName",
                schema: "warehouse",
                table: "adjustments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            // ── stocktakes ───────────────────────────────────────────────────
            migrationBuilder.AddColumn<DateOnly>(
                name: "VoucherDate",
                schema: "warehouse",
                table: "stocktakes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "warehouse",
                table: "stocktakes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByName",
                schema: "warehouse",
                table: "stocktakes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE warehouse.stocktakes
                SET "VoucherDate" = TO_DATE("Date", 'DD/MM/YYYY')
                WHERE "Date" ~ '^\d{2}/\d{2}/\d{4}$';

                UPDATE warehouse.stocktakes
                SET "VoucherDate" = DATE '2026-01-01'
                WHERE "VoucherDate" IS NULL;

                UPDATE warehouse.stocktakes
                SET "CreatedByName" = "CreatedBy",
                    "CreatedById" = '00000000-0000-0000-0000-000000000000'::uuid;
                """);

            migrationBuilder.DropColumn(
                name: "Date",
                schema: "warehouse",
                table: "stocktakes");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "warehouse",
                table: "stocktakes");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "VoucherDate",
                schema: "warehouse",
                table: "stocktakes",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedById",
                schema: "warehouse",
                table: "stocktakes",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByName",
                schema: "warehouse",
                table: "stocktakes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            // ── conversions ──────────────────────────────────────────────────
            migrationBuilder.AddColumn<DateOnly>(
                name: "VoucherDate",
                schema: "warehouse",
                table: "conversions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "warehouse",
                table: "conversions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByName",
                schema: "warehouse",
                table: "conversions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedById",
                schema: "warehouse",
                table: "conversions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByName",
                schema: "warehouse",
                table: "conversions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE warehouse.conversions
                SET "VoucherDate" = TO_DATE("Date", 'DD/MM/YYYY')
                WHERE "Date" ~ '^\d{2}/\d{2}/\d{4}$';

                UPDATE warehouse.conversions
                SET "VoucherDate" = DATE '2026-01-01'
                WHERE "VoucherDate" IS NULL;

                UPDATE warehouse.conversions
                SET "CreatedByName" = "CreatedBy",
                    "CreatedById" = '00000000-0000-0000-0000-000000000000'::uuid,
                    "ApprovedByName" = "ApprovedBy";
                """);

            migrationBuilder.DropColumn(
                name: "Date",
                schema: "warehouse",
                table: "conversions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "warehouse",
                table: "conversions");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                schema: "warehouse",
                table: "conversions");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "VoucherDate",
                schema: "warehouse",
                table: "conversions",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedById",
                schema: "warehouse",
                table: "conversions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByName",
                schema: "warehouse",
                table: "conversions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Date",
                schema: "warehouse",
                table: "adjustments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "warehouse",
                table: "adjustments",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE warehouse.adjustments
                SET "Date" = TO_CHAR("VoucherDate", 'DD/MM/YYYY'),
                    "CreatedBy" = "CreatedByName";
                """);

            migrationBuilder.DropColumn(name: "VoucherDate", schema: "warehouse", table: "adjustments");
            migrationBuilder.DropColumn(name: "CreatedById", schema: "warehouse", table: "adjustments");
            migrationBuilder.DropColumn(name: "CreatedByName", schema: "warehouse", table: "adjustments");

            migrationBuilder.AlterColumn<string>(
                name: "Date",
                schema: "warehouse",
                table: "adjustments",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "warehouse",
                table: "adjustments",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Date",
                schema: "warehouse",
                table: "stocktakes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "warehouse",
                table: "stocktakes",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE warehouse.stocktakes
                SET "Date" = TO_CHAR("VoucherDate", 'DD/MM/YYYY'),
                    "CreatedBy" = "CreatedByName";
                """);

            migrationBuilder.DropColumn(name: "VoucherDate", schema: "warehouse", table: "stocktakes");
            migrationBuilder.DropColumn(name: "CreatedById", schema: "warehouse", table: "stocktakes");
            migrationBuilder.DropColumn(name: "CreatedByName", schema: "warehouse", table: "stocktakes");

            migrationBuilder.AlterColumn<string>(
                name: "Date",
                schema: "warehouse",
                table: "stocktakes",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "warehouse",
                table: "stocktakes",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Date",
                schema: "warehouse",
                table: "conversions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "warehouse",
                table: "conversions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                schema: "warehouse",
                table: "conversions",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE warehouse.conversions
                SET "Date" = TO_CHAR("VoucherDate", 'DD/MM/YYYY'),
                    "CreatedBy" = "CreatedByName",
                    "ApprovedBy" = "ApprovedByName";
                """);

            migrationBuilder.DropColumn(name: "VoucherDate", schema: "warehouse", table: "conversions");
            migrationBuilder.DropColumn(name: "CreatedById", schema: "warehouse", table: "conversions");
            migrationBuilder.DropColumn(name: "CreatedByName", schema: "warehouse", table: "conversions");
            migrationBuilder.DropColumn(name: "ApprovedById", schema: "warehouse", table: "conversions");
            migrationBuilder.DropColumn(name: "ApprovedByName", schema: "warehouse", table: "conversions");

            migrationBuilder.AlterColumn<string>(
                name: "Date",
                schema: "warehouse",
                table: "conversions",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "warehouse",
                table: "conversions",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
