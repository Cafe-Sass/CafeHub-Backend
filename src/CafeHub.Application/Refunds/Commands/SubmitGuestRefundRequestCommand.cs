using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Idempotency;
using CafeHub.Application.Refunds.Contracts;

namespace CafeHub.Application.Refunds.Commands
{
    public sealed record SubmitGuestRefundRequestCommand
        : ICommand<SubmitGuestRefundRequestResult>, IIdempotentCommand, ITenantTransactionalCommand, ISensitiveRequest
    {
        public string? BillReference { get; init; }

        public Guid PaymentId { get; init; }

        public decimal ClaimedAmount { get; init; }

        public string? Reason { get; init; }

        public string? Details { get; init; }

        public string? GuestDisplayName { get; init; }

        public IReadOnlyList<RefundRequestEvidence> Evidence { get; init; } = [];

        public string? IdempotencyKey { get; set; }
    }
}
