using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddPrintJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "print_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    printer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "text", nullable: false),
                    source_type = table.Column<string>(type: "text", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dedupe_key = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error_code = table.Column<string>(type: "text", nullable: true),
                    document_payload = table.Column<string>(type: "jsonb", nullable: false),
                    lease_expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_print_jobs", x => x.id);
                    table.CheckConstraint("ck_print_jobs_attempts", "attempts >= 0");
                    table.CheckConstraint("ck_print_jobs_dedupe_key_length", "char_length(dedupe_key) BETWEEN 1 AND 160");
                    table.CheckConstraint("ck_print_jobs_document_type", "document_type IN ('OrderTicket', 'Receipt')");
                    table.CheckConstraint("ck_print_jobs_last_error_code", "last_error_code IS NULL OR last_error_code IN ('PrinterOffline', 'PaperOut', 'PayloadRejected', 'Unknown')");
                    table.CheckConstraint("ck_print_jobs_max_attempts", "max_attempts BETWEEN 1 AND 3");
                    table.CheckConstraint("ck_print_jobs_source_type", "source_type IN ('Order', 'Bill')");
                    table.CheckConstraint("ck_print_jobs_status", "status IN ('Queued', 'Claimed', 'Succeeded', 'Failed', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_print_jobs_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_print_jobs_printer_id",
                        column: x => x.printer_id,
                        principalTable: "printers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_print_jobs_branch_id",
                table: "print_jobs",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_print_jobs_printer_id",
                table: "print_jobs",
                column: "printer_id");

            migrationBuilder.CreateIndex(
                name: "uq_print_jobs_branch_dedupe_key",
                table: "print_jobs",
                columns: new[] { "branch_id", "dedupe_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "print_jobs");
        }
    }
}
