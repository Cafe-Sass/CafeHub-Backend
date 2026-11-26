using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeHub.Infrastructure.Persistence.Tenant.Configurations
{
    public sealed class AttendanceQrSessionConfiguration : IEntityTypeConfiguration<AttendanceQrSession>
    {
        public void Configure(EntityTypeBuilder<AttendanceQrSession> builder)
        {
            builder.ToTable("attendance_qr_sessions", table =>
            {
                table.HasCheckConstraint(
                    "ck_attendance_qr_sessions_token", "token ~ '^[A-Za-z0-9_-]{43}$'");
                table.HasCheckConstraint(
                    "ck_attendance_qr_sessions_window", "valid_to > valid_from");
            });

            builder.HasKey(session => session.Id).HasName("pk_attendance_qr_sessions");
            builder.Property(session => session.Id)
                .HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
            builder.Property(session => session.BranchId)
                .HasColumnName("branch_id").HasColumnType("uuid");
            builder.Property(session => session.Token)
                .HasColumnName("token").HasColumnType("text");
            builder.Property(session => session.ValidFrom)
                .HasColumnName("valid_from").HasColumnType("timestamptz");
            builder.Property(session => session.ValidTo)
                .HasColumnName("valid_to").HasColumnType("timestamptz");
            builder.Property(session => session.CreatedAt)
                .HasColumnName("created_at").HasColumnType("timestamptz");
            builder.Property(session => session.CreatedBy)
                .HasColumnName("created_by").HasColumnType("uuid");

            builder.HasIndex(session => session.Token)
                .HasDatabaseName("uq_attendance_qr_sessions_token")
                .IsUnique();
            builder.HasIndex(session => new { session.BranchId, session.ValidTo })
                .HasDatabaseName("ix_attendance_qr_sessions_branch_valid_to");
            builder.HasIndex(session => session.CreatedBy)
                .HasDatabaseName("ix_attendance_qr_sessions_created_by");

            builder.HasOne<Branch>()
                .WithMany()
                .HasForeignKey(session => session.BranchId)
                .HasConstraintName("fk_attendance_qr_sessions_branch_id")
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TenantUser>()
                .WithMany()
                .HasForeignKey(session => session.CreatedBy)
                .HasConstraintName("fk_attendance_qr_sessions_created_by")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
