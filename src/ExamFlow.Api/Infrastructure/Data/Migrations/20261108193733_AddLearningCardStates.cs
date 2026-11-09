using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningCardStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearningCardStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LearningDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Box = table.Column<int>(type: "integer", nullable: false),
                    Reviews = table.Column<int>(type: "integer", nullable: false),
                    Lapses = table.Column<int>(type: "integer", nullable: false),
                    LastGrade = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningCardStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningCardStates_LearningDocuments_LearningDocumentId",
                        column: x => x.LearningDocumentId,
                        principalTable: "LearningDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningCardStates_LearningDocumentId",
                table: "LearningCardStates",
                column: "LearningDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningCardStates_UserId_LearningDocumentId_QuestionHash",
                table: "LearningCardStates",
                columns: new[] { "UserId", "LearningDocumentId", "QuestionHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningCardStates");
        }
    }
}
