using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Master.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscription_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "text", nullable: true),
                    to_status = table.Column<string>(type: "text", nullable: false),
                    plan_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    period_number = table.Column<int>(type: "integer", nullable: true),
                    actor_platform_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_reference = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_history", x => x.id);
                    table.CheckConstraint("ck_subscription_history_actor_reference", "actor_reference IS NULL OR (char_length(actor_reference) BETWEEN 1 AND 256 AND actor_reference !~ '[[:cntrl:]]')");
                    table.CheckConstraint("ck_subscription_history_from_status", "from_status IS NULL OR from_status IN ('Active', 'Suspended', 'Expired', 'Cancelled')");
                    table.CheckConstraint("ck_subscription_history_period_number", "period_number IS NULL OR period_number >= 1");
                    table.CheckConstraint("ck_subscription_history_reason", "reason IS NULL OR (char_length(reason) BETWEEN 1 AND 256 AND reason !~ '[[:cntrl:]]')");
                    table.CheckConstraint("ck_subscription_history_to_status", "to_status IN ('Active', 'Suspended', 'Expired', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_subscription_history_actor_platform_admin_id",
                        column: x => x.actor_platform_admin_id,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscription_history_plan_version_id",
                        column: x => x.plan_version_id,
                        principalTable: "plan_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscription_history_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_subscription_history_actor_platform_admin_id",
                table: "subscription_history",
                column: "actor_platform_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_history_plan_version_id",
                table: "subscription_history",
                column: "plan_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_history_subscription_occurred_at",
                table: "subscription_history",
                columns: new[] { "subscription_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subscription_history");
        }
    }
}
