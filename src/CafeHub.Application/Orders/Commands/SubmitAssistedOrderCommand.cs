using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Idempotency;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Orders.Commands
{
    public sealed record SubmitAssistedOrderCommand
        : ICommand<SubmitOrderResult>, IIdempotentCommand, ISensitiveRequest
    {
        public string? IdempotencyKey { get; set; }

        public Guid TableId { get; init; }

        public OrderServiceType ServiceType { get; init; }

        public string GuestDisplayName { get; init; } = string.Empty;

        public decimal? ExpectedTotal { get; init; }

        public IReadOnlyList<SubmitOrderLine> Lines { get; init; } = [];
    }
}
