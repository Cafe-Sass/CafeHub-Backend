using CafeHub.Application.Orders.Contracts;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Orders
{
    internal sealed class CafeBusinessDateProvider(TenantDbContext context, TimeProvider timeProvider)
        : ICafeBusinessDateProvider
    {
        public async Task<DateOnly> GetBusinessDateAsync(CancellationToken cancellationToken)
        {
            var timezone = await context.CafeSettings.AsNoTracking()
                .Select(settings => settings.Timezone)
                .SingleOrDefaultAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(timezone))
            {
                throw new InvalidOperationException(
                    "The cafe time zone is required to resolve the business date.");
            }

            var local = TimeZoneInfo.ConvertTime(
                timeProvider.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(timezone));
            return DateOnly.FromDateTime(local.DateTime);
        }
    }
}
