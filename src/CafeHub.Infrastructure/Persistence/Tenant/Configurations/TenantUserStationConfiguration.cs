using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class TenantUserStationConfiguration : IEntityTypeConfiguration<TenantUserStation>
    {
        public void Configure(EntityTypeBuilder<TenantUserStation> builder)
        {
            builder.ToTable("tenant_user_stations");

            builder.HasKey(t => new { t.TenantUserId, t.StationId }).HasName("pk_tenant_user_stations");
            builder.Property(t => t.TenantUserId).HasColumnName("tenant_user_id").HasColumnType("uuid").IsRequired();
            builder.Property(t => t.StationId).HasColumnName("station_id").HasColumnType("uuid").IsRequired();
            builder.Property(t => t.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();

            builder.HasIndex(t => t.StationId).HasDatabaseName("ix_tenant_user_stations_station_id");

            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(t => t.TenantUserId)
                .HasConstraintName("fk_tenant_user_stations_tenant_user_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Station>()
                .WithMany()
                .HasForeignKey(t => t.StationId)
                .HasConstraintName("fk_tenant_user_stations_station_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
