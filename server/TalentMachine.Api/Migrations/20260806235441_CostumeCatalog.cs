using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TalentMachine.Api.Migrations
{
    /// <summary>
    /// Moves costumes from per-number "pieces" to a production-level catalog reused
    /// across numbers.
    ///
    /// The scaffolded version dropped CostumePieces BEFORE creating Costumes, which
    /// would have thrown every existing costume away and left CostumeAssignments
    /// pointing at ids that no longer meant anything. This version creates the new
    /// tables first, carries the data over, and only then drops the old table.
    ///
    /// Existing pieces are consolidated BY NAME within a production: five numbers
    /// wearing "Orphan rags" collapse into one catalog entry linked to all five.
    /// </summary>
    public partial class CostumeCatalog : Migration
    {
        /// <summary>Name a piece consolidates under: its label, else its description, else "Costume".</summary>
        private const string PieceName =
            @"COALESCE(NULLIF(btrim(p.""Label""), ''), NULLIF(btrim(p.""Description""), ''), 'Costume')";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- 1. New tables ------------------------------------------------
            migrationBuilder.CreateTable(
                name: "Costumes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ProductionId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Accessories = table.Column<string>(type: "text", nullable: true),
                    Shoes = table.Column<string>(type: "text", nullable: true),
                    PhotoUrl = table.Column<string>(type: "text", nullable: true),
                    VendorUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Needed"),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Costumes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CostumeNumbers",
                columns: table => new
                {
                    CostumeId = table.Column<int>(type: "integer", nullable: false),
                    MusicalNumberId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostumeNumbers", x => new { x.CostumeId, x.MusicalNumberId });
                    table.ForeignKey(
                        name: "FK_CostumeNumbers_Costumes_CostumeId",
                        column: x => x.CostumeId,
                        principalTable: "Costumes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CostumeNumbers_Numbers_MusicalNumberId",
                        column: x => x.MusicalNumberId,
                        principalTable: "Numbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CostumeNumbers_MusicalNumberId",
                table: "CostumeNumbers",
                column: "MusicalNumberId");
            migrationBuilder.CreateIndex(
                name: "IX_CostumeNumbers_TenantId",
                table: "CostumeNumbers",
                column: "TenantId");
            migrationBuilder.CreateIndex(
                name: "IX_Costumes_ProductionId_OrderIndex",
                table: "Costumes",
                columns: new[] { "ProductionId", "OrderIndex" });
            migrationBuilder.CreateIndex(
                name: "IX_Costumes_TenantId",
                table: "Costumes",
                column: "TenantId");

            // --- 2. Free the old FK so assignments can be repointed ------------
            migrationBuilder.DropForeignKey(
                name: "FK_CostumeAssignments_CostumePieces_CostumePieceId",
                table: "CostumeAssignments");

            // --- 3. Carry the data over ---------------------------------------
            // One catalog entry per (production, name). DISTINCT ON keeps the
            // lowest-id piece's details as the winner.
            migrationBuilder.Sql($@"
                INSERT INTO ""Costumes""
                    (""TenantId"",""ProductionId"",""Name"",""Description"",""Accessories"",
                     ""Shoes"",""PhotoUrl"",""VendorUrl"",""Status"",""OrderIndex"")
                SELECT DISTINCT ON (n.""ProductionId"", lower({PieceName}))
                       p.""TenantId"", n.""ProductionId"", {PieceName},
                       p.""Description"", p.""Accessories"", p.""Shoes"",
                       p.""PhotoUrl"", p.""VendorUrl"", p.""Status"", 0
                FROM ""CostumePieces"" p
                JOIN ""Numbers"" n ON n.""Id"" = p.""MusicalNumberId""
                ORDER BY n.""ProductionId"", lower({PieceName}), p.""Id"";");

            // Link every number that used a piece to the entry it consolidated into.
            migrationBuilder.Sql($@"
                INSERT INTO ""CostumeNumbers"" (""TenantId"",""CostumeId"",""MusicalNumberId"")
                SELECT DISTINCT p.""TenantId"", c.""Id"", p.""MusicalNumberId""
                FROM ""CostumePieces"" p
                JOIN ""Numbers"" n ON n.""Id"" = p.""MusicalNumberId""
                JOIN ""Costumes"" c
                  ON c.""ProductionId"" = n.""ProductionId""
                 AND lower(btrim(c.""Name"")) = lower({PieceName});");

            // Repoint per-kid assignments from the old piece id to the new entry.
            migrationBuilder.Sql($@"
                UPDATE ""CostumeAssignments"" a
                SET ""CostumePieceId"" = m.""NewId""
                FROM (
                    SELECT p.""Id"" AS ""OldId"", c.""Id"" AS ""NewId""
                    FROM ""CostumePieces"" p
                    JOIN ""Numbers"" n ON n.""Id"" = p.""MusicalNumberId""
                    JOIN ""Costumes"" c
                      ON c.""ProductionId"" = n.""ProductionId""
                     AND lower(btrim(c.""Name"")) = lower({PieceName})
                ) m
                WHERE a.""CostumePieceId"" = m.""OldId"";");

            // --- 4. Retire the old shape --------------------------------------
            migrationBuilder.DropTable(name: "CostumePieces");

            migrationBuilder.RenameColumn(
                name: "CostumePieceId",
                table: "CostumeAssignments",
                newName: "CostumeId");
            migrationBuilder.RenameIndex(
                name: "IX_CostumeAssignments_CostumePieceId",
                table: "CostumeAssignments",
                newName: "IX_CostumeAssignments_CostumeId");

            migrationBuilder.AddForeignKey(
                name: "FK_CostumeAssignments_Costumes_CostumeId",
                table: "CostumeAssignments",
                column: "CostumeId",
                principalTable: "Costumes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CostumeAssignments_Costumes_CostumeId",
                table: "CostumeAssignments");

            // Catalog ids mean nothing to the old per-number table — clear them so
            // the restored foreign key can't be violated. (Down loses costume data.)
            migrationBuilder.Sql(@"UPDATE ""CostumeAssignments"" SET ""CostumeId"" = NULL;");

            migrationBuilder.DropTable(name: "CostumeNumbers");
            migrationBuilder.DropTable(name: "Costumes");

            migrationBuilder.RenameColumn(
                name: "CostumeId",
                table: "CostumeAssignments",
                newName: "CostumePieceId");
            migrationBuilder.RenameIndex(
                name: "IX_CostumeAssignments_CostumeId",
                table: "CostumeAssignments",
                newName: "IX_CostumeAssignments_CostumePieceId");

            migrationBuilder.CreateTable(
                name: "CostumePieces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MusicalNumberId = table.Column<int>(type: "integer", nullable: false),
                    Accessories = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Gender = table.Column<string>(type: "text", nullable: false, defaultValue: "All"),
                    Label = table.Column<string>(type: "text", nullable: true),
                    PhotoUrl = table.Column<string>(type: "text", nullable: true),
                    Shoes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Needed"),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    VendorUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostumePieces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostumePieces_Numbers_MusicalNumberId",
                        column: x => x.MusicalNumberId,
                        principalTable: "Numbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CostumePieces_MusicalNumberId",
                table: "CostumePieces",
                column: "MusicalNumberId");
            migrationBuilder.CreateIndex(
                name: "IX_CostumePieces_TenantId",
                table: "CostumePieces",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_CostumeAssignments_CostumePieces_CostumePieceId",
                table: "CostumeAssignments",
                column: "CostumePieceId",
                principalTable: "CostumePieces",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
