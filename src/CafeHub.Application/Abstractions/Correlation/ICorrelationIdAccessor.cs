namespace CafeHub.Application.Abstractions.Correlation
{
    public interface ICorrelationIdAccessor
    {
        string? CorrelationId { get; }
    }
}
