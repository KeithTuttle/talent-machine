using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TalentMachine.Api.Migrations
{
    /// <inheritdoc />
    public partial class Round2CostumesFormationsStaff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ShowDate",
                table: "Productions",
                newName: "OpeningDate");

            migrationBuilder.RenameColumn(
                name: "Songwriter",
                table: "Numbers",
                newName: "TeachStatus");

            migrationBuilder.AddColumn<int>(
                name: "ChoreographerStaffId",
                table: "Numbers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CostumeLabel",
                table: "Numbers",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Invitations",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailSentAt",
                table: "Invitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Invitations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CostumeAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    MusicalNumberId = table.Column<int>(type: "integer", nullable: false),
                    PerformerId = table.Column<int>(type: "integer", nullable: false),
                    Size = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostumeAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostumeAssignments_Numbers_MusicalNumberId",
                        column: x => x.MusicalNumberId,
                        principalTable: "Numbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CostumeAssignments_Performers_PerformerId",
                        column: x => x.PerformerId,
                        principalTable: "Performers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CostumePieces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    MusicalNumberId = table.Column<int>(type: "integer", nullable: false),
                    Gender = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Accessories = table.Column<string>(type: "text", nullable: true),
                    Shoes = table.Column<string>(type: "text", nullable: true),
                    PhotoUrl = table.Column<string>(type: "text", nullable: true),
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

            migrationBuilder.CreateTable(
                name: "Formations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    MusicalNumberId = table.Column<int>(type: "integer", nullable: false),
                    FormationName = table.Column<string>(type: "text", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    Coordinates = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Formations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Formations_Numbers_MusicalNumberId",
                        column: x => x.MusicalNumberId,
                        principalTable: "Numbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffMembers",
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
                    table.PrimaryKey("PK_StaffMembers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionStaff",
                columns: table => new
                {
                    ProductionId = table.Column<int>(type: "integer", nullable: false),
                    StaffMemberId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionStaff", x => new { x.ProductionId, x.StaffMemberId, x.Role });
                    table.ForeignKey(
                        name: "FK_ProductionStaff_Productions_ProductionId",
                        column: x => x.ProductionId,
                        principalTable: "Productions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionStaff_StaffMembers_StaffMemberId",
                        column: x => x.StaffMemberId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Numbers_ChoreographerStaffId",
                table: "Numbers",
                column: "ChoreographerStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_CostumeAssignments_MusicalNumberId_PerformerId",
                table: "CostumeAssignments",
                columns: new[] { "MusicalNumberId", "PerformerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostumeAssignments_PerformerId",
                table: "CostumeAssignments",
                column: "PerformerId");

            migrationBuilder.CreateIndex(
                name: "IX_CostumeAssignments_TenantId",
                table: "CostumeAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CostumePieces_MusicalNumberId",
                table: "CostumePieces",
                column: "MusicalNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_CostumePieces_TenantId",
                table: "CostumePieces",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Formations_MusicalNumberId",
                table: "Formations",
                column: "MusicalNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_Formations_TenantId",
                table: "Formations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionStaff_StaffMemberId",
                table: "ProductionStaff",
                column: "StaffMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionStaff_TenantId",
                table: "ProductionStaff",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_TenantId",
                table: "StaffMembers",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Numbers_StaffMembers_ChoreographerStaffId",
                table: "Numbers",
                column: "ChoreographerStaffId",
                principalTable: "StaffMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Numbers_StaffMembers_ChoreographerStaffId",
                table: "Numbers");

            migrationBuilder.DropTable(
                name: "CostumeAssignments");

            migrationBuilder.DropTable(
                name: "CostumePieces");

            migrationBuilder.DropTable(
                name: "Formations");

            migrationBuilder.DropTable(
                name: "ProductionStaff");

            migrationBuilder.DropTable(
                name: "StaffMembers");

            migrationBuilder.DropIndex(
                name: "IX_Numbers_ChoreographerStaffId",
                table: "Numbers");

            migrationBuilder.DropColumn(
                name: "ChoreographerStaffId",
                table: "Numbers");

            migrationBuilder.DropColumn(
                name: "CostumeLabel",
                table: "Numbers");

            migrationBuilder.DropColumn(
                name: "EmailSentAt",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Invitations");

            migrationBuilder.RenameColumn(
                name: "OpeningDate",
                table: "Productions",
                newName: "ShowDate");

            migrationBuilder.RenameColumn(
                name: "TeachStatus",
                table: "Numbers",
                newName: "Songwriter");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Invitations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
