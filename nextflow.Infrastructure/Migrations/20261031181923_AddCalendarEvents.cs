using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calendar_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    scope = table.Column<byte>(type: "smallint", nullable: false),
                    date_rule = table.Column<byte>(type: "smallint", nullable: false),
                    month = table.Column<short>(type: "smallint", nullable: true),
                    day = table.Column<short>(type: "smallint", nullable: true),
                    weekday = table.Column<short>(type: "smallint", nullable: true),
                    nth = table.Column<short>(type: "smallint", nullable: true),
                    easter_offset_days = table.Column<short>(type: "smallint", nullable: true),
                    lead_days = table.Column<short>(type: "smallint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_events", x => x.id);
                    table.CheckConstraint("ck_calendar_events_date_rule", "date_rule IN (1, 2, 3, 4, 5)");
                    table.CheckConstraint("ck_calendar_events_scope", "scope IN (1, 2)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_name",
                table: "calendar_events",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendar_events");
        }
    }
}
