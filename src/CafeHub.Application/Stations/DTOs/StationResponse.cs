using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Stations.DTOs
{
    /// <summary>
    /// The station read model returned to a tenant user: identity, owning branch, name, status, sort order,
    /// timestamps, and an opaque concurrency token.
    /// </summary>
    public sealed class StationResponse
    {
        public required Guid Id { get; init; }

        public required Guid BranchId { get; init; }

        public required string Name { get; init; }

        public required string Status { get; init; }

        public required int SortOrder { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }

        public required DateTimeOffset UpdatedAt { get; init; }

        public required string ConcurrencyToken { get; init; }

        public static StationResponse From(Station station, string concurrencyToken)
        {
            ArgumentNullException.ThrowIfNull(station);
            ArgumentNullException.ThrowIfNull(concurrencyToken);

            return new StationResponse
            {
                Id = station.Id,
                BranchId = station.BranchId,
                Name = station.Name,
                Status = station.Status.ToString(),
                SortOrder = station.SortOrder,
                CreatedAt = station.CreatedAt,
                UpdatedAt = station.UpdatedAt,
                ConcurrencyToken = concurrencyToken,
            };
        }
    }
}
