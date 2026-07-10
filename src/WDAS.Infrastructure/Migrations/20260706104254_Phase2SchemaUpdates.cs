using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WDAS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2SchemaUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAtUtc",
                table: "Notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutoReplyMessage",
                table: "Delegations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotificationPreferencesJson",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutOfOfficeMessage",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "en");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReadAtUtc", table: "Notifications");
            migrationBuilder.DropColumn(name: "AutoReplyMessage", table: "Delegations");
            migrationBuilder.DropColumn(name: "NotificationPreferencesJson", table: "Users");
            migrationBuilder.DropColumn(name: "OutOfOfficeMessage", table: "Users");
            migrationBuilder.DropColumn(name: "PreferredLanguage", table: "Users");
        }
    }
}
