using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reported.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBirthdayToUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BirthdayMonth",
                table: "UserPreferences",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BirthdayDay",
                table: "UserPreferences",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BirthdayMonth",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "BirthdayDay",
                table: "UserPreferences");
        }
    }
}
