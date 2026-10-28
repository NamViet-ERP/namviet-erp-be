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
        migrationBuilder.Sql("""
            ALTER TABLE warehouse."ProjectBottlenecks" ADD COLUMN IF NOT EXISTS "ExpectedAt" date;
            ALTER TABLE warehouse."ProjectBottlenecks" ADD COLUMN IF NOT EXISTS "Reason" text NOT NULL DEFAULT '';
            ALTER TABLE warehouse."ProjectBottlenecks" ADD COLUMN IF NOT EXISTS "GanttTask" text;
            ALTER TABLE warehouse."ProjectBottlenecks" ADD COLUMN IF NOT EXISTS "Resolution" text;
            ALTER TABLE warehouse."ProjectBottlenecks" ADD COLUMN IF NOT EXISTS "Source" text NOT NULL DEFAULT 'manual';
            ALTER TABLE warehouse."ProjectBottlenecks" ADD COLUMN IF NOT EXISTS "HistoryJson" text NOT NULL DEFAULT '[]';

            ALTER TABLE warehouse."ProjectRequests" ADD COLUMN IF NOT EXISTS "ToDept" text;
            ALTER TABLE warehouse."ProjectRequests" ADD COLUMN IF NOT EXISTS "Body" text;
            ALTER TABLE warehouse."ProjectRequests" ALTER COLUMN "ExpectedDate" DROP NOT NULL;

            ALTER TABLE warehouse."ProjectMeetings" ADD COLUMN IF NOT EXISTS "Agenda" text NOT NULL DEFAULT '';
            ALTER TABLE warehouse."ProjectMeetings" ADD COLUMN IF NOT EXISTS "AttachmentsJson" text NOT NULL DEFAULT '[]';

            ALTER TABLE warehouse."ProjectMeetingActions" ADD COLUMN IF NOT EXISTS "GeneratedBottleneck" boolean NOT NULL DEFAULT FALSE;

            -- Safe cleanup of legacy ProjectCode1 shadow FKs, indexes, and columns
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

            CREATE INDEX IF NOT EXISTS "IX_ProjectBottlenecks_ProjectCode" ON warehouse."ProjectBottlenecks" ("ProjectCode");
            CREATE INDEX IF NOT EXISTS "IX_ProjectDocuments_ProjectCode" ON warehouse."ProjectDocuments" ("ProjectCode");
            CREATE INDEX IF NOT EXISTS "IX_ProjectMeetings_ProjectCode" ON warehouse."ProjectMeetings" ("ProjectCode");
            CREATE INDEX IF NOT EXISTS "IX_ProjectRequests_ProjectCode" ON warehouse."ProjectRequests" ("ProjectCode");

            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ProjectBottlenecks_projects_ProjectCode') THEN
                    ALTER TABLE warehouse."ProjectBottlenecks" ADD CONSTRAINT "FK_ProjectBottlenecks_projects_ProjectCode"
                        FOREIGN KEY ("ProjectCode") REFERENCES warehouse.projects ("ProjectCode") ON DELETE RESTRICT;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ProjectDocuments_projects_ProjectCode') THEN
                    ALTER TABLE warehouse."ProjectDocuments" ADD CONSTRAINT "FK_ProjectDocuments_projects_ProjectCode"
                        FOREIGN KEY ("ProjectCode") REFERENCES warehouse.projects ("ProjectCode") ON DELETE RESTRICT;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ProjectMeetings_projects_ProjectCode') THEN
                    ALTER TABLE warehouse."ProjectMeetings" ADD CONSTRAINT "FK_ProjectMeetings_projects_ProjectCode"
                        FOREIGN KEY ("ProjectCode") REFERENCES warehouse.projects ("ProjectCode") ON DELETE RESTRICT;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ProjectRequests_projects_ProjectCode') THEN
                    ALTER TABLE warehouse."ProjectRequests" ADD CONSTRAINT "FK_ProjectRequests_projects_ProjectCode"
                        FOREIGN KEY ("ProjectCode") REFERENCES warehouse.projects ("ProjectCode") ON DELETE RESTRICT;
                END IF;
            END $$;
        """);
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
