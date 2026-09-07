using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResumeMatcher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDataOwnershipAndRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Analyses_Resumes_ResumeId",
                table: "Analyses");

            migrationBuilder.DropIndex(
                name: "IX_Analyses_AnalysisInputHash",
                table: "Analyses");

            migrationBuilder.DropIndex(
                name: "IX_Analyses_ResumeId",
                table: "Analyses");

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "Resumes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Resumes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "Analyses",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Analyses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Preserve legacy age; never assign old records to whoever logs in first.
            migrationBuilder.Sql("""
                UPDATE "Resumes" r SET "UpdatedAt" = GREATEST(r."CreatedAt",
                    COALESCE((SELECT MAX(a."CreatedAt") FROM "Analyses" a WHERE a."ResumeId" = r."Id"), r."CreatedAt"));
                """);
            migrationBuilder.Sql("UPDATE \"Analyses\" SET \"UpdatedAt\" = \"CreatedAt\";");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Resumes_OwnerUserId_Id",
                table: "Resumes",
                columns: new[] { "OwnerUserId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Resumes_UpdatedAt",
                table: "Resumes",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Analyses_OwnerUserId_AnalysisInputHash",
                table: "Analyses",
                columns: new[] { "OwnerUserId", "AnalysisInputHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Analyses_OwnerUserId_ResumeId",
                table: "Analyses",
                columns: new[] { "OwnerUserId", "ResumeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Analyses_UpdatedAt",
                table: "Analyses",
                column: "UpdatedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_Analyses_Resumes_OwnerUserId_ResumeId",
                table: "Analyses",
                columns: new[] { "OwnerUserId", "ResumeId" },
                principalTable: "Resumes",
                principalColumns: new[] { "OwnerUserId", "Id" },
                onDelete: ReferentialAction.Cascade);
            // Allows a reviewed legacy-owner backfill to update both sides in one transaction.
            migrationBuilder.Sql("ALTER TABLE \"Analyses\" ALTER CONSTRAINT \"FK_Analyses_Resumes_OwnerUserId_ResumeId\" DEFERRABLE INITIALLY IMMEDIATE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverting to a single-user schema would remove the ownership boundary.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Resumes" WHERE "OwnerUserId" <> '')
                       OR EXISTS (SELECT 1 FROM "Analyses" WHERE "OwnerUserId" <> '') THEN
                        RAISE EXCEPTION 'Cannot remove ownership while assigned records exist. Use a reviewed recovery plan.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Analyses_Resumes_OwnerUserId_ResumeId",
                table: "Analyses");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Resumes_OwnerUserId_Id",
                table: "Resumes");

            migrationBuilder.DropIndex(
                name: "IX_Resumes_UpdatedAt",
                table: "Resumes");

            migrationBuilder.DropIndex(
                name: "IX_Analyses_OwnerUserId_AnalysisInputHash",
                table: "Analyses");

            migrationBuilder.DropIndex(
                name: "IX_Analyses_OwnerUserId_ResumeId",
                table: "Analyses");

            migrationBuilder.DropIndex(
                name: "IX_Analyses_UpdatedAt",
                table: "Analyses");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Resumes");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Resumes");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Analyses");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Analyses");

            migrationBuilder.CreateIndex(
                name: "IX_Analyses_AnalysisInputHash",
                table: "Analyses",
                column: "AnalysisInputHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Analyses_ResumeId",
                table: "Analyses",
                column: "ResumeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Analyses_Resumes_ResumeId",
                table: "Analyses",
                column: "ResumeId",
                principalTable: "Resumes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
