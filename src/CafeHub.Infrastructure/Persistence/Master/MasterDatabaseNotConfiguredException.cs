namespace CafeHub.Infrastructure.Persistence.Master;

public sealed class MasterDatabaseNotConfiguredException(string message) : InvalidOperationException(message);
