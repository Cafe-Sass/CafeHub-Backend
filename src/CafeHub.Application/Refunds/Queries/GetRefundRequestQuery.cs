using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Refunds.DTOs;

namespace CafeHub.Application.Refunds.Queries
{
    public sealed record GetRefundRequestQuery(Guid RefundRequestId) : IQuery<RefundRequestDetail>;
}
