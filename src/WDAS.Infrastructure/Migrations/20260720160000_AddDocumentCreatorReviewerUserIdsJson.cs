using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WDAS.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddDocumentCreatorReviewerUserIdsJson : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Documents" ADD COLUMN IF NOT EXISTS "CreatorReviewerUserIdsJson" text;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CreatorReviewerUserIdsJson",
            table: "Documents");
    }
}
