namespace CafeHub.WebApi.Correlation
{
    public sealed class GuidCorrelationIdGenerator : ICorrelationIdGenerator
    {
        public string Generate()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
