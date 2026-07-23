using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TalentMachine.Api.Migrations
{
    /// <inheritdoc />
    public partial class Props : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Props",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ProductionId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    StorageLocation = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Props", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PropAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    PropId = table.Column<int>(type: "integer", nullable: false),
                    SceneId = table.Column<int>(type: "integer", nullable: false),
                    PresetLocation = table.Column<string>(type: "text", nullable: true),
                    Handler = table.Column<string>(type: "text", nullable: true),
                    StrikeTo = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropAssignments_Props_PropId",
                        column: x => x.PropId,
                        principalTable: "Props",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PropAssignments_Scenes_SceneId",
                        column: x => x.SceneId,
                        principalTable: "Scenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropAssignments_PropId",
                table: "PropAssignments",
                column: "PropId");

            migrationBuilder.CreateIndex(
                name: "IX_PropAssignments_SceneId",
                table: "PropAssignments",
                column: "SceneId");

            migrationBuilder.CreateIndex(
                name: "IX_PropAssignments_TenantId",
                table: "PropAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Props_ProductionId_OrderIndex",
                table: "Props",
                columns: new[] { "ProductionId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_Props_TenantId",
                table: "Props",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropAssignments");

            migrationBuilder.DropTable(
                name: "Props");
        }
    }
}
