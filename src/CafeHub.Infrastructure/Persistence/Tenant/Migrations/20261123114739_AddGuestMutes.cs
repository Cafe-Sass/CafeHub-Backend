using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestMutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guest_mutes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<string>(type: "text", nullable: false),
                    muted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    lifted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    lifted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guest_mutes", x => x.id);
                    table.ForeignKey(
                        name: "fk_guest_mutes_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_guest_mutes_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_guest_mutes_lifted_by_user_id",
                        column: x => x.lifted_by_user_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_guest_mutes_muted_by_user_id",
                        column: x => x.muted_by_user_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_guest_mutes_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_guest_mutes_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_guest_mutes_branch_device",
                table: "guest_mutes",
                columns: new[] { "branch_id", "device_id" });

            migrationBuilder.CreateIndex(
                name: "ix_guest_mutes_created_by",
                table: "guest_mutes",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_guest_mutes_lifted_by_user_id",
                table: "guest_mutes",
                column: "lifted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_guest_mutes_muted_by_user_id",
                table: "guest_mutes",
                column: "muted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_guest_mutes_order_id",
                table: "guest_mutes",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_guest_mutes_updated_by",
                table: "guest_mutes",
                column: "updated_by");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guest_mutes");
        }
    }
}
