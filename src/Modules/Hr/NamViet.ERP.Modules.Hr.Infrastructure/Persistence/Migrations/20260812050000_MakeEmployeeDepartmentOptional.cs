using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NamViet.ERP.Modules.Hr.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class MakeEmployeeDepartmentOptional : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_employees_departments_DepartmentId",
            schema: "hr",
            table: "employees");

        migrationBuilder.AlterColumn<int>(
            name: "DepartmentId",
            schema: "hr",
            table: "employees",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AddForeignKey(
            name: "FK_employees_departments_DepartmentId",
            schema: "hr",
            table: "employees",
            column: "DepartmentId",
            principalSchema: "hr",
            principalTable: "departments",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE hr.employees
            SET "DepartmentId" = (
                SELECT d."Id" FROM hr.departments d
                WHERE d."DeletedAt" IS NULL
                ORDER BY d."Id"
                LIMIT 1
            )
            WHERE "DepartmentId" IS NULL;
            """);

        migrationBuilder.AlterColumn<int>(
            name: "DepartmentId",
            schema: "hr",
            table: "employees",
            type: "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);
    }
}
