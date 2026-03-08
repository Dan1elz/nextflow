using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "TotalPrice",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "UpdateAt",
                table: "order_items");

            migrationBuilder.RenameColumn(
                name: "DiscountAmount",
                table: "orders",
                newName: "TotalDiscount");

            migrationBuilder.AddColumn<string>(
                name: "LossReason",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Type",
                table: "orders",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_orders_UserId",
                table: "orders",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_orders_users_UserId",
                table: "orders",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_users_UserId",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_UserId",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "LossReason",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "orders");

            migrationBuilder.RenameColumn(
                name: "TotalDiscount",
                table: "orders",
                newName: "DiscountAmount");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "order_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPrice",
                table: "order_items",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdateAt",
                table: "order_items",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
