using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WDAS.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddDocumentRecipientReviewTracking : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "DocumentRecipients" ADD COLUMN IF NOT EXISTS "ReviewedAtUtc" timestamp with time zone;
            ALTER TABLE "DocumentRecipients" ADD COLUMN IF NOT EXISTS "ReviewComment" text;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ReviewedAtUtc",
            table: "DocumentRecipients");

        migrationBuilder.DropColumn(
            name: "ReviewComment",
            table: "DocumentRecipients");
    }
}
