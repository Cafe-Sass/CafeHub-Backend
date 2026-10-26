using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
    {
        public void Configure(EntityTypeBuilder<Branch> builder)
        {
            builder.ToTable("branches", t =>
            {
                t.HasCheckConstraint("ck_branches_name_length", "length(name) >= 1 AND length(name) <= 256");
                t.HasCheckConstraint("ck_branches_lifecycle_status", "lifecycle_status IN ('Active', 'Suspended', 'Retired')");
                t.HasCheckConstraint("ck_branches_address_length", "address IS NULL OR length(address) <= 512");
                t.HasCheckConstraint("ck_branches_contact_phone_length", "contact_phone IS NULL OR length(contact_phone) <= 32");
                t.HasCheckConstraint("ck_branches_opening_hours_summary_length", "opening_hours_summary IS NULL OR length(opening_hours_summary) <= 512");
            });

            builder.HasKey(b => b.Id).HasName("pk_branches");
            builder.Property(b => b.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

            builder.Property(b => b.Name).HasColumnName("name").HasColumnType("text").IsRequired();
            builder.Property(b => b.LifecycleStatus).HasColumnName("lifecycle_status").HasColumnType("text").HasConversion<string>().IsRequired();
            builder.Property(b => b.Address).HasColumnName("address").HasColumnType("text").IsRequired(false);
            builder.Property(b => b.ContactPhone).HasColumnName("contact_phone").HasColumnType("text").IsRequired(false);
            builder.Property(b => b.OpeningHoursSummary).HasColumnName("opening_hours_summary").HasColumnType("text").IsRequired(false);

            builder.Property(b => b.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(b => b.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(b => b.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(b => b.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.HasIndex(b => b.CreatedBy).HasDatabaseName("ix_branches_created_by");
            builder.HasIndex(b => b.UpdatedBy).HasDatabaseName("ix_branches_updated_by");

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(b => b.CreatedBy)
                .HasConstraintName("fk_branches_created_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(b => b.UpdatedBy)
                .HasConstraintName("fk_branches_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
