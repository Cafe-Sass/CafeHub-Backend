using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Master.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionRenewalRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscription_renewal_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    proposed_currency_code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    decided_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_renewal_requests", x => x.id);
                    table.CheckConstraint("ck_subscription_renewal_requests_currency_code_format", "proposed_currency_code ~ '^[a-z]{3}$'");
                    table.CheckConstraint("ck_subscription_renewal_requests_decided", "(status = 'Pending') = (decided_at IS NULL)");
                    table.CheckConstraint("ck_subscription_renewal_requests_decided_by", "decided_by IS NULL OR char_length(decided_by) BETWEEN 1 AND 256");
                    table.CheckConstraint("ck_subscription_renewal_requests_expiry", "expires_at > requested_at");
                    table.CheckConstraint("ck_subscription_renewal_requests_price_non_negative", "proposed_price >= 0");
                    table.CheckConstraint("ck_subscription_renewal_requests_status", "status IN ('Pending', 'Approved', 'Declined', 'Expired', 'Completed')");
                    table.ForeignKey(
                        name: "fk_subscription_renewal_requests_plan_version_id",
                        column: x => x.plan_version_id,
                        principalTable: "plan_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscription_renewal_requests_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_subscription_renewal_requests_plan_version_id",
                table: "subscription_renewal_requests",
                column: "plan_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_renewal_requests_subscription_requested_at",
                table: "subscription_renewal_requests",
                columns: new[] { "subscription_id", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "uq_subscription_renewal_requests_open",
                table: "subscription_renewal_requests",
                column: "subscription_id",
                unique: true,
                filter: "\"status\" IN ('Pending', 'Approved')");

            migrationBuilder.AddForeignKey(
                name: "fk_platform_payment_transactions_renewal_request_id",
                table: "platform_payment_transactions",
                column: "renewal_request_id",
                principalTable: "subscription_renewal_requests",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_platform_payment_transactions_renewal_request_id",
                table: "platform_payment_transactions");

            migrationBuilder.DropTable(
                name: "subscription_renewal_requests");
        }
    }
}
