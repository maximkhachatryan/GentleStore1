using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GentleStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreCustomDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomDomain",
                table: "Stores",
                type: "character varying(253)",
                maxLength: 253,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stores_CustomDomain",
                table: "Stores",
                column: "CustomDomain",
                unique: true,
                filter: "\"CustomDomain\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Stores_CustomDomain",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "CustomDomain",
                table: "Stores");
        }
    }
}
