using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlansAndSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MonthlyDimTests",
                table: "UserUsageLimits",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BillingSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    WhatsAppNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    InstagramUsername = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PaymentInstructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingSettings", x => x.Id);
                    table.CheckConstraint("CK_BillingSettings_Single", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    IsFree = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    MonthlyUploads = table.Column<int>(type: "integer", nullable: true),
                    MonthlyExams = table.Column<int>(type: "integer", nullable: true),
                    MonthlyBattles = table.Column<int>(type: "integer", nullable: true),
                    MonthlyDimTests = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                    table.CheckConstraint("CK_Plans_DurationDays", "\"DurationDays\" BETWEEN 1 AND 365");
                    table.CheckConstraint("CK_Plans_MonthlyBattles", "\"MonthlyBattles\" IS NULL OR \"MonthlyBattles\" BETWEEN 0 AND 100000");
                    table.CheckConstraint("CK_Plans_MonthlyDimTests", "\"MonthlyDimTests\" IS NULL OR \"MonthlyDimTests\" BETWEEN 0 AND 100000");
                    table.CheckConstraint("CK_Plans_MonthlyExams", "\"MonthlyExams\" IS NULL OR \"MonthlyExams\" BETWEEN 0 AND 100000");
                    table.CheckConstraint("CK_Plans_MonthlyUploads", "\"MonthlyUploads\" IS NULL OR \"MonthlyUploads\" BETWEEN 0 AND 100000");
                    table.CheckConstraint("CK_Plans_Price", "\"Price\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "PaymentRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Periods = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AdminNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRequests", x => x.Id);
                    table.CheckConstraint("CK_PaymentRequests_Periods", "\"Periods\" BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_PaymentRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentRequests_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.CheckConstraint("CK_Subscriptions_Period", "\"EndsAt\" > \"StartsAt\"");
                    table.ForeignKey(
                        name: "FK_Subscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Subscriptions_PaymentRequests_PaymentRequestId",
                        column: x => x.PaymentRequestId,
                        principalTable: "PaymentRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "BillingSettings",
                columns: new[] { "Id", "Enabled", "InstagramUsername", "PaymentInstructions", "UpdatedAt", "UpdatedByUserId", "WhatsAppNumber" },
                values: new object[] { 1, false, null, null, new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null });

            migrationBuilder.InsertData(
                table: "Plans",
                columns: new[] { "Id", "Description", "DurationDays", "IsActive", "IsFeatured", "IsFree", "MonthlyBattles", "MonthlyDimTests", "MonthlyExams", "MonthlyUploads", "Name", "Price", "SortOrder", "UpdatedAt", "UpdatedByUserId" },
                values: new object[,]
                {
                    { new Guid("6a1f0c3e-0000-4000-8000-000000000001"), "Saytı sınamaq üçün", 30, true, false, true, 0, 0, 3, 1, "Free", 0m, 0, new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("6a1f0c3e-0000-4000-8000-000000000002"), "İmtahandan əvvəlki 7 gün üçün", 7, true, false, false, 5, 5, 30, 3, "İmtahan həftəsi", 3m, 1, new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("6a1f0c3e-0000-4000-8000-000000000003"), "Müntəzəm hazırlıq üçün", 30, true, false, false, 10, 10, 60, 10, "Standart", 5m, 2, new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("6a1f0c3e-0000-4000-8000-000000000004"), "Ən çox seçilən paket", 30, true, true, false, 30, 30, 150, 25, "Pro", 7m, 3, new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("6a1f0c3e-0000-4000-8000-000000000005"), "Limitsiz istifadə", 30, true, false, false, null, null, null, null, "Premium", 10m, 4, new DateTimeOffset(new DateTime(2026, 11, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null }
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserUsageLimits_MonthlyDimTests",
                table: "UserUsageLimits",
                sql: "\"MonthlyDimTests\" IS NULL OR \"MonthlyDimTests\" BETWEEN 0 AND 100000");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_Code",
                table: "PaymentRequests",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_PlanId",
                table: "PaymentRequests",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_Status_CreatedAt",
                table: "PaymentRequests",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_UserId",
                table: "PaymentRequests",
                column: "UserId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_IsFree",
                table: "Plans",
                column: "IsFree",
                unique: true,
                filter: "\"IsFree\"");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_PaymentRequestId",
                table: "Subscriptions",
                column: "PaymentRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_PlanId",
                table: "Subscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UserId_EndsAt",
                table: "Subscriptions",
                columns: new[] { "UserId", "EndsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillingSettings");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "PaymentRequests");

            migrationBuilder.DropTable(
                name: "Plans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserUsageLimits_MonthlyDimTests",
                table: "UserUsageLimits");

            migrationBuilder.DropColumn(
                name: "MonthlyDimTests",
                table: "UserUsageLimits");
        }
    }
}
