using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class PrintConnectorConfiguration : IEntityTypeConfiguration<PrintConnector>
    {
        public void Configure(EntityTypeBuilder<PrintConnector> builder)
        {
            builder.ToTable("print_connectors", table =>
            {
                table.HasCheckConstraint(
                    "ck_print_connectors_name",
                    "char_length(name) BETWEEN 1 AND 64 AND name !~ '[[:cntrl:]]'");
                table.HasCheckConstraint(
                    "ck_print_connectors_key_hash",
                    "char_length(key_hash) = 64");
                table.HasCheckConstraint(
                    "ck_print_connectors_status",
                    "status IN ('Active', 'Revoked')");
            });

            builder.HasKey(connector => connector.Id).HasName("pk_print_connectors");
            builder.Property(connector => connector.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(connector => connector.BranchId).HasColumnName("branch_id").HasColumnType("uuid");
            builder.Property(connector => connector.Name).HasColumnName("name").HasColumnType("text");
            builder.Property(connector => connector.KeyHash).HasColumnName("key_hash").HasColumnType("text");
            builder.Property(connector => connector.Status)
                .HasColumnName("status").HasColumnType("text").HasConversion<string>();
            builder.Property(connector => connector.LastSeenAt).HasColumnName("last_seen_at").HasColumnType("timestamptz");
            builder.Property(connector => connector.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(connector => connector.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(connector => connector.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(connector => connector.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(connector => connector.BranchId).HasDatabaseName("ix_print_connectors_branch_id");
            builder.HasIndex(connector => connector.KeyHash)
                .HasDatabaseName("uq_print_connectors_key_hash")
                .IsUnique();
            builder.HasIndex(connector => connector.CreatedBy).HasDatabaseName("ix_print_connectors_created_by");
            builder.HasIndex(connector => connector.UpdatedBy).HasDatabaseName("ix_print_connectors_updated_by");

            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(connector => connector.BranchId)
                .HasConstraintName("fk_print_connectors_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(connector => connector.CreatedBy)
                .HasConstraintName("fk_print_connectors_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(connector => connector.UpdatedBy)
                .HasConstraintName("fk_print_connectors_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
