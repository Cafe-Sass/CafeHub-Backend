using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Master.Migrations
{
    /// <inheritdoc />
    public partial class CreateMasterIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_admins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    user_name = table.Column<string>(type: "text", nullable: false),
                    normalized_user_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    normalized_email = table.Column<string>(type: "text", nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    security_stamp = table.Column<string>(type: "text", nullable: false),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: false),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    lockout_end_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_admins", x => x.id);
                    table.CheckConstraint("ck_platform_admins_concurrency_stamp_length", "length(concurrency_stamp) >= 1 AND length(concurrency_stamp) <= 64");
                    table.CheckConstraint("ck_platform_admins_email_length", "email IS NULL OR (length(email) >= 1 AND length(email) <= 320)");
                    table.CheckConstraint("ck_platform_admins_normalized_email_length", "normalized_email IS NULL OR (length(normalized_email) >= 1 AND length(normalized_email) <= 320)");
                    table.CheckConstraint("ck_platform_admins_normalized_user_name_length", "length(normalized_user_name) >= 1 AND length(normalized_user_name) <= 256");
                    table.CheckConstraint("ck_platform_admins_password_hash_length", "length(password_hash) >= 1 AND length(password_hash) <= 512");
                    table.CheckConstraint("ck_platform_admins_phone_number_length", "phone_number IS NULL OR (length(phone_number) >= 1 AND length(phone_number) <= 64)");
                    table.CheckConstraint("ck_platform_admins_security_stamp_length", "length(security_stamp) >= 1 AND length(security_stamp) <= 64");
                    table.CheckConstraint("ck_platform_admins_user_name_length", "length(user_name) >= 1 AND length(user_name) <= 256");
                    table.ForeignKey(
                        name: "fk_platform_admins_created_by",
                        column: x => x.created_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_platform_admins_deleted_by",
                        column: x => x.deleted_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_platform_admins_updated_by",
                        column: x => x.updated_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "platform_admin_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    normalized_name = table.Column<string>(type: "text", nullable: false),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_admin_roles", x => x.id);
                    table.CheckConstraint("ck_platform_admin_roles_concurrency_stamp_length", "length(concurrency_stamp) >= 1 AND length(concurrency_stamp) <= 64");
                    table.CheckConstraint("ck_platform_admin_roles_name_length", "length(name) >= 1 AND length(name) <= 256");
                    table.CheckConstraint("ck_platform_admin_roles_normalized_name_length", "length(normalized_name) >= 1 AND length(normalized_name) <= 256");
                    table.ForeignKey(
                        name: "fk_platform_admin_roles_created_by",
                        column: x => x.created_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_platform_admin_roles_updated_by",
                        column: x => x.updated_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "platform_admin_user_claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    platform_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: false),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_admin_user_claims", x => x.id);
                    table.CheckConstraint("ck_platform_admin_user_claims_claim_type_length", "length(claim_type) >= 1 AND length(claim_type) <= 512");
                    table.CheckConstraint("ck_platform_admin_user_claims_claim_value_length", "claim_value IS NULL OR (length(claim_value) >= 0 AND length(claim_value) <= 1024)");
                    table.ForeignKey(
                        name: "fk_platform_admin_user_claims_platform_admin_id",
                        column: x => x.platform_admin_id,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "platform_admin_role_claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: false),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_admin_role_claims", x => x.id);
                    table.CheckConstraint("ck_platform_admin_role_claims_claim_type_length", "length(claim_type) >= 1 AND length(claim_type) <= 512");
                    table.CheckConstraint("ck_platform_admin_role_claims_claim_value_length", "claim_value IS NULL OR (length(claim_value) >= 0 AND length(claim_value) <= 1024)");
                    table.ForeignKey(
                        name: "fk_platform_admin_role_claims_role_id",
                        column: x => x.role_id,
                        principalTable: "platform_admin_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "platform_admin_user_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    platform_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_admin_user_roles", x => x.id);
                    table.ForeignKey(
                        name: "fk_platform_admin_user_roles_platform_admin_id",
                        column: x => x.platform_admin_id,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_platform_admin_user_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "platform_admin_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_platform_admin_role_claims_role_id",
                table: "platform_admin_role_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_platform_admin_roles_created_by",
                table: "platform_admin_roles",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_platform_admin_roles_updated_by",
                table: "platform_admin_roles",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_platform_admin_roles_normalized_name",
                table: "platform_admin_roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_platform_admin_user_claims_platform_admin_id",
                table: "platform_admin_user_claims",
                column: "platform_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_platform_admin_user_roles_role_id",
                table: "platform_admin_user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "uq_platform_admin_user_roles_user_role",
                table: "platform_admin_user_roles",
                columns: new[] { "platform_admin_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_platform_admins_created_by",
                table: "platform_admins",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_platform_admins_deleted_by",
                table: "platform_admins",
                column: "deleted_by");

            migrationBuilder.CreateIndex(
                name: "ix_platform_admins_updated_by",
                table: "platform_admins",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_platform_admins_normalized_email",
                table: "platform_admins",
                column: "normalized_email",
                unique: true,
                filter: "normalized_email IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_platform_admins_normalized_user_name",
                table: "platform_admins",
                column: "normalized_user_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_admin_role_claims");

            migrationBuilder.DropTable(
                name: "platform_admin_user_claims");

            migrationBuilder.DropTable(
                name: "platform_admin_user_roles");

            migrationBuilder.DropTable(
                name: "platform_admin_roles");

            migrationBuilder.DropTable(
                name: "platform_admins");
        }
    }
}
