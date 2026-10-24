namespace CafeHub.Infrastructure.Persistence.Master.Audit
{
    public sealed class PlatformAuditRecord
    {
        public Guid Id { get; set; }

        public DateTimeOffset RecordedAt { get; set; }

        public Guid? ActorPlatformAdminId { get; set; }

        public string ActorKind { get; set; } = string.Empty;

        public string ActionCode { get; set; } = string.Empty;

        public string SubjectType { get; set; } = string.Empty;

        public Guid? SubjectId { get; set; }

        public string? CorrelationId { get; set; }

        public string? ReasonCode { get; set; }

        public string? BeforeState { get; set; }

        public string? AfterState { get; set; }
    }
}
