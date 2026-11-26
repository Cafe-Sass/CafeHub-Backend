using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attendance_device_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_code = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_device_codes", x => x.id);
                    table.CheckConstraint("ck_attendance_device_codes_value", "char_length(device_code) BETWEEN 1 AND 64 AND device_code !~ '[[:cntrl:]]'");
                    table.ForeignKey(
                        name: "fk_attendance_device_codes_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_device_codes_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_device_codes_staff_id",
                        column: x => x.staff_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_device_codes_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance_devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    key_hash = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_devices", x => x.id);
                    table.CheckConstraint("ck_attendance_devices_key_hash", "char_length(key_hash) = 64");
                    table.CheckConstraint("ck_attendance_devices_name", "char_length(name) BETWEEN 1 AND 64 AND name !~ '[[:cntrl:]]'");
                    table.CheckConstraint("ck_attendance_devices_status", "status IN ('Active', 'Revoked')");
                    table.ForeignKey(
                        name: "fk_attendance_devices_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_devices_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_devices_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_device_codes_branch_id",
                table: "attendance_device_codes",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_device_codes_created_by",
                table: "attendance_device_codes",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_device_codes_staff_id",
                table: "attendance_device_codes",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_device_codes_updated_by",
                table: "attendance_device_codes",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_attendance_device_codes_branch_code",
                table: "attendance_device_codes",
                columns: new[] { "branch_id", "device_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_attendance_device_codes_branch_staff",
                table: "attendance_device_codes",
                columns: new[] { "branch_id", "staff_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attendance_devices_branch_id",
                table: "attendance_devices",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_devices_created_by",
                table: "attendance_devices",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_devices_updated_by",
                table: "attendance_devices",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_attendance_devices_key_hash",
                table: "attendance_devices",
                column: "key_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_device_codes");

            migrationBuilder.DropTable(
                name: "attendance_devices");
        }
    }
}
