using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBattleResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BattleResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BattleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomCode = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RangeFrom = table.Column<int>(type: "int", nullable: false),
                    RangeTo = table.Column<int>(type: "int", nullable: false),
                    QuestionCount = table.Column<int>(type: "int", nullable: false),
                    ParticipantCount = table.Column<int>(type: "int", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    CorrectCount = table.Column<int>(type: "int", nullable: false),
                    PlayedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BattleResults_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BattleResults_BattleId_UserId",
                table: "BattleResults",
                columns: new[] { "BattleId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BattleResults_UserId_PlayedAt",
                table: "BattleResults",
                columns: new[] { "UserId", "PlayedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BattleResults");
        }
    }
}
