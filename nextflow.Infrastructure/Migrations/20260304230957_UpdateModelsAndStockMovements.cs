using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateModelsAndStockMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "UpdateAt",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "UpdateAt",
                table: "contacts");

            migrationBuilder.RenameColumn(
                name: "Quotation",
                table: "stock_movements",
                newName: "Quote");

            migrationBuilder.AlterColumn<double>(
                name: "Quantity",
                table: "stock_movements",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Quote",
                table: "stock_movements",
                newName: "Quotation");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "stock_movements",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "stock_movements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdateAt",
                table: "stock_movements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdateAt",
                table: "contacts",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
