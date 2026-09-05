using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResumeMatcher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisResumeForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_Analyses_Resumes_ResumeId",
                table: "Analyses",
                column: "ResumeId",
                principalTable: "Resumes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Analyses_Resumes_ResumeId",
                table: "Analyses");
        }
    }
}
