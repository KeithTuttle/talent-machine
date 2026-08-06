using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TalentMachine.Api.Migrations
{
    /// <inheritdoc />
    public partial class CostumeLooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "CostumePieces",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CostumePieceId",
                table: "CostumeAssignments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostumeAssignments_CostumePieceId",
                table: "CostumeAssignments",
                column: "CostumePieceId");

            migrationBuilder.AddForeignKey(
                name: "FK_CostumeAssignments_CostumePieces_CostumePieceId",
                table: "CostumeAssignments",
                column: "CostumePieceId",
                principalTable: "CostumePieces",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CostumeAssignments_CostumePieces_CostumePieceId",
                table: "CostumeAssignments");

            migrationBuilder.DropIndex(
                name: "IX_CostumeAssignments_CostumePieceId",
                table: "CostumeAssignments");

            migrationBuilder.DropColumn(
                name: "Label",
                table: "CostumePieces");

            migrationBuilder.DropColumn(
                name: "CostumePieceId",
                table: "CostumeAssignments");
        }
    }
}
