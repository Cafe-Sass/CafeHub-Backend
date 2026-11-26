using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "refund_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    public_reference = table.Column<string>(type: "text", nullable: false),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    details = table.Column<string>(type: "text", nullable: true),
                    claimed_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    guest_display_name = table.Column<string>(type: "text", nullable: true),
                    assigned_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_note = table.Column<string>(type: "text", nullable: true),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refund_requests", x => x.id);
                    table.CheckConstraint("ck_refund_requests_claimed_amount", "claimed_amount > 0");
                    table.CheckConstraint("ck_refund_requests_decision_pair", "((status IN ('Approved', 'Rejected') AND resolution_note IS NOT NULL AND char_length(btrim(resolution_note)) > 0 AND decided_by_user_id IS NOT NULL AND decided_at IS NOT NULL) OR (status IN ('Open', 'InReview') AND resolution_note IS NULL AND decided_by_user_id IS NULL AND decided_at IS NULL))");
                    table.CheckConstraint("ck_refund_requests_details_length", "details IS NULL OR char_length(details) <= 2000");
                    table.CheckConstraint("ck_refund_requests_idempotency_key_format", "char_length(idempotency_key) BETWEEN 8 AND 64 AND idempotency_key ~ '^[A-Za-z0-9_-]+$'");
                    table.CheckConstraint("ck_refund_requests_public_reference_format", "char_length(public_reference) = 22 AND public_reference ~ '^[A-Za-z0-9_-]{22}$'");
                    table.CheckConstraint("ck_refund_requests_reason_length", "char_length(btrim(reason)) BETWEEN 3 AND 256");
                    table.CheckConstraint("ck_refund_requests_status", "status IN ('Open', 'InReview', 'Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "fk_refund_requests_assigned_user_id",
                        column: x => x.assigned_user_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_refund_requests_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_refund_requests_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_refund_requests_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_refund_requests_decided_by_user_id",
                        column: x => x.decided_by_user_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_refund_requests_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_refund_requests_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_refund_requests_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refund_request_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    refund_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_key = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refund_request_attachments", x => x.id);
                    table.CheckConstraint("ck_refund_request_attachments_content_type", "content_type IN ('image/jpeg', 'image/png', 'image/webp', 'application/pdf')");
                    table.CheckConstraint("ck_refund_request_attachments_media_key_length", "char_length(media_key) BETWEEN 1 AND 512");
                    table.CheckConstraint("ck_refund_request_attachments_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_refund_request_attachments_size", "size_bytes > 0");
                    table.ForeignKey(
                        name: "fk_refund_request_attachments_refund_request_id",
                        column: x => x.refund_request_id,
                        principalTable: "refund_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refund_request_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    refund_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    from_status = table.Column<string>(type: "text", nullable: true),
                    to_status = table.Column<string>(type: "text", nullable: true),
                    actor_kind = table.Column<string>(type: "text", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refund_request_events", x => x.id);
                    table.CheckConstraint("ck_refund_request_events_actor_kind", "actor_kind IN ('Guest', 'Staff')");
                    table.CheckConstraint("ck_refund_request_events_actor_pair", "((actor_kind = 'Guest' AND actor_user_id IS NULL) OR (actor_kind = 'Staff' AND actor_user_id IS NOT NULL))");
                    table.CheckConstraint("ck_refund_request_events_from_status", "from_status IS NULL OR from_status IN ('Open', 'InReview', 'Approved', 'Rejected')");
                    table.CheckConstraint("ck_refund_request_events_kind", "kind IN ('Submitted', 'Assigned', 'StatusChanged')");
                    table.CheckConstraint("ck_refund_request_events_to_status", "to_status IS NULL OR to_status IN ('Open', 'InReview', 'Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "fk_refund_request_events_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_refund_request_events_refund_request_id",
                        column: x => x.refund_request_id,
                        principalTable: "refund_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_refund_request_attachments_refund_request_id",
                table: "refund_request_attachments",
                column: "refund_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_refund_request_events_actor_user_id",
                table: "refund_request_events",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_refund_request_events_refund_request_id",
                table: "refund_request_events",
                column: "refund_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_refund_requests_assigned_user_id",
                table: "refund_requests",
                column: "assigned_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_refund_requests_bill_id",
                table: "refund_requests",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_refund_requests_branch_id",
                table: "refund_requests",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_refund_requests_created_by",
                table: "refund_requests",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_refund_requests_decided_by_user_id",
                table: "refund_requests",
                column: "decided_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_refund_requests_order_id",
                table: "refund_requests",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_refund_requests_payment_id",
                table: "refund_requests",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_refund_requests_updated_by",
                table: "refund_requests",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_refund_requests_branch_idempotency_key",
                table: "refund_requests",
                columns: new[] { "branch_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_refund_requests_public_reference",
                table: "refund_requests",
                column: "public_reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refund_request_attachments");

            migrationBuilder.DropTable(
                name: "refund_request_events");

            migrationBuilder.DropTable(
                name: "refund_requests");
        }
    }
}
