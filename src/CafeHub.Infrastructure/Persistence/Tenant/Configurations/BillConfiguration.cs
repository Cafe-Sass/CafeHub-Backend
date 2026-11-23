using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class BillConfiguration : IEntityTypeConfiguration<Bill>
    {
        public void Configure(EntityTypeBuilder<Bill> builder)
        {
            builder.ToTable("bills", table =>
            {
                table.HasCheckConstraint("ck_bills_status", "status IN ('Open', 'Closed')");
                table.HasCheckConstraint(
                    "ck_bills_public_reference_format",
                    "char_length(public_reference) = 22 AND public_reference ~ '^[A-Za-z0-9_-]{22}$'");
                table.HasCheckConstraint(
                    "ck_bills_close_pair",
                    "(closed_at IS NULL AND closed_by_user_id IS NULL AND close_reason IS NULL) OR (closed_at IS NOT NULL AND closed_by_user_id IS NOT NULL AND close_reason IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_bills_close_reason",
                    "close_reason IS NULL OR (status = 'Closed' AND char_length(close_reason) BETWEEN 3 AND 256)");
            });

            builder.HasKey(bill => bill.Id).HasName("pk_bills");
            builder.Property(bill => bill.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(bill => bill.BranchId).HasColumnName("branch_id").HasColumnType("uuid");
            builder.Property(bill => bill.TableId).HasColumnName("table_id").HasColumnType("uuid");
            builder.Property(bill => bill.Status).HasColumnName("status").HasColumnType("text").HasConversion<string>();
            builder.Property(bill => bill.PublicReference).HasColumnName("public_reference").HasColumnType("text");
            builder.Property(bill => bill.OpenedAt).HasColumnName("opened_at").HasColumnType("timestamptz");
            builder.Property(bill => bill.ClosedAt).HasColumnName("closed_at").HasColumnType("timestamptz");
            builder.Property(bill => bill.ClosedByUserId).HasColumnName("closed_by_user_id").HasColumnType("uuid");
            builder.Property(bill => bill.CloseReason).HasColumnName("close_reason").HasColumnType("text");
            builder.Property(bill => bill.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(bill => bill.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(bill => bill.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(bill => bill.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(bill => bill.TableId)
                .HasDatabaseName("uq_bills_table_open")
                .IsUnique()
                .HasFilter("status = 'Open'");
            builder.HasIndex(bill => bill.PublicReference)
                .HasDatabaseName("uq_bills_public_reference")
                .IsUnique();
            builder.HasIndex(bill => bill.BranchId).HasDatabaseName("ix_bills_branch_id");
            builder.HasIndex(bill => bill.ClosedByUserId).HasDatabaseName("ix_bills_closed_by_user_id");
            builder.HasIndex(bill => bill.CreatedBy).HasDatabaseName("ix_bills_created_by");
            builder.HasIndex(bill => bill.UpdatedBy).HasDatabaseName("ix_bills_updated_by");

            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(bill => bill.BranchId)
                .HasConstraintName("fk_bills_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<BranchTable>()
                .WithMany()
                .HasForeignKey(bill => bill.TableId)
                .HasConstraintName("fk_bills_table_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(bill => bill.ClosedByUserId)
                .HasConstraintName("fk_bills_closed_by_user_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(bill => bill.CreatedBy)
                .HasConstraintName("fk_bills_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(bill => bill.UpdatedBy)
                .HasConstraintName("fk_bills_updated_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<uint>("xmin").IsRowVersion();
        }
    }
}
