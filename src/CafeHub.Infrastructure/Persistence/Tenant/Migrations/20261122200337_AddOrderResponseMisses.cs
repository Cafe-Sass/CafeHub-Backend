using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderResponseMisses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_response_misses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expired_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_response_misses", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_response_misses_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_response_misses_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_response_misses_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_response_misses_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_response_miss_holders",
                columns: table => new
                {
                    order_response_miss_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_response_miss_holders", x => new { x.order_response_miss_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_order_response_miss_holders_miss_id",
                        column: x => x.order_response_miss_id,
                        principalTable: "order_response_misses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_response_miss_holders_user_id",
                        column: x => x.user_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_order_response_miss_holders_user_id",
                table: "order_response_miss_holders",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_response_misses_branch_expired_at",
                table: "order_response_misses",
                columns: new[] { "branch_id", "expired_at" });

            migrationBuilder.CreateIndex(
                name: "ix_order_response_misses_created_by",
                table: "order_response_misses",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_order_response_misses_updated_by",
                table: "order_response_misses",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_order_response_misses_order_id",
                table: "order_response_misses",
                column: "order_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_response_miss_holders");

            migrationBuilder.DropTable(
                name: "order_response_misses");
        }
    }
}
