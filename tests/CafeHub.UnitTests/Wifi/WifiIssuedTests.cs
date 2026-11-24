using System.Reflection;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Orders.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Application.Wifi;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Application.Wifi.Queries;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using CafeHub.Infrastructure.Wifi;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests.Wifi
{
    public sealed class WifiIssuedTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OtherBranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid PackageId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid OrderId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-24T12:00:00Z");

        [Fact]
        public void TheCodeIsReleasedByEitherFullOrderOrFullBillPayment()
        {
            Assert.True(WifiCodeRelease.IsReleased(10m, 10m, 20m, 10m));
            Assert.True(WifiCodeRelease.IsReleased(10m, 0m, 20m, 20m));
            Assert.False(WifiCodeRelease.IsReleased(10m, 5m, 20m, 5m));
            Assert.False(WifiCodeRelease.IsReleased(10m, 0m, null, null));
        }

        [Fact]
        public void ANamedSettlementReleasesOnlyItsOwnOrder()
        {
            Assert.True(WifiCodeRelease.IsReleased(10m, 10m, 30m, 10m));
            Assert.False(WifiCodeRelease.IsReleased(10m, 0m, 30m, 10m));
        }

        [Fact]
        public void LookupRowsDeriveTheirStatusFromTheAssignmentAndPayment()
        {
            var awaitingVoucher = IssuedWifiItem.From(Entry(code: null, orderPaid: 0m, billPaid: 0m));
            var awaitingPayment = IssuedWifiItem.From(Entry(code: "CODE-1", orderPaid: 0m, billPaid: 0m));
            var released = IssuedWifiItem.From(Entry(code: "CODE-1", orderPaid: 10m, billPaid: 0m));

            Assert.Equal("AwaitingVoucher", awaitingVoucher.Status);
            Assert.Null(awaitingVoucher.Code);
            Assert.Equal("AwaitingPayment", awaitingPayment.Status);
            Assert.Equal("CODE-1", awaitingPayment.Code);
            Assert.Equal("Released", released.Status);
            Assert.Equal("CODE-1", released.Code);
        }

        [Fact]
        public async Task TheLookupHandlerAppliesBranchScopeAndMapsThePage()
        {
            var store = new FakeIssuedWifiQueryStore
            {
                Page = new IssuedWifiListPage([Entry(code: "CODE-1", orderPaid: 10m, billPaid: 0m)], 1),
            };
            var handler = new GetIssuedWifiQueryHandler(
                store,
                StaffUser(),
                new PaginationOptions { DefaultPageSize = 20, MaxPageSize = 100 });

            var result = await handler.Handle(
                new GetIssuedWifiQuery { PackageId = PackageId, OrderNumber = 12, PageSize = 5 },
                default);
            var foreign = await handler.Handle(new GetIssuedWifiQuery { BranchId = OtherBranchId }, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(BranchId, store.LastRequest!.BranchId);
            Assert.Equal(PackageId, store.LastRequest.PackageId);
            Assert.Equal(12, store.LastRequest.OrderNumber);
            Assert.Equal(5, store.LastRequest.PageSize);
            Assert.Equal("Released", Assert.Single(result.Value!.Items).Status);
            Assert.False(foreign.IsSuccess);
            Assert.Equal(WifiErrors.NotFoundCode, foreign.Error!.Code);
        }

        [Fact]
        public void TheLookupEndpointUsesTheAnyPermissionPolicyAndCatalogCode()
        {
            var action = typeof(TenantWifiController).GetMethod(nameof(TenantWifiController.Issued))!;

            Assert.Equal("issued", Assert.IsType<HttpGetAttribute>(
                action.GetCustomAttribute<HttpGetAttribute>()).Template);
            Assert.Equal(
                TenantPolicies.WifiIssuedRead,
                Assert.IsType<TenantAuthorizeAttribute>(action.GetCustomAttribute<TenantAuthorizeAttribute>()).Policy);
            Assert.Equal("wifi.lookup", TenantPermissions.WifiLookup);
            Assert.Contains(TenantPermissions.WifiLookup, TenantPermissions.All);
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.WifiLookup));
        }

        [Fact]
        public void TheModelAndClaimGuardOneVoucherPerOrderItem()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql("Host=localhost;Database=test")
                .Options;
            using var context = new TenantDbContext(options);
            var model = context.GetService<IDesignTimeModel>().Model;
            var vouchers = model.FindEntityType(typeof(WifiVoucher))!;
            var assignmentIndex = vouchers.GetIndexes()
                .Single(index => index.GetDatabaseName() == "uq_wifi_vouchers_assigned_order_item_id");

            Assert.True(assignmentIndex.IsUnique);
            Assert.Contains(
                vouchers.GetCheckConstraints(),
                check => check.Name == "ck_wifi_vouchers_assignment_pair");
            Assert.Contains("FOR UPDATE SKIP LOCKED", WifiVoucherStore.ClaimSql, StringComparison.Ordinal);
            Assert.Contains("status = 'Available'", WifiVoucherStore.ClaimSql, StringComparison.Ordinal);
            Assert.DoesNotContain("code", WifiVoucherStore.ClaimSql[..WifiVoucherStore.ClaimSql.IndexOf("RETURNING", StringComparison.Ordinal)], StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void TheGuestStatusHidesTheCodeUntilItIsReleased()
        {
            var pending = GuestOrderStatus.From(EntryWithItem(null, pending: true));
            var released = GuestOrderStatus.From(EntryWithItem("CODE-1", pending: false));

            Assert.Null(pending.Items[0].WifiCode);
            Assert.True(pending.Items[0].WifiCodePending);
            Assert.Null(pending.Items[0].AddonNames.FirstOrDefault());
            Assert.Equal("CODE-1", released.Items[0].WifiCode);
            Assert.False(released.Items[0].WifiCodePending);
            Assert.Equal(60, released.Items[0].WifiDurationMinutes);
            Assert.Equal(1m, released.Items[0].WifiSpeedMbps);
        }

        private static IssuedWifiEntry Entry(string? code, decimal orderPaid, decimal billPaid) =>
            new(
                PackageId,
                "One Hour",
                60,
                1m,
                12,
                "Abcdefghijklmnopqrstuv",
                code is null ? null : Now,
                code,
                10m,
                orderPaid,
                10m,
                billPaid);

        private static GuestOrderEntry EntryWithItem(string? code, bool pending) =>
            new(
                OrderId,
                12,
                null,
                "WifiOnly",
                "Accepted",
                null,
                Now,
                Now.AddMinutes(5),
                10m,
                [
                    new GuestOrderEntryItem("One Hour", "WiFi", 1, [])
                    {
                        WifiDurationMinutes = 60,
                        WifiSpeedMbps = 1m,
                        WifiCode = code,
                        WifiCodePending = pending,
                    },
                ])
            {
                BillReference = "Abcdefghijklmnopqrstuv",
            };

        private static FakeTenantUser StaffUser() =>
            new(Guid.NewGuid(), new TenantAccountAccess(true, null, true) { BranchId = BranchId });

        private sealed class FakeTenantUser(Guid? userId, TenantAccountAccess? accountAccess) : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId => userId;

            public Guid? TenantId => Guid.Parse("55555555-5555-5555-5555-555555555555");

            public IReadOnlyCollection<string> Roles => [];

            public Guid? BranchId => AccountAccess?.BranchId;

            public TenantAccountAccess? AccountAccess { get; } = accountAccess;
        }

        private sealed class FakeIssuedWifiQueryStore : IIssuedWifiQueryStore
        {
            public IssuedWifiListPage Page { get; set; } = new([], null);

            public IssuedWifiListRequest? LastRequest { get; private set; }

            public Task<IssuedWifiListPage> ListAsync(
                IssuedWifiListRequest request,
                CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(Page);
            }
        }
    }
}
