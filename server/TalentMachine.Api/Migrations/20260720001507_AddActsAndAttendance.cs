using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TalentMachine.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddActsAndAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActId",
                table: "Numbers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Acts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ProductionId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RehearsalAttendances",
                columns: table => new
                {
                    RehearsalId = table.Column<int>(type: "integer", nullable: false),
                    PerformerId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RehearsalAttendances", x => new { x.RehearsalId, x.PerformerId });
                    table.ForeignKey(
                        name: "FK_RehearsalAttendances_Performers_PerformerId",
                        column: x => x.PerformerId,
                        principalTable: "Performers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RehearsalAttendances_Rehearsals_RehearsalId",
                        column: x => x.RehearsalId,
                        principalTable: "Rehearsals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Numbers_ActId",
                table: "Numbers",
                column: "ActId");

            migrationBuilder.CreateIndex(
                name: "IX_Acts_TenantId",
                table: "Acts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RehearsalAttendances_PerformerId",
                table: "RehearsalAttendances",
                column: "PerformerId");

            migrationBuilder.CreateIndex(
                name: "IX_RehearsalAttendances_TenantId",
                table: "RehearsalAttendances",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Numbers_Acts_ActId",
                table: "Numbers",
                column: "ActId",
                principalTable: "Acts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Numbers_Acts_ActId",
                table: "Numbers");

            migrationBuilder.DropTable(
                name: "Acts");

            migrationBuilder.DropTable(
                name: "RehearsalAttendances");

            migrationBuilder.DropIndex(
                name: "IX_Numbers_ActId",
                table: "Numbers");

            migrationBuilder.DropColumn(
                name: "ActId",
                table: "Numbers");
        }
    }
}
