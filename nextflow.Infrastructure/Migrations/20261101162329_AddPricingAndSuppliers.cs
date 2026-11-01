using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingAndSuppliers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "removed_on",
                table: "product_variants",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "price_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_history", x => x.id);
                    table.CheckConstraint("ck_price_history_valid_range", "valid_to IS NULL OR valid_to >= valid_from");
                    table.ForeignKey(
                        name: "fk_price_history_product_variant_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_price_history_user_changed_by_id",
                        column: x => x.changed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_events_calendar_events_event_id",
                        column: x => x.event_id,
                        principalTable: "calendar_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_events_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_suppliers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_preferred = table.Column<bool>(type: "boolean", nullable: false),
                    supplier_sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    lead_time_days = table.Column<int>(type: "integer", nullable: true),
                    min_order_qty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    order_multiple = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_suppliers", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_suppliers_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_product_suppliers_supplier_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_price_history_changed_by_id",
                table: "price_history",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_price_history_open",
                table: "price_history",
                column: "variant_id",
                unique: true,
                filter: "valid_to IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_product_events_event_id",
                table: "product_events",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_events_product_id_event_id",
                table: "product_events",
                columns: new[] { "product_id", "event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_suppliers_product_id_supplier_id",
                table: "product_suppliers",
                columns: new[] { "product_id", "supplier_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_suppliers_supplier_id",
                table: "product_suppliers",
                column: "supplier_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "price_history");

            migrationBuilder.DropTable(
                name: "product_events");

            migrationBuilder.DropTable(
                name: "product_suppliers");

            migrationBuilder.DropColumn(
                name: "removed_on",
                table: "product_variants");
        }
    }
}
