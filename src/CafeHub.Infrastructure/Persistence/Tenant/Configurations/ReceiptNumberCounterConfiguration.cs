using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class ReceiptNumberCounterConfiguration : IEntityTypeConfiguration<ReceiptNumberCounter>
    {
        public void Configure(EntityTypeBuilder<ReceiptNumberCounter> builder)
        {
            builder.ToTable("receipt_number_counters", table =>
            {
                table.HasCheckConstraint("ck_receipt_number_counters_last_number", "last_number >= 1");
            });

            builder.HasKey(counter => new { counter.BranchId, counter.BusinessDate })
                .HasName("pk_receipt_number_counters");
            builder.Property(counter => counter.BranchId)
                .HasColumnName("branch_id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(counter => counter.BusinessDate)
                .HasColumnName("business_date").HasColumnType("date").ValueGeneratedNever();
            builder.Property(counter => counter.LastNumber).HasColumnName("last_number").HasColumnType("integer");
            builder.Property(counter => counter.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(counter => counter.BranchId)
                .HasConstraintName("fk_receipt_number_counters_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
