using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class BranchWifiNetworkConfiguration : IEntityTypeConfiguration<BranchWifiNetwork>
    {
        public void Configure(EntityTypeBuilder<BranchWifiNetwork> builder)
        {
            builder.ToTable("branch_wifi_networks", table =>
            {
                table.HasCheckConstraint(
                    "ck_branch_wifi_networks_ssid",
                    "char_length(ssid) BETWEEN 1 AND 32 AND ssid !~ '[[:cntrl:]]'");
                table.HasCheckConstraint(
                    "ck_branch_wifi_networks_security", "security IN ('None', 'WPA', 'WEP')");
                table.HasCheckConstraint(
                    "ck_branch_wifi_networks_password",
                    "password IS NULL OR (char_length(password) BETWEEN 1 AND 63 AND password !~ '[[:cntrl:]]')");
            });

            builder.HasKey(network => network.BranchId).HasName("pk_branch_wifi_networks");
            builder.Property(network => network.BranchId)
                .HasColumnName("branch_id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(network => network.Ssid).HasColumnName("ssid").HasColumnType("text");
            builder.Property(network => network.Security)
                .HasColumnName("security").HasColumnType("text").HasConversion<string>();
            builder.Property(network => network.Password).HasColumnName("password").HasColumnType("text");
            builder.Property(network => network.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(network => network.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(network => network.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(network => network.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(network => network.CreatedBy)
                .HasDatabaseName("ix_branch_wifi_networks_created_by");
            builder.HasIndex(network => network.UpdatedBy)
                .HasDatabaseName("ix_branch_wifi_networks_updated_by");

            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(network => network.BranchId)
                .HasConstraintName("fk_branch_wifi_networks_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(network => network.CreatedBy)
                .HasConstraintName("fk_branch_wifi_networks_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(network => network.UpdatedBy)
                .HasConstraintName("fk_branch_wifi_networks_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
