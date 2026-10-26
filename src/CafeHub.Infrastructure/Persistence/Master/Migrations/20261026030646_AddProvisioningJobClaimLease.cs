using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Master.Migrations
{
    /// <inheritdoc />
    public partial class AddProvisioningJobClaimLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "due_at",
                table: "provisioning_jobs",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lease_expires_at",
                table: "provisioning_jobs",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "leased_by",
                table: "provisioning_jobs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "due_at",
                table: "provisioning_jobs");

            migrationBuilder.DropColumn(
                name: "lease_expires_at",
                table: "provisioning_jobs");

            migrationBuilder.DropColumn(
                name: "leased_by",
                table: "provisioning_jobs");
        }
    }
}
