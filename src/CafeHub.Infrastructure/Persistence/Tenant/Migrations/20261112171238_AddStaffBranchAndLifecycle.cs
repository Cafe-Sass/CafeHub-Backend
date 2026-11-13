using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffBranchAndLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                table: "tenant_users",
                type: "uuid",
                nullable: true);

            // Note: `xmin` is a PostgreSQL system column that already exists on every table;
            // mapping it as a row version reads it without adding any column or DDL.

            migrationBuilder.CreateIndex(
                name: "ix_tenant_users_branch_id",
                table: "tenant_users",
                column: "branch_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_users_branch_scope",
                table: "tenant_users",
                sql: "(user_kind = 'Staff' AND branch_id IS NOT NULL) OR (user_kind = 'Owner' AND branch_id IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_tenant_users_branch_id",
                table: "tenant_users",
                column: "branch_id",
                principalTable: "branches",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tenant_users_branch_id",
                table: "tenant_users");

            migrationBuilder.DropIndex(
                name: "ix_tenant_users_branch_id",
                table: "tenant_users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_users_branch_scope",
                table: "tenant_users");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "tenant_users");
        }
    }
}
