namespace CafeHub.Infrastructure.Insights
{
    public sealed class AiOptions
    {
        public string InsightsProvider { get; set; } = AiInsightsProviders.None;
    }
}
