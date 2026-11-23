using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddBills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "bill_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    table_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    public_reference = table.Column<string>(type: "text", nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    close_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bills", x => x.id);
                    table.CheckConstraint("ck_bills_close_pair", "(closed_at IS NULL AND closed_by_user_id IS NULL AND close_reason IS NULL) OR (closed_at IS NOT NULL AND closed_by_user_id IS NOT NULL AND close_reason IS NOT NULL)");
                    table.CheckConstraint("ck_bills_close_reason", "close_reason IS NULL OR (status = 'Closed' AND char_length(close_reason) BETWEEN 3 AND 256)");
                    table.CheckConstraint("ck_bills_public_reference_format", "char_length(public_reference) = 22 AND public_reference ~ '^[A-Za-z0-9_-]{22}$'");
                    table.CheckConstraint("ck_bills_status", "status IN ('Open', 'Closed')");
                    table.ForeignKey(
                        name: "fk_bills_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bills_closed_by_user_id",
                        column: x => x.closed_by_user_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bills_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bills_table_id",
                        column: x => x.table_id,
                        principalTable: "branch_tables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bills_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orders_bill_id",
                table: "orders",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_bills_branch_id",
                table: "bills",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_bills_closed_by_user_id",
                table: "bills",
                column: "closed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_bills_created_by",
                table: "bills",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_bills_updated_by",
                table: "bills",
                column: "updated_by");

            migrationBuilder.Sql(
                """
                CREATE INDEX ix_bills_table_id ON bills (table_id);
                """);

            migrationBuilder.CreateIndex(
                name: "uq_bills_public_reference",
                table: "bills",
                column: "public_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_bills_table_open",
                table: "bills",
                column: "table_id",
                unique: true,
                filter: "status = 'Open'");

            migrationBuilder.AddForeignKey(
                name: "fk_orders_bill_id",
                table: "orders",
                column: "bill_id",
                principalTable: "bills",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_orders_bill_id",
                table: "orders");

            migrationBuilder.Sql(
                """
                DROP INDEX ix_bills_table_id;
                """);

            migrationBuilder.DropTable(
                name: "bills");

            migrationBuilder.DropIndex(
                name: "ix_orders_bill_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "bill_id",
                table: "orders");
        }
    }
}
