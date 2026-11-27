using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Refunds.DTOs;

namespace CafeHub.Application.Refunds.Commands
{
    public sealed record AssignRefundRequestCommand : ICommand<AssignRefundRequestResult>
    {
        public Guid RefundRequestId { get; init; }

        public Guid AssignedUserId { get; init; }
    }
}
