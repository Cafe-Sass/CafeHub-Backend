namespace CafeHub.Domain.Tenants
{
    public sealed class TenantAuditRecord
    {
        private TenantAuditRecord()
        {
        }

        public Guid Id { get; private set; }

        public DateTimeOffset RecordedAt { get; private set; }

        public Guid? ActorUserId { get; private set; }

        public TenantAuditRecordKind ActorKind { get; private set; }

        public string ActionCode { get; private set; } = string.Empty;

        public string SubjectType { get; private set; } = string.Empty;

        public Guid? SubjectId { get; private set; }

        public string? BeforeState { get; private set; }

        public string? AfterState { get; private set; }

        public static TenantAuditRecord Create(
            Guid id,
            DateTimeOffset recordedAt,
            Guid? actorUserId,
            TenantAuditRecordKind actorKind,
            string actionCode,
            string subjectType,
            Guid? subjectId,
            string? beforeState,
            string? afterState)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The tenant audit record id is required.", nameof(id));
            }

            if (!Enum.IsDefined(actorKind))
            {
                throw new ArgumentException("The audit record actor kind is not defined.", nameof(actorKind));
            }

            if (string.IsNullOrWhiteSpace(actionCode) || actionCode.Length > 128)
            {
                throw new ArgumentException(
                    "The action code must be between 1 and 128 characters.",
                    nameof(actionCode));
            }

            if (string.IsNullOrWhiteSpace(subjectType) || subjectType.Length > 64)
            {
                throw new ArgumentException(
                    "The subject type must be between 1 and 64 characters.",
                    nameof(subjectType));
            }

            if (beforeState is not null && beforeState.Length > 4096)
            {
                throw new ArgumentException(
                    "The before state must not exceed 4096 characters.",
                    nameof(beforeState));
            }

            if (afterState is not null && afterState.Length > 4096)
            {
                throw new ArgumentException(
                    "The after state must not exceed 4096 characters.",
                    nameof(afterState));
            }

            return new TenantAuditRecord
            {
                Id = id,
                RecordedAt = recordedAt,
                ActorUserId = actorUserId,
                ActorKind = actorKind,
                ActionCode = actionCode,
                SubjectType = subjectType,
                SubjectId = subjectId,
                BeforeState = beforeState,
                AfterState = afterState,
            };
        }
    }
}
