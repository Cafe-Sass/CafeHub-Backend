using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Configurations
{
    public sealed class TenantDatabaseConfiguration : IEntityTypeConfiguration<global::CafeHub.Domain.Tenants.TenantDatabase>
    {
        public void Configure(EntityTypeBuilder<global::CafeHub.Domain.Tenants.TenantDatabase> builder)
        {
            builder.ToTable("tenant_databases", table =>
            {
                table.HasCheckConstraint("ck_tenant_databases_secret_reference_format", "secret_reference ~ '^[a-z0-9][a-z0-9-]*$'");
                table.HasCheckConstraint("ck_tenant_databases_secret_reference_length", "length(secret_reference) >= 1 AND length(secret_reference) <= 128");
                table.HasCheckConstraint("ck_tenant_databases_status", "status IN ('Pending', 'Provisioned', 'Failed')");
            });

            builder.HasKey(database => database.Id).HasName("pk_tenant_databases");
            builder.Property(database => database.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(database => database.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
            builder.Property(database => database.SecretReference).HasColumnName("secret_reference").HasColumnType("text").IsRequired();
            builder.Property(database => database.Status).HasColumnName("status").HasColumnType("text").HasConversion<string>().IsRequired();
            builder.Property(database => database.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(database => database.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(database => database.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(database => database.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.HasIndex(database => database.TenantId)
                .IsUnique()
                .HasDatabaseName("uq_tenant_databases_tenant_id");
            builder.HasIndex(database => database.SecretReference)
                .IsUnique()
                .HasDatabaseName("uq_tenant_databases_secret_reference");

            builder.HasOne<global::CafeHub.Domain.Tenants.Tenant>()
                .WithMany()
                .HasForeignKey(database => database.TenantId)
                .HasConstraintName("fk_tenant_databases_tenant_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(database => database.CreatedBy)
                .HasConstraintName("fk_tenant_databases_created_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(database => database.UpdatedBy)
                .HasConstraintName("fk_tenant_databases_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
