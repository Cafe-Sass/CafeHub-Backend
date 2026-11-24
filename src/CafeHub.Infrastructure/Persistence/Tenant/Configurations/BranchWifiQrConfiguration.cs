using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class BranchWifiQrConfiguration : IEntityTypeConfiguration<BranchWifiQr>
    {
        public void Configure(EntityTypeBuilder<BranchWifiQr> builder)
        {
            builder.ToTable("branch_wifi_qr", table =>
            {
                table.HasCheckConstraint(
                    "ck_branch_wifi_qr_code_format",
                    "char_length(qr_code) = 22 AND qr_code ~ '^[A-Za-z0-9_-]{22}$'");
                table.HasCheckConstraint(
                    "ck_branch_wifi_qr_status", "status IN ('Active', 'Inactive')");
            });

            builder.HasKey(qr => qr.BranchId).HasName("pk_branch_wifi_qr");
            builder.Property(qr => qr.BranchId).HasColumnName("branch_id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(qr => qr.QrCode).HasColumnName("qr_code").HasColumnType("text");
            builder.Property(qr => qr.Status).HasColumnName("status").HasColumnType("text").HasConversion<string>();
            builder.Property(qr => qr.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(qr => qr.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(qr => qr.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(qr => qr.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(qr => qr.QrCode)
                .HasDatabaseName("uq_branch_wifi_qr_code")
                .IsUnique();
            builder.HasIndex(qr => qr.CreatedBy).HasDatabaseName("ix_branch_wifi_qr_created_by");
            builder.HasIndex(qr => qr.UpdatedBy).HasDatabaseName("ix_branch_wifi_qr_updated_by");

            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(qr => qr.BranchId)
                .HasConstraintName("fk_branch_wifi_qr_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(qr => qr.CreatedBy)
                .HasConstraintName("fk_branch_wifi_qr_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(qr => qr.UpdatedBy)
                .HasConstraintName("fk_branch_wifi_qr_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
