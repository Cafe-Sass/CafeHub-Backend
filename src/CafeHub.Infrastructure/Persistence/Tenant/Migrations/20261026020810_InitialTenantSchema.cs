using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class InitialTenantSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_kind = table.Column<string>(type: "text", nullable: false),
                    user_name = table.Column<string>(type: "text", nullable: false),
                    normalized_user_name = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    normalized_email = table.Column<string>(type: "text", nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    lockout_end_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    security_stamp = table.Column<string>(type: "text", nullable: false),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_users", x => x.id);
                    table.CheckConstraint("ck_tenant_users_concurrency_stamp_length", "length(concurrency_stamp) >= 1 AND length(concurrency_stamp) <= 64");
                    table.CheckConstraint("ck_tenant_users_email_length", "email IS NULL OR (length(email) >= 1 AND length(email) <= 320)");
                    table.CheckConstraint("ck_tenant_users_full_name_length", "length(full_name) >= 1 AND length(full_name) <= 256");
                    table.CheckConstraint("ck_tenant_users_kind", "user_kind IN ('Owner', 'Staff')");
                    table.CheckConstraint("ck_tenant_users_normalized_email_length", "normalized_email IS NULL OR (length(normalized_email) >= 1 AND length(normalized_email) <= 320)");
                    table.CheckConstraint("ck_tenant_users_normalized_user_name_length", "length(normalized_user_name) >= 1 AND length(normalized_user_name) <= 256");
                    table.CheckConstraint("ck_tenant_users_password_hash_length", "length(password_hash) >= 1 AND length(password_hash) <= 512");
                    table.CheckConstraint("ck_tenant_users_phone_number_length", "phone_number IS NULL OR (length(phone_number) >= 1 AND length(phone_number) <= 64)");
                    table.CheckConstraint("ck_tenant_users_security_stamp_length", "length(security_stamp) >= 1 AND length(security_stamp) <= 64");
                    table.CheckConstraint("ck_tenant_users_user_name_length", "length(user_name) >= 1 AND length(user_name) <= 256");
                    table.ForeignKey(
                        name: "fk_tenant_users_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_users_deleted_by",
                        column: x => x.deleted_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_users_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    lifecycle_status = table.Column<string>(type: "text", nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    contact_phone = table.Column<string>(type: "text", nullable: true),
                    opening_hours_summary = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branches", x => x.id);
                    table.CheckConstraint("ck_branches_address_length", "address IS NULL OR length(address) <= 512");
                    table.CheckConstraint("ck_branches_contact_phone_length", "contact_phone IS NULL OR length(contact_phone) <= 32");
                    table.CheckConstraint("ck_branches_lifecycle_status", "lifecycle_status IN ('Active', 'Suspended', 'Retired')");
                    table.CheckConstraint("ck_branches_name_length", "length(name) >= 1 AND length(name) <= 256");
                    table.CheckConstraint("ck_branches_opening_hours_summary_length", "opening_hours_summary IS NULL OR length(opening_hours_summary) <= 512");
                    table.ForeignKey(
                        name: "fk_branches_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_branches_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cafe_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cafe_name = table.Column<string>(type: "text", nullable: false),
                    logo_media_reference = table.Column<string>(type: "text", nullable: true),
                    primary_color = table.Column<string>(type: "text", nullable: true),
                    secondary_color = table.Column<string>(type: "text", nullable: true),
                    accent_color = table.Column<string>(type: "text", nullable: true),
                    contact_phone = table.Column<string>(type: "text", nullable: true),
                    contact_email = table.Column<string>(type: "text", nullable: true),
                    contact_address = table.Column<string>(type: "text", nullable: true),
                    timezone = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cafe_settings", x => x.id);
                    table.CheckConstraint("ck_cafe_settings_cafe_name_length", "length(cafe_name) >= 1 AND length(cafe_name) <= 256");
                    table.CheckConstraint("ck_cafe_settings_color_format", "primary_color IS NULL OR primary_color ~ '^#[0-9a-fA-F]{6}$' AND secondary_color IS NULL OR secondary_color ~ '^#[0-9a-fA-F]{6}$' AND accent_color IS NULL OR accent_color ~ '^#[0-9a-fA-F]{6}$'");
                    table.CheckConstraint("ck_cafe_settings_contact_address_length", "contact_address IS NULL OR length(contact_address) <= 512");
                    table.CheckConstraint("ck_cafe_settings_contact_email_length", "contact_email IS NULL OR length(contact_email) <= 320");
                    table.CheckConstraint("ck_cafe_settings_contact_phone_length", "contact_phone IS NULL OR length(contact_phone) <= 32");
                    table.CheckConstraint("ck_cafe_settings_logo_media_reference_length", "logo_media_reference IS NULL OR length(logo_media_reference) <= 512");
                    table.CheckConstraint("ck_cafe_settings_timezone", "length(timezone) >= 1 AND length(timezone) <= 64");
                    table.ForeignKey(
                        name: "fk_cafe_settings_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cafe_settings_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_audit_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_kind = table.Column<string>(type: "text", nullable: false),
                    action_code = table.Column<string>(type: "text", nullable: false),
                    subject_type = table.Column<string>(type: "text", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    before_state = table.Column<string>(type: "jsonb", nullable: true),
                    after_state = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_audit_records", x => x.id);
                    table.CheckConstraint("ck_tenant_audit_records_action_code_length", "length(action_code) >= 1 AND length(action_code) <= 128");
                    table.CheckConstraint("ck_tenant_audit_records_kind", "actor_kind IN ('User', 'System')");
                    table.CheckConstraint("ck_tenant_audit_records_subject_type_length", "length(subject_type) >= 1 AND length(subject_type) <= 64");
                    table.ForeignKey(
                        name: "fk_tenant_audit_records_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    normalized_name = table.Column<string>(type: "text", nullable: false),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_roles", x => x.id);
                    table.CheckConstraint("ck_tenant_roles_concurrency_stamp_length", "length(concurrency_stamp) >= 1 AND length(concurrency_stamp) <= 64");
                    table.CheckConstraint("ck_tenant_roles_name_length", "length(name) >= 1 AND length(name) <= 256");
                    table.CheckConstraint("ck_tenant_roles_normalized_name_length", "length(normalized_name) >= 1 AND length(normalized_name) <= 256");
                    table.ForeignKey(
                        name: "fk_tenant_roles_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_roles_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_user_claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: false),
                    claim_value = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_user_claims_tenant_user_id",
                        column: x => x.tenant_user_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_role_claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: false),
                    claim_value = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_role_claims_role_id",
                        column: x => x.role_id,
                        principalTable: "tenant_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_user_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_user_roles", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_user_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "tenant_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_user_roles_tenant_user_id",
                        column: x => x.tenant_user_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_branches_created_by",
                table: "branches",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_branches_updated_by",
                table: "branches",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_cafe_settings_created_by",
                table: "cafe_settings",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_cafe_settings_updated_by",
                table: "cafe_settings",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_audit_records_actor_user_id",
                table: "tenant_audit_records",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_role_claims_role_id",
                table: "tenant_role_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_roles_created_by",
                table: "tenant_roles",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_roles_updated_by",
                table: "tenant_roles",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_tenant_roles_normalized_name",
                table: "tenant_roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_user_claims_tenant_user_id",
                table: "tenant_user_claims",
                column: "tenant_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_user_roles_role_id",
                table: "tenant_user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "uq_tenant_user_roles_user_role",
                table: "tenant_user_roles",
                columns: new[] { "tenant_user_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_users_created_by",
                table: "tenant_users",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_users_deleted_by",
                table: "tenant_users",
                column: "deleted_by");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_users_normalized_email",
                table: "tenant_users",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_users_updated_by",
                table: "tenant_users",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_tenant_users_normalized_user_name",
                table: "tenant_users",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_cafe_settings_singleton ON cafe_settings ((true))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropTable(
                name: "cafe_settings");

            migrationBuilder.DropTable(
                name: "tenant_audit_records");

            migrationBuilder.DropTable(
                name: "tenant_role_claims");

            migrationBuilder.DropTable(
                name: "tenant_user_claims");

            migrationBuilder.DropTable(
                name: "tenant_user_roles");

            migrationBuilder.DropTable(
                name: "tenant_roles");

            migrationBuilder.DropTable(
                name: "tenant_users");
        }
    }
}
