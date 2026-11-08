using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddForecastActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "dismiss_reason_code",
                table: "forecast_recommendations",
                type: "smallint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_promotions_forecast_recommendation_id",
                table: "promotions",
                column: "forecast_recommendation_id");

            migrationBuilder.AddForeignKey(
                name: "fk_promotions_forecast_recommendations_forecast_recommendation",
                table: "promotions",
                column: "forecast_recommendation_id",
                principalTable: "forecast_recommendations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_promotions_forecast_recommendations_forecast_recommendation",
                table: "promotions");

            migrationBuilder.DropIndex(
                name: "ix_promotions_forecast_recommendation_id",
                table: "promotions");

            migrationBuilder.DropColumn(
                name: "dismiss_reason_code",
                table: "forecast_recommendations");
        }
    }
}
