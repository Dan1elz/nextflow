using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddForecastSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "forecast_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    weekday = table.Column<byte>(type: "smallint", nullable: false),
                    time = table.Column<TimeOnly>(type: "time", nullable: false),
                    ai_batch_size = table.Column<int>(type: "integer", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forecast_settings", x => x.id);
                    table.CheckConstraint("ck_forecast_settings_ai_batch_size", "ai_batch_size between 1 and 100");
                    table.CheckConstraint("ck_forecast_settings_weekday", "weekday between 0 and 6");
                    table.ForeignKey(
                        name: "fk_forecast_settings_user_updated_by_id",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_forecast_settings_updated_by_id",
                table: "forecast_settings",
                column: "updated_by");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "forecast_settings");
        }
    }
}
