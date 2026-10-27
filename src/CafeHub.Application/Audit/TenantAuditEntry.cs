namespace CafeHub.Application.Audit
{
    public sealed class TenantAuditEntry
    {
        public required string Action { get; init; }

        public required string TargetType { get; init; }

        public Guid? TargetId { get; init; }

        public Guid? ActorUserId { get; init; }

        public TenantAuditReason? Reason { get; init; }
    }
}
