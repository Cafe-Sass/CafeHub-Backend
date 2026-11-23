using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Guests;
using CafeHub.Application.Guests.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Guests
{
    internal sealed class GuestMuteStore(TenantDbContext context, ITenantAuditWriter auditWriter) : IGuestMuteStore
    {
        private const string TargetType = "guest_mutes";

        public async Task<GuestMuteTarget?> FindOrderAsync(
            Guid? branchId,
            Guid orderId,
            CancellationToken cancellationToken)
        {
            var orders = context.Orders.AsNoTracking().Where(order => order.Id == orderId);
            if (branchId is { } branch)
            {
                orders = orders.Where(order => order.BranchId == branch);
            }

            return await orders
                .Select(order => new GuestMuteTarget(
                    order.Id,
                    order.BranchId,
                    order.OrderNumber,
                    order.GuestDeviceId))
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<GuestMuteEntry?> FindActiveAsync(
            Guid branchId,
            string deviceId,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            await Project(context.GuestMutes.AsNoTracking()
                    .Where(mute => mute.BranchId == branchId
                        && mute.DeviceId == deviceId
                        && mute.LiftedAt == null
                        && mute.ExpiresAt > now))
                .OrderByDescending(entry => entry.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<GuestMuteEntry?> FindAsync(
            Guid? branchId,
            Guid muteId,
            CancellationToken cancellationToken)
        {
            var entries = Project(context.GuestMutes.AsNoTracking()).Where(entry => entry.Id == muteId);
            if (branchId is { } branch)
            {
                entries = entries.Where(entry => entry.BranchId == branch);
            }

            return await entries.SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<Result> CreateAsync(GuestMute mute, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(mute);

            context.GuestMutes.Add(mute);
            var audit = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "guests.muted.succeeded",
                TargetType = TargetType,
                TargetId = mute.Id,
                ActorUserId = mute.MutedByUserId,
                Changes = TenantAuditChangeSets.GuestMuteCreate(mute),
            }, cancellationToken);
            if (audit.IsFailure)
            {
                return audit;
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> LiftAsync(
            Guid muteId,
            Guid liftedByUserId,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var mute = await context.GuestMutes
                .SingleOrDefaultAsync(candidate => candidate.Id == muteId, cancellationToken);
            if (mute is null)
            {
                return Result.Failure(GuestMuteErrors.NotFound());
            }

            if (!mute.Lift(liftedByUserId, now))
            {
                return Result.Success();
            }

            var audit = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "guests.unmuted.succeeded",
                TargetType = TargetType,
                TargetId = mute.Id,
                ActorUserId = liftedByUserId,
                Changes = TenantAuditChangeSets.GuestMuteLift(mute),
            }, cancellationToken);
            if (audit.IsFailure)
            {
                return audit;
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        private IQueryable<GuestMuteEntry> Project(IQueryable<GuestMute> mutes) => mutes
            .Select(mute => new GuestMuteEntry(
                mute.Id,
                mute.BranchId,
                mute.OrderId,
                context.Orders
                    .Where(order => order.Id == mute.OrderId)
                    .Select(order => order.OrderNumber)
                    .FirstOrDefault(),
                mute.MutedByUserId,
                mute.Reason,
                mute.CreatedAt,
                mute.ExpiresAt,
                mute.LiftedAt,
                mute.LiftedByUserId));
    }
}
