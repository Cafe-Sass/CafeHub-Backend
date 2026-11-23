using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Contracts
{
    /// <summary>
    /// Read model of one bill's settlement state: the bill, its attached orders (all Accepted, since
    /// attachment happens on accept), and its payments in stable order. Loaded inside the caller's
    /// branch scope (a null branch id is the owner path across branches).
    /// </summary>
    public sealed record BillSettlement(
        Bill Bill,
        IReadOnlyList<Order> AcceptedOrders,
        IReadOnlyList<Payment> Payments);
}
