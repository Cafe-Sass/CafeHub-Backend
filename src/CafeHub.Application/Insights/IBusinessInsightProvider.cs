using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Insights
{
    public interface IBusinessInsightProvider
    {
        Task<Result<IReadOnlyList<BusinessInsightCard>>> GetAsync(
            BusinessInsightInput input,
            CancellationToken cancellationToken);
    }
}
