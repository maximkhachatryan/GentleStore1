using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GentleStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreTheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Theme",
                table: "Stores",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Theme",
                table: "Stores");
        }
    }
}
