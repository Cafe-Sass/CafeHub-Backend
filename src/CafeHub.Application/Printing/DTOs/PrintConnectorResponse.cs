using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Printing.DTOs
{
    public sealed record PrintConnectorResponse(
        Guid Id,
        Guid BranchId,
        string Name,
        string Status,
        DateTimeOffset? LastSeenAt,
        DateTimeOffset CreatedAt)
    {
        public static PrintConnectorResponse From(PrintConnector connector) =>
            new(
                connector.Id,
                connector.BranchId,
                connector.Name,
                connector.Status.ToString(),
                connector.LastSeenAt,
                connector.CreatedAt);
    }

    public sealed record PrintConnectorCreatedResponse(
        Guid Id,
        Guid BranchId,
        string Name,
        string Status,
        DateTimeOffset CreatedAt,
        string Key);
}
