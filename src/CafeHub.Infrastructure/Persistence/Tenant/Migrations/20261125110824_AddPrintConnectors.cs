using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddPrintConnectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "print_connectors",
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
                    table.PrimaryKey("pk_print_connectors", x => x.id);
                    table.CheckConstraint("ck_print_connectors_key_hash", "char_length(key_hash) = 64");
                    table.CheckConstraint("ck_print_connectors_name", "char_length(name) BETWEEN 1 AND 64 AND name !~ '[[:cntrl:]]'");
                    table.CheckConstraint("ck_print_connectors_status", "status IN ('Active', 'Revoked')");
                    table.ForeignKey(
                        name: "fk_print_connectors_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_print_connectors_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_print_connectors_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_print_connectors_branch_id",
                table: "print_connectors",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_print_connectors_created_by",
                table: "print_connectors",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_print_connectors_updated_by",
                table: "print_connectors",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_print_connectors_key_hash",
                table: "print_connectors",
                column: "key_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "print_connectors");
        }
    }
}
