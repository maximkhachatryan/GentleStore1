using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GentleStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductVariants_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VariantAttributeDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VariantAttributeDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VariantAttributeDefinitions_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VariantAttributeOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantAttributeDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VariantAttributeOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VariantAttributeOptions_VariantAttributeDefinitions_Variant~",
                        column: x => x.VariantAttributeDefinitionId,
                        principalTable: "VariantAttributeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VariantAttributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantAttributeDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    VariantAttributeOptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VariantAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VariantAttributes_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VariantAttributes_VariantAttributeDefinitions_VariantAttrib~",
                        column: x => x.VariantAttributeDefinitionId,
                        principalTable: "VariantAttributeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VariantAttributes_VariantAttributeOptions_VariantAttributeO~",
                        column: x => x.VariantAttributeOptionId,
                        principalTable: "VariantAttributeOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_DisplayOrder",
                table: "ProductVariants",
                columns: new[] { "ProductId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_VariantAttributeDefinitions_StoreId_Name",
                table: "VariantAttributeDefinitions",
                columns: new[] { "StoreId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VariantAttributeOptions_VariantAttributeDefinitionId_Value",
                table: "VariantAttributeOptions",
                columns: new[] { "VariantAttributeDefinitionId", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VariantAttributes_ProductVariantId_Name",
                table: "VariantAttributes",
                columns: new[] { "ProductVariantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VariantAttributes_VariantAttributeDefinitionId",
                table: "VariantAttributes",
                column: "VariantAttributeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_VariantAttributes_VariantAttributeOptionId",
                table: "VariantAttributes",
                column: "VariantAttributeOptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VariantAttributes");

            migrationBuilder.DropTable(
                name: "ProductVariants");

            migrationBuilder.DropTable(
                name: "VariantAttributeOptions");

            migrationBuilder.DropTable(
                name: "VariantAttributeDefinitions");
        }
    }
}
