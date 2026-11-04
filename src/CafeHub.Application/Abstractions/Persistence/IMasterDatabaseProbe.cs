namespace CafeHub.Application.Abstractions.Persistence
{
    public interface IMasterDatabaseProbe
    {
        Task<bool> CanConnectAsync(CancellationToken cancellationToken);
    }
}
