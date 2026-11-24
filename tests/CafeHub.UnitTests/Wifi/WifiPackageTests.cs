using System.Reflection;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
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
    public sealed class WifiPackageTests
    {
        private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-24T12:00:00Z");

        [Fact]
        public void FieldBoundariesAreEnforcedByTheDomain()
        {
            var valid = WifiPackage.Create(
                Guid.CreateVersion7(),
                new string('a', WifiPackage.MaximumNameLength),
                WifiPackage.MaximumDurationMinutes,
                WifiPackage.MaximumSpeedMbps,
                WifiDataMode.Limited,
                WifiPackage.MaximumQuotaMb,
                0m,
                0,
                Now,
                UserId);

            Assert.Equal(WifiPackage.MaximumDurationMinutes, valid.DurationMinutes);
            Assert.Equal(WifiPackage.MaximumQuotaMb, valid.QuotaMb);
            Assert.Equal(WifiPackageStatus.Active, valid.Status);

            Assert.Throws<ArgumentException>(() => Create(name: new string('a', 49)));
            Assert.Throws<ArgumentException>(() => Create(name: " "));
            Assert.Throws<ArgumentException>(() => Create(name: "Bad\nName"));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(duration: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(duration: 43_201));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(speed: 0m));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(speed: -1m));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(speed: 1.005m));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(speed: 1_000_000m));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Create(dataMode: WifiDataMode.Limited, quota: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Create(dataMode: WifiDataMode.Limited, quota: 1_000_001));
            Assert.Throws<ArgumentException>(() =>
                Create(dataMode: WifiDataMode.Limited, quota: null));
            Assert.Throws<ArgumentException>(() =>
                Create(dataMode: WifiDataMode.Unlimited, quota: 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(price: -0.01m));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(price: 1.005m));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(sortOrder: -1));
        }

        [Fact]
        public void ValidatorsRefuseTheSameBoundaries()
        {
            var validator = new CreateWifiPackageCommandValidator();
            validator.TestValidate(ValidCreate()).ShouldNotHaveAnyValidationErrors();
            validator.TestValidate(ValidCreate() with { Name = new string('a', 49) })
                .ShouldHaveValidationErrorFor(command => command.Name);
            validator.TestValidate(ValidCreate() with { DurationMinutes = 43_201 })
                .ShouldHaveValidationErrorFor(command => command.DurationMinutes);
            validator.TestValidate(ValidCreate() with { SpeedMbps = 0m })
                .ShouldHaveValidationErrorFor(command => command.SpeedMbps);
            validator.TestValidate(ValidCreate() with { DataMode = (WifiDataMode)(-1) })
                .ShouldHaveValidationErrorFor(command => command.DataMode);
            validator.TestValidate(ValidCreate() with { DataMode = WifiDataMode.Limited, QuotaMb = null })
                .ShouldHaveValidationErrorFor(command => command.QuotaMb);
            validator.TestValidate(ValidCreate() with { DataMode = WifiDataMode.Unlimited, QuotaMb = 10 })
                .ShouldHaveValidationErrorFor(command => command.QuotaMb);
            validator.TestValidate(ValidCreate() with { QuotaMb = 1_000_001 })
                .ShouldHaveValidationErrorFor(command => command.QuotaMb);
            validator.TestValidate(ValidCreate() with { Price = -1m })
                .ShouldHaveValidationErrorFor(command => command.Price);
            validator.TestValidate(ValidCreate() with { SortOrder = -1 })
                .ShouldHaveValidationErrorFor(command => command.SortOrder);

            new UpdateWifiPackageCommandValidator()
                .TestValidate(new UpdateWifiPackageCommand(
                    Guid.CreateVersion7(), "Hour", 60, 1m, WifiDataMode.Unlimited, null, 5m, 0, null!))
                .ShouldHaveValidationErrorFor(command => command.ConcurrencyToken);
            new DeactivateWifiPackageCommandValidator()
                .TestValidate(new DeactivateWifiPackageCommand(Guid.Empty, null!))
                .ShouldHaveValidationErrorFor(command => command.PackageId);
        }

        [Fact]
        public async Task CreatingIsRefusedWhileThePolicyIsFree()
        {
            var packages = new FakeWifiPackageStore();
            var handler = CreateHandler(packages, WifiPolicy.Free);

            var result = await handler.Handle(ValidCreate(), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(WifiErrors.PackagesRequirePaidPolicyCode, result.Error!.Code);
            Assert.Equal(0, packages.Writes);
        }

        [Fact]
        public async Task CreatingUnderPaidStoresTheTrimmedPackage()
        {
            var packages = new FakeWifiPackageStore();
            var handler = CreateHandler(packages, WifiPolicy.Paid);

            var result = await handler.Handle(ValidCreate() with { Name = "  One hour  " }, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal("One hour", result.Value!.Name);
            Assert.Equal("Active", result.Value.Status);
            var stored = Assert.Single(packages.Packages);
            Assert.Equal("One hour", stored.Name);
        }

        [Fact]
        public async Task DuplicateActiveNamesConflictIncludingUpdatesAndReactivation()
        {
            var packages = new FakeWifiPackageStore();
            var active = NewPackage("One Hour", 5m);
            var other = NewPackage("Three Hours", 25m);
            packages.Seed(active);
            packages.Seed(other);
            var settings = new FakeWifiSettingsStore(WifiPolicy.Paid);
            var createHandler = CreateHandler(packages, WifiPolicy.Paid);

            var create = await createHandler.Handle(ValidCreate() with { Name = "one hour" }, default);
            var update = await new UpdateWifiPackageCommandHandler(packages, settings, StaffUser()).Handle(
                new UpdateWifiPackageCommand(other.Id, "ONE HOUR", 180, 3m, WifiDataMode.Unlimited, null, 25m, 0, "2"),
                default);
            var inactive = NewPackage("One Hour", 10m);
            inactive.Deactivate(Now, UserId);
            packages.Seed(inactive);
            var reactivate = await new WifiPackageLifecycleCommandHandler(packages, settings, StaffUser())
                .Handle(new ReactivateWifiPackageCommand(inactive.Id, "3"), default);

            Assert.Equal(WifiErrors.PackageNameConflictCode, create.Error!.Code);
            Assert.Equal(WifiErrors.PackageNameConflictCode, update.Error!.Code);
            Assert.Equal(WifiErrors.PackageNameConflictCode, reactivate.Error!.Code);
            Assert.Equal(0, packages.Writes);
        }

        [Fact]
        public async Task ManagementIsRefusedWhileThePolicyIsFreeOnEveryWritePath()
        {
            var packages = new FakeWifiPackageStore();
            var package = NewPackage("One Hour", 5m);
            packages.Seed(package);
            var settings = new FakeWifiSettingsStore(WifiPolicy.Free);
            var user = StaffUser();

            var update = await new UpdateWifiPackageCommandHandler(packages, settings, user).Handle(
                new UpdateWifiPackageCommand(package.Id, "Hour", 60, 1m, WifiDataMode.Unlimited, null, 5m, 0, "1"),
                default);
            var deactivate = await new WifiPackageLifecycleCommandHandler(packages, settings, user).Handle(
                new DeactivateWifiPackageCommand(package.Id, "1"), default);
            var reactivate = await new WifiPackageLifecycleCommandHandler(packages, settings, user).Handle(
                new ReactivateWifiPackageCommand(package.Id, "1"), default);

            Assert.All(
                new[] { update, deactivate, reactivate },
                result => Assert.Equal(WifiErrors.PackagesRequirePaidPolicyCode, result.Error!.Code));
            Assert.Equal(0, packages.Writes);
        }

        [Fact]
        public async Task DeactivationAndReactivationWorkUnderPaid()
        {
            var packages = new FakeWifiPackageStore();
            var package = NewPackage("One Hour", 5m);
            packages.Seed(package);
            var handler = new WifiPackageLifecycleCommandHandler(
                packages, new FakeWifiSettingsStore(WifiPolicy.Paid), StaffUser());

            var deactivated = await handler.Handle(new DeactivateWifiPackageCommand(package.Id, "1"), default);
            var reactivated = await handler.Handle(new ReactivateWifiPackageCommand(package.Id, "2"), default);

            Assert.True(deactivated.IsSuccess, deactivated.Error?.Code);
            Assert.Equal("Inactive", deactivated.Value!.Status);
            Assert.True(reactivated.IsSuccess, reactivated.Error?.Code);
            Assert.Equal("Active", reactivated.Value!.Status);
        }

        [Fact]
        public void PriceChangesAreAuditedWithOldAndNewValues()
        {
            var package = NewPackage("One Hour", 5m);
            var before = WifiPackageAuditValues.From(package);
            package.Update(
                package.Name, package.DurationMinutes, package.SpeedMbps,
                package.DataMode, package.QuotaMb, 25m, package.SortOrder, Now, UserId);
            var changes = TenantAuditChangeSets.WifiPackageUpdate(before, package);
            var price = changes.Fields.Single(field => field.Name == nameof(WifiPackage.Price));

            Assert.Equal("5", price.Before);
            Assert.Equal("25", price.After);
            var snapshot = TenantAuditSnapshotSerializer.Create("wifi_packages", changes);
            Assert.True(snapshot.IsSuccess, snapshot.Error?.Code);
            Assert.Contains("\"Price\":\"25\"", snapshot.Value.AfterState, StringComparison.Ordinal);
            Assert.Contains("\"Price\":\"5\"", snapshot.Value.BeforeState, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ListMapsThePageAndGetUnknownIsNotFound()
        {
            var store = new FakeWifiPackageStore();
            store.Seed(NewPackage("One Hour", 5m));
            var handler = new GetWifiPackagesQueryHandler(
                store,
                new PaginationOptions { DefaultPageSize = 20, MaxPageSize = 100 });

            var page = await handler.Handle(
                new GetWifiPackagesQuery { PageSize = 2, IncludeTotalCount = true },
                default);
            var missing = await new GetWifiPackageQueryHandler(store).Handle(
                new GetWifiPackageQuery(Guid.CreateVersion7()), default);

            Assert.True(page.IsSuccess, page.Error?.Code);
            Assert.Equal(2, store.LastRequest!.PageSize);
            Assert.True(store.LastRequest.IncludeTotalCount);
            Assert.Single(page.Value!.Items);
            Assert.False(missing.IsSuccess);
            Assert.Equal(WifiErrors.PackageNotFoundCode, missing.Error!.Code);
        }

        [Fact]
        public void EndpointsRequireWifiManage()
        {
            Assert.Equal(
                "t/{slug}/api/v1/wifi/packages",
                typeof(TenantWifiPackagesController).GetCustomAttribute<RouteAttribute>()!.Template);

            var actions = new[]
            {
                nameof(TenantWifiPackagesController.List),
                nameof(TenantWifiPackagesController.Get),
                nameof(TenantWifiPackagesController.Create),
                nameof(TenantWifiPackagesController.Update),
                nameof(TenantWifiPackagesController.Deactivate),
                nameof(TenantWifiPackagesController.Reactivate),
            };
            Assert.All(actions, action => Assert.Equal(
                TenantPermissions.WifiManage,
                Assert.IsType<TenantAuthorizeAttribute>(
                    typeof(TenantWifiPackagesController).GetMethod(action)!
                        .GetCustomAttribute<TenantAuthorizeAttribute>()).Policy));
            Assert.Equal(WifiDataMode.Limited, TenantWifiPackagesController.ParseDataMode("limited"));
            Assert.Equal((WifiDataMode)(-1), TenantWifiPackagesController.ParseDataMode("unknown"));
        }

        private static WifiPackage NewPackage(string name, decimal price) =>
            WifiPackage.Create(
                Guid.CreateVersion7(), name, 60, 1m, WifiDataMode.Unlimited, null, price, 0, Now, UserId);

        private static CreateWifiPackageCommand ValidCreate() =>
            new("One Hour", 60, 1m, WifiDataMode.Unlimited, null, 5m, 0);

        private static WifiPackage Create(
            string name = "One Hour",
            int duration = 60,
            decimal? speed = 1m,
            WifiDataMode dataMode = WifiDataMode.Unlimited,
            int? quota = null,
            decimal price = 5m,
            int sortOrder = 0) =>
            WifiPackage.Create(
                Guid.CreateVersion7(), name, duration, speed, dataMode, quota, price, sortOrder, Now, UserId);

        private static CreateWifiPackageCommandHandler CreateHandler(
            IWifiPackageStore packages,
            WifiPolicy policy) =>
            new(packages, new FakeWifiSettingsStore(policy), StaffUser(), new FixedClock(Now));

        private static FakeTenantUser StaffUser() =>
            new(UserId, new TenantAccountAccess(true, null, true) { BranchId = Guid.NewGuid() });

        private sealed class FakeTenantUser(Guid? userId, TenantAccountAccess? accountAccess) : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId => userId;

            public Guid? TenantId => Guid.Parse("11111111-1111-1111-1111-111111111111");

            public IReadOnlyCollection<string> Roles => [];

            public Guid? BranchId => AccountAccess?.BranchId;

            public TenantAccountAccess? AccountAccess { get; } = accountAccess;
        }

        private sealed class FixedClock(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private sealed class FakeWifiSettingsStore(WifiPolicy policy) : IWifiSettingsStore
        {
            public Task<WifiSettingsResponse> GetAsync(Guid? branchId, CancellationToken cancellationToken) =>
                Task.FromResult(new WifiSettingsResponse(policy, "1", []));

            public Task<Result<WifiSettingsResponse>> UpdatePolicyAsync(
                WifiPolicy requestedPolicy,
                string concurrencyToken,
                Guid actorUserId,
                Guid? branchId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The package flow must not update the policy.");

            public Task<Result<WifiNetworkResponse?>> SetNetworkAsync(
                Guid? branchIdScope,
                Guid branchId,
                string? ssid,
                WifiSecurity? security,
                string? password,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The package flow must not update a network.");
        }

        private sealed class FakeWifiPackageStore : IWifiPackageStore
        {
            private readonly Dictionary<Guid, int> versions = [];
            private int nextVersion = 1;

            public List<WifiPackage> Packages { get; } = [];

            public int Writes { get; private set; }

            public WifiPackageListRequest? LastRequest { get; private set; }

            public void Seed(WifiPackage package)
            {
                Packages.Add(package);
                versions[package.Id] = nextVersion++;
            }

            public Task<WifiPackageListPage> ListAsync(
                WifiPackageListRequest request,
                CancellationToken cancellationToken)
            {
                LastRequest = request;
                var entries = Packages
                    .Where(package => request.Status is null || package.Status == request.Status)
                    .OrderBy(package => package.SortOrder)
                    .ThenBy(package => package.Id)
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
                    .ToList();
                return Task.FromResult(new WifiPackageListPage(entries, entries.Count));
            }

            public Task<WifiPackageResponse?> GetAsync(Guid packageId, CancellationToken cancellationToken)
            {
                var package = Packages.FirstOrDefault(candidate => candidate.Id == packageId);
                return Task.FromResult(package is null
                    ? null
                    : WifiPackageResponse.From(package, versions[package.Id].ToString()));
            }

            public Task<bool> ActiveNameExistsAsync(
                string name,
                Guid? excludePackageId,
                CancellationToken cancellationToken) =>
                Task.FromResult(Packages.Any(package =>
                    package.Status == WifiPackageStatus.Active
                    && package.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                    && package.Id != excludePackageId));

            public Task<Result<WifiPackageResponse>> CreateAsync(
                WifiPackage package,
                CancellationToken cancellationToken)
            {
                Writes++;
                Seed(package);
                return Task.FromResult(Result.Success(
                    WifiPackageResponse.From(package, versions[package.Id].ToString())));
            }

            public Task<Result<WifiPackageResponse>> UpdateAsync(
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
                Writes++;
                var package = Packages.Single(candidate => candidate.Id == packageId);
                package.Update(name, durationMinutes, speedMbps, dataMode, quotaMb, price, sortOrder, Now, actorUserId);
                versions[packageId] = nextVersion++;
                return Task.FromResult(Result.Success(
                    WifiPackageResponse.From(package, versions[packageId].ToString())));
            }

            public Task<Result<WifiPackageResponse>> ChangeStatusAsync(
                Guid packageId,
                WifiPackageStatus status,
                string concurrencyToken,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                Writes++;
                var package = Packages.Single(candidate => candidate.Id == packageId);
                if (status == WifiPackageStatus.Active)
                {
                    package.Reactivate(Now, actorUserId);
                }
                else
                {
                    package.Deactivate(Now, actorUserId);
                }

                versions[packageId] = nextVersion++;
                return Task.FromResult(Result.Success(
                    WifiPackageResponse.From(package, versions[packageId].ToString())));
            }
        }
    }
}
