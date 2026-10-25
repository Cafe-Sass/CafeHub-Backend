using System.Text.RegularExpressions;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Infrastructure.Persistence.Master.Audit;

namespace CafeHub.Infrastructure.Audit
{
    internal sealed class PlatformAuditWriter : IPlatformAuditWriter
    {
        private const int MaxActionCodeLength = 128;
        private const int MaxSubjectTypeLength = 64;
        private const int MaxReasonLength = 64;

        private static readonly Regex ActionCodePattern = new(
            "^[a-z0-9]+(?:_[a-z0-9]+)*(?:\\.[a-z0-9]+(?:_[a-z0-9]+)*)+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ReasonCodePattern = new("^[a-z][a-z0-9_]{0,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly ICurrentUser currentUser;
        private readonly TimeProvider timeProvider;
        private readonly IPlatformAuditRecordStore store;
        private readonly ICorrelationIdAccessor? correlationIdAccessor;

        public PlatformAuditWriter(
            ICurrentUser currentUser,
            TimeProvider timeProvider,
            IPlatformAuditRecordStore store,
            ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.correlationIdAccessor = correlationIdAccessor;
        }

        public void Write(PlatformAuditEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            var actionCode = ValidateActionCode(entry);
            var subjectType = ValidateSubjectType(entry.TargetType);
            var reasonCode = ValidateReason(entry.Reason);
            var actorId = ResolveActorId(entry.ActorKind);

            store.Add(new PlatformAuditRecord
            {
                Id = Guid.CreateVersion7(),
                RecordedAt = timeProvider.GetUtcNow(),
                ActorPlatformAdminId = actorId,
                ActorKind = ActorKindLabel(entry.ActorKind),
                ActionCode = actionCode,
                SubjectType = subjectType,
                SubjectId = entry.TargetId,
                CorrelationId = correlationIdAccessor?.CorrelationId,
                ReasonCode = reasonCode,
            });
        }

        private Guid? ResolveActorId(PlatformAuditActorKind actorKind)
        {
            if (actorKind == PlatformAuditActorKind.System)
            {
                return null;
            }

            if (!currentUser.IsAuthenticated || currentUser.AdminId is null)
            {
                throw new InvalidOperationException("An audit entry with actor kind Admin requires an authenticated administrator.");
            }

            return currentUser.AdminId;
        }

        private static string ValidateActionCode(PlatformAuditEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Action) || entry.Action.Length > MaxActionCodeLength || !ActionCodePattern.IsMatch(entry.Action))
            {
                throw new ArgumentException($"The audit action must be a lowercase dot-separated code whose segments may contain underscores, 1 to {MaxActionCodeLength} characters.", nameof(entry));
            }

            var outcomeLabel = entry.Outcome.ToString().ToLowerInvariant();
            if (!entry.Action.EndsWith($".{outcomeLabel}", StringComparison.Ordinal))
            {
                throw new ArgumentException("The audit action must end with the outcome of the entry.", nameof(entry));
            }

            return entry.Action;
        }

        private static string ValidateSubjectType(string? subjectType)
        {
            if (string.IsNullOrWhiteSpace(subjectType) || subjectType.Length > MaxSubjectTypeLength)
            {
                throw new ArgumentException($"The audit subject type must be 1 to {MaxSubjectTypeLength} characters.", nameof(subjectType));
            }

            return subjectType;
        }

        private static string? ValidateReason(string? reason)
        {
            if (reason is null)
            {
                return null;
            }

            if (reason.Length > MaxReasonLength || !ReasonCodePattern.IsMatch(reason))
            {
                throw new ArgumentException("The audit reason must be a short machine code, never free text.", nameof(reason));
            }

            return reason;
        }

        private static string ActorKindLabel(PlatformAuditActorKind actorKind)
        {
            return actorKind switch
            {
                PlatformAuditActorKind.Admin => "PlatformAdmin",
                PlatformAuditActorKind.System => "System",
                _ => throw new ArgumentOutOfRangeException(nameof(actorKind), actorKind, "The audit actor kind is not supported."),
            };
        }
    }
}
