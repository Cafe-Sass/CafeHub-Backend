using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddCafePaymentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cafe_payment_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    online_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    method_label = table.Column<string>(type: "text", nullable: false, defaultValue: "InstaPay"),
                    payment_link = table.Column<string>(type: "text", nullable: true),
                    payment_address = table.Column<string>(type: "text", nullable: true),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cafe_payment_settings", x => x.id);
                    table.CheckConstraint(
                        "ck_cafe_payment_settings_enable_needs_destination",
                        "NOT online_enabled OR payment_link IS NOT NULL OR payment_address IS NOT NULL");
                    table.CheckConstraint(
                        "ck_cafe_payment_settings_method_label_length",
                        "length(method_label) >= 1 AND length(method_label) <= 32");
                    table.CheckConstraint(
                        "ck_cafe_payment_settings_payment_link_format",
                        "payment_link IS NULL OR (payment_link ~ '^https://' AND length(payment_link) <= 512)");
                    table.CheckConstraint(
                        "ck_cafe_payment_settings_payment_address_format",
                        "payment_address IS NULL OR (length(payment_address) >= 1 AND length(payment_address) <= 128 AND payment_address !~ '[[:cntrl:]]')");
                    table.CheckConstraint(
                        "ck_cafe_payment_settings_display_name_format",
                        "display_name IS NULL OR (length(display_name) >= 1 AND length(display_name) <= 128 AND display_name !~ '[[:cntrl:]]')");
                    table.ForeignKey(
                        name: "fk_cafe_payment_settings_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cafe_payment_settings_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cafe_payment_settings_created_by",
                table: "cafe_payment_settings",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_cafe_payment_settings_updated_by",
                table: "cafe_payment_settings",
                column: "updated_by");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_cafe_payment_settings_singleton ON cafe_payment_settings ((true))");

            migrationBuilder.InsertData(
                table: "cafe_payment_settings",
                columns: new[] { "id", "online_enabled", "method_label", "payment_link", "payment_address", "display_name", "created_at", "updated_at", "created_by", "updated_by" },
                values: new object[]
                {
                    new Guid("0a4d5f31-6c2e-4b7a-9f8d-1e2c3b4a5d6f"),
                    false,
                    "InstaPay",
                    null,
                    null,
                    null,
                    new DateTimeOffset(2026, 11, 23, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 11, 23, 0, 0, 0, TimeSpan.Zero),
                    null,
                    null,
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS uq_cafe_payment_settings_singleton");

            migrationBuilder.DeleteData(
                table: "cafe_payment_settings",
                keyColumn: "id",
                keyValue: new Guid("0a4d5f31-6c2e-4b7a-9f8d-1e2c3b4a5d6f"));

            migrationBuilder.DropTable(
                name: "cafe_payment_settings");
        }
    }
}
