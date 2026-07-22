using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TalentMachine.Api.Migrations
{
    /// <inheritdoc />
    public partial class ScenesAndCharacterLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SceneId",
                table: "Numbers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NumberCharacters",
                columns: table => new
                {
                    MusicalNumberId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberCharacters", x => new { x.MusicalNumberId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_NumberCharacters_Numbers_MusicalNumberId",
                        column: x => x.MusicalNumberId,
                        principalTable: "Numbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NumberCharacters_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Scenes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ProductionId = table.Column<int>(type: "integer", nullable: false),
                    ActId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Setting = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scenes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scenes_Acts_ActId",
                        column: x => x.ActId,
                        principalTable: "Acts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SceneCharacters",
                columns: table => new
                {
                    SceneId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneCharacters", x => new { x.SceneId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_SceneCharacters_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SceneCharacters_Scenes_SceneId",
                        column: x => x.SceneId,
                        principalTable: "Scenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Numbers_SceneId",
                table: "Numbers",
                column: "SceneId");

            migrationBuilder.CreateIndex(
                name: "IX_NumberCharacters_RoleId",
                table: "NumberCharacters",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_NumberCharacters_TenantId",
                table: "NumberCharacters",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SceneCharacters_RoleId",
                table: "SceneCharacters",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_SceneCharacters_TenantId",
                table: "SceneCharacters",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Scenes_ActId",
                table: "Scenes",
                column: "ActId");

            migrationBuilder.CreateIndex(
                name: "IX_Scenes_ProductionId_OrderIndex",
                table: "Scenes",
                columns: new[] { "ProductionId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_Scenes_TenantId",
                table: "Scenes",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Numbers_Scenes_SceneId",
                table: "Numbers",
                column: "SceneId",
                principalTable: "Scenes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Numbers_Scenes_SceneId",
                table: "Numbers");

            migrationBuilder.DropTable(
                name: "NumberCharacters");

            migrationBuilder.DropTable(
                name: "SceneCharacters");

            migrationBuilder.DropTable(
                name: "Scenes");

            migrationBuilder.DropIndex(
                name: "IX_Numbers_SceneId",
                table: "Numbers");

            migrationBuilder.DropColumn(
                name: "SceneId",
                table: "Numbers");
        }
    }
}
