using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Idempotency;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Orders
{
    public sealed record SubmitOrderCommand
        : ICommand<SubmitOrderResult>, ITenantTransactionalCommand, IIdempotentCommand, ISensitiveRequest
    {
        public string? IdempotencyKey { get; set; }

        public string? Code { get; init; }

        public OrderServiceType ServiceType { get; init; }

        public OrderSource Source { get; init; } = OrderSource.Guest;

        public string GuestDisplayName { get; init; } = string.Empty;

        public string? GuestDeviceId { get; init; }

        public Guid? ActingStaffUserId { get; init; }

        public decimal? ExpectedTotal { get; init; }

        public IReadOnlyList<SubmitOrderLine> Lines { get; init; } = [];
    }
}
