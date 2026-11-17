using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SetDefaultInstagramAccount : Migration
    {
        // Only fills an empty value so an account an admin already set is kept.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "BillingSettings" SET "InstagramUsername" = 'examr_az'
                WHERE "Id" = 1 AND "InstagramUsername" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "BillingSettings" SET "InstagramUsername" = NULL
                WHERE "Id" = 1 AND "InstagramUsername" = 'examr_az';
                """);
        }
    }
}
