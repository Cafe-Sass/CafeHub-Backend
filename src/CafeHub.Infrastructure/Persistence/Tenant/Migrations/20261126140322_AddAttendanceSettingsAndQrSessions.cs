using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceSettingsAndQrSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attendance_qr_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_qr_sessions", x => x.id);
                    table.CheckConstraint("ck_attendance_qr_sessions_token", "token ~ '^[A-Za-z0-9_-]{43}$'");
                    table.CheckConstraint("ck_attendance_qr_sessions_window", "valid_to > valid_from");
                    table.ForeignKey(
                        name: "fk_attendance_qr_sessions_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_qr_sessions_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "text", nullable: false, defaultValue: "Off"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_settings", x => x.id);
                    table.CheckConstraint("ck_attendance_settings_mode", "mode IN ('Off', 'Qr', 'Device')");
                    table.ForeignKey(
                        name: "fk_attendance_settings_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_settings_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_qr_sessions_branch_valid_to",
                table: "attendance_qr_sessions",
                columns: new[] { "branch_id", "valid_to" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_qr_sessions_created_by",
                table: "attendance_qr_sessions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "uq_attendance_qr_sessions_token",
                table: "attendance_qr_sessions",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attendance_settings_created_by",
                table: "attendance_settings",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_settings_updated_by",
                table: "attendance_settings",
                column: "updated_by");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_attendance_settings_singleton ON attendance_settings ((true));");

            migrationBuilder.Sql(
                "INSERT INTO attendance_settings (id, mode, created_at, updated_at, created_by, updated_by) "
                + "VALUES ('7c1d3f5a-2b4e-4d8a-9c6f-0e5b8a2d1f47', 'Off', now(), now(), NULL, NULL);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_qr_sessions");

            migrationBuilder.DropTable(
                name: "attendance_settings");
        }
    }
}
