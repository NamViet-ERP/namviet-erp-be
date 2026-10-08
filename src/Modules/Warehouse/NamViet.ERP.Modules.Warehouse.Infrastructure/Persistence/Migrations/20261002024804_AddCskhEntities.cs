using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCskhEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectBottlenecks",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    ImpactLevel = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Department = table.Column<string>(type: "text", nullable: false),
                    Assignee = table.Column<string>(type: "text", nullable: false),
                    ReportedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DelayedDays = table.Column<int>(type: "integer", nullable: false),
                    ProjectCode1 = table.Column<string>(type: "character varying(50)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectBottlenecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectBottlenecks_projects_ProjectCode1",
                        column: x => x.ProjectCode1,
                        principalSchema: "warehouse",
                        principalTable: "projects",
                        principalColumn: "ProjectCode");
                });

            migrationBuilder.CreateTable(
                name: "ProjectDocuments",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DocType = table.Column<string>(type: "text", nullable: false),
                    Stage = table.Column<string>(type: "text", nullable: false),
                    Author = table.Column<string>(type: "text", nullable: false),
                    InternalApproved = table.Column<bool>(type: "boolean", nullable: false),
                    HandoverSigned = table.Column<bool>(type: "boolean", nullable: false),
                    HandoverDate = table.Column<DateOnly>(type: "date", nullable: true),
                    HandoverRecipient = table.Column<string>(type: "text", nullable: true),
                    SlaDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OverdueDays = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StatusLabel = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    DownloadUrl = table.Column<string>(type: "text", nullable: false),
                    ProjectCode1 = table.Column<string>(type: "character varying(50)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectDocuments_projects_ProjectCode1",
                        column: x => x.ProjectCode1,
                        principalSchema: "warehouse",
                        principalTable: "projects",
                        principalColumn: "ProjectCode");
                });

            migrationBuilder.CreateTable(
                name: "ProjectMeetings",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    Organizer = table.Column<string>(type: "text", nullable: false),
                    MeetingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    MinutesHtml = table.Column<string>(type: "text", nullable: false),
                    ProjectCode1 = table.Column<string>(type: "character varying(50)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMeetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectMeetings_projects_ProjectCode1",
                        column: x => x.ProjectCode1,
                        principalSchema: "warehouse",
                        principalTable: "projects",
                        principalColumn: "ProjectCode");
                });

            migrationBuilder.CreateTable(
                name: "ProjectRequests",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Requester = table.Column<string>(type: "text", nullable: false),
                    RequestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ProjectCode1 = table.Column<string>(type: "character varying(50)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectRequests_projects_ProjectCode1",
                        column: x => x.ProjectCode1,
                        principalSchema: "warehouse",
                        principalTable: "projects",
                        principalColumn: "ProjectCode");
                });

            migrationBuilder.CreateTable(
                name: "ProjectMeetingActions",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Assignee = table.Column<string>(type: "text", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsDone = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMeetingActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectMeetingActions_ProjectMeetings_MeetingId",
                        column: x => x.MeetingId,
                        principalSchema: "warehouse",
                        principalTable: "ProjectMeetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBottlenecks_ProjectCode1",
                schema: "warehouse",
                table: "ProjectBottlenecks",
                column: "ProjectCode1");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDocuments_ProjectCode1",
                schema: "warehouse",
                table: "ProjectDocuments",
                column: "ProjectCode1");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMeetingActions_MeetingId",
                schema: "warehouse",
                table: "ProjectMeetingActions",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMeetings_ProjectCode1",
                schema: "warehouse",
                table: "ProjectMeetings",
                column: "ProjectCode1");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRequests_ProjectCode1",
                schema: "warehouse",
                table: "ProjectRequests",
                column: "ProjectCode1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectBottlenecks",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "ProjectDocuments",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "ProjectMeetingActions",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "ProjectRequests",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "ProjectMeetings",
                schema: "warehouse");
        }
    }
}
