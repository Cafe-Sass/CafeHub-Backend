using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Tables.Contracts;
using CafeHub.Application.Wifi;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Wifi
{
    internal sealed class WifiQrStore(
        TenantDbContext context,
        ITableQrCodeGenerator codeGenerator,
        ITenantAuditWriter auditWriter,
        TimeProvider timeProvider) : IWifiQrStore
    {
        private const string TargetType = "branch_wifi_qr";

        public async Task<Result<WifiQrData>> GetOrCreateAsync(
            Guid? branchIdScope,
            Guid branchId,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            if (!await BranchExistsAsync(branchIdScope, branchId, cancellationToken))
            {
                return Result.Failure<WifiQrData>(WifiErrors.NotFound());
            }

            var qr = await context.BranchWifiQr
                .FirstOrDefaultAsync(candidate => candidate.BranchId == branchId, cancellationToken);
            if (qr is not null)
            {
                return Result.Success(ToData(qr));
            }

            qr = BranchWifiQr.Create(
                branchId,
                codeGenerator.Generate(),
                timeProvider.GetUtcNow(),
                actorUserId);
            context.BranchWifiQr.Add(qr);
            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "branch_wifi_qr.created.succeeded",
                TargetType = TargetType,
                TargetId = branchId,
                ActorUserId = actorUserId,
                Changes = TenantAuditChanges.Create(new[]
                {
                    new TenantAuditChanges.FieldChange(nameof(BranchWifiQr.Status), null, qr.Status.ToString()),
                }),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<WifiQrData>(auditResult.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success(ToData(qr));
            }
            catch (DbUpdateException exception) when (IsBranchClash(exception))
            {
                context.Entry(qr).State = EntityState.Detached;
                var stored = await context.BranchWifiQr.AsNoTracking()
                    .SingleOrDefaultAsync(candidate => candidate.BranchId == branchId, cancellationToken);
                return stored is null
                    ? Result.Failure<WifiQrData>(WifiErrors.QrConflict())
                    : Result.Success(ToData(stored));
            }
        }

        public async Task<Result<WifiQrData>> RegenerateAsync(
            Guid? branchIdScope,
            Guid branchId,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            await context.LockBranchesAsync(cancellationToken, branchId);
            if (!await BranchExistsAsync(branchIdScope, branchId, cancellationToken))
            {
                return Result.Failure<WifiQrData>(WifiErrors.NotFound());
            }

            var qr = await context.BranchWifiQr
                .FirstOrDefaultAsync(candidate => candidate.BranchId == branchId, cancellationToken);
            var now = timeProvider.GetUtcNow();
            var created = qr is null;
            if (qr is null)
            {
                qr = BranchWifiQr.Create(branchId, codeGenerator.Generate(), now, actorUserId);
                context.BranchWifiQr.Add(qr);
            }
            else
            {
                var replacement = codeGenerator.Generate();
                for (var attempt = 0; attempt < 3 && replacement == qr.QrCode; attempt++)
                {
                    replacement = codeGenerator.Generate();
                }

                qr.Regenerate(replacement, now, actorUserId);
            }

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = created
                    ? "branch_wifi_qr.created.succeeded"
                    : "branch_wifi_qr.regenerated.succeeded",
                TargetType = TargetType,
                TargetId = branchId,
                ActorUserId = actorUserId,
                Changes = created
                    ? TenantAuditChanges.Create(new[]
                    {
                        new TenantAuditChanges.FieldChange(
                            nameof(BranchWifiQr.Status), null, qr.Status.ToString()),
                    })
                    : null,
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<WifiQrData>(auditResult.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success(ToData(qr));
            }
            catch (DbUpdateException exception) when (IsCodeClash(exception))
            {
                return Result.Failure<WifiQrData>(WifiErrors.QrConflict());
            }
        }

        public async Task<WifiQrContext?> ResolveAsync(string code, CancellationToken cancellationToken)
        {
            var branchId = await (
                    from qr in context.BranchWifiQr.AsNoTracking()
                    join branch in context.Branches on qr.BranchId equals branch.Id
                    where qr.QrCode == code
                        && qr.Status == WifiQrStatus.Active
                        && branch.LifecycleStatus == BranchLifecycleStatus.Active
                    select (Guid?)qr.BranchId)
                .SingleOrDefaultAsync(cancellationToken);
            if (branchId is null)
            {
                return null;
            }

            var settings = await context.WifiSettings.AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);
            var policy = settings?.Policy ?? WifiPolicy.Free;
            var network = await context.BranchWifiNetworks.AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.BranchId == branchId, cancellationToken);
            var packages = await context.WifiPackages.AsNoTracking()
                .Where(package => package.Status == WifiPackageStatus.Active)
                .OrderBy(package => package.SortOrder)
                .ThenBy(package => package.Id)
                .ToListAsync(cancellationToken);

            return new WifiQrContext(branchId.Value, policy, network, packages);
        }

        private async Task<bool> BranchExistsAsync(
            Guid? branchIdScope,
            Guid branchId,
            CancellationToken cancellationToken)
        {
            if (branchIdScope.HasValue && branchIdScope != branchId)
            {
                return false;
            }

            return await context.Branches.AsNoTracking()
                .AnyAsync(branch => branch.Id == branchId, cancellationToken);
        }

        private static WifiQrData ToData(BranchWifiQr qr) => new(qr.BranchId, qr.QrCode);

        private static bool IsBranchClash(DbUpdateException exception) =>
            exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName == "pk_branch_wifi_qr";

        private static bool IsCodeClash(DbUpdateException exception) =>
            exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName == "uq_branch_wifi_qr_code";
    }
}
