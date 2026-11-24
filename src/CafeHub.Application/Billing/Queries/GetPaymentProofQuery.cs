using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Billing.DTOs;

namespace CafeHub.Application.Billing.Queries
{
    public sealed record GetPaymentProofQuery(Guid PaymentId) : IQuery<PaymentProofFile>;
}
