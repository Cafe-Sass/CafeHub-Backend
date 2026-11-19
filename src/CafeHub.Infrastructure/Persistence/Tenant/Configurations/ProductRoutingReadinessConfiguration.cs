using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class ProductRoutingReadinessConfiguration : IEntityTypeConfiguration<ProductRoutingReadiness>
    {
        public void Configure(EntityTypeBuilder<ProductRoutingReadiness> builder)
        {
            builder.ToTable("product_routing_readiness", table =>
                table.HasCheckConstraint("ck_product_routing_readiness_singleton", "singleton"));
            builder.HasKey(row => row.Singleton).HasName("pk_product_routing_readiness");
            builder.Property(row => row.Singleton).HasColumnName("singleton").ValueGeneratedNever();
            builder.Property(row => row.VerifiedAt).HasColumnName("verified_at");
        }
    }
}
