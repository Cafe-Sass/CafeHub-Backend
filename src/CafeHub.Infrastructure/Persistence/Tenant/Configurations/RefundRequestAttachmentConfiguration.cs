using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class RefundRequestAttachmentConfiguration : IEntityTypeConfiguration<RefundRequestAttachment>
    {
        public void Configure(EntityTypeBuilder<RefundRequestAttachment> builder)
        {
            builder.ToTable("refund_request_attachments", table =>
            {
                table.HasCheckConstraint(
                    "ck_refund_request_attachments_media_key_length",
                    "char_length(media_key) BETWEEN 1 AND 512");
                table.HasCheckConstraint(
                    "ck_refund_request_attachments_content_type",
                    "content_type IN ('image/jpeg', 'image/png', 'image/webp', 'application/pdf')");
                table.HasCheckConstraint("ck_refund_request_attachments_size", "size_bytes > 0");
                table.HasCheckConstraint(
                    "ck_refund_request_attachments_sha256",
                    "sha256 ~ '^[0-9a-f]{64}$'");
            });

            builder.HasKey(attachment => attachment.Id).HasName("pk_refund_request_attachments");
            builder.Property(attachment => attachment.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(attachment => attachment.RefundRequestId).HasColumnName("refund_request_id").HasColumnType("uuid");
            builder.Property(attachment => attachment.MediaKey).HasColumnName("media_key").HasColumnType("text");
            builder.Property(attachment => attachment.ContentType).HasColumnName("content_type").HasColumnType("text");
            builder.Property(attachment => attachment.SizeBytes).HasColumnName("size_bytes").HasColumnType("bigint");
            builder.Property(attachment => attachment.Sha256).HasColumnName("sha256").HasColumnType("text");
            builder.Property(attachment => attachment.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");

            builder.HasIndex(attachment => attachment.RefundRequestId)
                .HasDatabaseName("ix_refund_request_attachments_refund_request_id");
        }
    }
}
