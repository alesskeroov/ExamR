using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDimTests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DimBankQuestions",
                columns: table => new
                {
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Track = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false),
                    Subject = table.Column<string>(type: "character varying(10)", unicode: false, maxLength: 10, nullable: false),
                    Topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DimBankQuestions", x => x.QuestionId);
                    table.ForeignKey(
                        name: "FK_DimBankQuestions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DimTests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Track = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false),
                    GroupCode = table.Column<string>(type: "character varying(10)", unicode: false, maxLength: 10, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DurationMinutes = table.Column<short>(type: "smallint", nullable: false),
                    AttemptLimit = table.Column<short>(type: "smallint", nullable: true),
                    AvailableFrom = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    AvailableTo = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    Sections = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DimTests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DimAttempts",
                columns: table => new
                {
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    Items = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DimAttempts", x => x.ExamId);
                    table.ForeignKey(
                        name: "FK_DimAttempts_DimTests_TestId",
                        column: x => x.TestId,
                        principalTable: "DimTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DimAttempts_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DimAttempts_TestId_UserId",
                table: "DimAttempts",
                columns: new[] { "TestId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_DimBankQuestions_Track_Subject",
                table: "DimBankQuestions",
                columns: new[] { "Track", "Subject" });

            migrationBuilder.CreateIndex(
                name: "IX_DimTests_Track_IsPublished",
                table: "DimTests",
                columns: new[] { "Track", "IsPublished" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DimAttempts");

            migrationBuilder.DropTable(
                name: "DimBankQuestions");

            migrationBuilder.DropTable(
                name: "DimTests");
        }
    }
}
