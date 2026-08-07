using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TalentMachine.Api.Migrations
{
    /// <summary>
    /// Moves size, alteration notes and fitting off the per-number assignment and
    /// onto a per-(costume, performer) row.
    ///
    /// A fitting is a real-world event that happens once: fit a kid in the street
    /// wear and they're fitted for it everywhere it's worn. Storing it per number
    /// meant a kid in twelve numbers counted as twelve outstanding fittings, which
    /// pushed one real production's total past 400.
    ///
    /// The scaffolded version dropped the three columns before creating the new
    /// table, throwing every size and tick away. This one creates the table, carries
    /// the data across, and only then drops the columns. Assignments with no explicit
    /// costume are resolved through the number they belong to when that number wears
    /// exactly one costume — which is how most ticks were recorded, since the per-kid
    /// picker stays hidden there.
    /// </summary>
    public partial class FittingPerCostume : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CostumeFittings",
                columns: table => new
                {
                    CostumeId = table.Column<int>(type: "integer", nullable: false),
                    PerformerId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Size = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    IsFitted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostumeFittings", x => new { x.CostumeId, x.PerformerId });
                    table.ForeignKey(
                        name: "FK_CostumeFittings_Costumes_CostumeId",
                        column: x => x.CostumeId,
                        principalTable: "Costumes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CostumeFittings_Performers_PerformerId",
                        column: x => x.PerformerId,
                        principalTable: "Performers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CostumeFittings_PerformerId",
                table: "CostumeFittings",
                column: "PerformerId");
            migrationBuilder.CreateIndex(
                name: "IX_CostumeFittings_TenantId",
                table: "CostumeFittings",
                column: "TenantId");

            // Collapse the per-number rows into one per (costume, performer): keep the
            // first non-empty size and note, and treat fitted-anywhere as fitted.
            migrationBuilder.Sql(@"
                WITH single_costume AS (
                    SELECT ""MusicalNumberId"" AS nid, MIN(""CostumeId"") AS cid
                    FROM ""CostumeNumbers""
                    GROUP BY ""MusicalNumberId""
                    HAVING count(*) = 1
                ),
                resolved AS (
                    SELECT COALESCE(a.""CostumeId"", sc.cid) AS cid,
                           a.""PerformerId""                 AS pid,
                           NULLIF(btrim(a.""Size""), '')      AS size,
                           NULLIF(btrim(a.""Notes""), '')     AS notes,
                           a.""IsFitted""                     AS fitted
                    FROM ""CostumeAssignments"" a
                    LEFT JOIN single_costume sc ON sc.nid = a.""MusicalNumberId""
                    WHERE COALESCE(a.""CostumeId"", sc.cid) IS NOT NULL
                )
                INSERT INTO ""CostumeFittings""
                    (""TenantId"",""CostumeId"",""PerformerId"",""Size"",""Notes"",""IsFitted"")
                SELECT c.""TenantId"", r.cid, r.pid,
                       (array_agg(r.size)  FILTER (WHERE r.size  IS NOT NULL))[1],
                       (array_agg(r.notes) FILTER (WHERE r.notes IS NOT NULL))[1],
                       bool_or(r.fitted)
                FROM resolved r
                JOIN ""Costumes"" c ON c.""Id"" = r.cid
                GROUP BY c.""TenantId"", r.cid, r.pid;");

            migrationBuilder.DropColumn(name: "IsFitted", table: "CostumeAssignments");
            migrationBuilder.DropColumn(name: "Notes", table: "CostumeAssignments");
            migrationBuilder.DropColumn(name: "Size", table: "CostumeAssignments");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFitted", table: "CostumeAssignments",
                type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(
                name: "Notes", table: "CostumeAssignments", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "Size", table: "CostumeAssignments", type: "text", nullable: true);

            // Fan the per-costume fitting back out to every number that wears it.
            migrationBuilder.Sql(@"
                WITH single_costume AS (
                    SELECT ""MusicalNumberId"" AS nid, MIN(""CostumeId"") AS cid
                    FROM ""CostumeNumbers""
                    GROUP BY ""MusicalNumberId""
                    HAVING count(*) = 1
                )
                UPDATE ""CostumeAssignments"" a
                SET ""Size"" = f.""Size"", ""Notes"" = f.""Notes"", ""IsFitted"" = f.""IsFitted""
                FROM ""CostumeFittings"" f
                LEFT JOIN single_costume sc ON TRUE
                WHERE f.""PerformerId"" = a.""PerformerId""
                  AND f.""CostumeId"" = COALESCE(
                        a.""CostumeId"",
                        (SELECT cid FROM single_costume s WHERE s.nid = a.""MusicalNumberId""));");

            migrationBuilder.DropTable(name: "CostumeFittings");
        }
    }
}
