using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQueryIndexesAndUniqueProductCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_sales_IsActive_CreateAt",
                table: "sales",
                columns: new[] { "IsActive", "CreateAt" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_PurchaseStatus_ReceivedAt",
                table: "purchase_orders",
                columns: new[] { "PurchaseStatus", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_products_ProductCode",
                table: "products",
                column: "ProductCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_Status_CreateAt",
                table: "orders",
                columns: new[] { "Status", "CreateAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sales_IsActive_CreateAt",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "IX_purchase_orders_PurchaseStatus_ReceivedAt",
                table: "purchase_orders");

            migrationBuilder.DropIndex(
                name: "IX_products_ProductCode",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_orders_Status_CreateAt",
                table: "orders");
        }
    }
}
