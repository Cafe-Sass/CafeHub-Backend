using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Tenant
{
    public sealed class TenantDbContext(DbContextOptions<TenantDbContext> options) : DbContext(options)
    {
    }
}
