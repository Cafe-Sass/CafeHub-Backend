using Microsoft.Extensions.Logging;

namespace CafeHub.Application.Insights
{
    public sealed class BusinessInsightGenerator(
        IBusinessInsightProvider provider,
        ILogger<BusinessInsightGenerator> logger)
    {
        public async Task<BusinessInsightResult> GenerateAsync(
            BusinessInsightInput input,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(input);

            try
            {
                var providerResult = await provider.GetAsync(input, cancellationToken);
                if (providerResult.IsSuccess)
                {
                    var cards = BusinessInsightValidator.Validate(input, providerResult.Value);
                    if (cards.Count > 0)
                    {
                        return new BusinessInsightResult(BusinessInsightSources.Ai, cards);
                    }

                    LogFallback("Insights.NoValidCards");
                }
                else
                {
                    LogFallback(providerResult.Error?.Code ?? "Insights.ProviderFailure");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                LogFallback(exception.GetType().Name);
            }

            return new BusinessInsightResult(
                BusinessInsightSources.Rules,
                BusinessInsightRules.Generate(input));
        }

        private void LogFallback(string reason)
        {
            logger.LogWarning(
                "The insight provider did not return usable cards ({Reason}); rule-based insights are used.",
                reason);
        }
    }
}
