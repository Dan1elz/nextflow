using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddForecastEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "forecast_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trigger = table.Column<byte>(type: "smallint", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: true),
                    horizon_months = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    reference_date = table.Column<DateOnly>(type: "date", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    ai_provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ai_model = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    summary = table.Column<string>(type: "text", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    products_analyzed = table.Column<int>(type: "integer", nullable: false),
                    products_sent_to_ai = table.Column<int>(type: "integer", nullable: false),
                    batches_total = table.Column<int>(type: "integer", nullable: false),
                    batches_completed = table.Column<int>(type: "integer", nullable: false),
                    batches_failed = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forecast_runs", x => x.id);
                    table.CheckConstraint("ck_forecast_runs_batches", "batches_completed + batches_failed <= batches_total");
                    table.CheckConstraint("ck_forecast_runs_horizon", "horizon_months between 0 and 6");
                    table.ForeignKey(
                        name: "fk_forecast_runs_user_requested_by_id",
                        column: x => x.requested_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "forecast_ai_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    forecast_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_number = table.Column<short>(type: "smallint", nullable: false),
                    products_count = table.Column<int>(type: "integer", nullable: false),
                    product_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forecast_ai_batches", x => x.id);
                    table.CheckConstraint("ck_forecast_ai_batches_number", "batch_number >= 1");
                    table.ForeignKey(
                        name: "fk_forecast_ai_batches_forecast_run_forecast_run_id",
                        column: x => x.forecast_run_id,
                        principalTable: "forecast_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "forecast_projections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    forecast_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<short>(type: "smallint", nullable: false),
                    month = table.Column<short>(type: "smallint", nullable: false),
                    projected_quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forecast_projections", x => x.id);
                    table.CheckConstraint("ck_forecast_projections_month", "month between 1 and 12");
                    table.CheckConstraint("ck_forecast_projections_quantity", "projected_quantity >= 0");
                    table.ForeignKey(
                        name: "fk_forecast_projections_forecast_run_forecast_run_id",
                        column: x => x.forecast_run_id,
                        principalTable: "forecast_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_forecast_projections_product_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "forecast_recommendations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    forecast_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<byte>(type: "smallint", nullable: false),
                    priority = table.Column<byte>(type: "smallint", nullable: false),
                    confidence = table.Column<byte>(type: "smallint", nullable: false),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    suggested_quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: true),
                    calculated_quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: true),
                    suggested_discount_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    suggested_promotion_days = table.Column<int>(type: "integer", nullable: true),
                    deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    reason_calculated = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ai_adjusted = table.Column<bool>(type: "boolean", nullable: false),
                    current_stock = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    incoming_stock = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    projected_demand = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    next_month_demand = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    coverage_days = table.Column<decimal>(type: "numeric(8,1)", precision: 8, scale: 1, nullable: true),
                    next_month_seasonal_index = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    safety_stock = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    lead_time_days = table.Column<int>(type: "integer", nullable: false),
                    lead_time_source = table.Column<byte>(type: "smallint", nullable: false),
                    months_of_history = table.Column<short>(type: "smallint", nullable: false),
                    based_on_last_year = table.Column<bool>(type: "boolean", nullable: false),
                    applied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    applied_by = table.Column<Guid>(type: "uuid", nullable: true),
                    applied_purchase_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    applied_promotion_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dismiss_reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forecast_recommendations", x => x.id);
                    table.CheckConstraint("ck_forecast_recommendations_discount", "suggested_discount_percent is null or suggested_discount_percent between 0 and 90");
                    table.CheckConstraint("ck_forecast_recommendations_quantity", "suggested_quantity is null or suggested_quantity >= 0");
                    table.ForeignKey(
                        name: "fk_forecast_recommendations_forecast_run_forecast_run_id",
                        column: x => x.forecast_run_id,
                        principalTable: "forecast_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_forecast_recommendations_product_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_forecast_recommendations_promotion_applied_promotion_id",
                        column: x => x.applied_promotion_id,
                        principalTable: "promotions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_forecast_recommendations_purchase_order_applied_purchase_or",
                        column: x => x.applied_purchase_order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_forecast_recommendations_user_applied_by",
                        column: x => x.applied_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "forecast_recommendation_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    share_percent = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forecast_recommendation_items", x => x.id);
                    table.CheckConstraint("ck_forecast_recommendation_items_quantity", "quantity >= 0");
                    table.CheckConstraint("ck_forecast_recommendation_items_share", "share_percent between 0 and 100");
                    table.ForeignKey(
                        name: "fk_forecast_recommendation_items_forecast_recommendations_reco",
                        column: x => x.recommendation_id,
                        principalTable: "forecast_recommendations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_forecast_recommendation_items_product_variant_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_forecast_ai_batches_forecast_run_id_batch_number",
                table: "forecast_ai_batches",
                columns: new[] { "forecast_run_id", "batch_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_forecast_projections_forecast_run_id_product_id_year_month",
                table: "forecast_projections",
                columns: new[] { "forecast_run_id", "product_id", "year", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_forecast_projections_product_id_year_month",
                table: "forecast_projections",
                columns: new[] { "product_id", "year", "month" });

            migrationBuilder.CreateIndex(
                name: "ix_forecast_recommendation_items_recommendation_id_variant_id",
                table: "forecast_recommendation_items",
                columns: new[] { "recommendation_id", "variant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_forecast_recommendation_items_variant_id",
                table: "forecast_recommendation_items",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_forecast_recommendations_applied_by",
                table: "forecast_recommendations",
                column: "applied_by");

            migrationBuilder.CreateIndex(
                name: "ix_forecast_recommendations_applied_promotion_id",
                table: "forecast_recommendations",
                column: "applied_promotion_id");

            migrationBuilder.CreateIndex(
                name: "ix_forecast_recommendations_applied_purchase_order_id",
                table: "forecast_recommendations",
                column: "applied_purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_forecast_recommendations_forecast_run_id",
                table: "forecast_recommendations",
                column: "forecast_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_forecast_recommendations_forecast_run_id_product_id",
                table: "forecast_recommendations",
                columns: new[] { "forecast_run_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_forecast_recommendations_pending",
                table: "forecast_recommendations",
                column: "status",
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ix_forecast_recommendations_product_id_status",
                table: "forecast_recommendations",
                columns: new[] { "product_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_forecast_runs_requested_by_id",
                table: "forecast_runs",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "ix_forecast_runs_started_at",
                table: "forecast_runs",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ux_forecast_runs_running",
                table: "forecast_runs",
                column: "status",
                unique: true,
                filter: "status = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "forecast_ai_batches");

            migrationBuilder.DropTable(
                name: "forecast_projections");

            migrationBuilder.DropTable(
                name: "forecast_recommendation_items");

            migrationBuilder.DropTable(
                name: "forecast_recommendations");

            migrationBuilder.DropTable(
                name: "forecast_runs");
        }
    }
}
