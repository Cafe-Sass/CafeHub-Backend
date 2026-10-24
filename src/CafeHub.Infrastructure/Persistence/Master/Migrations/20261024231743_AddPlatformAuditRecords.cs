using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Master.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformAuditRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_audit_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    actor_platform_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_kind = table.Column<string>(type: "text", nullable: false),
                    action_code = table.Column<string>(type: "text", nullable: false),
                    subject_type = table.Column<string>(type: "text", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<string>(type: "text", nullable: true),
                    reason_code = table.Column<string>(type: "text", nullable: true),
                    before_state = table.Column<string>(type: "jsonb", nullable: true),
                    after_state = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_audit_records", x => x.id);
                    table.CheckConstraint("ck_platform_audit_records_action_code_length", "length(action_code) >= 1 AND length(action_code) <= 128");
                    table.CheckConstraint("ck_platform_audit_records_correlation_id_format", "correlation_id IS NULL OR correlation_id ~ '^[A-Za-z0-9._:-]{1,64}$'");
                    table.CheckConstraint("ck_platform_audit_records_kind", "actor_kind IN ('PlatformAdmin', 'System')");
                    table.CheckConstraint("ck_platform_audit_records_reason_code_format", "reason_code IS NULL OR reason_code ~ '^[a-z][a-z0-9_]{0,63}$'");
                    table.CheckConstraint("ck_platform_audit_records_subject_type_length", "length(subject_type) >= 1 AND length(subject_type) <= 64");
                    table.ForeignKey(
                        name: "fk_platform_audit_records_actor_platform_admin_id",
                        column: x => x.actor_platform_admin_id,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_records_actor_platform_admin_id",
                table: "platform_audit_records",
                column: "actor_platform_admin_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_audit_records");
        }
    }
}
