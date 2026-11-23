namespace CafeHub.Application.Orders.Contracts
{
    public interface IOrderTableStore
    {
        Task<OrderTable?> FindActiveTableAsync(Guid tableId, CancellationToken cancellationToken);
    }

    public sealed record OrderTable(Guid BranchId, string QrCode);
}
