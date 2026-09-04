using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResumeMatcher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgreSqlWithAnalysisCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Analyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisInputHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    JobDescription = table.Column<string>(type: "text", nullable: false),
                    OverallScore = table.Column<double>(type: "double precision", nullable: false),
                    SkillsScore = table.Column<double>(type: "double precision", nullable: false),
                    ExperienceScore = table.Column<double>(type: "double precision", nullable: false),
                    SeniorityScore = table.Column<double>(type: "double precision", nullable: false),
                    RequirementsScore = table.Column<double>(type: "double precision", nullable: false),
                    EducationScore = table.Column<double>(type: "double precision", nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Analyses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Resumes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExtractedText = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resumes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Analyses_AnalysisInputHash",
                table: "Analyses",
                column: "AnalysisInputHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Analyses_ResumeId",
                table: "Analyses",
                column: "ResumeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Analyses");

            migrationBuilder.DropTable(
                name: "Resumes");
        }
    }
}
