using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lots", x => x.id);
                    table.ForeignKey(
                        name: "fk_lots_product_variant_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_lot_id",
                table: "stock_movements",
                column: "lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_balances_lot_id",
                table: "stock_balances",
                column: "lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_lots_expires_on",
                table: "lots",
                column: "expires_on");

            migrationBuilder.CreateIndex(
                name: "ix_lots_variant_id_code",
                table: "lots",
                columns: new[] { "variant_id", "code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_stock_balances_lots_lot_id",
                table: "stock_balances",
                column: "lot_id",
                principalTable: "lots",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_stock_movements_lots_lot_id",
                table: "stock_movements",
                column: "lot_id",
                principalTable: "lots",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_stock_balances_lots_lot_id",
                table: "stock_balances");

            migrationBuilder.DropForeignKey(
                name: "fk_stock_movements_lots_lot_id",
                table: "stock_movements");

            migrationBuilder.DropTable(
                name: "lots");

            migrationBuilder.DropIndex(
                name: "ix_stock_movements_lot_id",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "ix_stock_balances_lot_id",
                table: "stock_balances");
        }
    }
}
