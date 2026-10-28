using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20261028000000_AddCskhExtendedFields")]
public partial class AddCskhExtendedFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(
            name: "ExpectedAt",
            schema: "warehouse",
            table: "ProjectBottlenecks",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Reason",
            schema: "warehouse",
            table: "ProjectBottlenecks",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "GanttTask",
            schema: "warehouse",
            table: "ProjectBottlenecks",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Resolution",
            schema: "warehouse",
            table: "ProjectBottlenecks",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Source",
            schema: "warehouse",
            table: "ProjectBottlenecks",
            type: "text",
            nullable: false,
            defaultValue: "manual");

        migrationBuilder.AddColumn<string>(
            name: "HistoryJson",
            schema: "warehouse",
            table: "ProjectBottlenecks",
            type: "text",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "ToDept",
            schema: "warehouse",
            table: "ProjectRequests",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Body",
            schema: "warehouse",
            table: "ProjectRequests",
            type: "text",
            nullable: true);

        migrationBuilder.AlterColumn<DateOnly>(
            name: "ExpectedDate",
            schema: "warehouse",
            table: "ProjectRequests",
            type: "date",
            nullable: true,
            oldClrType: typeof(DateOnly),
            oldType: "date");

        migrationBuilder.AddColumn<string>(
            name: "Agenda",
            schema: "warehouse",
            table: "ProjectMeetings",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "AttachmentsJson",
            schema: "warehouse",
            table: "ProjectMeetings",
            type: "text",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<bool>(
            name: "GeneratedBottleneck",
            schema: "warehouse",
            table: "ProjectMeetingActions",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        // Safe cleanup of legacy ProjectCode1 shadow FKs, indexes, and columns
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ProjectBottlenecks_projects_ProjectCode1') THEN
                    ALTER TABLE warehouse."ProjectBottlenecks" DROP CONSTRAINT "FK_ProjectBottlenecks_projects_ProjectCode1";
                END IF;
                IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ProjectDocuments_projects_ProjectCode1') THEN
                    ALTER TABLE warehouse."ProjectDocuments" DROP CONSTRAINT "FK_ProjectDocuments_projects_ProjectCode1";
                END IF;
                IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ProjectMeetings_projects_ProjectCode1') THEN
                    ALTER TABLE warehouse."ProjectMeetings" DROP CONSTRAINT "FK_ProjectMeetings_projects_ProjectCode1";
                END IF;
                IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ProjectRequests_projects_ProjectCode1') THEN
                    ALTER TABLE warehouse."ProjectRequests" DROP CONSTRAINT "FK_ProjectRequests_projects_ProjectCode1";
                END IF;
            END $$;

            DROP INDEX IF EXISTS warehouse."IX_ProjectBottlenecks_ProjectCode1";
            DROP INDEX IF EXISTS warehouse."IX_ProjectDocuments_ProjectCode1";
            DROP INDEX IF EXISTS warehouse."IX_ProjectMeetings_ProjectCode1";
            DROP INDEX IF EXISTS warehouse."IX_ProjectRequests_ProjectCode1";

            ALTER TABLE warehouse."ProjectBottlenecks" DROP COLUMN IF EXISTS "ProjectCode1";
            ALTER TABLE warehouse."ProjectDocuments" DROP COLUMN IF EXISTS "ProjectCode1";
            ALTER TABLE warehouse."ProjectMeetings" DROP COLUMN IF EXISTS "ProjectCode1";
            ALTER TABLE warehouse."ProjectRequests" DROP COLUMN IF EXISTS "ProjectCode1";
        """);

        migrationBuilder.CreateIndex(
            name: "IX_ProjectBottlenecks_ProjectCode",
            schema: "warehouse",
            table: "ProjectBottlenecks",
            column: "ProjectCode");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectDocuments_ProjectCode",
            schema: "warehouse",
            table: "ProjectDocuments",
            column: "ProjectCode");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectMeetings_ProjectCode",
            schema: "warehouse",
            table: "ProjectMeetings",
            column: "ProjectCode");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectRequests_ProjectCode",
            schema: "warehouse",
            table: "ProjectRequests",
            column: "ProjectCode");

        migrationBuilder.AddForeignKey(
            name: "FK_ProjectBottlenecks_projects_ProjectCode",
            schema: "warehouse",
            table: "ProjectBottlenecks",
            column: "ProjectCode",
            principalSchema: "warehouse",
            principalTable: "projects",
            principalColumn: "ProjectCode",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_ProjectDocuments_projects_ProjectCode",
            schema: "warehouse",
            table: "ProjectDocuments",
            column: "ProjectCode",
            principalSchema: "warehouse",
            principalTable: "projects",
            principalColumn: "ProjectCode",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_ProjectMeetings_projects_ProjectCode",
            schema: "warehouse",
            table: "ProjectMeetings",
            column: "ProjectCode",
            principalSchema: "warehouse",
            principalTable: "projects",
            principalColumn: "ProjectCode",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_ProjectRequests_projects_ProjectCode",
            schema: "warehouse",
            table: "ProjectRequests",
            column: "ProjectCode",
            principalSchema: "warehouse",
            principalTable: "projects",
            principalColumn: "ProjectCode",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ProjectBottlenecks_projects_ProjectCode",
            schema: "warehouse",
            table: "ProjectBottlenecks");

        migrationBuilder.DropForeignKey(
            name: "FK_ProjectDocuments_projects_ProjectCode",
            schema: "warehouse",
            table: "ProjectDocuments");

        migrationBuilder.DropForeignKey(
            name: "FK_ProjectMeetings_projects_ProjectCode",
            schema: "warehouse",
            table: "ProjectMeetings");

        migrationBuilder.DropForeignKey(
            name: "FK_ProjectRequests_projects_ProjectCode",
            schema: "warehouse",
            table: "ProjectRequests");

        migrationBuilder.DropIndex(
            name: "IX_ProjectBottlenecks_ProjectCode",
            schema: "warehouse",
            table: "ProjectBottlenecks");

        migrationBuilder.DropIndex(
            name: "IX_ProjectDocuments_ProjectCode",
            schema: "warehouse",
            table: "ProjectDocuments");

        migrationBuilder.DropIndex(
            name: "IX_ProjectMeetings_ProjectCode",
            schema: "warehouse",
            table: "ProjectMeetings");

        migrationBuilder.DropIndex(
            name: "IX_ProjectRequests_ProjectCode",
            schema: "warehouse",
            table: "ProjectRequests");

        migrationBuilder.DropColumn(
            name: "ExpectedAt",
            schema: "warehouse",
            table: "ProjectBottlenecks");

        migrationBuilder.DropColumn(
            name: "Reason",
            schema: "warehouse",
            table: "ProjectBottlenecks");

        migrationBuilder.DropColumn(
            name: "GanttTask",
            schema: "warehouse",
            table: "ProjectBottlenecks");

        migrationBuilder.DropColumn(
            name: "Resolution",
            schema: "warehouse",
            table: "ProjectBottlenecks");

        migrationBuilder.DropColumn(
            name: "Source",
            schema: "warehouse",
            table: "ProjectBottlenecks");

        migrationBuilder.DropColumn(
            name: "HistoryJson",
            schema: "warehouse",
            table: "ProjectBottlenecks");

        migrationBuilder.AlterColumn<DateOnly>(
            name: "ExpectedDate",
            schema: "warehouse",
            table: "ProjectRequests",
            type: "date",
            nullable: false,
            oldClrType: typeof(DateOnly),
            oldType: "date",
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "ToDept",
            schema: "warehouse",
            table: "ProjectRequests");

        migrationBuilder.DropColumn(
            name: "Body",
            schema: "warehouse",
            table: "ProjectRequests");

        migrationBuilder.DropColumn(
            name: "Agenda",
            schema: "warehouse",
            table: "ProjectMeetings");

        migrationBuilder.DropColumn(
            name: "AttachmentsJson",
            schema: "warehouse",
            table: "ProjectMeetings");

        migrationBuilder.DropColumn(
            name: "GeneratedBottleneck",
            schema: "warehouse",
            table: "ProjectMeetingActions");
    }
}
