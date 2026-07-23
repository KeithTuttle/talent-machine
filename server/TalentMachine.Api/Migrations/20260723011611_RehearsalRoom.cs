using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TalentMachine.Api.Migrations
{
    /// <inheritdoc />
    public partial class RehearsalRoom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Room",
                table: "Rehearsals",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Room",
                table: "Rehearsals");
        }
    }
}
