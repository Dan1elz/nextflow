using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiForecastModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeadTimeDays",
                table: "suppliers",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedAt",
                table: "purchase_orders",
                type: "timestamp with time zone",
                nullable: true);

            // Pedidos já recebidos antes desta versão: melhor estimativa da data de recebimento = última atualização.
            migrationBuilder.Sql(
                "UPDATE purchase_orders SET \"ReceivedAt\" = COALESCE(\"UpdateAt\", \"CreateAt\") " +
                "WHERE \"PurchaseStatus\" = 3 AND \"ReceivedAt\" IS NULL;");

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "products",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostPrice",
                table: "products",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinStock",
                table: "products",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "forecast_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    HorizonMonths = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    AiProvider = table.Column<string>(type: "text", nullable: true),
                    AiModel = table.Column<string>(type: "text", nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    InputSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    ProductsAnalyzed = table.Column<int>(type: "integer", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_forecast_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_forecast_runs_users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_sales_monthly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    QuantitySold = table.Column<decimal>(type: "numeric", nullable: false),
                    Revenue = table.Column<decimal>(type: "numeric", nullable: false),
                    OrdersCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_sales_monthly", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_sales_monthly_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_seasonality",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    SeasonalIndex = table.Column<decimal>(type: "numeric", nullable: false),
                    MonthsOfHistory = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_seasonality", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_seasonality_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Source = table.Column<byte>(type: "smallint", nullable: false),
                    ForecastRecommendationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_promotions_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_promotions_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "forecast_projections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ForecastRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    ProjectedQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_forecast_projections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_forecast_projections_forecast_runs_ForecastRunId",
                        column: x => x.ForecastRunId,
                        principalTable: "forecast_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_forecast_projections_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "forecast_recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ForecastRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<byte>(type: "smallint", nullable: false),
                    Priority = table.Column<byte>(type: "smallint", nullable: false),
                    Confidence = table.Column<byte>(type: "smallint", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    SuggestedQuantity = table.Column<decimal>(type: "numeric", nullable: true),
                    SuggestedDiscountPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    SuggestedPromotionDays = table.Column<int>(type: "integer", nullable: true),
                    Deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CurrentStock = table.Column<decimal>(type: "numeric", nullable: false),
                    IncomingStock = table.Column<decimal>(type: "numeric", nullable: false),
                    ProjectedDemand = table.Column<decimal>(type: "numeric", nullable: false),
                    NextMonthDemand = table.Column<decimal>(type: "numeric", nullable: false),
                    CoverageDays = table.Column<decimal>(type: "numeric", nullable: true),
                    NextMonthSeasonalIndex = table.Column<decimal>(type: "numeric", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DismissReason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_forecast_recommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_forecast_recommendations_forecast_runs_ForecastRunId",
                        column: x => x.ForecastRunId,
                        principalTable: "forecast_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_forecast_recommendations_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_forecast_recommendations_promotions_PromotionId",
                        column: x => x.PromotionId,
                        principalTable: "promotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_forecast_recommendations_purchase_orders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_forecast_projections_ForecastRunId",
                table: "forecast_projections",
                column: "ForecastRunId");

            migrationBuilder.CreateIndex(
                name: "IX_forecast_projections_ProductId",
                table: "forecast_projections",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_forecast_recommendations_ForecastRunId",
                table: "forecast_recommendations",
                column: "ForecastRunId");

            migrationBuilder.CreateIndex(
                name: "IX_forecast_recommendations_ProductId",
                table: "forecast_recommendations",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_forecast_recommendations_PromotionId",
                table: "forecast_recommendations",
                column: "PromotionId");

            migrationBuilder.CreateIndex(
                name: "IX_forecast_recommendations_PurchaseOrderId",
                table: "forecast_recommendations",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_forecast_recommendations_Status",
                table: "forecast_recommendations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_forecast_runs_RequestedByUserId",
                table: "forecast_runs",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_product_sales_monthly_ProductId_Year_Month",
                table: "product_sales_monthly",
                columns: new[] { "ProductId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_seasonality_ProductId_Month",
                table: "product_seasonality",
                columns: new[] { "ProductId", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_promotions_CreatedByUserId",
                table: "promotions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_promotions_ProductId_StartDate_EndDate",
                table: "promotions",
                columns: new[] { "ProductId", "StartDate", "EndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "forecast_projections");

            migrationBuilder.DropTable(
                name: "forecast_recommendations");

            migrationBuilder.DropTable(
                name: "product_sales_monthly");

            migrationBuilder.DropTable(
                name: "product_seasonality");

            migrationBuilder.DropTable(
                name: "forecast_runs");

            migrationBuilder.DropTable(
                name: "promotions");

            migrationBuilder.DropColumn(
                name: "LeadTimeDays",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "products");

            migrationBuilder.DropColumn(
                name: "CostPrice",
                table: "products");

            migrationBuilder.DropColumn(
                name: "MinStock",
                table: "products");
        }
    }
}
