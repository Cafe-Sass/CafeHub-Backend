using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class StationConfiguration : IEntityTypeConfiguration<Station>
    {
        public void Configure(EntityTypeBuilder<Station> builder)
        {
            builder.ToTable("stations", t =>
            {
                t.HasCheckConstraint("ck_stations_name_length", "length(name) >= 1 AND length(name) <= 64");
                t.HasCheckConstraint("ck_stations_status", "status IN ('Active', 'Inactive')");
                t.HasCheckConstraint("ck_stations_sort_order", "sort_order >= 0");
            });

            builder.HasKey(s => s.Id).HasName("pk_stations");
            builder.Property(s => s.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

            builder.Property(s => s.BranchId).HasColumnName("branch_id").HasColumnType("uuid").IsRequired();
            builder.Property(s => s.Name).HasColumnName("name").HasColumnType("text").IsRequired();
            builder.Property(s => s.Status).HasColumnName("status").HasColumnType("text").HasConversion<string>().IsRequired();
            builder.Property(s => s.SortOrder).HasColumnName("sort_order").HasColumnType("integer").IsRequired();

            builder.Property(s => s.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.HasIndex(s => s.BranchId).HasDatabaseName("ix_stations_branch_id");
            builder.HasIndex(s => s.CreatedBy).HasDatabaseName("ix_stations_created_by");
            builder.HasIndex(s => s.UpdatedBy).HasDatabaseName("ix_stations_updated_by");

            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(s => s.BranchId)
                .HasConstraintName("fk_stations_branch_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(s => s.CreatedBy)
                .HasConstraintName("fk_stations_created_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(s => s.UpdatedBy)
                .HasConstraintName("fk_stations_updated_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<uint>("xmin").IsRowVersion();
        }
    }
}
