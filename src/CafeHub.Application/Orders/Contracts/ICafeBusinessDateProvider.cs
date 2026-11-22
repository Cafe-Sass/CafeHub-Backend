namespace CafeHub.Application.Orders.Contracts
{
    /// <summary>
    /// The current calendar date in the cafe's own time zone, which is the numbering and reporting day.
    /// </summary>
    public interface ICafeBusinessDateProvider
    {
        Task<DateOnly> GetBusinessDateAsync(CancellationToken cancellationToken);
    }
}
