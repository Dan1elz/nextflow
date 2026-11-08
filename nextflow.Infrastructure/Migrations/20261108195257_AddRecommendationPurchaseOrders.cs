using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecommendationPurchaseOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "forecast_recommendation_purchase_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forecast_recommendation_purchase_orders", x => x.id);
                    table.ForeignKey(
                        name: "fk_forecast_recommendation_purchase_orders_forecast_recommenda",
                        column: x => x.recommendation_id,
                        principalTable: "forecast_recommendations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_forecast_recommendation_purchase_orders_purchase_order_purc",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_forecast_recommendation_purchase_orders_purchase_order_id",
                table: "forecast_recommendation_purchase_orders",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_forecast_recommendation_purchase_orders_recommendation_id_p",
                table: "forecast_recommendation_purchase_orders",
                columns: new[] { "recommendation_id", "purchase_order_id" },
                unique: true);

            // Recomendações já aplicadas antes da F3.9: o pedido único entra na lista (RN-DF28).
            migrationBuilder.Sql("""
                INSERT INTO forecast_recommendation_purchase_orders (id, recommendation_id, purchase_order_id, created_at)
                SELECT gen_random_uuid(), id, applied_purchase_order_id, COALESCE(applied_at, now())
                FROM forecast_recommendations
                WHERE applied_purchase_order_id IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "forecast_recommendation_purchase_orders");
        }
    }
}
