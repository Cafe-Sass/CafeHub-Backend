using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branch_tables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "text", nullable: true),
                    qr_code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branch_tables", x => x.id);
                    table.CheckConstraint("ck_branch_tables_label_length", "label IS NULL OR (length(label) >= 1 AND length(label) <= 64)");
                    table.CheckConstraint("ck_branch_tables_number_range", "number >= 1 AND number <= 9999");
                    table.CheckConstraint("ck_branch_tables_qr_code_format", "length(qr_code) = 22 AND qr_code ~ '^[A-Za-z0-9_-]{22}$'");
                    table.CheckConstraint("ck_branch_tables_status", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_branch_tables_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_branch_tables_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_branch_tables_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_branch_tables_branch_id",
                table: "branch_tables",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_branch_tables_created_by",
                table: "branch_tables",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_branch_tables_updated_by",
                table: "branch_tables",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_branch_tables_qr_code",
                table: "branch_tables",
                column: "qr_code",
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX uq_branch_tables_branch_active_number
                ON branch_tables (branch_id, number)
                WHERE status = 'Active';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS uq_branch_tables_branch_active_number;
                """);

            migrationBuilder.DropTable(
                name: "branch_tables");
        }
    }
}
