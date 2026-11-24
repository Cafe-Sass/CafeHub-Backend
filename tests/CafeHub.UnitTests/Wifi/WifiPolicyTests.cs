using System.Reflection;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Wifi;
using CafeHub.Application.Wifi.Commands;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Application.Wifi.Queries;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Wifi
{
    public sealed class WifiPolicyTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OtherBranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-24T12:00:00Z");

        [Fact]
        public void ADefaultPolicyIsFreeAndSwitchingKeepsBothDataSets()
        {
            var settings = WifiSettings.Create(Guid.CreateVersion7(), Now, UserId);
            var network = BranchWifiNetwork.Create(BranchId, "CafeGuest", WifiSecurity.WPA, "secret-pass", Now, UserId);
            var store = new FakeWifiSettingsStore(settings.Policy, network);

            Assert.Equal(WifiPolicy.Free, settings.Policy);

            settings.SetPolicy(WifiPolicy.Paid, Now, UserId);
            Assert.Equal(WifiPolicy.Paid, settings.Policy);

            settings.SetPolicy(WifiPolicy.Free, Now, UserId);
            Assert.Equal(WifiPolicy.Free, settings.Policy);
            Assert.Equal("CafeGuest", store.Networks.Single().Ssid);
        }

        [Fact]
        public async Task SwitchingThePolicyNeverTouchesTheBranchNetworks()
        {
            var network = BranchWifiNetwork.Create(BranchId, "CafeGuest", WifiSecurity.WPA, "secret-pass", Now, UserId);
            var store = new FakeWifiSettingsStore(WifiPolicy.Free, network);
            var handler = new UpdateWifiSettingsCommandHandler(store, StaffUser(BranchId));

            var paid = await handler.Handle(new UpdateWifiSettingsCommand(WifiPolicy.Paid, store.Token), default);
            var restored = await new GetWifiSettingsQueryHandler(store, StaffUser(BranchId))
                .Handle(new GetWifiSettingsQuery(), default);
            var free = await handler.Handle(new UpdateWifiSettingsCommand(WifiPolicy.Free, store.Token), default);

            Assert.True(paid.IsSuccess, paid.Error?.Code);
            Assert.True(free.IsSuccess, free.Error?.Code);
            Assert.Equal(WifiPolicy.Free, store.Policy);
            Assert.Equal("CafeGuest", Assert.Single(store.Networks).Ssid);
            Assert.Equal("CafeGuest", Assert.Single(restored.Value!.Networks).Ssid);
        }

        [Fact]
        public async Task AnotherBranchesStaffGetsTheUniformNotFound()
        {
            var network = BranchWifiNetwork.Create(BranchId, "CafeGuest", WifiSecurity.WPA, null, Now, UserId);
            var store = new FakeWifiSettingsStore(WifiPolicy.Free, network);
            var handler = new SetBranchWifiNetworkCommandHandler(store, StaffUser(OtherBranchId));

            var result = await handler.Handle(
                new SetBranchWifiNetworkCommand(BranchId, "Other", WifiSecurity.WPA, "password1"),
                default);

            Assert.False(result.IsSuccess);
            Assert.Equal(WifiErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(0, store.NetworkWrites);
            Assert.Equal("CafeGuest", store.Networks.Single().Ssid);
        }

        [Fact]
        public async Task TheOwnBranchSetsAndClearsTheNetwork()
        {
            var store = new FakeWifiSettingsStore(WifiPolicy.Free, null);
            var handler = new SetBranchWifiNetworkCommandHandler(store, StaffUser(BranchId));

            var set = await handler.Handle(
                new SetBranchWifiNetworkCommand(BranchId, "  CafeGuest  ", WifiSecurity.WPA, "  secret  "),
                default);
            var cleared = await handler.Handle(
                new SetBranchWifiNetworkCommand(BranchId, null, null, null),
                default);

            Assert.True(set.IsSuccess, set.Error?.Code);
            Assert.Equal("CafeGuest", set.Value!.Ssid);
            Assert.Equal("WPA", set.Value.Security);
            Assert.Equal("secret", set.Value.Password);
            Assert.True(cleared.IsSuccess, cleared.Error?.Code);
            Assert.Null(cleared.Value);
            Assert.Empty(store.Networks);
        }

        [Fact]
        public void InvalidLengthsAndControlCharactersAreRefused()
        {
            Assert.True(BranchWifiNetwork.IsValidSsid(new string('a', 32)));
            Assert.False(BranchWifiNetwork.IsValidSsid(new string('a', 33)));
            Assert.False(BranchWifiNetwork.IsValidSsid("   "));
            Assert.False(BranchWifiNetwork.IsValidSsid("Cafe\nGuest"));
            Assert.True(BranchWifiNetwork.IsValidPassword(new string('p', 63)));
            Assert.False(BranchWifiNetwork.IsValidPassword(new string('p', 64)));
            Assert.False(BranchWifiNetwork.IsValidPassword("bad\u0001pass"));
            Assert.True(BranchWifiNetwork.IsValidPassword(null));

            Assert.Throws<ArgumentException>(
                () => BranchWifiNetwork.Create(BranchId, new string('a', 33), WifiSecurity.None, null, Now, UserId));
            Assert.Throws<ArgumentException>(
                () => BranchWifiNetwork.Create(BranchId, "Cafe", WifiSecurity.WPA, new string('p', 64), Now, UserId));
            Assert.Throws<ArgumentException>(
                () => BranchWifiNetwork.Create(BranchId, "Cafe", WifiSecurity.WPA, "bad\u0001pass", Now, UserId));
        }

        [Fact]
        public void ValidatorsRefuseBadPoliciesAndSecurityValues()
        {
            var policyValidator = new UpdateWifiSettingsCommandValidator();
            policyValidator.TestValidate(new UpdateWifiSettingsCommand((WifiPolicy)(-1), "1"))
                .ShouldHaveValidationErrorFor(command => command.Policy);
            policyValidator.TestValidate(new UpdateWifiSettingsCommand(WifiPolicy.Free, null!))
                .ShouldHaveValidationErrorFor(command => command.ConcurrencyToken);
            policyValidator.TestValidate(new UpdateWifiSettingsCommand(WifiPolicy.Paid, "1"))
                .ShouldNotHaveAnyValidationErrors();

            var networkValidator = new SetBranchWifiNetworkCommandValidator();
            networkValidator.TestValidate(new SetBranchWifiNetworkCommand(Guid.Empty, "Cafe", WifiSecurity.None, null))
                .ShouldHaveValidationErrorFor(command => command.BranchId);
            networkValidator.TestValidate(new SetBranchWifiNetworkCommand(BranchId, "Cafe", null, null))
                .ShouldHaveValidationErrorFor(command => command.Security);
            networkValidator.TestValidate(new SetBranchWifiNetworkCommand(BranchId, "Cafe", WifiSecurity.None, "bad\u0001pass"))
                .ShouldHaveValidationErrorFor(command => command.Password);
            networkValidator.TestValidate(new SetBranchWifiNetworkCommand(BranchId, null, null, null))
                .ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void AuditNeverContainsThePassword()
        {
            var network = BranchWifiNetwork.Create(BranchId, "CafeGuest", WifiSecurity.WPA, "top-secret", Now, UserId);
            var create = TenantAuditSnapshotSerializer.Create(
                "branch_wifi_networks",
                TenantAuditChangeSets.BranchWifiNetworkCreate(
                    BranchId, "CafeGuest", WifiSecurity.WPA, "top-secret"));
            var clear = TenantAuditSnapshotSerializer.Create(
                "branch_wifi_networks",
                TenantAuditChangeSets.BranchWifiNetworkClear(network));
            var settings = TenantAuditSnapshotSerializer.Create(
                "wifi_settings",
                TenantAuditChangeSets.WifiSettingsPolicy(WifiPolicy.Free, WifiPolicy.Paid));

            Assert.True(create.IsSuccess, create.Error?.Code);
            Assert.Contains("\"Ssid\":\"CafeGuest\"", create.Value.AfterState, StringComparison.Ordinal);
            Assert.Contains("\"Security\":\"WPA\"", create.Value.AfterState, StringComparison.Ordinal);
            Assert.DoesNotContain("top-secret", create.Value.AfterState, StringComparison.Ordinal);
            Assert.DoesNotContain("password", create.Value.AfterState, StringComparison.OrdinalIgnoreCase);
            Assert.True(clear.IsSuccess, clear.Error?.Code);
            Assert.DoesNotContain("top-secret", clear.Value.BeforeState ?? string.Empty, StringComparison.Ordinal);
            Assert.True(settings.IsSuccess, settings.Error?.Code);
            Assert.Contains("\"Policy\":\"Paid\"", settings.Value.AfterState, StringComparison.Ordinal);
        }

        [Fact]
        public void EndpointsRequireWifiManageAndTheCatalogOffersIt()
        {
            var get = typeof(TenantWifiController).GetMethod(nameof(TenantWifiController.GetSettings))!;
            var put = typeof(TenantWifiController).GetMethod(nameof(TenantWifiController.PutSettings))!;
            var network = typeof(TenantBranchWifiController).GetMethod(nameof(TenantBranchWifiController.SetNetwork))!;

            Assert.Equal(
                "t/{slug}/api/v1/wifi",
                typeof(TenantWifiController).GetCustomAttribute<RouteAttribute>()!.Template);
            Assert.Equal("settings", Assert.IsType<HttpGetAttribute>(
                get.GetCustomAttribute<HttpGetAttribute>()).Template);
            Assert.Equal("settings", Assert.IsType<HttpPutAttribute>(
                put.GetCustomAttribute<HttpPutAttribute>()).Template);
            Assert.Equal("network", Assert.IsType<HttpPutAttribute>(
                network.GetCustomAttribute<HttpPutAttribute>()).Template);
            Assert.Equal(
                "t/{slug}/api/v1/branches/{branchId:guid}/wifi",
                typeof(TenantBranchWifiController).GetCustomAttribute<RouteAttribute>()!.Template);
            Assert.All(
                new[] { get, put, network },
                action => Assert.Equal(
                    TenantPermissions.WifiManage,
                    Assert.IsType<TenantAuthorizeAttribute>(
                        action.GetCustomAttribute<TenantAuthorizeAttribute>()).Policy));

            Assert.Equal("wifi.manage", TenantPermissions.WifiManage);
            Assert.Contains(TenantPermissions.WifiManage, TenantPermissions.All);
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.WifiManage));
            Assert.False(TenantPermissions.IsOwnerOnly(TenantPermissions.WifiManage));
        }

        private static FakeTenantUser StaffUser(Guid branchId) =>
            new(UserId, new TenantAccountAccess(true, null, true) { BranchId = branchId });

        private sealed class FakeTenantUser(Guid? userId, TenantAccountAccess? accountAccess) : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId => userId;

            public Guid? TenantId => BranchId;

            public IReadOnlyCollection<string> Roles => [];

            public Guid? BranchId => AccountAccess?.BranchId;

            public TenantAccountAccess? AccountAccess { get; } = accountAccess;
        }

        private sealed class FakeWifiSettingsStore(WifiPolicy policy, BranchWifiNetwork? network) : IWifiSettingsStore
        {
            private readonly List<BranchWifiNetwork> networks = network is null ? [] : [network];

            public WifiPolicy Policy { get; private set; } = policy;

            public string Token { get; private set; } = "1";

            public int NetworkWrites { get; private set; }

            public IReadOnlyList<BranchWifiNetwork> Networks => networks;

            public Task<WifiSettingsResponse> GetAsync(Guid? branchId, CancellationToken cancellationToken) =>
                Task.FromResult(new WifiSettingsResponse(
                    Policy,
                    Token,
                    networks
                        .Where(candidate => branchId is null || candidate.BranchId == branchId)
                        .Select(candidate => new WifiNetworkResponse(
                            candidate.BranchId,
                            candidate.Ssid,
                            candidate.Security.ToString(),
                            candidate.Password))
                        .ToList()));

            public Task<Result<WifiSettingsResponse>> UpdatePolicyAsync(
                WifiPolicy requestedPolicy,
                string concurrencyToken,
                Guid actorUserId,
                Guid? branchId,
                CancellationToken cancellationToken)
            {
                if (concurrencyToken != Token)
                {
                    return Task.FromResult(Result.Failure<WifiSettingsResponse>(WifiErrors.ConcurrencyConflict()));
                }

                Policy = requestedPolicy;
                Token = (int.Parse(Token, System.Globalization.CultureInfo.InvariantCulture) + 1)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
                return Task.FromResult(Result.Success(new WifiSettingsResponse(
                    Policy,
                    Token,
                    networks
                        .Where(candidate => branchId is null || candidate.BranchId == branchId)
                        .Select(candidate => new WifiNetworkResponse(
                            candidate.BranchId,
                            candidate.Ssid,
                            candidate.Security.ToString(),
                            candidate.Password))
                        .ToList())));
            }

            public Task<Result<WifiNetworkResponse?>> SetNetworkAsync(
                Guid? branchIdScope,
                Guid branchId,
                string? ssid,
                WifiSecurity? security,
                string? password,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                NetworkWrites++;
                if (branchIdScope.HasValue && branchIdScope != branchId)
                {
                    return Task.FromResult(Result.Failure<WifiNetworkResponse?>(WifiErrors.NotFound()));
                }

                var existing = networks.FirstOrDefault(candidate => candidate.BranchId == branchId);
                if (string.IsNullOrWhiteSpace(ssid))
                {
                    if (existing is not null)
                    {
                        networks.Remove(existing);
                    }

                    return Task.FromResult(Result.Success<WifiNetworkResponse?>(null));
                }

                var stored = BranchWifiNetwork.Create(
                    branchId, ssid, security ?? WifiSecurity.None, password, Now, actorUserId);
                if (existing is not null)
                {
                    networks.Remove(existing);
                }

                networks.Add(stored);
                return Task.FromResult(Result.Success<WifiNetworkResponse?>(
                    new WifiNetworkResponse(branchId, stored.Ssid, stored.Security.ToString(), stored.Password)));
            }
        }
    }
}
