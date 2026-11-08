using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "applied_transfer_id",
                table: "forecast_recommendations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "transfer_from_location_id",
                table: "forecast_recommendations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "transfer_to_location_id",
                table: "forecast_recommendations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stock_transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    from_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    forecast_recommendation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_transfers", x => x.id);
                    table.CheckConstraint("ck_stock_transfers_locations", "from_location_id <> to_location_id");
                    table.ForeignKey(
                        name: "fk_stock_transfers_forecast_recommendations_forecast_recommend",
                        column: x => x.forecast_recommendation_id,
                        principalTable: "forecast_recommendations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_stock_transfers_stock_locations_from_location_id",
                        column: x => x.from_location_id,
                        principalTable: "stock_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_transfers_stock_locations_to_location_id",
                        column: x => x.to_location_id,
                        principalTable: "stock_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_transfers_user_created_by_id",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfer_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_transfer_items", x => x.id);
                    table.CheckConstraint("ck_stock_transfer_items_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_stock_transfer_items_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_transfer_items_stock_transfers_transfer_id",
                        column: x => x.transfer_id,
                        principalTable: "stock_transfers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_forecast_recommendations_applied_transfer_id",
                table: "forecast_recommendations",
                column: "applied_transfer_id");

            migrationBuilder.CreateIndex(
                name: "ix_forecast_recommendations_transfer_from_location_id",
                table: "forecast_recommendations",
                column: "transfer_from_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_forecast_recommendations_transfer_to_location_id",
                table: "forecast_recommendations",
                column: "transfer_to_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfer_items_transfer_id",
                table: "stock_transfer_items",
                column: "transfer_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfer_items_variant_id",
                table: "stock_transfer_items",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfers_created_by_id",
                table: "stock_transfers",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfers_forecast_recommendation_id",
                table: "stock_transfers",
                column: "forecast_recommendation_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfers_from_location_id",
                table: "stock_transfers",
                column: "from_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfers_number",
                table: "stock_transfers",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfers_to_location_id",
                table: "stock_transfers",
                column: "to_location_id");

            migrationBuilder.AddForeignKey(
                name: "fk_forecast_recommendations_stock_location_transfer_from_locat",
                table: "forecast_recommendations",
                column: "transfer_from_location_id",
                principalTable: "stock_locations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_forecast_recommendations_stock_location_transfer_to_locatio",
                table: "forecast_recommendations",
                column: "transfer_to_location_id",
                principalTable: "stock_locations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_forecast_recommendations_stock_transfer_applied_transfer_id",
                table: "forecast_recommendations",
                column: "applied_transfer_id",
                principalTable: "stock_transfers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_forecast_recommendations_stock_location_transfer_from_locat",
                table: "forecast_recommendations");

            migrationBuilder.DropForeignKey(
                name: "fk_forecast_recommendations_stock_location_transfer_to_locatio",
                table: "forecast_recommendations");

            migrationBuilder.DropForeignKey(
                name: "fk_forecast_recommendations_stock_transfer_applied_transfer_id",
                table: "forecast_recommendations");

            migrationBuilder.DropTable(
                name: "stock_transfer_items");

            migrationBuilder.DropTable(
                name: "stock_transfers");

            migrationBuilder.DropIndex(
                name: "ix_forecast_recommendations_applied_transfer_id",
                table: "forecast_recommendations");

            migrationBuilder.DropIndex(
                name: "ix_forecast_recommendations_transfer_from_location_id",
                table: "forecast_recommendations");

            migrationBuilder.DropIndex(
                name: "ix_forecast_recommendations_transfer_to_location_id",
                table: "forecast_recommendations");

            migrationBuilder.DropColumn(
                name: "applied_transfer_id",
                table: "forecast_recommendations");

            migrationBuilder.DropColumn(
                name: "transfer_from_location_id",
                table: "forecast_recommendations");

            migrationBuilder.DropColumn(
                name: "transfer_to_location_id",
                table: "forecast_recommendations");
        }
    }
}
