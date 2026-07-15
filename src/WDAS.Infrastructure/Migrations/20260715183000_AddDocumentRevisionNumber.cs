using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WDAS.Infrastructure.Persistence;

#nullable disable

namespace WDAS.Infrastructure.Migrations;

[DbContext(typeof(WdasDbContext))]
[Migration("20260715183000_AddDocumentRevisionNumber")]
public partial class AddDocumentRevisionNumber : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "RevisionNumber",
            table: "Documents",
            type: "integer",
            nullable: false,
            defaultValue: 1);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RevisionNumber",
            table: "Documents");
    }
}
