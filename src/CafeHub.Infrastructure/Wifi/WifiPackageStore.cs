using System.Globalization;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Wifi;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Wifi
{
    internal sealed class WifiPackageStore(
        TenantDbContext context,
        ITenantAuditWriter auditWriter,
        TimeProvider timeProvider) : IWifiPackageStore
    {
        private const string TargetType = "wifi_packages";

        public async Task<WifiPackageListPage> ListAsync(
            WifiPackageListRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var packages = context.WifiPackages.AsNoTracking();
            if (request.Status is { } status)
            {
                packages = packages.Where(package => package.Status == status);
            }

            int? totalCount = request.IncludeTotalCount
                ? await packages.CountAsync(cancellationToken)
                : null;

            var rows = await packages
                .OrderBy(package => package.SortOrder)
                .ThenBy(package => package.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize + 1)
                .Select(package => new WifiPackageListEntry(
                    package.Id,
                    package.Name,
                    package.DurationMinutes,
                    package.SpeedMbps,
                    package.DataMode,
                    package.QuotaMb,
                    package.Price,
                    package.SortOrder,
                    package.Status))
                .ToListAsync(cancellationToken);

            return new WifiPackageListPage(rows, totalCount);
        }

        public async Task<WifiPackageResponse?> GetAsync(
            Guid packageId,
            CancellationToken cancellationToken)
        {
            var package = await context.WifiPackages
                .FirstOrDefaultAsync(candidate => candidate.Id == packageId, cancellationToken);
            return package is null ? null : ToResponse(package, GetVersion(package));
        }

        public Task<bool> ActiveNameExistsAsync(
            string name,
            Guid? excludePackageId,
            CancellationToken cancellationToken)
        {
            var normalized = name.Trim().ToLowerInvariant();
            return context.WifiPackages.AnyAsync(
                package => package.Status == WifiPackageStatus.Active
                    && package.Name.ToLower() == normalized
                    && (excludePackageId == null || package.Id != excludePackageId),
                cancellationToken);
        }

        public async Task<Result<WifiPackageResponse>> CreateAsync(
            WifiPackage package,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(package);

            context.WifiPackages.Add(package);
            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "wifi_packages.created.succeeded",
                TargetType = TargetType,
                TargetId = package.Id,
                ActorUserId = package.CreatedBy,
                Changes = TenantAuditChangeSets.WifiPackageCreate(package),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<WifiPackageResponse>(auditResult.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsActiveNameClash(exception))
            {
                return Result.Failure<WifiPackageResponse>(WifiErrors.PackageNameConflict());
            }

            return Result.Success(ToResponse(package, GetVersion(package)));
        }

        public async Task<Result<WifiPackageResponse>> UpdateAsync(
            Guid packageId,
            string concurrencyToken,
            string name,
            int durationMinutes,
            decimal? speedMbps,
            WifiDataMode dataMode,
            int? quotaMb,
            decimal price,
            int sortOrder,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var package = await context.WifiPackages
                .FirstOrDefaultAsync(candidate => candidate.Id == packageId, cancellationToken);
            if (package is null)
            {
                return Result.Failure<WifiPackageResponse>(WifiErrors.PackageNotFound());
            }

            if (!TryParseVersion(concurrencyToken, out var version))
            {
                return Result.Failure<WifiPackageResponse>(InvalidConcurrencyToken());
            }

            if (GetVersion(package) != version)
            {
                return Result.Failure<WifiPackageResponse>(WifiErrors.PackageConcurrencyConflict());
            }

            var before = WifiPackageAuditValues.From(package);
            if (!package.Update(
                    name,
                    durationMinutes,
                    speedMbps,
                    dataMode,
                    quotaMb,
                    price,
                    sortOrder,
                    timeProvider.GetUtcNow(),
                    actorUserId))
            {
                return Result.Success(ToResponse(package, version));
            }

            context.Entry(package).Property<uint>("xmin").OriginalValue = version;
            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "wifi_packages.updated.succeeded",
                TargetType = TargetType,
                TargetId = package.Id,
                ActorUserId = actorUserId,
                Changes = TenantAuditChangeSets.WifiPackageUpdate(before, package),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<WifiPackageResponse>(auditResult.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<WifiPackageResponse>(WifiErrors.PackageConcurrencyConflict());
            }
            catch (DbUpdateException exception) when (IsActiveNameClash(exception))
            {
                return Result.Failure<WifiPackageResponse>(WifiErrors.PackageNameConflict());
            }

            return Result.Success(ToResponse(package, GetVersion(package)));
        }

        public async Task<Result<WifiPackageResponse>> ChangeStatusAsync(
            Guid packageId,
            WifiPackageStatus status,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var package = await context.WifiPackages
                .FirstOrDefaultAsync(candidate => candidate.Id == packageId, cancellationToken);
            if (package is null)
            {
                return Result.Failure<WifiPackageResponse>(WifiErrors.PackageNotFound());
            }

            if (!TryParseVersion(concurrencyToken, out var version))
            {
                return Result.Failure<WifiPackageResponse>(InvalidConcurrencyToken());
            }

            if (GetVersion(package) != version)
            {
                return Result.Failure<WifiPackageResponse>(WifiErrors.PackageConcurrencyConflict());
            }

            var oldStatus = package.Status;
            var changed = status == WifiPackageStatus.Active
                ? package.Reactivate(timeProvider.GetUtcNow(), actorUserId)
                : package.Deactivate(timeProvider.GetUtcNow(), actorUserId);
            if (!changed)
            {
                return Result.Success(ToResponse(package, version));
            }

            context.Entry(package).Property<uint>("xmin").OriginalValue = version;
            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = status == WifiPackageStatus.Active
                    ? "wifi_packages.reactivated.succeeded"
                    : "wifi_packages.deactivated.succeeded",
                TargetType = TargetType,
                TargetId = package.Id,
                ActorUserId = actorUserId,
                Changes = TenantAuditChanges.Update(new[]
                {
                    new TenantAuditChanges.FieldChange(
                        nameof(WifiPackage.Status), oldStatus.ToString(), package.Status.ToString()),
                }),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<WifiPackageResponse>(auditResult.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<WifiPackageResponse>(WifiErrors.PackageConcurrencyConflict());
            }
            catch (DbUpdateException exception) when (IsActiveNameClash(exception))
            {
                return Result.Failure<WifiPackageResponse>(WifiErrors.PackageNameConflict());
            }

            return Result.Success(ToResponse(package, GetVersion(package)));
        }

        private static WifiPackageResponse ToResponse(WifiPackage package, uint version) =>
            WifiPackageResponse.From(package, version.ToString(CultureInfo.InvariantCulture));

        private uint GetVersion(WifiPackage package) =>
            context.Entry(package).Property<uint>("xmin").CurrentValue;

        private static bool TryParseVersion(string? value, out uint version) =>
            uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out version);

        private static Error InvalidConcurrencyToken() =>
            ValidationError.Create(
                [new FieldError("concurrencyToken", "The concurrency token is not a valid unsigned integer.")]);

        private static bool IsActiveNameClash(DbUpdateException exception) =>
            exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName == "uq_wifi_packages_active_name";
    }
}
