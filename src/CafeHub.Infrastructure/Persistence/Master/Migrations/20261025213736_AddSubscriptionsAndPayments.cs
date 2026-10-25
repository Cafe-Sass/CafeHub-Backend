using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Master.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionsAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_payment_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    onboarding_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    renewal_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency_code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    settled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    failure_code = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_payment_transactions", x => x.id);
                    table.CheckConstraint("ck_platform_payment_transactions_amount_non_negative", "amount >= 0");
                    table.CheckConstraint("ck_platform_payment_transactions_currency_code_format", "currency_code ~ '^[a-z]{3}$'");
                    table.CheckConstraint("ck_platform_payment_transactions_failure_code_length", "failure_code IS NULL OR length(failure_code) <= 128");
                    table.CheckConstraint("ck_platform_payment_transactions_idempotency_key_length", "length(idempotency_key) >= 8 AND length(idempotency_key) <= 128");
                    table.CheckConstraint("ck_platform_payment_transactions_status", "status IN ('Initiated', 'Succeeded', 'Failed')");
                    table.CheckConstraint("ck_platform_payment_transactions_target", "num_nonnulls(onboarding_request_id, renewal_request_id) = 1");
                    table.ForeignKey(
                        name: "fk_platform_payment_transactions_onboarding_request_id",
                        column: x => x.onboarding_request_id,
                        principalTable: "onboarding_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    onboarding_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscriptions", x => x.id);
                    table.CheckConstraint("ck_subscriptions_owner", "num_nonnulls(onboarding_request_id, tenant_id) = 1");
                    table.CheckConstraint("ck_subscriptions_status", "status IN ('Active', 'Suspended', 'Expired', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_subscriptions_onboarding_request_id",
                        column: x => x.onboarding_request_id,
                        principalTable: "onboarding_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscription_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_number = table.Column<int>(type: "integer", nullable: false),
                    period_status = table.Column<string>(type: "text", nullable: false),
                    agreed_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency_code = table.Column<string>(type: "text", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    renewal_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_periods", x => x.id);
                    table.CheckConstraint("ck_subscription_periods_currency_code_format", "currency_code ~ '^[a-z]{3}$'");
                    table.CheckConstraint("ck_subscription_periods_period_number_positive", "period_number >= 1");
                    table.CheckConstraint("ck_subscription_periods_price_non_negative", "agreed_price >= 0");
                    table.CheckConstraint("ck_subscription_periods_status", "period_status IN ('Active', 'Ended', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_subscription_periods_plan_version_id",
                        column: x => x.plan_version_id,
                        principalTable: "plan_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscription_periods_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_platform_payment_transactions_onboarding_request_id",
                table: "platform_payment_transactions",
                column: "onboarding_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_platform_payment_transactions_renewal_request_id",
                table: "platform_payment_transactions",
                column: "renewal_request_id");

            migrationBuilder.CreateIndex(
                name: "uq_platform_payment_transactions_key",
                table: "platform_payment_transactions",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscription_periods_plan_version_id",
                table: "subscription_periods",
                column: "plan_version_id");

            migrationBuilder.CreateIndex(
                name: "uq_subscription_periods_active",
                table: "subscription_periods",
                column: "subscription_id",
                unique: true,
                filter: "\"period_status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "uq_subscription_periods_period_number",
                table: "subscription_periods",
                columns: new[] { "subscription_id", "period_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_subscription_periods_renewal_request",
                table: "subscription_periods",
                column: "renewal_request_id",
                unique: true,
                filter: "\"renewal_request_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_subscriptions_onboarding_request_id",
                table: "subscriptions",
                column: "onboarding_request_id",
                unique: true,
                filter: "\"onboarding_request_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_subscriptions_tenant_id",
                table: "subscriptions",
                column: "tenant_id",
                unique: true,
                filter: "\"tenant_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_payment_transactions");

            migrationBuilder.DropTable(
                name: "subscription_periods");

            migrationBuilder.DropTable(
                name: "subscriptions");
        }
    }
}
