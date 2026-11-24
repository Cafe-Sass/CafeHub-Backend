using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class ReceiptLineConfiguration : IEntityTypeConfiguration<ReceiptLine>
    {
        public void Configure(EntityTypeBuilder<ReceiptLine> builder)
        {
            builder.ToTable("receipt_lines", table =>
            {
                table.HasCheckConstraint("ck_receipt_lines_order_number", "order_number >= 1");
                table.HasCheckConstraint(
                    "ck_receipt_lines_guest_name",
                    "char_length(guest_display_name) BETWEEN 1 AND 64 AND guest_display_name !~ '[[:cntrl:]]'");
                table.HasCheckConstraint(
                    "ck_receipt_lines_product_name", "char_length(product_name) BETWEEN 1 AND 96");
                table.HasCheckConstraint("ck_receipt_lines_size_name", "char_length(size_name) BETWEEN 1 AND 32");
                table.HasCheckConstraint(
                    "ck_receipt_lines_addons",
                    "addons IS NULL OR (char_length(addons) BETWEEN 1 AND 512 AND addons !~ '[[:cntrl:]]')");
                table.HasCheckConstraint("ck_receipt_lines_quantity", "quantity BETWEEN 1 AND 50");
                table.HasCheckConstraint(
                    "ck_receipt_lines_unit_price", "unit_price >= 0 AND unit_price <= 99999999.99");
                table.HasCheckConstraint(
                    "ck_receipt_lines_line_total", "line_total >= 0 AND line_total <= 99999999.99");
                table.HasCheckConstraint(
                    "ck_receipt_lines_wifi_code",
                    "wifi_code IS NULL OR (char_length(wifi_code) BETWEEN 4 AND 64 AND wifi_code ~ '^[A-Za-z0-9-]+$')");
            });

            builder.HasKey(line => line.Id).HasName("pk_receipt_lines");
            builder.Property(line => line.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(line => line.ReceiptId).HasColumnName("receipt_id").HasColumnType("uuid");
            builder.Property(line => line.OrderNumber).HasColumnName("order_number").HasColumnType("integer");
            builder.Property(line => line.GuestDisplayName).HasColumnName("guest_display_name").HasColumnType("text");
            builder.Property(line => line.ProductName).HasColumnName("product_name").HasColumnType("text");
            builder.Property(line => line.SizeName).HasColumnName("size_name").HasColumnType("text");
            builder.Property(line => line.Addons).HasColumnName("addons").HasColumnType("text");
            builder.Property(line => line.Quantity).HasColumnName("quantity").HasColumnType("integer");
            builder.Property(line => line.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
            builder.Property(line => line.LineTotal).HasColumnName("line_total").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
            builder.Property(line => line.WifiCode).HasColumnName("wifi_code").HasColumnType("text");
            builder.Property(line => line.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            builder.HasIndex(line => line.ReceiptId).HasDatabaseName("ix_receipt_lines_receipt_id");
        }
    }
}
