using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionPassages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestionPassages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromNumber = table.Column<int>(type: "integer", nullable: false),
                    ToNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Text = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionPassages", x => x.Id);
                    table.CheckConstraint("CK_QuestionPassages_Range", "\"FromNumber\" >= 1 AND \"ToNumber\" >= \"FromNumber\"");
                    table.ForeignKey(
                        name: "FK_QuestionPassages_UploadedDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "UploadedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionPassages_DocumentId_FromNumber",
                table: "QuestionPassages",
                columns: new[] { "DocumentId", "FromNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionPassages");
        }
    }
}
