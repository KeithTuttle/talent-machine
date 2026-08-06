using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TalentMachine.Api.Migrations
{
    /// <inheritdoc />
    public partial class CostumeReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Status is stored as the enum NAME, so existing rows must land on a real
            // member — EF's generated "" default would fail to parse on read.
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "CostumePieces",
                type: "text",
                nullable: false,
                defaultValue: "Needed");

            migrationBuilder.AddColumn<bool>(
                name: "IsFitted",
                table: "CostumeAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "CostumePieces");

            migrationBuilder.DropColumn(
                name: "IsFitted",
                table: "CostumeAssignments");
        }
    }
}
