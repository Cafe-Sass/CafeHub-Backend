using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Stations.DTOs;

namespace CafeHub.Application.Stations.Queries
{
    public sealed record GetStationQuery(Guid StationId) : IQuery<StationResponse>;
}
