using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NamViet.ERP.Modules.Hr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CandidateRecruitmentPlanAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecruitmentPlanId",
                schema: "hr",
                table: "candidates",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidates_RecruitmentPlanId",
                schema: "hr",
                table: "candidates",
                column: "RecruitmentPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_candidates_recruitment_plans_RecruitmentPlanId",
                schema: "hr",
                table: "candidates",
                column: "RecruitmentPlanId",
                principalSchema: "hr",
                principalTable: "recruitment_plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_candidates_recruitment_plans_RecruitmentPlanId",
                schema: "hr",
                table: "candidates");

            migrationBuilder.DropIndex(
                name: "IX_candidates_RecruitmentPlanId",
                schema: "hr",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "RecruitmentPlanId",
                schema: "hr",
                table: "candidates");
        }
    }
}
