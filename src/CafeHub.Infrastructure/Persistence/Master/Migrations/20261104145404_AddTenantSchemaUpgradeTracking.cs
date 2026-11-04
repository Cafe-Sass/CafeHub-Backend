using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Master.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSchemaUpgradeTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "schema_migration_id",
                table: "tenant_databases",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tenant_schema_upgrade_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_migration_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    failure_code = table.Column<string>(type: "text", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    lease_expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    leased_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_schema_upgrade_runs", x => x.id);
                    table.CheckConstraint("ck_tenant_schema_upgrade_runs_attempt_count_not_negative", "attempt_count >= 0");
                    table.CheckConstraint("ck_tenant_schema_upgrade_runs_failure_code_length", "failure_code IS NULL OR (length(failure_code) >= 1 AND length(failure_code) <= 64)");
                    table.CheckConstraint("ck_tenant_schema_upgrade_runs_status", "status IN ('Pending', 'Running', 'Succeeded', 'Failed')");
                    table.CheckConstraint("ck_tenant_schema_upgrade_runs_target_migration_id_length", "length(target_migration_id) >= 1 AND length(target_migration_id) <= 150");
                    table.ForeignKey(
                        name: "fk_tenant_schema_upgrade_runs_created_by",
                        column: x => x.created_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_schema_upgrade_runs_requested_by",
                        column: x => x.requested_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_schema_upgrade_runs_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_schema_upgrade_runs_updated_by",
                        column: x => x.updated_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_databases_schema_migration_id_length",
                table: "tenant_databases",
                sql: "schema_migration_id IS NULL OR (length(schema_migration_id) >= 1 AND length(schema_migration_id) <= 150)");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_schema_upgrade_runs_created_by",
                table: "tenant_schema_upgrade_runs",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_schema_upgrade_runs_requested_by",
                table: "tenant_schema_upgrade_runs",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_schema_upgrade_runs_tenant_id",
                table: "tenant_schema_upgrade_runs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_schema_upgrade_runs_updated_by",
                table: "tenant_schema_upgrade_runs",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_tenant_schema_upgrade_runs_active",
                table: "tenant_schema_upgrade_runs",
                column: "tenant_id",
                unique: true,
                filter: "status IN ('Pending', 'Running')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_schema_upgrade_runs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_databases_schema_migration_id_length",
                table: "tenant_databases");

            migrationBuilder.DropColumn(
                name: "schema_migration_id",
                table: "tenant_databases");
        }
    }
}
