using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TalentMachine.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGuardiansAndRehearsals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Guardians",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guardians", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rehearsals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ProductionId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    MusicalNumberId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rehearsals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rehearsals_Numbers_MusicalNumberId",
                        column: x => x.MusicalNumberId,
                        principalTable: "Numbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Rehearsals_Productions_ProductionId",
                        column: x => x.ProductionId,
                        principalTable: "Productions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PerformerGuardians",
                columns: table => new
                {
                    PerformerId = table.Column<int>(type: "integer", nullable: false),
                    GuardianId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformerGuardians", x => new { x.PerformerId, x.GuardianId });
                    table.ForeignKey(
                        name: "FK_PerformerGuardians_Guardians_GuardianId",
                        column: x => x.GuardianId,
                        principalTable: "Guardians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PerformerGuardians_Performers_PerformerId",
                        column: x => x.PerformerId,
                        principalTable: "Performers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RehearsalAttendees",
                columns: table => new
                {
                    RehearsalId = table.Column<int>(type: "integer", nullable: false),
                    PerformerId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    IsExcluded = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RehearsalAttendees", x => new { x.RehearsalId, x.PerformerId });
                    table.ForeignKey(
                        name: "FK_RehearsalAttendees_Performers_PerformerId",
                        column: x => x.PerformerId,
                        principalTable: "Performers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RehearsalAttendees_Rehearsals_RehearsalId",
                        column: x => x.RehearsalId,
                        principalTable: "Rehearsals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Guardians_TenantId",
                table: "Guardians",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformerGuardians_GuardianId",
                table: "PerformerGuardians",
                column: "GuardianId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformerGuardians_TenantId",
                table: "PerformerGuardians",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RehearsalAttendees_PerformerId",
                table: "RehearsalAttendees",
                column: "PerformerId");

            migrationBuilder.CreateIndex(
                name: "IX_RehearsalAttendees_TenantId",
                table: "RehearsalAttendees",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Rehearsals_MusicalNumberId",
                table: "Rehearsals",
                column: "MusicalNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_Rehearsals_ProductionId_Date",
                table: "Rehearsals",
                columns: new[] { "ProductionId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Rehearsals_TenantId",
                table: "Rehearsals",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PerformerGuardians");

            migrationBuilder.DropTable(
                name: "RehearsalAttendees");

            migrationBuilder.DropTable(
                name: "Guardians");

            migrationBuilder.DropTable(
                name: "Rehearsals");
        }
    }
}
