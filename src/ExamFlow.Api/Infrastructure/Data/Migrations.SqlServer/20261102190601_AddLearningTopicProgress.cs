using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningTopicProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearningTopicProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearningDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TopicKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CurrentStep = table.Column<int>(type: "int", nullable: false),
                    StepCount = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    Correct = table.Column<int>(type: "int", nullable: false),
                    Partial = table.Column<int>(type: "int", nullable: false),
                    AwaitingAnswer = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningTopicProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningTopicProgress_LearningDocuments_LearningDocumentId",
                        column: x => x.LearningDocumentId,
                        principalTable: "LearningDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningTopicProgress_LearningDocumentId",
                table: "LearningTopicProgress",
                column: "LearningDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningTopicProgress_UserId_LearningDocumentId_TopicKey",
                table: "LearningTopicProgress",
                columns: new[] { "UserId", "LearningDocumentId", "TopicKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningTopicProgress");
        }
    }
}
