using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Billing.DTOs;

namespace CafeHub.Application.Billing.Queries
{
    public sealed record GetReceiptQuery(Guid ReceiptId) : IQuery<ReceiptDetail>;
}
