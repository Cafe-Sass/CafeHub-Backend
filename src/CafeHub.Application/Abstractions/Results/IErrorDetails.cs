namespace CafeHub.Application.Abstractions.Results
{
    public interface IErrorDetails
    {
        IReadOnlyDictionary<string, object?> Details { get; }
    }
}
