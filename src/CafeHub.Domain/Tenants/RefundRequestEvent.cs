namespace CafeHub.Domain.Tenants
{
    public sealed class RefundRequestEvent
    {
        private RefundRequestEvent()
        {
        }

        public Guid Id { get; private set; }

        public Guid RefundRequestId { get; private set; }

        public RefundRequestEventKind Kind { get; private set; }

        public RefundRequestStatus? FromStatus { get; private set; }

        public RefundRequestStatus? ToStatus { get; private set; }

        public RefundRequestActorKind ActorKind { get; private set; }

        public Guid? ActorUserId { get; private set; }

        public string? Note { get; private set; }

        public DateTimeOffset OccurredAt { get; private set; }

        public static RefundRequestEvent Create(
            Guid id,
            Guid refundRequestId,
            RefundRequestEventKind kind,
            RefundRequestStatus? fromStatus,
            RefundRequestStatus? toStatus,
            RefundRequestActorKind actorKind,
            Guid? actorUserId,
            string? note,
            DateTimeOffset occurredAt)
        {
            if (id == Guid.Empty || refundRequestId == Guid.Empty)
            {
                throw new ArgumentException("Event identifiers are required.");
            }

            if (!Enum.IsDefined(kind) || !Enum.IsDefined(actorKind))
            {
                throw new ArgumentException("The event kind and actor kind must be defined.");
            }

            if ((fromStatus is { } from && !Enum.IsDefined(from))
                || (toStatus is { } to && !Enum.IsDefined(to)))
            {
                throw new ArgumentException("Event statuses must be defined.");
            }

            if ((actorKind == RefundRequestActorKind.Guest && actorUserId is not null)
                || (actorKind == RefundRequestActorKind.Staff && actorUserId is null)
                || actorUserId == Guid.Empty)
            {
                throw new ArgumentException("The event actor does not match its actor kind.", nameof(actorUserId));
            }

            return new RefundRequestEvent
            {
                Id = id,
                RefundRequestId = refundRequestId,
                Kind = kind,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                ActorKind = actorKind,
                ActorUserId = actorUserId,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                OccurredAt = occurredAt,
            };
        }
    }
}
