namespace CafeHub.Application.Billing.Contracts
{
    /// <summary>
    /// Read port for bills. The list returns one row per bill with its derived accepted-order total and
    /// confirmed-payment total, filtered inside the caller's branch scope; the detail returns the
    /// settlement snapshot (bill, its attached orders, its payments) for the grouped view. Both reads
    /// are lock-free, so the confirmation write keeps its own locking snapshot.
    /// </summary>
    public interface IBillQueryStore
    {
        Task<BillListPage> ListAsync(BillListRequest request, CancellationToken cancellationToken);

        Task<BillSettlement?> FindDetailAsync(
            Guid? branchId,
            Guid billId,
            CancellationToken cancellationToken);
    }
}
