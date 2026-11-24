using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Contracts
{
    public sealed record PaymentReview(BillSettlement Settlement, Payment Payment);
}
