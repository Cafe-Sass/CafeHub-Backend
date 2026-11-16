using System.Globalization;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Staff;
using CafeHub.Application.Stations;
using CafeHub.Application.Stations.Contracts;
using CafeHub.Application.Stations.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Stations
{
    internal sealed class StationStore(
        TenantDbContext context,
        ITenantAuditWriter auditWriter,
        TimeProvider timeProvider) : IStationStore
    {
        private const string TargetType = "stations";
        private const string ActiveNameIndex = "uq_stations_branch_active_name";

        public async Task<Result<StationResponse>> CreateAsync(
            Guid branchId,
            string name,
            int sortOrder,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            await context.LockBranchesAsync(cancellationToken, branchId);
            var branch = await context.Branches
                .FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken);
            if (branch is null)
            {
                return Result.Failure<StationResponse>(StationsErrors.BranchNotFound());
            }

            if (branch.LifecycleStatus != BranchLifecycleStatus.Active)
            {
                return Result.Failure<StationResponse>(StationsErrors.BranchNotActive());
            }

            if (await ActiveNameExistsAsync(branchId, name, null, cancellationToken))
            {
                return Result.Failure<StationResponse>(StationsErrors.NameConflict());
            }

            var station = Station.Create(
                Guid.CreateVersion7(),
                branchId,
                name,
                sortOrder,
                timeProvider.GetUtcNow(),
                actorUserId);

            context.Stations.Add(station);

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "stations.created.succeeded",
                TargetType = TargetType,
                TargetId = station.Id,
                ActorUserId = actorUserId,
                Changes = TenantAuditChangeSets.StationCreate(station),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<StationResponse>(auditResult.Error!);
            }

            var save = await TrySaveAsync(cancellationToken);
            if (save is not null)
            {
                return Result.Failure<StationResponse>(save);
            }

            return Result.Success(StationResponse.From(station, ConcurrencyToken(station)));
        }

        public async Task<Result<StationResponse>> UpdateAsync(
            Guid stationId,
            Guid? scopeBranchId,
            string name,
            int sortOrder,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var station = await LoadInScopeAsync(stationId, scopeBranchId, cancellationToken);
            if (station is null)
            {
                return Result.Failure<StationResponse>(StationsErrors.NotFound());
            }

            if (!uint.TryParse(concurrencyToken, out var token))
            {
                return Result.Failure<StationResponse>(StationsErrors.InvalidConcurrencyToken());
            }

            context.Entry(station).Property<uint>("xmin").OriginalValue = token;

            await context.LockBranchesAsync(cancellationToken, station.BranchId);
            if (station.Status == StationStatus.Active
                && await ActiveNameExistsAsync(station.BranchId, name, station.Id, cancellationToken))
            {
                return Result.Failure<StationResponse>(StationsErrors.NameConflict());
            }

            var changes = TenantAuditChangeSets.StationUpdate(station, name, sortOrder);

            station.Rename(name, sortOrder, timeProvider.GetUtcNow(), actorUserId);

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "stations.updated.succeeded",
                TargetType = TargetType,
                TargetId = station.Id,
                ActorUserId = actorUserId,
                Changes = changes,
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<StationResponse>(auditResult.Error!);
            }

            var save = await TrySaveAsync(cancellationToken);
            if (save is not null)
            {
                return Result.Failure<StationResponse>(save);
            }

            return Result.Success(StationResponse.From(station, ConcurrencyToken(station)));
        }

        public async Task<Result<StationResponse>> DeactivateAsync(
            Guid stationId,
            Guid? scopeBranchId,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            return await ChangeStatusAsync(
                stationId,
                scopeBranchId,
                StationStatus.Inactive,
                "stations.deactivated.succeeded",
                concurrencyToken,
                actorUserId,
                cancellationToken);
        }

        public async Task<Result<StationResponse>> ReactivateAsync(
            Guid stationId,
            Guid? scopeBranchId,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            return await ChangeStatusAsync(
                stationId,
                scopeBranchId,
                StationStatus.Active,
                "stations.reactivated.succeeded",
                concurrencyToken,
                actorUserId,
                cancellationToken);
        }

        public async Task<StationResponse?> GetByIdAsync(
            Guid stationId,
            Guid? scopeBranchId,
            CancellationToken cancellationToken)
        {
            var row = await context.Stations.AsNoTracking()
                .Where(station => station.Id == stationId
                    && (scopeBranchId == null || station.BranchId == scopeBranchId))
                .Select(station => new { Station = station, Version = EF.Property<uint>(station, "xmin") })
                .SingleOrDefaultAsync(cancellationToken);

            return row is null
                ? null
                : StationResponse.From(row.Station, row.Version.ToString(CultureInfo.InvariantCulture));
        }

        public async Task<StationListPage> ListAsync(
            StationListRequest request,
            CancellationToken cancellationToken)
        {
            var query = context.Stations.Where(station => station.BranchId == request.BranchId);
            if (request.Status is { } status)
            {
                query = query.Where(station => station.Status == status);
            }

            int? totalCount = null;
            if (request.IncludeTotalCount)
            {
                totalCount = await query.CountAsync(cancellationToken);
            }

            var fetched = await query
                .OrderBy(station => station.SortOrder)
                .ThenBy(station => station.Name)
                .ThenBy(station => station.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize + 1)
                .ToListAsync(cancellationToken);

            var items = new List<StationResponse>(fetched.Count);
            foreach (var station in fetched)
            {
                items.Add(StationResponse.From(station, ConcurrencyToken(station)));
            }

            return new StationListPage(items, totalCount);
        }

        public async Task<Result> SetStaffStationsAsync(
            Guid staffId,
            IReadOnlyList<Guid> stationIds,
            Guid? scopeBranchId,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var staff = await context.TenantUsers
                .FirstOrDefaultAsync(u => u.Id == staffId, cancellationToken);
            if (staff is null || staff.UserKind != TenantUserKind.Staff)
            {
                return Result.Failure(StaffErrors.NotFound());
            }

            if (scopeBranchId is { } scope && staff.BranchId != scope)
            {
                return Result.Failure(StationsErrors.NotFound());
            }

            var staffBranchId = staff.BranchId!.Value;

            var stations = await context.Stations
                .Where(s => stationIds.Contains(s.Id))
                .ToListAsync(cancellationToken);

            if (stations.Count != stationIds.Count)
            {
                return Result.Failure(StationsErrors.NotFound());
            }

            foreach (var station in stations)
            {
                if (station.Status != StationStatus.Active || station.BranchId != staffBranchId)
                {
                    return Result.Failure(StationsErrors.NotFound());
                }
            }

            var current = await context.TenantUserStations
                .Where(link => link.TenantUserId == staffId)
                .ToListAsync(cancellationToken);

            foreach (var link in current.Where(link => !stationIds.Contains(link.StationId)))
            {
                context.TenantUserStations.Remove(link);
            }

            var now = timeProvider.GetUtcNow();
            var assigned = current.Select(link => link.StationId).ToHashSet();
            foreach (var stationId in stationIds.Where(id => !assigned.Contains(id)).Order())
            {
                context.TenantUserStations.Add(TenantUserStation.Create(staffId, stationId, now));
            }

            var changes = TenantAuditChangeSets.StaffStations(staff, stationIds);
            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "stations.staff.updated.succeeded",
                TargetType = "tenant_users",
                TargetId = staffId,
                ActorUserId = actorUserId,
                Changes = changes,
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure(auditResult.Error!);
            }

            var save = await TrySaveAsync(cancellationToken);
            if (save is not null)
            {
                return Result.Failure(save);
            }

            return Result.Success();
        }

        private async Task<Result<StationResponse>> ChangeStatusAsync(
            Guid stationId,
            Guid? scopeBranchId,
            StationStatus newStatus,
            string auditAction,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var station = await LoadInScopeAsync(stationId, scopeBranchId, cancellationToken);
            if (station is null)
            {
                return Result.Failure<StationResponse>(StationsErrors.NotFound());
            }

            if (!uint.TryParse(concurrencyToken, out var token))
            {
                return Result.Failure<StationResponse>(StationsErrors.InvalidConcurrencyToken());
            }

            await context.LockBranchesAsync(cancellationToken, station.BranchId);

            var version = context.Entry(station).Property<uint>("xmin");
            if (version.CurrentValue != token)
            {
                return Result.Failure<StationResponse>(StationsErrors.ConcurrencyConflict());
            }

            if (station.Status == newStatus)
            {
                return Result.Success(StationResponse.From(
                    station,
                    version.CurrentValue.ToString(CultureInfo.InvariantCulture)));
            }

            if (newStatus == StationStatus.Active
                && await ActiveNameExistsAsync(station.BranchId, station.Name, station.Id, cancellationToken))
            {
                return Result.Failure<StationResponse>(StationsErrors.NameConflict());
            }

            version.OriginalValue = token;
            var changes = TenantAuditChangeSets.StationLifecycleStatus(station, newStatus);

            if (newStatus == StationStatus.Active)
            {
                station.Reactivate(timeProvider.GetUtcNow(), actorUserId);
            }
            else
            {
                station.Deactivate(timeProvider.GetUtcNow(), actorUserId);
            }

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = auditAction,
                TargetType = TargetType,
                TargetId = station.Id,
                ActorUserId = actorUserId,
                Changes = changes,
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<StationResponse>(auditResult.Error!);
            }

            var save = await TrySaveAsync(cancellationToken);
            if (save is not null)
            {
                return Result.Failure<StationResponse>(save);
            }

            return Result.Success(StationResponse.From(station, ConcurrencyToken(station)));
        }

        private Task<Station?> LoadInScopeAsync(
            Guid stationId,
            Guid? scopeBranchId,
            CancellationToken cancellationToken) =>
            context.Stations.FirstOrDefaultAsync(
                station => station.Id == stationId
                    && (scopeBranchId == null || station.BranchId == scopeBranchId),
                cancellationToken);

        private Task<bool> ActiveNameExistsAsync(
            Guid branchId,
            string name,
            Guid? excludeStationId,
            CancellationToken cancellationToken)
        {
            var normalized = name.Trim().ToLower();

            return context.Stations.AnyAsync(
                station => station.BranchId == branchId
                    && station.Status == StationStatus.Active
                    && station.Name.ToLower() == normalized
                    && (excludeStationId == null || station.Id != excludeStationId),
                cancellationToken);
        }

        private async Task<Error?> TrySaveAsync(CancellationToken cancellationToken)
        {
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return null;
            }
            catch (DbUpdateConcurrencyException)
            {
                return StationsErrors.ConcurrencyConflict();
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
            } violation && violation.ConstraintName == ActiveNameIndex)
            {
                return StationsErrors.NameConflict();
            }
        }

        private string ConcurrencyToken(Station station) =>
            context.Entry(station).Property<uint>("xmin").CurrentValue.ToString(CultureInfo.InvariantCulture);
    }
}
