using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class PrintRouteConfiguration : IEntityTypeConfiguration<PrintRoute>
    {
        public void Configure(EntityTypeBuilder<PrintRoute> builder)
        {
            builder.ToTable("print_routes", table =>
            {
                table.HasCheckConstraint(
                    "ck_print_routes_document_type", "document_type IN ('OrderTicket', 'Receipt')");
            });

            builder.HasKey(route => route.Id).HasName("pk_print_routes");
            builder.Property(route => route.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(route => route.BranchId).HasColumnName("branch_id").HasColumnType("uuid");
            builder.Property(route => route.StationId).HasColumnName("station_id").HasColumnType("uuid");
            builder.Property(route => route.DocumentType)
                .HasColumnName("document_type").HasColumnType("text").HasConversion<string>();
            builder.Property(route => route.PrinterId).HasColumnName("printer_id").HasColumnType("uuid");
            builder.Property(route => route.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(route => route.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(route => route.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(route => route.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(route => route.BranchId).HasDatabaseName("ix_print_routes_branch_id");
            builder.HasIndex(route => route.StationId).HasDatabaseName("ix_print_routes_station_id");
            builder.HasIndex(route => route.PrinterId).HasDatabaseName("ix_print_routes_printer_id");
            builder.HasIndex(route => route.CreatedBy).HasDatabaseName("ix_print_routes_created_by");
            builder.HasIndex(route => route.UpdatedBy).HasDatabaseName("ix_print_routes_updated_by");

            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(route => route.BranchId)
                .HasConstraintName("fk_print_routes_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Station>()
                .WithMany()
                .HasForeignKey(route => route.StationId)
                .HasConstraintName("fk_print_routes_station_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Printer>()
                .WithMany()
                .HasForeignKey(route => route.PrinterId)
                .HasConstraintName("fk_print_routes_printer_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(route => route.CreatedBy)
                .HasConstraintName("fk_print_routes_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(route => route.UpdatedBy)
                .HasConstraintName("fk_print_routes_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
