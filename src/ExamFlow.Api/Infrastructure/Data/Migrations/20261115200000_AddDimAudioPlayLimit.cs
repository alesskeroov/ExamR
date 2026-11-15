using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDimAudioPlayLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS: an earlier version of AddDimAudio (in a hand-run script) already created both, under that
            // migration's id. This one must then only record itself instead of failing.
            migrationBuilder.Sql("""ALTER TABLE "DimAudios" ADD COLUMN IF NOT EXISTS "MaxPlays" integer;""");
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "DimAudioPlays" (
                    "ExamId" uuid NOT NULL,
                    "AudioId" uuid NOT NULL,
                    "Count" integer NOT NULL,
                    CONSTRAINT "PK_DimAudioPlays" PRIMARY KEY ("ExamId", "AudioId"),
                    CONSTRAINT "FK_DimAudioPlays_Exams_ExamId" FOREIGN KEY ("ExamId") REFERENCES "Exams" ("Id") ON DELETE CASCADE
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DimAudioPlays");

            migrationBuilder.DropColumn(
                name: "MaxPlays",
                table: "DimAudios");
        }
    }
}
