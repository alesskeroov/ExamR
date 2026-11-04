using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionExplanations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExplanationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Total = table.Column<int>(type: "int", nullable: false),
                    Done = table.Column<int>(type: "int", nullable: false),
                    Failed = table.Column<int>(type: "int", nullable: false),
                    EstimatedUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    SpentUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Provider = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    Requests = table.Column<int>(type: "int", nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    FailedQuestionIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastError = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExplanationJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExplanationJobs_UploadedDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "UploadedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuestionExplanations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    SourceQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Explanation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    QuestionType = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    ReviewReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Model = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    CostUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionExplanations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionExplanations_Questions_SourceQuestionId",
                        column: x => x.SourceQuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExplanationJobs_CreatedAt",
                table: "ExplanationJobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExplanationJobs_DocumentId_Active",
                table: "ExplanationJobs",
                column: "DocumentId",
                unique: true,
                filter: "[Status] IN ('Pending', 'Running')");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionExplanations_Fingerprint",
                table: "QuestionExplanations",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionExplanations_SourceQuestionId",
                table: "QuestionExplanations",
                column: "SourceQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionExplanations_Status",
                table: "QuestionExplanations",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExplanationJobs");

            migrationBuilder.DropTable(
                name: "QuestionExplanations");
        }
    }
}
