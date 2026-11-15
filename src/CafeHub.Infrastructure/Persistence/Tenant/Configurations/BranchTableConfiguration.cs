using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class BranchTableConfiguration : IEntityTypeConfiguration<BranchTable>
    {
        public void Configure(EntityTypeBuilder<BranchTable> builder)
        {
            builder.ToTable("branch_tables", t =>
            {
                t.HasCheckConstraint("ck_branch_tables_number_range", "number >= 1 AND number <= 9999");
                t.HasCheckConstraint("ck_branch_tables_label_length", "label IS NULL OR (length(label) >= 1 AND length(label) <= 64)");
                t.HasCheckConstraint("ck_branch_tables_qr_code_format", "length(qr_code) = 22 AND qr_code ~ '^[A-Za-z0-9_-]{22}$'");
                t.HasCheckConstraint("ck_branch_tables_status", "status IN ('Active', 'Inactive')");
            });

            builder.HasKey(t => t.Id).HasName("pk_branch_tables");
            builder.Property(t => t.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

            builder.Property(t => t.BranchId).HasColumnName("branch_id").HasColumnType("uuid").IsRequired();
            builder.Property(t => t.Number).HasColumnName("number").HasColumnType("integer").IsRequired();
            builder.Property(t => t.Label).HasColumnName("label").HasColumnType("text").IsRequired(false);
            builder.Property(t => t.QrCode).HasColumnName("qr_code").HasColumnType("text").IsRequired();
            builder.Property(t => t.Status).HasColumnName("status").HasColumnType("text").HasConversion<string>().IsRequired();

            builder.Property(t => t.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(t => t.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(t => t.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.HasIndex(t => t.BranchId).HasDatabaseName("ix_branch_tables_branch_id");
            builder.HasIndex(t => t.CreatedBy).HasDatabaseName("ix_branch_tables_created_by");
            builder.HasIndex(t => t.UpdatedBy).HasDatabaseName("ix_branch_tables_updated_by");
            builder.HasIndex(t => t.QrCode).HasDatabaseName("uq_branch_tables_qr_code").IsUnique();

            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(t => t.BranchId)
                .HasConstraintName("fk_branch_tables_branch_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(t => t.CreatedBy)
                .HasConstraintName("fk_branch_tables_created_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(t => t.UpdatedBy)
                .HasConstraintName("fk_branch_tables_updated_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<uint>("xmin").IsRowVersion();
        }
    }
}
