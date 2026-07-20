using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TalentMachine.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLevelGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CastMemberships_LevelGroups_LevelGroupId",
                table: "CastMemberships");

            migrationBuilder.DropTable(
                name: "LevelGroups");

            migrationBuilder.DropIndex(
                name: "IX_CastMemberships_LevelGroupId",
                table: "CastMemberships");

            migrationBuilder.DropColumn(
                name: "LevelGroupId",
                table: "CastMemberships");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LevelGroupId",
                table: "CastMemberships",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LevelGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Color = table.Column<string>(type: "text", nullable: true),
                    Level = table.Column<string>(type: "text", nullable: true),
                    MaxAge = table.Column<int>(type: "integer", nullable: true),
                    MinAge = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    ProductionId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LevelGroups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CastMemberships_LevelGroupId",
                table: "CastMemberships",
                column: "LevelGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_LevelGroups_TenantId",
                table: "LevelGroups",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_CastMemberships_LevelGroups_LevelGroupId",
                table: "CastMemberships",
                column: "LevelGroupId",
                principalTable: "LevelGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
