using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResumeMatcher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResumeOptimization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Analyses_OwnerUserId_Id",
                table: "Analyses",
                columns: new[] { "OwnerUserId", "Id" });

            migrationBuilder.CreateTable(
                name: "Optimizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ResumeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalText = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SuggestionsJson = table.Column<string>(type: "text", nullable: false),
                    DecisionsJson = table.Column<string>(type: "text", nullable: true),
                    AdaptedText = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Optimizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Optimizations_Analyses_OwnerUserId_AnalysisId",
                        columns: x => new { x.OwnerUserId, x.AnalysisId },
                        principalTable: "Analyses",
                        principalColumns: new[] { "OwnerUserId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Optimizations_Resumes_OwnerUserId_ResumeId",
                        columns: x => new { x.OwnerUserId, x.ResumeId },
                        principalTable: "Resumes",
                        principalColumns: new[] { "OwnerUserId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Optimizations_OwnerUserId_AnalysisId",
                table: "Optimizations",
                columns: new[] { "OwnerUserId", "AnalysisId" });

            migrationBuilder.CreateIndex(
                name: "IX_Optimizations_OwnerUserId_ResumeId",
                table: "Optimizations",
                columns: new[] { "OwnerUserId", "ResumeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Optimizations_UpdatedAt",
                table: "Optimizations",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Optimizations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Analyses_OwnerUserId_Id",
                table: "Analyses");
        }
    }
}
