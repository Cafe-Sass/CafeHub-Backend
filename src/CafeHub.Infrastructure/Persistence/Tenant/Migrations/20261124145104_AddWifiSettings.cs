using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddWifiSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branch_wifi_networks",
                columns: table => new
                {
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ssid = table.Column<string>(type: "text", nullable: false),
                    security = table.Column<string>(type: "text", nullable: false),
                    password = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branch_wifi_networks", x => x.branch_id);
                    table.CheckConstraint("ck_branch_wifi_networks_password", "password IS NULL OR (char_length(password) BETWEEN 1 AND 63 AND password !~ '[[:cntrl:]]')");
                    table.CheckConstraint("ck_branch_wifi_networks_security", "security IN ('None', 'WPA', 'WEP')");
                    table.CheckConstraint("ck_branch_wifi_networks_ssid", "char_length(ssid) BETWEEN 1 AND 32 AND ssid !~ '[[:cntrl:]]'");
                    table.ForeignKey(
                        name: "fk_branch_wifi_networks_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_branch_wifi_networks_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_branch_wifi_networks_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wifi_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy = table.Column<string>(type: "text", nullable: false, defaultValue: "Free"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wifi_settings", x => x.id);
                    table.CheckConstraint("ck_wifi_settings_policy", "policy IN ('Free', 'Paid')");
                    table.ForeignKey(
                        name: "fk_wifi_settings_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_wifi_settings_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_branch_wifi_networks_created_by",
                table: "branch_wifi_networks",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_branch_wifi_networks_updated_by",
                table: "branch_wifi_networks",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_wifi_settings_created_by",
                table: "wifi_settings",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_wifi_settings_updated_by",
                table: "wifi_settings",
                column: "updated_by");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_wifi_settings_singleton ON wifi_settings ((true))");

            migrationBuilder.InsertData(
                table: "wifi_settings",
                columns: new[] { "id", "policy", "created_at", "updated_at", "created_by", "updated_by" },
                values: new object[]
                {
                    new Guid("1b3c5d7e-9f20-4a6b-8c1d-2e3f4a5b6c7d"),
                    "Free",
                    new DateTimeOffset(2026, 11, 24, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 11, 24, 0, 0, 0, TimeSpan.Zero),
                    null,
                    null,
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS uq_wifi_settings_singleton");

            migrationBuilder.DeleteData(
                table: "wifi_settings",
                keyColumn: "id",
                keyValue: new Guid("1b3c5d7e-9f20-4a6b-8c1d-2e3f4a5b6c7d"));

            migrationBuilder.DropTable(
                name: "branch_wifi_networks");

            migrationBuilder.DropTable(
                name: "wifi_settings");
        }
    }
}
