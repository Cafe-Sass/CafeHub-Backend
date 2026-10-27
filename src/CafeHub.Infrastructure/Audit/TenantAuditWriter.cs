using System.Text.RegularExpressions;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;

namespace CafeHub.Infrastructure.Audit
{
    internal sealed class TenantAuditWriter : ITenantAuditWriter
    {
        private const int MaxActionCodeLength = 128;
        private const int MaxSubjectTypeLength = 64;

        private static readonly Regex ActionCodePattern = new(
            "^[a-z0-9]+(?:_[a-z0-9]+)*(?:\\.[a-z0-9]+(?:_[a-z0-9]+)*)+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly ICurrentTenantUser currentTenantUser;
        private readonly TimeProvider timeProvider;
        private readonly TenantDbContext context;
        private readonly ITenantContextAccessor tenantContextAccessor;

        public TenantAuditWriter(
            ICurrentTenantUser currentTenantUser,
            TimeProvider timeProvider,
            TenantDbContext context,
            ITenantContextAccessor tenantContextAccessor)
        {
            this.currentTenantUser = currentTenantUser ?? throw new ArgumentNullException(nameof(currentTenantUser));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
        }

        public Task<Result> WriteAsync(TenantAuditEntry entry, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(entry);
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsValid(entry))
            {
                return Task.FromResult(Result.Failure(TenantAuditErrors.InvalidEntry()));
            }

            var tenantContext = tenantContextAccessor.GetRequired();
            var actorId = tenantContext.IsActive
                && currentTenantUser.IsAuthenticated
                && currentTenantUser.TenantId == tenantContext.TenantId
                && currentTenantUser.UserId is { } authenticatedUserId
                ? authenticatedUserId
                : entry.ActorUserId;
            var actorKind = actorId.HasValue ? TenantAuditRecordKind.User : TenantAuditRecordKind.System;

            context.TenantAuditRecords.Add(TenantAuditRecord.Create(
                Guid.CreateVersion7(),
                timeProvider.GetUtcNow(),
                actorId,
                actorKind,
                entry.Action,
                entry.TargetType,
                entry.TargetId,
                null,
                null));

            return Task.FromResult(Result.Success());
        }

        private static bool IsValid(TenantAuditEntry entry)
        {
            return !string.IsNullOrWhiteSpace(entry.Action)
                && entry.Action.Length <= MaxActionCodeLength
                && ActionCodePattern.IsMatch(entry.Action)
                && !string.IsNullOrWhiteSpace(entry.TargetType)
                && entry.TargetType.Length <= MaxSubjectTypeLength
                && (entry.Reason is null || Enum.IsDefined(entry.Reason.Value));
        }
    }

    internal static class TenantAuditErrors
    {
        public static Error InvalidEntry() => Error.Failure("Audit.InvalidTenantEntry", "The tenant audit entry is invalid.");
    }
}
