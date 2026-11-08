using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MarkDimSourceDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDimSource",
                table: "UploadedDocuments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Documents that already feed the DİM bank are DİM sources.
            migrationBuilder.Sql("""
                UPDATE "UploadedDocuments" SET "IsDimSource" = TRUE
                WHERE "Id" IN (SELECT q."DocumentId" FROM "DimBankQuestions" b JOIN "Questions" q ON q."Id" = b."QuestionId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDimSource",
                table: "UploadedDocuments");
        }
    }
}
