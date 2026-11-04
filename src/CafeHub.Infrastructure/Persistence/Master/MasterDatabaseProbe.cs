using CafeHub.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master
{
    public sealed class MasterDatabaseProbe(MasterDbContext context) : IMasterDatabaseProbe
    {
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken)
        {
            return context.Database.CanConnectAsync(cancellationToken);
        }
    }
}
