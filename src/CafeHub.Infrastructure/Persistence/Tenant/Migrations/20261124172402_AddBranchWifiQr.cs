using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchWifiQr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branch_wifi_qr",
                columns: table => new
                {
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qr_code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branch_wifi_qr", x => x.branch_id);
                    table.CheckConstraint("ck_branch_wifi_qr_code_format", "char_length(qr_code) = 22 AND qr_code ~ '^[A-Za-z0-9_-]{22}$'");
                    table.CheckConstraint("ck_branch_wifi_qr_status", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_branch_wifi_qr_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_branch_wifi_qr_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_branch_wifi_qr_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_branch_wifi_qr_created_by",
                table: "branch_wifi_qr",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_branch_wifi_qr_updated_by",
                table: "branch_wifi_qr",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_branch_wifi_qr_code",
                table: "branch_wifi_qr",
                column: "qr_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branch_wifi_qr");
        }
    }
}
