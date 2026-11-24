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
    internal sealed class WifiSettingsStore(
        TenantDbContext context,
        ITenantAuditWriter auditWriter,
        TimeProvider timeProvider) : IWifiSettingsStore
    {
        private const string SettingsTargetType = "wifi_settings";
        private const string NetworkTargetType = "branch_wifi_networks";

        public async Task<WifiSettingsResponse> GetAsync(Guid? branchId, CancellationToken cancellationToken)
        {
            var settings = await context.WifiSettings.FirstOrDefaultAsync(cancellationToken);
            if (settings is null)
            {
                return new WifiSettingsResponse(
                    WifiPolicy.Free,
                    string.Empty,
                    await ReadNetworksAsync(branchId, cancellationToken));
            }

            var token = context.Entry(settings).Property<uint>("xmin").CurrentValue
                .ToString(CultureInfo.InvariantCulture);
            return new WifiSettingsResponse(
                settings.Policy,
                token,
                await ReadNetworksAsync(branchId, cancellationToken));
        }

        public async Task<Result<WifiSettingsResponse>> UpdatePolicyAsync(
            WifiPolicy policy,
            string concurrencyToken,
            Guid actorUserId,
            Guid? branchId,
            CancellationToken cancellationToken)
        {
            var settings = await context.WifiSettings.FirstOrDefaultAsync(cancellationToken);
            var now = timeProvider.GetUtcNow();
            var isCreate = settings is null;
            var previousPolicy = settings?.Policy ?? WifiPolicy.Free;
            if (settings is null)
            {
                if (!string.IsNullOrEmpty(concurrencyToken))
                {
                    return Result.Failure<WifiSettingsResponse>(WifiErrors.ConcurrencyConflict());
                }

                settings = WifiSettings.Create(Guid.CreateVersion7(), now, actorUserId);
                settings.SetPolicy(policy, now, actorUserId);
                context.WifiSettings.Add(settings);
            }
            else
            {
                if (!uint.TryParse(concurrencyToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var token))
                {
                    return Result.Failure<WifiSettingsResponse>(ValidationError.Create(
                        [new FieldError("concurrencyToken", "The concurrency token is not a valid unsigned integer.")]));
                }

                if (settings.Policy == policy)
                {
                    return Result.Success(await BuildResponseAsync(settings, branchId, cancellationToken));
                }

                context.Entry(settings).Property<uint>("xmin").OriginalValue = token;
                settings.SetPolicy(policy, now, actorUserId);
            }

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = isCreate ? "wifi_settings.create.succeeded" : "wifi_settings.update.succeeded",
                TargetType = SettingsTargetType,
                TargetId = settings.Id,
                ActorUserId = actorUserId,
                Changes = isCreate
                    ? TenantAuditChangeSets.WifiSettingsCreate(settings)
                    : TenantAuditChangeSets.WifiSettingsPolicy(previousPolicy, policy),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<WifiSettingsResponse>(auditResult.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<WifiSettingsResponse>(WifiErrors.ConcurrencyConflict());
            }
            catch (DbUpdateException exception) when (IsSingletonClash(exception))
            {
                return Result.Failure<WifiSettingsResponse>(WifiErrors.ConcurrencyConflict());
            }

            return Result.Success(await BuildResponseAsync(settings, branchId, cancellationToken));
        }

        public async Task<Result<WifiNetworkResponse?>> SetNetworkAsync(
            Guid? branchIdScope,
            Guid branchId,
            string? ssid,
            WifiSecurity? security,
            string? password,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            if (branchIdScope.HasValue && branchIdScope != branchId)
            {
                return Result.Failure<WifiNetworkResponse?>(WifiErrors.NotFound());
            }

            var branchExists = await context.Branches.AsNoTracking()
                .AnyAsync(branch => branch.Id == branchId, cancellationToken);
            if (!branchExists)
            {
                return Result.Failure<WifiNetworkResponse?>(WifiErrors.NotFound());
            }

            var network = await context.BranchWifiNetworks
                .FirstOrDefaultAsync(candidate => candidate.BranchId == branchId, cancellationToken);
            var now = timeProvider.GetUtcNow();

            if (string.IsNullOrWhiteSpace(ssid))
            {
                if (network is null)
                {
                    return Result.Success<WifiNetworkResponse?>(null);
                }

                var cleared = await auditWriter.WriteAsync(new TenantAuditEntry
                {
                    Action = "branch_wifi_networks.cleared.succeeded",
                    TargetType = NetworkTargetType,
                    TargetId = network.BranchId,
                    ActorUserId = actorUserId,
                    Changes = TenantAuditChangeSets.BranchWifiNetworkClear(network),
                }, cancellationToken);
                if (cleared.IsFailure)
                {
                    return Result.Failure<WifiNetworkResponse?>(cleared.Error!);
                }

                context.BranchWifiNetworks.Remove(network);
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success<WifiNetworkResponse?>(null);
            }

            if (security is not { } securityType)
            {
                return Result.Failure<WifiNetworkResponse?>(ValidationError.Create(
                    [new FieldError("security", "The WiFi security type is required when a network name is set.")]));
            }

            var isCreate = network is null;
            var changes = isCreate
                ? TenantAuditChangeSets.BranchWifiNetworkCreate(branchId, ssid, securityType, password)
                : TenantAuditChangeSets.BranchWifiNetworkUpdate(network!, ssid, securityType, password);
            if (network is null)
            {
                network = BranchWifiNetwork.Create(branchId, ssid, securityType, password, now, actorUserId);
                context.BranchWifiNetworks.Add(network);
            }
            else
            {
                network.Update(ssid, securityType, password, now, actorUserId);
            }

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "branch_wifi_networks.set.succeeded",
                TargetType = NetworkTargetType,
                TargetId = branchId,
                ActorUserId = actorUserId,
                Changes = changes,
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<WifiNetworkResponse?>(auditResult.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success<WifiNetworkResponse?>(ToResponse(network));
        }

        private async Task<WifiSettingsResponse> BuildResponseAsync(
            WifiSettings settings,
            Guid? branchId,
            CancellationToken cancellationToken)
        {
            var token = context.Entry(settings).Property<uint>("xmin").CurrentValue
                .ToString(CultureInfo.InvariantCulture);
            return new WifiSettingsResponse(
                settings.Policy,
                token,
                await ReadNetworksAsync(branchId, cancellationToken));
        }

        private async Task<IReadOnlyList<WifiNetworkResponse>> ReadNetworksAsync(
            Guid? branchId,
            CancellationToken cancellationToken)
        {
            var networks = context.BranchWifiNetworks.AsNoTracking();
            if (branchId is { } branch)
            {
                networks = networks.Where(network => network.BranchId == branch);
            }

            return await networks
                .OrderBy(network => network.BranchId)
                .Select(network => new WifiNetworkResponse(
                    network.BranchId,
                    network.Ssid,
                    network.Security.ToString(),
                    network.Password))
                .ToListAsync(cancellationToken);
        }

        private static WifiNetworkResponse ToResponse(BranchWifiNetwork network) =>
            new(network.BranchId, network.Ssid, network.Security.ToString(), network.Password);

        private static bool IsSingletonClash(DbUpdateException exception) =>
            exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName == "uq_wifi_settings_singleton";
    }
}
