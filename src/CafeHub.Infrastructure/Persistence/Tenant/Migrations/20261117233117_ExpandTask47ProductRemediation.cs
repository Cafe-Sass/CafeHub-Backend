using System;
using CafeHub.Infrastructure.Provisioning;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class ExpandTask47ProductRemediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "id",
                table: "product_tags",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "product_tags",
                type: "text",
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_stations_branch_id_id",
                table: "stations",
                columns: new[] { "branch_id", "id" });

            migrationBuilder.CreateTable(
                name: "product_branch_station_slots",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_branch_station_slots", x => new { x.product_id, x.branch_id });
                    table.ForeignKey(
                        name: "fk_product_branch_station_slots_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_branch_station_slots_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_routing_readiness",
                columns: table => new
                {
                    singleton = table.Column<bool>(type: "boolean", nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_routing_readiness", x => x.singleton);
                    table.CheckConstraint("ck_product_routing_readiness_singleton", "singleton");
                });

            migrationBuilder.CreateTable(
                name: "product_station_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    station_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_by = table.Column<Guid>(type: "uuid", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_station_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_station_assignments_ended_by",
                        column: x => x.ended_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_station_assignments_slot",
                        columns: x => new { x.product_id, x.branch_id },
                        principalTable: "product_branch_station_slots",
                        principalColumns: new[] { "product_id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_station_assignments_started_by",
                        column: x => x.started_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_station_assignments_station",
                        columns: x => new { x.branch_id, x.station_id },
                        principalTable: "stations",
                        principalColumns: new[] { "branch_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_product_tags_status",
                table: "product_tags",
                sql: "status IN ('Active', 'Inactive')");

            migrationBuilder.CreateIndex(
                name: "ix_product_branch_station_slots_branch_id",
                table: "product_branch_station_slots",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_station_assignments_ended_by",
                table: "product_station_assignments",
                column: "ended_by");

            migrationBuilder.CreateIndex(
                name: "ix_product_station_assignments_slot",
                table: "product_station_assignments",
                columns: new[] { "product_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_product_station_assignments_started_by",
                table: "product_station_assignments",
                column: "started_by");

            migrationBuilder.CreateIndex(
                name: "ix_product_station_assignments_station",
                table: "product_station_assignments",
                columns: new[] { "branch_id", "station_id" });

            migrationBuilder.CreateIndex(
                name: "uq_product_station_assignments_current",
                table: "product_station_assignments",
                columns: new[] { "product_id", "branch_id" },
                unique: true,
                filter: "ended_at IS NULL");

            migrationBuilder.Sql(TenantReadinessPrivileges.ReadOnlySql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE product_tags, product_routing_readiness, product_branch_station_slots,
                    product_station_assignments IN ACCESS EXCLUSIVE MODE;

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM product_station_assignments)
                        OR EXISTS (SELECT 1 FROM product_branch_station_slots)
                        OR EXISTS (SELECT 1 FROM product_routing_readiness)
                        OR EXISTS (SELECT 1 FROM product_tags WHERE id IS NOT NULL OR status <> 'Active') THEN
                        RAISE EXCEPTION 'Task 47 remediation data must be preserved; downgrade is blocked';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "product_routing_readiness");

            migrationBuilder.DropTable(
                name: "product_station_assignments");

            migrationBuilder.DropTable(
                name: "product_branch_station_slots");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_stations_branch_id_id",
                table: "stations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_product_tags_status",
                table: "product_tags");

            migrationBuilder.DropColumn(
                name: "id",
                table: "product_tags");

            migrationBuilder.DropColumn(
                name: "status",
                table: "product_tags");
        }
    }
}
