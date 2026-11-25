using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class PrintJobConfiguration : IEntityTypeConfiguration<PrintJob>
    {
        public void Configure(EntityTypeBuilder<PrintJob> builder)
        {
            builder.ToTable("print_jobs", table =>
            {
                table.HasCheckConstraint(
                    "ck_print_jobs_document_type", "document_type IN ('OrderTicket', 'Receipt')");
                table.HasCheckConstraint(
                    "ck_print_jobs_source_type", "source_type IN ('Order', 'Bill')");
                table.HasCheckConstraint(
                    "ck_print_jobs_dedupe_key_length", "char_length(dedupe_key) BETWEEN 1 AND 160");
                table.HasCheckConstraint(
                    "ck_print_jobs_status",
                    "status IN ('Queued', 'Claimed', 'Succeeded', 'Failed', 'Cancelled')");
                table.HasCheckConstraint("ck_print_jobs_attempts", "attempts >= 0");
                table.HasCheckConstraint(
                    "ck_print_jobs_max_attempts", "max_attempts BETWEEN 1 AND 3");
                table.HasCheckConstraint(
                    "ck_print_jobs_last_error_code",
                    "last_error_code IS NULL OR last_error_code IN ('PrinterOffline', 'PaperOut', 'PayloadRejected', 'Unknown')");
            });

            builder.HasKey(job => job.Id).HasName("pk_print_jobs");
            builder.Property(job => job.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(job => job.BranchId).HasColumnName("branch_id").HasColumnType("uuid");
            builder.Property(job => job.PrinterId).HasColumnName("printer_id").HasColumnType("uuid");
            builder.Property(job => job.DocumentType)
                .HasColumnName("document_type").HasColumnType("text").HasConversion<string>();
            builder.Property(job => job.SourceType).HasColumnName("source_type").HasColumnType("text");
            builder.Property(job => job.SourceId).HasColumnName("source_id").HasColumnType("uuid");
            builder.Property(job => job.DedupeKey).HasColumnName("dedupe_key").HasColumnType("text");
            builder.Property(job => job.Status)
                .HasColumnName("status").HasColumnType("text").HasConversion<string>();
            builder.Property(job => job.Attempts).HasColumnName("attempts");
            builder.Property(job => job.MaxAttempts).HasColumnName("max_attempts");
            builder.Property(job => job.LastErrorCode).HasColumnName("last_error_code").HasColumnType("text");
            builder.Property(job => job.DocumentPayload).HasColumnName("document_payload").HasColumnType("jsonb");
            builder.Property(job => job.LeaseExpiresAt).HasColumnName("lease_expires_at").HasColumnType("timestamptz");
            builder.Property(job => job.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(job => job.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

            builder.HasIndex(job => job.BranchId).HasDatabaseName("ix_print_jobs_branch_id");
            builder.HasIndex(job => job.PrinterId).HasDatabaseName("ix_print_jobs_printer_id");
            builder.HasIndex(job => new { job.BranchId, job.DedupeKey })
                .HasDatabaseName("uq_print_jobs_branch_dedupe_key")
                .IsUnique();

            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(job => job.BranchId)
                .HasConstraintName("fk_print_jobs_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Printer>()
                .WithMany()
                .HasForeignKey(job => job.PrinterId)
                .HasConstraintName("fk_print_jobs_printer_id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
