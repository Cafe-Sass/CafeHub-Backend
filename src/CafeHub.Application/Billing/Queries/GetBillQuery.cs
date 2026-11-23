using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Billing.DTOs;

namespace CafeHub.Application.Billing.Queries
{
    public sealed record GetBillQuery(Guid BillId) : IQuery<BillDetail>;
}
