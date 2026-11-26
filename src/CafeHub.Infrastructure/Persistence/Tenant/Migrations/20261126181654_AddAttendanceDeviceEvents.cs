using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceDeviceEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "check_in_session_id",
                table: "attendance_records",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "attendance_device_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_event_id = table.Column<string>(type: "text", nullable: false),
                    device_code = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    direction = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_device_events", x => x.id);
                    table.CheckConstraint("ck_attendance_device_events_code", "char_length(device_code) BETWEEN 1 AND 64 AND device_code !~ '[[:cntrl:]]'");
                    table.CheckConstraint("ck_attendance_device_events_direction", "direction IS NULL OR direction IN ('In', 'Out')");
                    table.CheckConstraint("ck_attendance_device_events_event_id", "char_length(device_event_id) BETWEEN 1 AND 128 AND device_event_id !~ '[[:cntrl:]]'");
                    table.CheckConstraint("ck_attendance_device_events_status", "status IN ('Processed', 'Unmatched')");
                    table.ForeignKey(
                        name: "fk_attendance_device_events_device_id",
                        column: x => x.device_id,
                        principalTable: "attendance_devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_device_events_record_id",
                        column: x => x.record_id,
                        principalTable: "attendance_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_attendance_records_source_shape",
                table: "attendance_records",
                sql: "(source = 'Qr' AND check_in_session_id IS NOT NULL) OR (source = 'Device' AND check_in_session_id IS NULL AND check_out_session_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_device_events_device_status_occurred_at",
                table: "attendance_device_events",
                columns: new[] { "device_id", "status", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_device_events_record_id",
                table: "attendance_device_events",
                column: "record_id");

            migrationBuilder.CreateIndex(
                name: "uq_attendance_device_events_device_event",
                table: "attendance_device_events",
                columns: new[] { "device_id", "device_event_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_device_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_attendance_records_source_shape",
                table: "attendance_records");

            migrationBuilder.AlterColumn<Guid>(
                name: "check_in_session_id",
                table: "attendance_records",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
