using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecommendationStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "forecast_recommendation_stores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    share_percent = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    shortfall = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forecast_recommendation_stores", x => x.id);
                    table.ForeignKey(
                        name: "fk_forecast_recommendation_stores_forecast_recommendations_rec",
                        column: x => x.recommendation_id,
                        principalTable: "forecast_recommendations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_forecast_recommendation_stores_store_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_forecast_recommendation_stores_recommendation_id_store_id",
                table: "forecast_recommendation_stores",
                columns: new[] { "recommendation_id", "store_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_forecast_recommendation_stores_store_id",
                table: "forecast_recommendation_stores",
                column: "store_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "forecast_recommendation_stores");
        }
    }
}
