using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Master.Migrations
{
    /// <inheritdoc />
    public partial class AddProvisioningJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "provisioning_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provisioning_jobs", x => x.id);
                    table.CheckConstraint("ck_provisioning_jobs_status", "status IN ('Pending', 'Running', 'Succeeded', 'Failed')");
                    table.ForeignKey(
                        name: "fk_provisioning_jobs_created_by",
                        column: x => x.created_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_provisioning_jobs_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_provisioning_jobs_updated_by",
                        column: x => x.updated_by,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provisioning_job_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provisioning_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    error_code = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provisioning_job_attempts", x => x.id);
                    table.CheckConstraint("ck_provisioning_job_attempts_attempt_number_positive", "attempt_number >= 1");
                    table.CheckConstraint("ck_provisioning_job_attempts_error_code_length", "error_code IS NULL OR length(error_code) <= 128");
                    table.CheckConstraint("ck_provisioning_job_attempts_status", "status IN ('Running', 'Succeeded', 'Failed')");
                    table.ForeignKey(
                        name: "fk_provisioning_job_attempts_provisioning_job_id",
                        column: x => x.provisioning_job_id,
                        principalTable: "provisioning_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_provisioning_job_attempts_attempt",
                table: "provisioning_job_attempts",
                columns: new[] { "provisioning_job_id", "attempt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provisioning_jobs_created_by",
                table: "provisioning_jobs",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_provisioning_jobs_updated_by",
                table: "provisioning_jobs",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_provisioning_jobs_tenant_id",
                table: "provisioning_jobs",
                column: "tenant_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provisioning_job_attempts");

            migrationBuilder.DropTable(
                name: "provisioning_jobs");
        }
    }
}
