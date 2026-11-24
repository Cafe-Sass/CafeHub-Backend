using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Idempotency;

namespace CafeHub.Application.Billing.Commands
{
    /// <summary>
    /// Submits one guest payment screenshot against an open bill's public reference. Marked sensitive
    /// so the claimed note and the image are never logged, and tenant transactional so the payment
    /// and any state it touches commit together; the media upload happens inside the handler before
    /// the payment is inserted.
    /// </summary>
    public sealed record SubmitPaymentProofCommand
        : ICommand<SubmitPaymentProofResult>, IIdempotentCommand, ITenantTransactionalCommand, ISensitiveRequest
    {
        public string? BillReference { get; init; }

        public decimal ClaimedAmount { get; init; }

        public string? Note { get; init; }

        public Stream? Content { get; init; }

        public string? IdempotencyKey { get; set; }
    }
}
