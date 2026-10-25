using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Master.Migrations
{
    /// <inheritdoc />
    public partial class AddPlansCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    is_selectable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plans", x => x.id);
                    table.CheckConstraint("ck_plans_code_format", "code ~ '^[a-z][a-z0-9-]{2,63}$'");
                    table.CheckConstraint("ck_plans_display_name_length", "length(display_name) >= 1 AND length(display_name) <= 256");
                    table.ForeignKey(
                        name: "fk_plans_created_by",
                        column: x => x.created_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plans_updated_by",
                        column: x => x.updated_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency_code = table.Column<string>(type: "text", nullable: false),
                    billing_period = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_versions", x => x.id);
                    table.CheckConstraint("ck_plan_versions_billing_period", "billing_period IN ('Monthly', 'Annual')");
                    table.CheckConstraint("ck_plan_versions_currency_code_format", "currency_code ~ '^[a-z]{3}$'");
                    table.CheckConstraint("ck_plan_versions_price_non_negative", "price >= 0");
                    table.CheckConstraint("ck_plan_versions_status", "status IN ('Draft', 'Active', 'Retired')");
                    table.CheckConstraint("ck_plan_versions_version_number_positive", "version_number >= 1");
                    table.ForeignKey(
                        name: "fk_plan_versions_created_by",
                        column: x => x.created_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plan_versions_plan_id",
                        column: x => x.plan_id,
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plan_versions_updated_by",
                        column: x => x.updated_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_entitlements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entitlement_key = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_entitlements", x => x.id);
                    table.CheckConstraint("ck_plan_entitlements_entitlement_key_format", "entitlement_key ~ '^[a-z][a-z0-9_.]*$'");
                    table.CheckConstraint("ck_plan_entitlements_entitlement_key_length", "length(entitlement_key) >= 1 AND length(entitlement_key) <= 128");
                    table.CheckConstraint("ck_plan_entitlements_value_length", "length(value) >= 1 AND length(value) <= 512");
                    table.ForeignKey(
                        name: "fk_plan_entitlements_plan_version_id",
                        column: x => x.plan_version_id,
                        principalTable: "plan_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_plan_entitlements_key",
                table: "plan_entitlements",
                columns: new[] { "plan_version_id", "entitlement_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plan_versions_created_by",
                table: "plan_versions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_plan_versions_updated_by",
                table: "plan_versions",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_plan_versions_plan_version",
                table: "plan_versions",
                columns: new[] { "plan_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plans_created_by",
                table: "plans",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_plans_updated_by",
                table: "plans",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_plans_code",
                table: "plans",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plan_entitlements");

            migrationBuilder.DropTable(
                name: "plan_versions");

            migrationBuilder.DropTable(
                name: "plans");
        }
    }
}
