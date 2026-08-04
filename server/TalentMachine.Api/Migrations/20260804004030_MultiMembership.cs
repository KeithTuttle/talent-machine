using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TalentMachine.Api.Migrations
{
    /// <inheritdoc />
    public partial class MultiMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Memberships_ClerkUserId",
                table: "Memberships");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_ClerkUserId",
                table: "Memberships",
                column: "ClerkUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_ClerkUserId_TenantId",
                table: "Memberships",
                columns: new[] { "ClerkUserId", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Memberships_ClerkUserId",
                table: "Memberships");

            migrationBuilder.DropIndex(
                name: "IX_Memberships_ClerkUserId_TenantId",
                table: "Memberships");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_ClerkUserId",
                table: "Memberships",
                column: "ClerkUserId",
                unique: true);
        }
    }
}
