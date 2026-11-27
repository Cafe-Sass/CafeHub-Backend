using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Refunds.DTOs;

namespace CafeHub.Application.Refunds.Commands
{
    public sealed record ChangeRefundRequestStatusCommand : ICommand<ChangeRefundRequestStatusResult>
    {
        public Guid RefundRequestId { get; init; }

        public string? Status { get; init; }

        public string? Note { get; init; }
    }
}
