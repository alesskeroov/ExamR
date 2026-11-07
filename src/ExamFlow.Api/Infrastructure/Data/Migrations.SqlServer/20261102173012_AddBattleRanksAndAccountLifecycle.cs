using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBattleRanksAndAccountLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CorrectTimeMs",
                table: "BattleResults",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "RatingAfter",
                table: "BattleResults",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RatingBefore",
                table: "BattleResults",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RatingOutcome",
                table: "BattleResults",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "BattleRatings",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentRating = table.Column<int>(type: "int", nullable: false),
                    HighestRating = table.Column<int>(type: "int", nullable: false),
                    TotalRankedBattles = table.Column<int>(type: "int", nullable: false),
                    Wins = table.Column<int>(type: "int", nullable: false),
                    Losses = table.Column<int>(type: "int", nullable: false),
                    Draws = table.Column<int>(type: "int", nullable: false),
                    LastRatingUpdate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleRatings", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_BattleRatings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BattleRatings_CurrentRating_Wins",
                table: "BattleRatings",
                columns: new[] { "CurrentRating", "Wins" },
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BattleRatings");

            migrationBuilder.DropColumn(
                name: "CorrectTimeMs",
                table: "BattleResults");

            migrationBuilder.DropColumn(
                name: "RatingAfter",
                table: "BattleResults");

            migrationBuilder.DropColumn(
                name: "RatingBefore",
                table: "BattleResults");

            migrationBuilder.DropColumn(
                name: "RatingOutcome",
                table: "BattleResults");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AspNetUsers");
        }
    }
}
