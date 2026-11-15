using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDimAudio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DimAudioPlays",
                columns: table => new
                {
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    AudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DimAudioPlays", x => new { x.ExamId, x.AudioId });
                    table.ForeignKey(
                        name: "FK_DimAudioPlays_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DimAudios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    QuestionFrom = table.Column<int>(type: "integer", nullable: false),
                    QuestionTo = table.Column<int>(type: "integer", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    MaxPlays = table.Column<int>(type: "integer", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DimAudios", x => x.Id);
                    table.CheckConstraint("CK_DimAudios_Range", "\"QuestionFrom\" >= 1 AND \"QuestionTo\" >= \"QuestionFrom\"");
                    table.ForeignKey(
                        name: "FK_DimAudios_UploadedDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "UploadedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DimAudios_DocumentId",
                table: "DimAudios",
                column: "DocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DimAudioPlays");

            migrationBuilder.DropTable(
                name: "DimAudios");
        }
    }
}
