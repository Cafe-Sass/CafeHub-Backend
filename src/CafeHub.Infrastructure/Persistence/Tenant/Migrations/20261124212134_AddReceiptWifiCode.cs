using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptWifiCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "wifi_code",
                table: "receipt_lines",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipt_lines_wifi_code",
                table: "receipt_lines",
                sql: "wifi_code IS NULL OR (char_length(wifi_code) BETWEEN 4 AND 64 AND wifi_code ~ '^[A-Za-z0-9-]+$')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_receipt_lines_wifi_code",
                table: "receipt_lines");

            migrationBuilder.DropColumn(
                name: "wifi_code",
                table: "receipt_lines");
        }
    }
}
