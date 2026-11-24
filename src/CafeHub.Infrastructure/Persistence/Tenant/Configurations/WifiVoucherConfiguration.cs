using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class WifiVoucherConfiguration : IEntityTypeConfiguration<WifiVoucher>
    {
        public void Configure(EntityTypeBuilder<WifiVoucher> builder)
        {
            builder.ToTable("wifi_vouchers", table =>
            {
                table.HasCheckConstraint(
                    "ck_wifi_vouchers_code",
                    "char_length(code) BETWEEN 4 AND 64 AND code ~ '^[A-Za-z0-9-]+$'");
                table.HasCheckConstraint(
                    "ck_wifi_vouchers_status", "status IN ('Available', 'Assigned', 'Void')");
                table.HasCheckConstraint(
                    "ck_wifi_vouchers_assignment_pair",
                    "(status = 'Assigned') = (assigned_order_item_id IS NOT NULL AND assigned_at IS NOT NULL)");
            });

            builder.HasKey(voucher => voucher.Id).HasName("pk_wifi_vouchers");
            builder.Property(voucher => voucher.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(voucher => voucher.PackageId).HasColumnName("package_id").HasColumnType("uuid");
            builder.Property(voucher => voucher.Code).HasColumnName("code").HasColumnType("text");
            builder.Property(voucher => voucher.Status)
                .HasColumnName("status").HasColumnType("text").HasConversion<string>();
            builder.Property(voucher => voucher.AssignedOrderItemId)
                .HasColumnName("assigned_order_item_id").HasColumnType("uuid");
            builder.Property(voucher => voucher.AssignedAt).HasColumnName("assigned_at").HasColumnType("timestamptz");
            builder.Property(voucher => voucher.ImportedBy).HasColumnName("imported_by").HasColumnType("uuid");
            builder.Property(voucher => voucher.ImportedAt).HasColumnName("imported_at").HasColumnType("timestamptz");
            builder.Property(voucher => voucher.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

            builder.HasIndex(voucher => new { voucher.PackageId, voucher.Code })
                .HasDatabaseName("uq_wifi_vouchers_package_id_code")
                .IsUnique();
            builder.HasIndex(voucher => voucher.AssignedOrderItemId)
                .HasDatabaseName("uq_wifi_vouchers_assigned_order_item_id")
                .IsUnique();
            builder.HasIndex(voucher => voucher.ImportedBy)
                .HasDatabaseName("ix_wifi_vouchers_imported_by");

            builder.HasOne<WifiPackage>()
                .WithMany()
                .HasForeignKey(voucher => voucher.PackageId)
                .HasConstraintName("fk_wifi_vouchers_package_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<OrderItem>()
                .WithMany()
                .HasForeignKey(voucher => voucher.AssignedOrderItemId)
                .HasConstraintName("fk_wifi_vouchers_assigned_order_item_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(voucher => voucher.ImportedBy)
                .HasConstraintName("fk_wifi_vouchers_imported_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
