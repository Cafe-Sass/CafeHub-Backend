using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentProofs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "media_reference",
                table: "payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "claimed_amount",
                table: "payments",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "payments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateCheckConstraint(
                name: "ck_payments_media_reference_length",
                table: "payments",
                sql: "media_reference IS NULL OR length(media_reference) <= 512");

            migrationBuilder.CreateCheckConstraint(
                name: "ck_payments_claimed_amount_range",
                table: "payments",
                sql: "claimed_amount IS NULL OR claimed_amount > 0");

            migrationBuilder.CreateCheckConstraint(
                name: "ck_payments_note_length",
                table: "payments",
                sql: "note IS NULL OR (length(note) >= 1 AND length(note) <= 140 AND note !~ '[[:cntrl:]]')");

            migrationBuilder.CreateCheckConstraint(
                name: "ck_payments_online_proof",
                table: "payments",
                sql: "method <> 'Online' OR (media_reference IS NOT NULL AND claimed_amount IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_payments_online_proof",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payments_note_length",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payments_claimed_amount_range",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payments_media_reference_length",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "note",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "claimed_amount",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "media_reference",
                table: "payments");
        }
    }
}
