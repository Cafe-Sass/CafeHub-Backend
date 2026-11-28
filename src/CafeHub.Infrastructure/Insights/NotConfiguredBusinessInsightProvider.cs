using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Insights;

namespace CafeHub.Infrastructure.Insights
{
    public sealed class NotConfiguredBusinessInsightProvider : IBusinessInsightProvider
    {
        public Task<Result<IReadOnlyList<BusinessInsightCard>>> GetAsync(
            BusinessInsightInput input,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(input);

            return Task.FromResult(
                Result.Success<IReadOnlyList<BusinessInsightCard>>(Array.Empty<BusinessInsightCard>()));
        }
    }
}
