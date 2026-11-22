using CafeHub.Application.Identity;
using CafeHub.Application.Orders;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CafeHub.Infrastructure.Orders
{
    internal sealed class OrderExpiryStore(TenantDbContext context) : IOrderExpiryStore
    {
        private const string StaleBranchIdsSql =
            """
            SELECT id FROM orders
            WHERE status = 'Pending' AND response_deadline <= @now AND branch_id = @scope
            """;

        private const string StaleOrderIdSql =
            """
            SELECT id FROM orders
            WHERE status = 'Pending' AND response_deadline <= @now AND id = @scope
            """;

        private const string ExpireBranchSql =
            """
            UPDATE orders
            SET status = 'Expired', updated_at = @now, updated_by = NULL
            WHERE status = 'Pending' AND response_deadline <= @now AND branch_id = @scope
            RETURNING id
            """;

        private const string ExpireOrderSql =
            """
            UPDATE orders
            SET status = 'Expired', updated_at = @now, updated_by = NULL
            WHERE status = 'Pending' AND response_deadline <= @now AND id = @scope
            RETURNING id
            """;

        public async Task<IReadOnlyList<Guid>> ExpireStaleOrdersAsync(
            Guid? branchId,
            Guid? orderId,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            if (branchId is { } branch)
            {
                return await ExpireAsync(StaleBranchIdsSql, ExpireBranchSql, branch, now, cancellationToken);
            }

            if (orderId is { } order)
            {
                return await ExpireAsync(StaleOrderIdSql, ExpireOrderSql, order, now, cancellationToken);
            }

            throw new ArgumentException("The branch id or the order id is required.", nameof(branchId));
        }

        private async Task<IReadOnlyList<Guid>> ExpireAsync(
            string staleIdsSql,
            string expireSql,
            Guid scope,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var staleIds = await ReadIdsAsync(staleIdsSql, scope, now, cancellationToken);
            if (staleIds.Count == 0)
            {
                return staleIds;
            }

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var expiredIds = await ReadIdsAsync(expireSql, scope, now, cancellationToken);
            if (expiredIds.Count == 0)
            {
                return expiredIds;
            }

            var expiredOrders = await context.Orders
                .AsNoTracking()
                .Where(order => expiredIds.Contains(order.Id))
                .Select(order => new { order.Id, order.BranchId })
                .ToListAsync(cancellationToken);
            var itemStations = await context.OrderItems
                .AsNoTracking()
                .Where(item => expiredIds.Contains(item.OrderId))
                .Select(item => new { item.OrderId, item.StationId })
                .ToListAsync(cancellationToken);
            var candidates = await ListHolderCandidatesAsync(expiredOrders[0].BranchId, cancellationToken);

            foreach (var expiredOrder in expiredOrders)
            {
                var miss = OrderResponseMiss.Create(
                    Guid.CreateVersion7(),
                    expiredOrder.Id,
                    expiredOrder.BranchId,
                    expiredAt: now,
                    now);
                var itemStationIds = itemStations
                    .Where(item => item.OrderId == expiredOrder.Id)
                    .Select(item => (Guid?)item.StationId)
                    .ToList();
                foreach (var holderId in OrderResponseMissHolders.Resolve(candidates, itemStationIds))
                {
                    miss.AddHolder(holderId, now);
                }

                context.OrderResponseMisses.Add(miss);
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return expiredIds;
        }

        private async Task<List<Guid>> ReadIdsAsync(
            string sql,
            Guid scope,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var connection = context.Database.GetDbConnection();
            var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();

            var nowParameter = command.CreateParameter();
            nowParameter.ParameterName = "now";
            nowParameter.Value = now;
            command.Parameters.Add(nowParameter);
            var scopeParameter = command.CreateParameter();
            scopeParameter.ParameterName = "scope";
            scopeParameter.Value = scope;
            command.Parameters.Add(scopeParameter);

            var ids = new List<Guid>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }

            return ids;
        }

        private async Task<IReadOnlyList<MissHolderCandidate>> ListHolderCandidatesAsync(
            Guid branchId,
            CancellationToken cancellationToken)
        {
            var staff = context.TenantUsers
                .Where(user => user.DeletedAt == null
                    && user.UserKind == TenantUserKind.Staff
                    && user.BranchId == branchId
                    && context.Branches.Any(branch =>
                        branch.Id == user.BranchId && branch.LifecycleStatus == BranchLifecycleStatus.Active));

            var roleDecide =
                from user in staff
                join membership in context.TenantUserRoles on user.Id equals membership.TenantUserId
                join role in context.TenantRoles on membership.RoleId equals role.Id
                where role.DeletedAt == null && role.NormalizedName != TenantRole.NormalizedOwnerName
                join claim in context.TenantRoleClaims on role.Id equals claim.RoleId
                where claim.ClaimType == TenantPermissions.ClaimType
                    && claim.ClaimValue == TenantPermissions.OrdersDecide
                select user.Id;

            var personalDecide =
                from user in staff
                join claim in context.TenantUserClaims on user.Id equals claim.TenantUserId
                where claim.ClaimType == TenantPermissions.ClaimType
                    && claim.ClaimValue == TenantPermissions.OrdersDecide
                select user.Id;

            var deciderIds = await roleDecide.Union(personalDecide).ToListAsync(cancellationToken);
            if (deciderIds.Count == 0)
            {
                return [];
            }

            var assignments = await context.TenantUserStations
                .Where(assignment => deciderIds.Contains(assignment.TenantUserId)
                    && context.Stations.Any(station =>
                        station.Id == assignment.StationId
                        && station.Status == StationStatus.Active
                        && station.BranchId == branchId))
                .Select(assignment => new { assignment.TenantUserId, assignment.StationId })
                .ToListAsync(cancellationToken);

            var candidates = new List<MissHolderCandidate>();
            foreach (var userId in deciderIds)
            {
                var stationIds = assignments
                    .Where(assignment => assignment.TenantUserId == userId)
                    .Select(assignment => assignment.StationId)
                    .ToList();
                candidates.Add(new MissHolderCandidate(userId, stationIds.Count > 0 ? stationIds : null));
            }

            return candidates;
        }
    }
}
