using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddPrinters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "printers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    station_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    profile = table.Column<string>(type: "text", nullable: false),
                    connection_kind = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_printers", x => x.id);
                    table.CheckConstraint("ck_printers_connection_kind", "char_length(connection_kind) BETWEEN 1 AND 32 AND connection_kind !~ '[[:cntrl:]]'");
                    table.CheckConstraint("ck_printers_name", "char_length(name) BETWEEN 1 AND 64 AND name !~ '[[:cntrl:]]'");
                    table.CheckConstraint("ck_printers_profile", "profile IN ('Escpos80mm', 'Escpos58mm')");
                    table.CheckConstraint("ck_printers_status", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_printers_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_printers_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_printers_station_id",
                        column: x => x.station_id,
                        principalTable: "stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_printers_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "print_routes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    station_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_type = table.Column<string>(type: "text", nullable: false),
                    printer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_print_routes", x => x.id);
                    table.CheckConstraint("ck_print_routes_document_type", "document_type IN ('OrderTicket', 'Receipt')");
                    table.ForeignKey(
                        name: "fk_print_routes_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_print_routes_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_print_routes_printer_id",
                        column: x => x.printer_id,
                        principalTable: "printers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_print_routes_station_id",
                        column: x => x.station_id,
                        principalTable: "stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_print_routes_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_print_routes_branch_id",
                table: "print_routes",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_print_routes_created_by",
                table: "print_routes",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_print_routes_printer_id",
                table: "print_routes",
                column: "printer_id");

            migrationBuilder.CreateIndex(
                name: "ix_print_routes_station_id",
                table: "print_routes",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "ix_print_routes_updated_by",
                table: "print_routes",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_printers_branch_id",
                table: "printers",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_printers_created_by",
                table: "printers",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_printers_station_id",
                table: "printers",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "ix_printers_updated_by",
                table: "printers",
                column: "updated_by");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_print_routes_branch_station_document ON print_routes (branch_id, station_id, document_type) NULLS NOT DISTINCT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS uq_print_routes_branch_station_document");
            migrationBuilder.DropTable(
                name: "print_routes");

            migrationBuilder.DropTable(
                name: "printers");
        }
    }
}
