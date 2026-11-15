using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Stations.Contracts;
using CafeHub.Application.Stations.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Stations
{
    internal sealed class FakeStationStore : IStationStore
    {
        public List<CreateStationCall> Created { get; } = new();
        public List<UpdateStationCall> Updated { get; } = new();
        public List<StatusChangeCall> Deactivated { get; } = new();
        public List<StatusChangeCall> Reactivated { get; } = new();
        public List<(Guid StationId, Guid? ScopeBranchId)> GetByIdRequests { get; } = new();
        public List<StationListRequest> ListRequests { get; } = new();
        public StationResponse? Response { get; set; }
        public StationResponse? GetByIdResponse { get; set; }
        public List<StationResponse> ListResponses { get; } = new();
        public Error? Failure { get; set; }

        public Task<Result<StationResponse>> CreateAsync(
            Guid branchId,
            string name,
            int sortOrder,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            Created.Add(new CreateStationCall(branchId, name, sortOrder, actorUserId));
            return Task.FromResult(Respond());
        }

        public Task<Result<StationResponse>> UpdateAsync(
            Guid stationId,
            Guid? scopeBranchId,
            string name,
            int sortOrder,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            Updated.Add(new UpdateStationCall(stationId, scopeBranchId, name, sortOrder, concurrencyToken, actorUserId));
            return Task.FromResult(Respond());
        }

        public Task<Result<StationResponse>> DeactivateAsync(
            Guid stationId,
            Guid? scopeBranchId,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            Deactivated.Add(new StatusChangeCall(stationId, scopeBranchId, concurrencyToken, actorUserId));
            return Task.FromResult(Respond());
        }

        public Task<Result<StationResponse>> ReactivateAsync(
            Guid stationId,
            Guid? scopeBranchId,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            Reactivated.Add(new StatusChangeCall(stationId, scopeBranchId, concurrencyToken, actorUserId));
            return Task.FromResult(Respond());
        }

        public Task<StationResponse?> GetByIdAsync(
            Guid stationId,
            Guid? scopeBranchId,
            CancellationToken cancellationToken)
        {
            GetByIdRequests.Add((stationId, scopeBranchId));
            return Task.FromResult(GetByIdResponse);
        }

        public Task<StationListPage> ListAsync(StationListRequest request, CancellationToken cancellationToken)
        {
            ListRequests.Add(request);
            var items = ListResponses.Take(request.PageSize + 1).ToList();
            var totalCount = request.IncludeTotalCount ? (int?)ListResponses.Count : null;
            return Task.FromResult(new StationListPage(items, totalCount));
        }

        private Result<StationResponse> Respond() =>
            Failure is { } failure ? Result.Failure<StationResponse>(failure) : Result.Success(Response!);
    }

    internal sealed record CreateStationCall(Guid BranchId, string Name, int SortOrder, Guid ActorUserId);

    internal sealed record UpdateStationCall(
        Guid StationId,
        Guid? ScopeBranchId,
        string Name,
        int SortOrder,
        string ConcurrencyToken,
        Guid ActorUserId);

    internal sealed record StatusChangeCall(
        Guid StationId,
        Guid? ScopeBranchId,
        string ConcurrencyToken,
        Guid ActorUserId);

    internal static class StationResponses
    {
        public static StationResponse Create(Guid? id = null, Guid? branchId = null, StationStatus status = StationStatus.Active)
        {
            var station = Station.Create(id ?? Guid.NewGuid(), branchId ?? Guid.NewGuid(), "Bar", 0, DateTimeOffset.UtcNow);
            if (status == StationStatus.Inactive)
            {
                station.Deactivate(DateTimeOffset.UtcNow, null);
            }

            return StationResponse.From(station, "7");
        }
    }
}
