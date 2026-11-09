using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearningCardOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LearningDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Front = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Back = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Hidden = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningCardOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningCardOverrides_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearningCardOverrides_LearningDocuments_LearningDocumentId",
                        column: x => x.LearningDocumentId,
                        principalTable: "LearningDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningUserCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LearningDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Front = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Back = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningUserCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningUserCards_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearningUserCards_LearningDocuments_LearningDocumentId",
                        column: x => x.LearningDocumentId,
                        principalTable: "LearningDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningCardOverrides_LearningDocumentId",
                table: "LearningCardOverrides",
                column: "LearningDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningCardOverrides_UserId_LearningDocumentId_CardId",
                table: "LearningCardOverrides",
                columns: new[] { "UserId", "LearningDocumentId", "CardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningUserCards_LearningDocumentId",
                table: "LearningUserCards",
                column: "LearningDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningUserCards_UserId_LearningDocumentId",
                table: "LearningUserCards",
                columns: new[] { "UserId", "LearningDocumentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningCardOverrides");

            migrationBuilder.DropTable(
                name: "LearningUserCards");
        }
    }
}
