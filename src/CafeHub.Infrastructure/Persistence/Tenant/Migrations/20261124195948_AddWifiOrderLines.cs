using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddWifiOrderLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_service_type",
                table: "orders");

            migrationBuilder.AlterColumn<Guid>(
                name: "table_id",
                table: "orders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "wifi_duration_minutes",
                table: "order_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "wifi_package_id",
                table: "order_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "wifi_speed_mbps",
                table: "order_items",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "table_id",
                table: "bills",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_service_table",
                table: "orders",
                sql: "(service_type = 'WifiOnly' AND table_id IS NULL) OR (service_type <> 'WifiOnly' AND table_id IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_service_type",
                table: "orders",
                sql: "service_type IN ('DineIn', 'Takeaway', 'WifiOnly')");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_wifi_package_id",
                table: "order_items",
                column: "wifi_package_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_items_product_or_wifi",
                table: "order_items",
                sql: "(product_id IS NULL) <> (wifi_package_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_items_wifi_snapshot",
                table: "order_items",
                sql: "wifi_package_id IS NULL OR (wifi_duration_minutes IS NOT NULL AND wifi_duration_minutes BETWEEN 1 AND 43200)");

            migrationBuilder.AddForeignKey(
                name: "fk_order_items_wifi_package_id",
                table: "order_items",
                column: "wifi_package_id",
                principalTable: "wifi_packages",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_order_items_wifi_package_id",
                table: "order_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_service_table",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_service_type",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_order_items_wifi_package_id",
                table: "order_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_items_product_or_wifi",
                table: "order_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_items_wifi_snapshot",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "wifi_duration_minutes",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "wifi_package_id",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "wifi_speed_mbps",
                table: "order_items");

            migrationBuilder.AlterColumn<Guid>(
                name: "table_id",
                table: "orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "table_id",
                table: "bills",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_service_type",
                table: "orders",
                sql: "service_type IN ('DineIn', 'Takeaway')");
        }
    }
}
