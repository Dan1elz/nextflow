using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "average_cost",
                table: "product_variants",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "last_cost",
                table: "product_variants",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stock_balances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_balances", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_balances_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_balances_stock_location_location_id",
                        column: x => x.location_id,
                        principalTable: "stock_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<byte>(type: "smallint", nullable: false),
                    reason = table.Column<byte>(type: "smallint", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    balance_after = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    source_type = table.Column<byte>(type: "smallint", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_movements", x => x.id);
                    table.CheckConstraint("ck_stock_movements_signed_quantity", "(type = 1 AND quantity > 0) OR (type = 2 AND quantity < 0)");
                    table.CheckConstraint("ck_stock_movements_unit_cost", "unit_cost IS NULL OR unit_cost >= 0");
                    table.ForeignKey(
                        name: "fk_stock_movements_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_stock_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "stock_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_user_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_balances_location_id",
                table: "stock_balances",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_balances_variant_id_location_id_lot_id",
                table: "stock_balances",
                columns: new[] { "variant_id", "location_id", "lot_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_location_id",
                table: "stock_movements",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_source_type_source_id",
                table: "stock_movements",
                columns: new[] { "source_type", "source_id" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_user_id",
                table: "stock_movements",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_variant_id_location_id_created_at",
                table: "stock_movements",
                columns: new[] { "variant_id", "location_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_balances");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropColumn(
                name: "average_cost",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "last_cost",
                table: "product_variants");
        }
    }
}
