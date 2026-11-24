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
    public sealed class WifiVoucherTests
    {
        private static readonly Guid PackageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-24T12:00:00Z");

        [Fact]
        public async Task ValidCodesAreAddedAndDuplicatesAndInvalidAreCounted()
        {
            var package = SamplePackage();
            var packages = new FakeWifiPackageStore(package);
            var vouchers = new FakeWifiVoucherStore(["CODE-2"]);
            var handler = new ImportWifiVouchersCommandHandler(vouchers, packages, StaffUser(), new FixedClock(Now));

            var result = await handler.Handle(
                new ImportWifiVouchersCommand(
                    PackageId,
                    ["CODE-1", "CODE-1", "abc", "bad code", "CODE-2", "CODE-2"]),
                default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(1, result.Value!.Added);
            Assert.Equal(3, result.Value.Duplicate);
            Assert.Equal(2, result.Value.Invalid);
            var stored = Assert.Single(vouchers.Added);
            Assert.Equal("CODE-1", stored.Code);
            Assert.Equal(WifiVoucherStatus.Available, stored.Status);
            Assert.Equal(1, vouchers.LastCounts!.Added);
            Assert.Equal(3, vouchers.LastCounts.Duplicate);
            Assert.Equal(2, vouchers.LastCounts.Invalid);
            Assert.DoesNotContain(vouchers.Added, voucher => voucher.Code is "abc" or "bad code" or "CODE-2");
        }

        [Fact]
        public async Task ImportRefusesAMissingPackage()
        {
            var handler = new ImportWifiVouchersCommandHandler(
                new FakeWifiVoucherStore([]),
                new FakeWifiPackageStore(null),
                StaffUser(),
                new FixedClock(Now));

            var result = await handler.Handle(
                new ImportWifiVouchersCommand(PackageId, ["CODE-1"]),
                default);

            Assert.Equal(WifiErrors.PackageNotFoundCode, result.Error!.Code);
        }

        [Fact]
        public void MoreThanOneThousandCodesIsRefused()
        {
            var validator = new ImportWifiVouchersCommandValidator();

            validator.TestValidate(new ImportWifiVouchersCommand(
                    PackageId,
                    Enumerable.Range(1, 1001).Select(index => $"CODE-{index}").ToArray()))
                .ShouldHaveValidationErrorFor(command => command.Codes);
            validator.TestValidate(new ImportWifiVouchersCommand(
                    PackageId,
                    Enumerable.Range(1, 1000).Select(index => $"CODE-{index}").ToArray()))
                .ShouldNotHaveAnyValidationErrors();
            validator.TestValidate(new ImportWifiVouchersCommand(PackageId, []))
                .ShouldHaveValidationErrorFor(command => command.Codes);
        }

        [Fact]
        public async Task TheSummaryCountsMatch()
        {
            var handler = new GetWifiVoucherSummaryQueryHandler(
                new FakeWifiVoucherStore([]) { Summary = new WifiVoucherSummary(4, 2, 1, 7) },
                new FakeWifiPackageStore(SamplePackage()));

            var result = await handler.Handle(new GetWifiVoucherSummaryQuery(PackageId), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(new WifiVoucherSummary(4, 2, 1, 7), result.Value);
            Assert.Equal(
                WifiErrors.PackageNotFoundCode,
                (await new GetWifiVoucherSummaryQueryHandler(
                    new FakeWifiVoucherStore([]),
                    new FakeWifiPackageStore(null))
                    .Handle(new GetWifiVoucherSummaryQuery(PackageId), default)).Error!.Code);
        }

        [Fact]
        public void VoidOnlyAcceptsAnAvailableVoucher()
        {
            var voucher = WifiVoucher.Create(Guid.CreateVersion7(), PackageId, "CODE-1", Now, UserId);

            voucher.Void(Now);
            Assert.Equal(WifiVoucherStatus.Void, voucher.Status);
            Assert.Throws<InvalidOperationException>(() => voucher.Void(Now));
            Assert.False(WifiVoucher.IsValidCode("abc"));
            Assert.False(WifiVoucher.IsValidCode("bad code"));
            Assert.False(WifiVoucher.IsValidCode(new string('a', 65)));
            Assert.True(WifiVoucher.IsValidCode("A-1b"));
        }

        [Fact]
        public async Task VoidDelegatesTheTrimmedCodeAndRefusesUnavailableVouchers()
        {
            var vouchers = new FakeWifiVoucherStore(["CODE-1"]);
            vouchers.Seed("ASSIGNED-1", WifiVoucherStatus.Assigned);
            var handler = new VoidWifiVoucherCommandHandler(
                vouchers, new FakeWifiPackageStore(SamplePackage()), StaffUser());

            var unknown = await handler.Handle(new VoidWifiVoucherCommand(PackageId, "MISSING-1"), default);
            var unavailable = await handler.Handle(new VoidWifiVoucherCommand(PackageId, "ASSIGNED-1"), default);
            var voided = await handler.Handle(new VoidWifiVoucherCommand(PackageId, "  CODE-1  "), default);

            Assert.Equal(WifiErrors.VoucherNotFoundCode, unknown.Error!.Code);
            Assert.Equal(WifiErrors.VoucherNotAvailableCode, unavailable.Error!.Code);
            Assert.True(voided.IsSuccess, voided.Error?.Code);
            Assert.Equal("CODE-1", vouchers.LastVoidedCode);
            Assert.Equal(WifiVoucherStatus.Void, vouchers.StatusOf("CODE-1"));
        }

        [Fact]
        public void ImportAndVoidAuditContainsCountsAndNoCodes()
        {
            var import = TenantAuditSnapshotSerializer.Create(
                "wifi_vouchers",
                TenantAuditChangeSets.WifiVoucherImport(new WifiVoucherImportResult(3, 2, 1)));
            var voidAudit = TenantAuditSnapshotSerializer.Create(
                "wifi_vouchers",
                TenantAuditChangeSets.WifiVoucherVoid());

            Assert.True(import.IsSuccess, import.Error?.Code);
            Assert.Contains("\"Added\":\"3\"", import.Value.AfterState, StringComparison.Ordinal);
            Assert.Contains("\"Duplicate\":\"2\"", import.Value.AfterState, StringComparison.Ordinal);
            Assert.Contains("\"Invalid\":\"1\"", import.Value.AfterState, StringComparison.Ordinal);
            Assert.DoesNotContain("CODE-1", import.Value.AfterState, StringComparison.Ordinal);
            Assert.True(voidAudit.IsSuccess, voidAudit.Error?.Code);
            Assert.Contains("\"Voided\":\"1\"", voidAudit.Value.AfterState, StringComparison.Ordinal);
        }

        [Fact]
        public void EndpointsRequireWifiManage()
        {
            Assert.Equal(
                "t/{slug}/api/v1/wifi/packages/{packageId:guid}/vouchers",
                typeof(TenantWifiVouchersController).GetCustomAttribute<RouteAttribute>()!.Template);

            var actions = new[]
            {
                nameof(TenantWifiVouchersController.Import),
                nameof(TenantWifiVouchersController.Summary),
                nameof(TenantWifiVouchersController.Void),
            };
            Assert.All(actions, action => Assert.Equal(
                TenantPermissions.WifiManage,
                Assert.IsType<TenantAuthorizeAttribute>(
                    typeof(TenantWifiVouchersController).GetMethod(action)!
                        .GetCustomAttribute<TenantAuthorizeAttribute>()).Policy));

            Assert.Equal("import", Assert.IsType<HttpPostAttribute>(
                typeof(TenantWifiVouchersController).GetMethod(nameof(TenantWifiVouchersController.Import))!
                    .GetCustomAttribute<HttpPostAttribute>()).Template);
            Assert.Equal("summary", Assert.IsType<HttpGetAttribute>(
                typeof(TenantWifiVouchersController).GetMethod(nameof(TenantWifiVouchersController.Summary))!
                    .GetCustomAttribute<HttpGetAttribute>()).Template);
            Assert.Equal("void", Assert.IsType<HttpPostAttribute>(
                typeof(TenantWifiVouchersController).GetMethod(nameof(TenantWifiVouchersController.Void))!
                    .GetCustomAttribute<HttpPostAttribute>()).Template);
        }

        private static WifiPackageResponse SamplePackage() =>
            WifiPackageResponse.From(
                WifiPackage.Create(
                    PackageId, "One Hour", 60, 1m, WifiDataMode.Unlimited, null, 5m, 0, Now, UserId),
                "1");

        private static FakeTenantUser StaffUser() =>
            new(UserId, new TenantAccountAccess(true, null, true) { BranchId = Guid.NewGuid() });

        private sealed class FakeTenantUser(Guid? userId, TenantAccountAccess? accountAccess) : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId => userId;

            public Guid? TenantId => Guid.Parse("33333333-3333-3333-3333-333333333333");

            public IReadOnlyCollection<string> Roles => [];

            public Guid? BranchId => AccountAccess?.BranchId;

            public TenantAccountAccess? AccountAccess { get; } = accountAccess;
        }

        private sealed class FixedClock(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private sealed class FakeWifiPackageStore(WifiPackageResponse? package) : IWifiPackageStore
        {
            public Task<WifiPackageListPage> ListAsync(
                WifiPackageListRequest request,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The voucher flow must not list packages.");

            public Task<WifiPackageResponse?> GetAsync(Guid packageId, CancellationToken cancellationToken) =>
                Task.FromResult(packageId == PackageId ? package : null);

            public Task<bool> ActiveNameExistsAsync(
                string name,
                Guid? excludePackageId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The voucher flow must not check package names.");

            public Task<Result<WifiPackageResponse>> CreateAsync(
                WifiPackage package,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The voucher flow must not create packages.");

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
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The voucher flow must not update packages.");

            public Task<Result<WifiPackageResponse>> ChangeStatusAsync(
                Guid packageId,
                WifiPackageStatus status,
                string concurrencyToken,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The voucher flow must not change package status.");
        }

        private sealed class FakeWifiVoucherStore(IEnumerable<string> existingCodes) : IWifiVoucherStore
        {
            private readonly HashSet<string> existing = new(existingCodes, StringComparer.Ordinal);
            private readonly Dictionary<string, WifiVoucherStatus> statuses = new(StringComparer.Ordinal);

            public List<WifiVoucher> Added { get; } = [];

            public WifiVoucherImportResult? LastCounts { get; private set; }

            public WifiVoucherSummary Summary { get; set; } = new(0, 0, 0, 0);

            public string? LastVoidedCode { get; private set; }

            public void Seed(string code, WifiVoucherStatus status) => statuses[code] = status;

            public WifiVoucherStatus StatusOf(string code) =>
                statuses.TryGetValue(code, out var status) ? status : WifiVoucherStatus.Available;

            public Task<Result> IssueForOrderAsync(
                Order order,
                Guid? actorUserId,
                CancellationToken cancellationToken) =>
                Task.FromResult(Result.Success());

            public Task<bool> HasAvailableAsync(Guid packageId, CancellationToken cancellationToken) =>
                Task.FromResult(existing.Count > 0 || statuses.Values.Contains(WifiVoucherStatus.Available));

            public Task<IReadOnlySet<string>> FindExistingCodesAsync(
                Guid packageId,
                IReadOnlyCollection<string> codes,
                CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlySet<string>>(
                    existing.Intersect(codes, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal));

            public Task<Result> InsertImportedAsync(
                Guid packageId,
                IReadOnlyList<WifiVoucher> vouchers,
                WifiVoucherImportResult counts,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                Added.AddRange(vouchers);
                LastCounts = counts;
                return Task.FromResult(Result.Success());
            }

            public Task<WifiVoucherSummary> GetSummaryAsync(
                Guid packageId,
                CancellationToken cancellationToken) =>
                Task.FromResult(Summary);

            public Task<Result> VoidAsync(
                Guid packageId,
                string code,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                if (!existing.Contains(code) && !statuses.ContainsKey(code))
                {
                    return Task.FromResult(Result.Failure(WifiErrors.VoucherNotFound()));
                }

                if (StatusOf(code) != WifiVoucherStatus.Available)
                {
                    return Task.FromResult(Result.Failure(WifiErrors.VoucherNotAvailable()));
                }

                LastVoidedCode = code;
                statuses[code] = WifiVoucherStatus.Void;
                return Task.FromResult(Result.Success());
            }
        }
    }
}
