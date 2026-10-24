namespace CafeHub.Application.Audit
{
    public sealed class PlatformAuditEntry
    {
        public required PlatformAuditActorKind ActorKind { get; init; }

        public required string Action { get; init; }

        public required string TargetType { get; init; }

        public Guid? TargetId { get; init; }

        public required PlatformAuditOutcome Outcome { get; init; }

        public string? Reason { get; init; }
    }
}
