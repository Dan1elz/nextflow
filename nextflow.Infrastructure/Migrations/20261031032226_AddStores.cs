using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    default_sale_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    default_receive_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_sellable = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_locations", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_locations_store_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_stores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_stores", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_stores_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_stores_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_locations_store_id",
                table: "stock_locations",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "ix_stores_default_receive_location_id",
                table: "stores",
                column: "default_receive_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stores_default_sale_location_id",
                table: "stores",
                column: "default_sale_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_stores_store_id",
                table: "user_stores",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_stores_user_id_store_id",
                table: "user_stores",
                columns: new[] { "user_id", "store_id" },
                unique: true);

            // Par circular stores ⇄ stock_locations (DOMAIN_LEVELS, par 1): a FK dos padrões é conferida no fim da
            // transação, para a loja e o local "Loja" entrarem no mesmo commit. Fora do modelo do EF, que recusaria o ciclo.
            migrationBuilder.Sql("""
                ALTER TABLE stores
                    ADD CONSTRAINT fk_stores_stock_locations_default_sale_location_id
                    FOREIGN KEY (default_sale_location_id) REFERENCES stock_locations (id)
                    DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE stores
                    ADD CONSTRAINT fk_stores_stock_locations_default_receive_location_id
                    FOREIGN KEY (default_receive_location_id) REFERENCES stock_locations (id)
                    DEFERRABLE INITIALLY DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE stores DROP CONSTRAINT fk_stores_stock_locations_default_sale_location_id;
                ALTER TABLE stores DROP CONSTRAINT fk_stores_stock_locations_default_receive_location_id;
                """);

            migrationBuilder.DropTable(
                name: "stock_locations");

            migrationBuilder.DropTable(
                name: "user_stores");

            migrationBuilder.DropTable(
                name: "stores");
        }
    }
}
