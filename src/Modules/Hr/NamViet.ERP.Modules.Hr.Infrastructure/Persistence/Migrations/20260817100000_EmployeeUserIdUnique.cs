using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Hr.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HrDbContext))]
[Migration("20260817100000_EmployeeUserIdUnique")]
public partial class EmployeeUserIdUnique : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_employees_UserId",
            schema: "hr",
            table: "employees",
            column: "UserId",
            unique: true,
            filter: "\"UserId\" IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_employees_UserId",
            schema: "hr",
            table: "employees");
    }
}
