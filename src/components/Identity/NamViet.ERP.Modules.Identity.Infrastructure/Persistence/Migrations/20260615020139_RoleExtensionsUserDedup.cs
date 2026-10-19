using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NamViet.ERP.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoleExtensionsUserDedup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Department",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "JoinedDate",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Phone",
                schema: "identity",
                table: "users");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "identity",
                table: "roles",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Module",
                schema: "identity",
                table: "roles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "General");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                schema: "identity",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "Module",
                schema: "identity",
                table: "roles");

            migrationBuilder.AddColumn<string>(
                name: "Department",
                schema: "identity",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "JoinedDate",
                schema: "identity",
                table: "users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                schema: "identity",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
