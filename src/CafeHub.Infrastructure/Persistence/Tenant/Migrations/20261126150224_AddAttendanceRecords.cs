using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attendance_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_in_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    check_out_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    check_in_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_out_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "text", nullable: false, defaultValue: "Qr"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_records", x => x.id);
                    table.CheckConstraint("ck_attendance_records_source", "source IN ('Qr', 'Device')");
                    table.CheckConstraint("ck_attendance_records_window", "check_out_at IS NULL OR check_out_at > check_in_at");
                    table.ForeignKey(
                        name: "fk_attendance_records_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_records_check_in_session_id",
                        column: x => x.check_in_session_id,
                        principalTable: "attendance_qr_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_records_check_out_session_id",
                        column: x => x.check_out_session_id,
                        principalTable: "attendance_qr_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_records_staff_id",
                        column: x => x.staff_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_branch_check_in_at",
                table: "attendance_records",
                columns: new[] { "branch_id", "check_in_at" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_check_in_session_id",
                table: "attendance_records",
                column: "check_in_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_check_out_session_id",
                table: "attendance_records",
                column: "check_out_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_staff_check_in_at",
                table: "attendance_records",
                columns: new[] { "staff_id", "check_in_at" });

            migrationBuilder.CreateIndex(
                name: "uq_attendance_records_open_staff",
                table: "attendance_records",
                column: "staff_id",
                unique: true,
                filter: "check_out_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "uq_attendance_records_staff_check_in_session",
                table: "attendance_records",
                columns: new[] { "staff_id", "check_in_session_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_attendance_records_staff_check_out_session",
                table: "attendance_records",
                columns: new[] { "staff_id", "check_out_session_id" },
                unique: true,
                filter: "check_out_session_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_records");
        }
    }
}
