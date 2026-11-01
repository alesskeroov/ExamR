using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartStudy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Explanation",
                table: "Questions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StudyAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    OptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrectIndex = table.Column<int>(type: "int", nullable: true),
                    Answer = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    Score = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyAttempts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudyAttempts_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudyAttempts_QuestionId",
                table: "StudyAttempts",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyAttempts_UserId_QuestionId_CompletedAt",
                table: "StudyAttempts",
                columns: new[] { "UserId", "QuestionId", "CompletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudyAttempts");

            migrationBuilder.DropColumn(
                name: "Explanation",
                table: "Questions");
        }
    }
}
