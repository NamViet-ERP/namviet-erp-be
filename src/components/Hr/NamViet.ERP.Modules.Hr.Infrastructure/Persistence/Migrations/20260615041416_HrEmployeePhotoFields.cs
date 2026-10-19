using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NamViet.ERP.Modules.Hr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HrEmployeePhotoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CccdFrontUrl",
                schema: "hr",
                table: "employees",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CccdFrontUrl",
                schema: "hr",
                table: "employees");
        }
    }
}
