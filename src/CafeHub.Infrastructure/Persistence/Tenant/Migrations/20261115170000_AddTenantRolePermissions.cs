using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "tenant_roles",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by",
                table: "tenant_roles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_tenant_user_claims_permission",
                table: "tenant_user_claims",
                columns: new[] { "tenant_user_id", "claim_value" },
                unique: true,
                filter: "claim_type = 'cafehub.permission' AND claim_value IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_roles_deleted_by",
                table: "tenant_roles",
                column: "deleted_by");

            migrationBuilder.CreateIndex(
                name: "uq_tenant_role_claims_permission",
                table: "tenant_role_claims",
                columns: new[] { "role_id", "claim_value" },
                unique: true,
                filter: "claim_type = 'cafehub.permission' AND claim_value IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_tenant_roles_deleted_by",
                table: "tenant_roles",
                column: "deleted_by",
                principalTable: "tenant_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tenant_roles_deleted_by",
                table: "tenant_roles");

            migrationBuilder.DropIndex(
                name: "uq_tenant_user_claims_permission",
                table: "tenant_user_claims");

            migrationBuilder.DropIndex(
                name: "ix_tenant_roles_deleted_by",
                table: "tenant_roles");

            migrationBuilder.DropIndex(
                name: "uq_tenant_role_claims_permission",
                table: "tenant_role_claims");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "tenant_roles");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "tenant_roles");

        }
    }
}
