using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reported.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppealRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppealRecord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DiscordId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    DiscordName = table.Column<string>(type: "TEXT", nullable: false),
                    AppealWins = table.Column<int>(type: "INTEGER", nullable: false),
                    AppealAttempts = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealRecord", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppealRecord_DiscordId",
                table: "AppealRecord",
                column: "DiscordId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppealRecord");
        }
    }
}
