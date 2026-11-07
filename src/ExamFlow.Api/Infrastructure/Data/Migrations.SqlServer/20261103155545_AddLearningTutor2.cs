using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningTutor2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearningQuizItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearningDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReferenceAnswer = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    KeyPointsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ChunkOrdinal = table.Column<int>(type: "int", nullable: false),
                    PageFrom = table.Column<int>(type: "int", nullable: true),
                    PageTo = table.Column<int>(type: "int", nullable: true),
                    UserAnswer = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Feedback = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MissingPointsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AiChecked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningQuizItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningQuizItems_LearningDocuments_LearningDocumentId",
                        column: x => x.LearningDocumentId,
                        principalTable: "LearningDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearningDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentSha256 = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Length = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    Minutes = table.Column<int>(type: "int", nullable: true),
                    QuestionNumbersJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CacheKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PromptVersion = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Saved = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningMaterials_LearningDocuments_LearningDocumentId",
                        column: x => x.LearningDocumentId,
                        principalTable: "LearningDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningQuizItems_LearningDocumentId",
                table: "LearningQuizItems",
                column: "LearningDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningQuizItems_UserId_LearningDocumentId_CreatedAt",
                table: "LearningQuizItems",
                columns: new[] { "UserId", "LearningDocumentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningMaterials_CacheKey",
                table: "LearningMaterials",
                column: "CacheKey");

            migrationBuilder.CreateIndex(
                name: "IX_LearningMaterials_LearningDocumentId",
                table: "LearningMaterials",
                column: "LearningDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningMaterials_UserId_LearningDocumentId_CreatedAt",
                table: "LearningMaterials",
                columns: new[] { "UserId", "LearningDocumentId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningQuizItems");

            migrationBuilder.DropTable(
                name: "LearningMaterials");
        }
    }
}
