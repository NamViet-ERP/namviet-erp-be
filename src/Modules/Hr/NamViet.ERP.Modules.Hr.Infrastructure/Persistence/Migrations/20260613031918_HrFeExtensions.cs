using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NamViet.ERP.Modules.Hr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HrFeExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankAccountName",
                schema: "hr",
                table: "employees",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankBranch",
                schema: "hr",
                table: "employees",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Branch",
                schema: "hr",
                table: "employees",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CccdBackUrl",
                schema: "hr",
                table: "employees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CmndIssueDate",
                schema: "hr",
                table: "employees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CmndIssuePlace",
                schema: "hr",
                table: "employees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CmndNumber",
                schema: "hr",
                table: "employees",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DegreePhotoUrl",
                schema: "hr",
                table: "employees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HadSocialInsurance",
                schema: "hr",
                table: "employees",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Major",
                schema: "hr",
                table: "employees",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaritalStatus",
                schema: "hr",
                table: "employees",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OfficialDate",
                schema: "hr",
                table: "employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ResignDate",
                schema: "hr",
                table: "employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "School",
                schema: "hr",
                table: "employees",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Segment",
                schema: "hr",
                table: "employees",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkflowData",
                schema: "hr",
                table: "candidates",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankAccountName",
                schema: "hr",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "BankBranch",
                schema: "hr",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "Branch",
                schema: "hr",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "CccdBackUrl",
                schema: "hr",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "CmndIssueDate",
                schema: "hr",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "CmndIssuePlace",
                schema: "hr",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "CmndNumber",
                schema: "hr",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "DegreePhotoUrl",
                schema: "hr",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "HadSocialInsurance",
                schema: "hr",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "Major",
                schema: "hr",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "MaritalStatus",
                schema: "hr",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "OfficialDate",
                schema: "hr",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "ResignDate",
                schema: "hr",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "School",
                schema: "hr",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "Segment",
                schema: "hr",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "WorkflowData",
                schema: "hr",
                table: "candidates");
        }
    }
}
