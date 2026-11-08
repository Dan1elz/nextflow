using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDerivedForecastData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_sales_monthly",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<short>(type: "smallint", nullable: false),
                    month = table.Column<short>(type: "smallint", nullable: false),
                    quantity_sold = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    quantity_sold_on_promo = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    days_out_of_stock = table.Column<short>(type: "smallint", nullable: false),
                    lost_demand_qty = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    avg_price = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    revenue = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_sales_monthly", x => x.id);
                    table.CheckConstraint("ck_product_sales_monthly_days_out_of_stock", "days_out_of_stock between 0 and 31");
                    table.CheckConstraint("ck_product_sales_monthly_month", "month between 1 and 12");
                    table.CheckConstraint("ck_product_sales_monthly_quantities", "quantity_sold >= 0 and quantity_sold_on_promo >= 0 and quantity_sold_on_promo <= quantity_sold and lost_demand_qty >= 0");
                    table.ForeignKey(
                        name: "fk_product_sales_monthly_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_seasonality",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    month = table.Column<short>(type: "smallint", nullable: false),
                    seasonal_index = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    months_of_history = table.Column<short>(type: "smallint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_seasonality", x => x.id);
                    table.CheckConstraint("ck_product_seasonality_history", "months_of_history between 12 and 24");
                    table.CheckConstraint("ck_product_seasonality_index", "seasonal_index between 0.05 and 5");
                    table.CheckConstraint("ck_product_seasonality_month", "month between 1 and 12");
                    table.ForeignKey(
                        name: "fk_product_seasonality_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_daily_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    closing_quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_daily_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_daily_snapshots_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_daily_snapshots_stock_location_location_id",
                        column: x => x.location_id,
                        principalTable: "stock_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_product_sales_monthly_product_id_year_month",
                table: "product_sales_monthly",
                columns: new[] { "product_id", "year", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_seasonality_product_id_month",
                table: "product_seasonality",
                columns: new[] { "product_id", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_daily_snapshots_location_id",
                table: "stock_daily_snapshots",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_daily_snapshots_variant_id_location_id_day",
                table: "stock_daily_snapshots",
                columns: new[] { "variant_id", "location_id", "day" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_sales_monthly");

            migrationBuilder.DropTable(
                name: "product_seasonality");

            migrationBuilder.DropTable(
                name: "stock_daily_snapshots");
        }
    }
}
