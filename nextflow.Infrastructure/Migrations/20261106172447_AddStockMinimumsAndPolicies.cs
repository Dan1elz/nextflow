using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockMinimumsAndPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "abc_class",
                table: "products",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "default_min_quantity",
                table: "products",
                type: "numeric(14,3)",
                precision: 14,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "target_coverage_days",
                table: "products",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stock_location_minimums",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_location_minimums", x => x.id);
                    table.CheckConstraint("ck_stock_location_minimums_min_quantity", "min_quantity >= 0");
                    table.ForeignKey(
                        name: "fk_stock_location_minimums_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_stock_location_minimums_stock_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "stock_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    abc_class = table.Column<byte>(type: "smallint", nullable: false),
                    target_coverage_days = table.Column<int>(type: "integer", nullable: false),
                    safety_days = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_policies", x => x.id);
                    table.CheckConstraint("ck_stock_policies_safety_days", "safety_days >= 0");
                    table.CheckConstraint("ck_stock_policies_target_coverage_days", "target_coverage_days >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_location_minimums_location_id",
                table: "stock_location_minimums",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_location_minimums_variant_id_location_id",
                table: "stock_location_minimums",
                columns: new[] { "variant_id", "location_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_policies_abc_class",
                table: "stock_policies",
                column: "abc_class",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_location_minimums");

            migrationBuilder.DropTable(
                name: "stock_policies");

            migrationBuilder.DropColumn(
                name: "abc_class",
                table: "products");

            migrationBuilder.DropColumn(
                name: "default_min_quantity",
                table: "products");

            migrationBuilder.DropColumn(
                name: "target_coverage_days",
                table: "products");
        }
    }
}
