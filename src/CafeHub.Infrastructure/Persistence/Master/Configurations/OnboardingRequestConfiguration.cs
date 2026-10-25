using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Plans;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Master.Configurations
{
    public sealed class OnboardingRequestConfiguration : IEntityTypeConfiguration<OnboardingRequest>
    {
        public void Configure(EntityTypeBuilder<OnboardingRequest> builder)
        {
            builder.ToTable("onboarding_requests", table =>
            {
                table.HasCheckConstraint("ck_onboarding_requests_created_via", "created_via IN ('PublicRegistration', 'Support')");
                table.HasCheckConstraint("ck_onboarding_requests_support_actor", "created_via = 'PublicRegistration' OR created_by IS NOT NULL");
                table.HasCheckConstraint("ck_onboarding_requests_owner_email_length", "length(owner_email) >= 1 AND length(owner_email) <= 320");
                table.HasCheckConstraint("ck_onboarding_requests_owner_full_name_length", "length(owner_full_name) >= 1 AND length(owner_full_name) <= 256");
                table.HasCheckConstraint("ck_onboarding_requests_owner_phone_number_length", "owner_phone_number IS NULL OR (length(owner_phone_number) >= 1 AND length(owner_phone_number) <= 64)");
                table.HasCheckConstraint("ck_onboarding_requests_cafe_name_length", "length(cafe_name) >= 1 AND length(cafe_name) <= 256");
                table.HasCheckConstraint("ck_onboarding_requests_owner_password_hash_length", "owner_password_hash IS NULL OR length(owner_password_hash) BETWEEN 1 AND 512");
                table.HasCheckConstraint("ck_onboarding_requests_owner_credential", "created_via = 'Support' OR owner_password_hash IS NOT NULL");
                table.HasCheckConstraint("ck_onboarding_requests_slug_format", "tenant_slug ~ '^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$'");
                table.HasCheckConstraint("ck_onboarding_requests_slug_lowercase", "tenant_slug = lower(tenant_slug)");
                table.HasCheckConstraint("ck_onboarding_requests_slug_reserved", "tenant_slug NOT IN ('admin','api','t','www','app','static','assets','health','login','signup','support','platform')");
                table.HasCheckConstraint("ck_onboarding_requests_status", "onboarding_status IN ('AwaitingPayment', 'PaymentFailed', 'PaidAwaitingReview', 'Approved', 'Rejected', 'Cancelled')");
                table.HasCheckConstraint("ck_onboarding_requests_rejection_reason_length", "rejection_reason IS NULL OR length(rejection_reason) <= 1024");
            });

            builder.HasKey(request => request.Id).HasName("pk_onboarding_requests");
            builder.Property(request => request.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(request => request.CreatedVia).HasColumnName("created_via").HasColumnType("text").HasConversion<string>().IsRequired();
            builder.Property(request => request.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(request => request.PlanVersionId).HasColumnName("plan_version_id").HasColumnType("uuid").IsRequired();
            builder.Property(request => request.OwnerEmail).HasColumnName("owner_email").HasColumnType("text").IsRequired();
            builder.Property(request => request.OwnerFullName).HasColumnName("owner_full_name").HasColumnType("text").IsRequired();
            builder.Property(request => request.OwnerPhoneNumber).HasColumnName("owner_phone_number").HasColumnType("text").IsRequired(false);
            builder.Property(request => request.CafeName).HasColumnName("cafe_name").HasColumnType("text").IsRequired();
            builder.Property(request => request.OwnerPasswordHash).HasColumnName("owner_password_hash").HasColumnType("text").IsRequired(false);
            builder.Property(request => request.TenantSlug).HasColumnName("tenant_slug").HasColumnType("text").IsRequired();
            builder.Property(request => request.Status).HasColumnName("onboarding_status").HasColumnType("text").HasConversion<string>().IsRequired();
            builder.Property(request => request.ReviewedBy).HasColumnName("reviewed_by").HasColumnType("uuid").IsRequired(false);
            builder.Property(request => request.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("timestamptz").IsRequired(false);
            builder.Property(request => request.RejectionReason).HasColumnName("rejection_reason").HasColumnType("text").IsRequired(false);
            builder.Property(request => request.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(request => request.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
            builder.Property(request => request.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid").IsRequired(false);

            builder.HasIndex(request => request.TenantSlug)
                .IsUnique()
                .HasDatabaseName("uq_onboarding_requests_slug")
                .HasFilter("\"onboarding_status\" NOT IN ('Approved', 'Rejected', 'Cancelled')");
            builder.HasIndex(request => request.CreatedBy).HasDatabaseName("ix_onboarding_requests_created_by");
            builder.HasIndex(request => request.PlanVersionId).HasDatabaseName("ix_onboarding_requests_plan_version_id");
            builder.HasIndex(request => request.ReviewedBy).HasDatabaseName("ix_onboarding_requests_reviewed_by");
            builder.HasIndex(request => request.UpdatedBy).HasDatabaseName("ix_onboarding_requests_updated_by");

            builder.HasOne<PlanVersion>()
                .WithMany()
                .HasForeignKey(request => request.PlanVersionId)
                .HasConstraintName("fk_onboarding_requests_plan_version_id")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(request => request.CreatedBy)
                .HasConstraintName("fk_onboarding_requests_created_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(request => request.ReviewedBy)
                .HasConstraintName("fk_onboarding_requests_reviewed_by")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<PlatformAdminUser>()
                .WithMany()
                .HasForeignKey(request => request.UpdatedBy)
                .HasConstraintName("fk_onboarding_requests_updated_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
