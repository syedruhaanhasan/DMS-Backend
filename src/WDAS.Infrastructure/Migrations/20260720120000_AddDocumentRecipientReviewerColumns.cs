using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WDAS.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddDocumentRecipientReviewerColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "DocumentRecipients" ADD COLUMN IF NOT EXISTS "ReviewerUserId" integer;
            ALTER TABLE "DocumentRecipients" ADD COLUMN IF NOT EXISTS "AddedByUserId" integer;
            CREATE INDEX IF NOT EXISTS "IX_DocumentRecipients_ReviewerUserId" ON "DocumentRecipients" ("ReviewerUserId");
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "IX_DocumentRecipients_ReviewerUserId";
            """);

        migrationBuilder.DropColumn(
            name: "ReviewerUserId",
            table: "DocumentRecipients");

        migrationBuilder.DropColumn(
            name: "AddedByUserId",
            table: "DocumentRecipients");
    }
}
