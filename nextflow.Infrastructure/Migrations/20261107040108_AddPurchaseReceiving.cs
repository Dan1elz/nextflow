using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseReceiving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "real_lead_time_days",
                table: "purchase_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "received_by",
                table: "purchase_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "average_cost_after",
                table: "purchase_items",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "product_variants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "last_cost",
                table: "product_suppliers",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_received_by_id",
                table: "purchase_orders",
                column: "received_by");

            migrationBuilder.AddForeignKey(
                name: "fk_purchase_orders_user_received_by_id",
                table: "purchase_orders",
                column: "received_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_purchase_orders_user_received_by_id",
                table: "purchase_orders");

            migrationBuilder.DropIndex(
                name: "ix_purchase_orders_received_by_id",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "real_lead_time_days",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "received_by",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "average_cost_after",
                table: "purchase_items");

            migrationBuilder.DropColumn(
                name: "version",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "last_cost",
                table: "product_suppliers");
        }
    }
}
