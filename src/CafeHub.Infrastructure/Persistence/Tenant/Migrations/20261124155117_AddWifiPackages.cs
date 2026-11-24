using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddWifiPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wifi_packages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    speed_mbps = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    data_mode = table.Column<string>(type: "text", nullable: false),
                    quota_mb = table.Column<int>(type: "integer", nullable: true),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wifi_packages", x => x.id);
                    table.CheckConstraint("ck_wifi_packages_data_mode", "data_mode IN ('Unlimited', 'Limited')");
                    table.CheckConstraint("ck_wifi_packages_duration", "duration_minutes BETWEEN 1 AND 43200");
                    table.CheckConstraint("ck_wifi_packages_name", "char_length(name) BETWEEN 1 AND 48 AND name !~ '[[:cntrl:]]'");
                    table.CheckConstraint("ck_wifi_packages_price", "price >= 0");
                    table.CheckConstraint("ck_wifi_packages_quota", "quota_mb IS NULL OR quota_mb BETWEEN 1 AND 1000000");
                    table.CheckConstraint("ck_wifi_packages_quota_mode", "(data_mode = 'Limited') = (quota_mb IS NOT NULL)");
                    table.CheckConstraint("ck_wifi_packages_sort_order", "sort_order >= 0");
                    table.CheckConstraint("ck_wifi_packages_speed", "speed_mbps IS NULL OR (speed_mbps > 0 AND speed_mbps <= 999999.99)");
                    table.CheckConstraint("ck_wifi_packages_status", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_wifi_packages_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_wifi_packages_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_wifi_packages_created_by",
                table: "wifi_packages",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_wifi_packages_updated_by",
                table: "wifi_packages",
                column: "updated_by");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_wifi_packages_active_name ON wifi_packages (lower(name)) WHERE status = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS uq_wifi_packages_active_name");

            migrationBuilder.DropTable(
                name: "wifi_packages");
        }
    }
}
