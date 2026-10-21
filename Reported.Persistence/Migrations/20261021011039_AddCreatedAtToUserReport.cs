using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reported.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedAtToUserReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "UserReport",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UserReport");
        }
    }
}
