using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reported.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscordName",
                table: "UserReport",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InitiatedDiscordName",
                table: "UserReport",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscordName",
                table: "UserReport");

            migrationBuilder.DropColumn(
                name: "InitiatedDiscordName",
                table: "UserReport");
        }
    }
}
