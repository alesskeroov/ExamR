using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionVisuals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestionVisuals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OptionPosition = table.Column<byte>(type: "tinyint", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    PageNumber = table.Column<int>(type: "int", nullable: false),
                    Left = table.Column<double>(type: "float", nullable: false),
                    Bottom = table.Column<double>(type: "float", nullable: false),
                    Width = table.Column<double>(type: "float", nullable: false),
                    Height = table.Column<double>(type: "float", nullable: false),
                    MappingStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StorageKey = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: true),
                    ContentType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ByteSize = table.Column<int>(type: "int", nullable: false),
                    PixelWidth = table.Column<int>(type: "int", nullable: false),
                    PixelHeight = table.Column<int>(type: "int", nullable: false),
                    Sha256 = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionVisuals", x => x.Id);
                    table.CheckConstraint("CK_QuestionVisuals_OptionPosition", "[OptionPosition] IS NULL OR [OptionPosition] BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_QuestionVisuals_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QuestionVisuals_UploadedDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "UploadedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionVisuals_DocumentId_SortOrder",
                table: "QuestionVisuals",
                columns: new[] { "DocumentId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionVisuals_QuestionId",
                table: "QuestionVisuals",
                column: "QuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionVisuals");
        }
    }
}
