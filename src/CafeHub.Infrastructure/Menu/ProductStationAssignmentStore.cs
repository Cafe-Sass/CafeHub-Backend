using System.Globalization;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Menu;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Menu
{
    internal sealed class ProductStationAssignmentStore(
        TenantDbContext context, ITenantAuditWriter audit, TimeProvider timeProvider) : IProductStationAssignmentStore
    {
        public async Task<Result<ProductStationAssignmentResponse>> GetAsync(
            Guid productId, Guid branchId, CancellationToken cancellationToken)
        {
            var row = await ReadAsync(productId, branchId, cancellationToken);
            if (row is null)
            {
                return Result.Failure<ProductStationAssignmentResponse>(ProductErrors.NotFound());
            }

            return row.Ready
                ? Result.Success(ToResponse(productId, branchId, row))
                : Result.Failure<ProductStationAssignmentResponse>(ProductErrors.RoutingNotReady());
        }

        public async Task<Result<ProductRouting>> ResolveRoutingAsync(
            Guid productId, Guid branchId, CancellationToken cancellationToken)
        {
            var row = await ReadAsync(productId, branchId, cancellationToken);
            if (row is null)
            {
                return Result.Failure<ProductRouting>(ProductErrors.NotFound());
            }

            if (!row.Ready)
            {
                return Result.Failure<ProductRouting>(ProductErrors.RoutingNotReady());
            }

            var state = Classify(row.BranchActive, row.StationId, row.StationActive);
            return Result.Success(new ProductRouting(
                state, state == ProductRoutingState.Assigned ? row.StationId : null, row.ProductAvailable));
        }

        public async Task<Result<ProductStationAssignmentResponse>> SetAsync(
            Guid productId, Guid branchId, Guid? stationId, string token, Guid actorId, CancellationToken cancellationToken)
        {
            await context.LockBranchesAsync(cancellationToken, branchId);
            if (stationId is { } targetId)
            {
                await context.LockBranchStationAsync(branchId, targetId, cancellationToken);
            }

            await context.LockProductForReferenceAsync(productId, cancellationToken);
            var row = await ReadAsync(productId, branchId, cancellationToken);
            if (row is null || !row.BranchActive)
            {
                return Result.Failure<ProductStationAssignmentResponse>(ProductErrors.NotFound());
            }

            if (!row.Ready)
            {
                return Result.Failure<ProductStationAssignmentResponse>(ProductErrors.RoutingNotReady());
            }

            if (!string.Equals(Token(productId, branchId, row.Version), token, StringComparison.Ordinal))
            {
                return Result.Failure<ProductStationAssignmentResponse>(ProductErrors.ConcurrencyConflict());
            }

            if (row.StationId == stationId)
            {
                return Result.Success(ToResponse(productId, branchId, row));
            }

            if (stationId is { } requested && !await context.Stations.AnyAsync(
                station => station.BranchId == branchId && station.Id == requested && station.Status == StationStatus.Active,
                cancellationToken))
            {
                return Result.Failure<ProductStationAssignmentResponse>(ProductErrors.InvalidStation());
            }

            var slot = await context.ProductBranchStationSlots.FindAsync([productId, branchId], cancellationToken);
            var now = timeProvider.GetUtcNow();
            if (slot is null)
            {
                slot = ProductBranchStationSlot.Create(productId, branchId, now);
                context.ProductBranchStationSlots.Add(slot);
            }
            else
            {
                slot.Touch(now);
                context.Entry(slot).Property(item => item.UpdatedAt).IsModified = true;
            }

            ProductStationAssignment? replacement = stationId is { } id
                ? ProductStationAssignment.Create(productId, branchId, id, now, actorId)
                : null;
            var auditResult = await audit.WriteAsync(new TenantAuditEntry
            {
                Action = "products.station_assignment.changed",
                TargetType = "products",
                TargetId = productId,
                ActorUserId = actorId,
                Changes = TenantAuditChanges.Update(
                [
                    new("BranchId", null, branchId.ToString()),
                    new("StationId", row.StationId?.ToString(), stationId?.ToString()),
                    new("AssignmentId", row.AssignmentId?.ToString(), replacement?.Id.ToString()),
                ]),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<ProductStationAssignmentResponse>(auditResult.Error!);
            }

            if (row.AssignmentId is { } previous)
            {
                var ended = await context.ProductStationAssignments
                    .Where(item => item.Id == previous && item.EndedAt == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.EndedAt, now)
                        .SetProperty(item => item.EndedBy, actorId), cancellationToken);
                if (ended != 1)
                {
                    return Result.Failure<ProductStationAssignmentResponse>(ProductErrors.ConcurrencyConflict());
                }
            }

            if (replacement is not null)
            {
                context.ProductStationAssignments.Add(replacement);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<ProductStationAssignmentResponse>(ProductErrors.ConcurrencyConflict());
            }

            var version = context.Entry(slot).Property<uint>("xmin").CurrentValue;
            return Result.Success(ToResponse(productId, branchId, row with
            {
                AssignmentId = replacement?.Id,
                StationId = replacement?.StationId,
                StationActive = replacement is not null,
                Version = version,
            }));
        }

        internal static ProductRoutingState Classify(
            bool branchActive, Guid? stationId, bool stationActive)
        {
            if (!branchActive)
            {
                return ProductRoutingState.BranchInactive;
            }


            return stationId is null
                ? ProductRoutingState.Unassigned
                : stationActive ? ProductRoutingState.Assigned : ProductRoutingState.StationInactive;
        }

        internal static string Token(Guid productId, Guid branchId, uint? version) =>
            $"{productId:N}:{branchId:N}:{version?.ToString(CultureInfo.InvariantCulture) ?? "empty"}";

        private static ProductStationAssignmentResponse ToResponse(Guid productId, Guid branchId, RoutingRow row)
        {
            var state = Classify(row.BranchActive, row.StationId, row.StationActive);
            var assignment = row.AssignmentId is { } id && row.StationId is { } stationId
                ? new ProductStationAssignmentItem(id, stationId)
                : null;
            return new ProductStationAssignmentResponse(
                productId, branchId, assignment, state, Token(productId, branchId, row.Version));
        }

        private Task<RoutingRow?> ReadAsync(Guid productId, Guid branchId, CancellationToken cancellationToken)
        {
            return (
                from product in context.Products.AsNoTracking()
                where product.Id == productId && context.MenuCategories.EffectivelyVisible()
                    .Any(category => category.Id == product.CategoryId)
                from branch in context.Branches.AsNoTracking()
                where branch.Id == branchId
                join gate in context.ProductRoutingReadiness.AsNoTracking()
                    on true equals gate.Singleton into gates
                from gate in gates.DefaultIfEmpty()
                from assignment in context.ProductStationAssignments.AsNoTracking()
                    .Where(item => item.ProductId == productId && item.BranchId == branchId && item.EndedAt == null)
                    .DefaultIfEmpty()
                select new RoutingRow(
                    gate != null,
                    branch.LifecycleStatus == BranchLifecycleStatus.Active,
                    product.Status == ProductStatus.Active
                        && product.Sizes.Any(size => size.Status == ProductComponentStatus.Active && size.IsDefault),
                    assignment == null ? null : assignment.Id,
                    assignment == null ? null : assignment.StationId,
                    assignment != null && context.Stations.Any(station =>
                        station.Id == assignment.StationId && station.BranchId == branchId
                        && station.Status == StationStatus.Active),
                    context.ProductBranchStationSlots
                        .Where(slot => slot.ProductId == productId && slot.BranchId == branchId)
                        .Select(slot => (uint?)EF.Property<uint>(slot, "xmin"))
                        .FirstOrDefault()))
                .SingleOrDefaultAsync(cancellationToken);
        }

        private sealed record RoutingRow(
            bool Ready, bool BranchActive, bool ProductAvailable, Guid? AssignmentId, Guid? StationId, bool StationActive, uint? Version);
    }
}
