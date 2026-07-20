using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WDAS.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddDocumentRecipientReturnWorkflowStep : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "DocumentRecipients" ADD COLUMN IF NOT EXISTS "ReturnWorkflowStepId" integer;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ReturnWorkflowStepId",
            table: "DocumentRecipients");
    }
}
