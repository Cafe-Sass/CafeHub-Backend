using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Master.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "onboarding_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_via = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    plan_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_email = table.Column<string>(type: "text", nullable: false),
                    owner_full_name = table.Column<string>(type: "text", nullable: false),
                    owner_phone_number = table.Column<string>(type: "text", nullable: true),
                    cafe_name = table.Column<string>(type: "text", nullable: false),
                    owner_password_hash = table.Column<string>(type: "text", nullable: true),
                    tenant_slug = table.Column<string>(type: "text", nullable: false),
                    onboarding_status = table.Column<string>(type: "text", nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_onboarding_requests", x => x.id);
                    table.CheckConstraint("ck_onboarding_requests_cafe_name_length", "length(cafe_name) >= 1 AND length(cafe_name) <= 256");
                    table.CheckConstraint("ck_onboarding_requests_created_via", "created_via IN ('PublicRegistration', 'Support')");
                    table.CheckConstraint("ck_onboarding_requests_owner_credential", "created_via = 'Support' OR owner_password_hash IS NOT NULL");
                    table.CheckConstraint("ck_onboarding_requests_owner_email_length", "length(owner_email) >= 1 AND length(owner_email) <= 320");
                    table.CheckConstraint("ck_onboarding_requests_owner_full_name_length", "length(owner_full_name) >= 1 AND length(owner_full_name) <= 256");
                    table.CheckConstraint("ck_onboarding_requests_owner_password_hash_length", "owner_password_hash IS NULL OR length(owner_password_hash) BETWEEN 1 AND 512");
                    table.CheckConstraint("ck_onboarding_requests_owner_phone_number_length", "owner_phone_number IS NULL OR (length(owner_phone_number) >= 1 AND length(owner_phone_number) <= 64)");
                    table.CheckConstraint("ck_onboarding_requests_rejection_reason_length", "rejection_reason IS NULL OR length(rejection_reason) <= 1024");
                    table.CheckConstraint("ck_onboarding_requests_slug_format", "tenant_slug ~ '^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$'");
                    table.CheckConstraint("ck_onboarding_requests_slug_lowercase", "tenant_slug = lower(tenant_slug)");
                    table.CheckConstraint("ck_onboarding_requests_slug_reserved", "tenant_slug NOT IN ('admin','api','t','www','app','static','assets','health','login','signup','support','platform')");
                    table.CheckConstraint("ck_onboarding_requests_status", "onboarding_status IN ('AwaitingPayment', 'PaymentFailed', 'PaidAwaitingReview', 'Approved', 'Rejected', 'Cancelled')");
                    table.CheckConstraint("ck_onboarding_requests_support_actor", "created_via = 'PublicRegistration' OR created_by IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_onboarding_requests_created_by",
                        column: x => x.created_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_onboarding_requests_plan_version_id",
                        column: x => x.plan_version_id,
                        principalTable: "plan_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_onboarding_requests_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_onboarding_requests_updated_by",
                        column: x => x.updated_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_onboarding_requests_created_by",
                table: "onboarding_requests",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_onboarding_requests_plan_version_id",
                table: "onboarding_requests",
                column: "plan_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_onboarding_requests_reviewed_by",
                table: "onboarding_requests",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "ix_onboarding_requests_updated_by",
                table: "onboarding_requests",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_onboarding_requests_slug",
                table: "onboarding_requests",
                column: "tenant_slug",
                unique: true,
                filter: "\"onboarding_status\" NOT IN ('Approved', 'Rejected', 'Cancelled')");

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX uq_onboarding_requests_public_owner_email
                ON onboarding_requests (lower(owner_email))
                WHERE created_via = 'PublicRegistration';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS uq_onboarding_requests_public_owner_email;
                """);

            migrationBuilder.DropTable(
                name: "onboarding_requests");
        }
    }
}
