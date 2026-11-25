using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class PrinterConfiguration : IEntityTypeConfiguration<Printer>
    {
        public void Configure(EntityTypeBuilder<Printer> builder)
        {
            builder.ToTable("printers", table =>
            {
                table.HasCheckConstraint(
                    "ck_printers_name", "char_length(name) BETWEEN 1 AND 64 AND name !~ '[[:cntrl:]]'");
                table.HasCheckConstraint(
                    "ck_printers_profile", "profile IN ('Escpos80mm', 'Escpos58mm')");
                table.HasCheckConstraint(
                    "ck_printers_connection_kind",
                    "char_length(connection_kind) BETWEEN 1 AND 32 AND connection_kind !~ '[[:cntrl:]]'");
                table.HasCheckConstraint("ck_printers_status", "status IN ('Active', 'Inactive')");
            });

            builder.HasKey(printer => printer.Id).HasName("pk_printers");
            builder.Property(printer => printer.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(printer => printer.BranchId).HasColumnName("branch_id").HasColumnType("uuid");
            builder.Property(printer => printer.StationId).HasColumnName("station_id").HasColumnType("uuid");
            builder.Property(printer => printer.Name).HasColumnName("name").HasColumnType("text");
            builder.Property(printer => printer.Profile)
                .HasColumnName("profile").HasColumnType("text").HasConversion<string>();
            builder.Property(printer => printer.ConnectionKind).HasColumnName("connection_kind").HasColumnType("text");
            builder.Property(printer => printer.Status)
                .HasColumnName("status").HasColumnType("text").HasConversion<string>();
            builder.Property(printer => printer.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(printer => printer.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            builder.Property(printer => printer.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(printer => printer.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");

            builder.HasIndex(printer => printer.BranchId).HasDatabaseName("ix_printers_branch_id");
            builder.HasIndex(printer => printer.StationId).HasDatabaseName("ix_printers_station_id");
            builder.HasIndex(printer => printer.CreatedBy).HasDatabaseName("ix_printers_created_by");
            builder.HasIndex(printer => printer.UpdatedBy).HasDatabaseName("ix_printers_updated_by");

            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(printer => printer.BranchId)
                .HasConstraintName("fk_printers_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Station>()
                .WithMany()
                .HasForeignKey(printer => printer.StationId)
                .HasConstraintName("fk_printers_station_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(printer => printer.CreatedBy)
                .HasConstraintName("fk_printers_created_by")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(printer => printer.UpdatedBy)
                .HasConstraintName("fk_printers_updated_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<uint>("xmin").IsRowVersion();
        }
    }
}
