using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TalentMachine.Api.Migrations
{
    /// <summary>
    /// Retires the number-level free-text costume label now that costumes are real
    /// catalog entries. The label was a second, parallel way to say what a number
    /// wears — it drove the grid colors and the quick-change fallback while the
    /// catalog drove everything else.
    ///
    /// The scaffolded version simply dropped the column. This one first turns any
    /// label still carrying information into a catalog costume linked to its number,
    /// so nothing a user typed is lost. Numbers that already have costumes linked
    /// keep those — the catalog is the truth there, and the label was redundant.
    /// </summary>
    public partial class RetireCostumeLabel : Migration
    {
        /// <summary>Numbers with a real label and no costume linked yet.</summary>
        private const string Unconverted = @"
            n.""CostumeLabel"" IS NOT NULL AND btrim(n.""CostumeLabel"") <> ''
            AND NOT EXISTS (SELECT 1 FROM ""CostumeNumbers"" cn WHERE cn.""MusicalNumberId"" = n.""Id"")";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One catalog entry per distinct label in a production, skipping names
            // the catalog already has (those get reused by the link step below).
            migrationBuilder.Sql($@"
                INSERT INTO ""Costumes"" (""TenantId"",""ProductionId"",""Name"",""Status"",""OrderIndex"")
                SELECT DISTINCT ON (n.""ProductionId"", lower(btrim(n.""CostumeLabel"")))
                       n.""TenantId"", n.""ProductionId"", btrim(n.""CostumeLabel""), 'Needed', 0
                FROM ""Numbers"" n
                WHERE {Unconverted}
                  AND NOT EXISTS (
                      SELECT 1 FROM ""Costumes"" c
                      WHERE c.""ProductionId"" = n.""ProductionId""
                        AND lower(btrim(c.""Name"")) = lower(btrim(n.""CostumeLabel"")))
                ORDER BY n.""ProductionId"", lower(btrim(n.""CostumeLabel"")), n.""Id"";");

            // Link each such number to the entry matching its label.
            migrationBuilder.Sql($@"
                INSERT INTO ""CostumeNumbers"" (""TenantId"",""CostumeId"",""MusicalNumberId"")
                SELECT DISTINCT n.""TenantId"", c.""Id"", n.""Id""
                FROM ""Numbers"" n
                JOIN ""Costumes"" c
                  ON c.""ProductionId"" = n.""ProductionId""
                 AND lower(btrim(c.""Name"")) = lower(btrim(n.""CostumeLabel""))
                WHERE {Unconverted};");

            migrationBuilder.DropColumn(name: "CostumeLabel", table: "Numbers");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CostumeLabel",
                table: "Numbers",
                type: "text",
                nullable: true);

            // Best-effort restore: a number wearing exactly one costume gets its name
            // back as the label. Numbers wearing several can't be expressed as one
            // label, so they come back empty.
            migrationBuilder.Sql(@"
                UPDATE ""Numbers"" n
                SET ""CostumeLabel"" = c.""Name""
                FROM ""CostumeNumbers"" cn
                JOIN ""Costumes"" c ON c.""Id"" = cn.""CostumeId""
                WHERE cn.""MusicalNumberId"" = n.""Id""
                  AND (SELECT count(*) FROM ""CostumeNumbers"" x WHERE x.""MusicalNumberId"" = n.""Id"") = 1;");
        }
    }
}
