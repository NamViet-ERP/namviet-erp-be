using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NamViet.ERP.Modules.Hr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HrEnumAndAssigneeFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Assignee",
                schema: "hr",
                table: "social_insurance_tasks");

            migrationBuilder.AddColumn<int>(
                name: "AssigneeId",
                schema: "hr",
                table: "social_insurance_tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "hr",
                table: "rewards",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "hr",
                table: "admin_requests",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_social_insurance_tasks_AssigneeId",
                schema: "hr",
                table: "social_insurance_tasks",
                column: "AssigneeId");

            migrationBuilder.AddForeignKey(
                name: "FK_social_insurance_tasks_employees_AssigneeId",
                schema: "hr",
                table: "social_insurance_tasks",
                column: "AssigneeId",
                principalSchema: "hr",
                principalTable: "employees",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_social_insurance_tasks_employees_AssigneeId",
                schema: "hr",
                table: "social_insurance_tasks");

            migrationBuilder.DropIndex(
                name: "IX_social_insurance_tasks_AssigneeId",
                schema: "hr",
                table: "social_insurance_tasks");

            migrationBuilder.DropColumn(
                name: "AssigneeId",
                schema: "hr",
                table: "social_insurance_tasks");

            migrationBuilder.AddColumn<string>(
                name: "Assignee",
                schema: "hr",
                table: "social_insurance_tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "hr",
                table: "rewards",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "hr",
                table: "admin_requests",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);
        }
    }
}
