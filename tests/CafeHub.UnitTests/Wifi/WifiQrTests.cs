using System.Reflection;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Tables;
using CafeHub.Application.Tenancy;
using CafeHub.Application.Wifi;
using CafeHub.Application.Wifi.Commands;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Application.Wifi.Queries;
using CafeHub.Domain.Tenants;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using CafeHub.WebApi.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Wifi
{
    public sealed class WifiQrTests
    {
        private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid BranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid OtherBranchId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid UserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-24T12:00:00Z");
        private const string Code = "Abcdefghijklmnopqrstuv";
        private const string Replacement = "Zyxwvutsrqponmlkjihgfe";

        [Fact]
        public void CodesAreTwentyTwoUrlSafeCharactersAndRegenerationMustDiffer()
        {
            Assert.True(BranchWifiQr.IsValidCode(Code));
            Assert.False(BranchWifiQr.IsValidCode("short"));
            Assert.False(BranchWifiQr.IsValidCode(new string('a', 23)));
            Assert.False(BranchWifiQr.IsValidCode("Abcdefghijklmnopqrstu/"));

            var qr = BranchWifiQr.Create(BranchId, Code, Now, UserId);
            qr.Regenerate(Replacement, Now, UserId);

            Assert.Equal(Replacement, qr.QrCode);
            Assert.Equal(WifiQrStatus.Active, qr.Status);
            Assert.Throws<ArgumentException>(() => qr.Regenerate(Replacement, Now, UserId));
        }

        [Fact]
        public async Task RegenerationInvalidatesTheOldCode()
        {
            var store = new FakeWifiQrStore(BranchId, Code);
            var regenerate = new RegenerateWifiQrCommandHandler(store, StaffUser(BranchId));
            var guest = new GetGuestWifiQueryHandler(store, TenantAccessor());

            var before = await guest.Handle(new GetGuestWifiQuery(Code), default);
            var regenerated = await regenerate.Handle(new RegenerateWifiQrCommand(BranchId), default);
            var oldCode = await guest.Handle(new GetGuestWifiQuery(Code), default);
            var newCode = await guest.Handle(new GetGuestWifiQuery(regenerated.Value!.Code), default);

            Assert.True(before.IsSuccess, before.Error?.Code);
            Assert.True(regenerated.IsSuccess, regenerated.Error?.Code);
            Assert.NotEqual(Code, regenerated.Value.Code);
            Assert.Equal(GuestQrErrors.NotFoundCode, oldCode.Error!.Code);
            Assert.True(newCode.IsSuccess, newCode.Error?.Code);
        }

        [Fact]
        public async Task UnknownMalformedAndRegeneratedCodesUseTheSameNotFound()
        {
            var store = new FakeWifiQrStore(BranchId, Code);
            var guest = new GetGuestWifiQueryHandler(store, TenantAccessor());

            var malformed = await guest.Handle(new GetGuestWifiQuery("x"), default);
            var unknown = await guest.Handle(new GetGuestWifiQuery(Replacement), default);
            var regenerated = await guest.Handle(new GetGuestWifiQuery("Zzzzzzzzzzzzzzzzzzzzzz"), default);

            Assert.All(
                new[] { malformed, unknown, regenerated },
                result => Assert.Equal(GuestQrErrors.NotFoundCode, result.Error!.Code));
        }

        [Fact]
        public async Task FreeReturnsOnlyTheNetworkDetails()
        {
            var store = new FakeWifiQrStore(BranchId, Code)
            {
                Context = new WifiQrContext(
                    BranchId,
                    WifiPolicy.Free,
                    BranchWifiNetwork.Create(BranchId, "CafeGuest", WifiSecurity.WPA, "secret-pass", Now, UserId),
                    [ActivePackage("One Hour", 5m, 0)]),
            };

            var result = await new GetGuestWifiQueryHandler(store, TenantAccessor())
                .Handle(new GetGuestWifiQuery(Code), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal("Free", result.Value!.Policy);
            Assert.Equal("CafeGuest", result.Value.Network!.Ssid);
            Assert.Equal("WPA", result.Value.Network.Security);
            Assert.Equal("secret-pass", result.Value.Network.Password);
            Assert.Empty(result.Value.Packages);
        }

        [Fact]
        public async Task PaidReturnsOnlyActivePackagesWithoutInternalIds()
        {
            var store = new FakeWifiQrStore(BranchId, Code)
            {
                Context = new WifiQrContext(
                    BranchId,
                    WifiPolicy.Paid,
                    BranchWifiNetwork.Create(BranchId, "CafeGuest", WifiSecurity.WPA, null, Now, UserId),
                    [
                        ActivePackage("Three Hours", 25m, 1),
                        ActivePackage("One Hour", 5m, 0),
                        InactivePackage("Retired", 10m, 2),
                    ]),
            };

            var result = await new GetGuestWifiQueryHandler(store, TenantAccessor())
                .Handle(new GetGuestWifiQuery(Code), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal("Paid", result.Value!.Policy);
            Assert.Null(result.Value.Network);
            Assert.Equal(new[] { "One Hour", "Three Hours" }, result.Value.Packages.Select(package => package.Name));
            Assert.DoesNotContain(
                typeof(GuestWifiPackage).GetProperties(),
                property => property.Name.Contains("Id", StringComparison.Ordinal));
            Assert.DoesNotContain(
                typeof(GuestWifiInfo).GetProperties(),
                property => property.Name.Contains("Branch", StringComparison.Ordinal));
        }

        [Fact]
        public async Task AnotherBranchesStaffCannotReadOrRegenerateTheQr()
        {
            var store = new FakeWifiQrStore(BranchId, Code);
            var user = StaffUser(OtherBranchId);

            var read = await new GetWifiQrQueryHandler(store, user)
                .Handle(new GetWifiQrQuery(BranchId), default);
            var regenerate = await new RegenerateWifiQrCommandHandler(store, user)
                .Handle(new RegenerateWifiQrCommand(BranchId), default);

            Assert.Equal(WifiErrors.NotFoundCode, read.Error!.Code);
            Assert.Equal(WifiErrors.NotFoundCode, regenerate.Error!.Code);
            Assert.Equal(0, store.Writes);
        }

        [Fact]
        public async Task TheOwnerCanReadAnyBranch()
        {
            var store = new FakeWifiQrStore(BranchId, Code);

            var result = await new GetWifiQrQueryHandler(store, OwnerUser())
                .Handle(new GetWifiQrQuery(BranchId), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(Code, result.Value!.Code);
        }

        [Fact]
        public void ManagementEndpointsRequireWifiManageAndBuildTheWifiLink()
        {
            var get = typeof(TenantBranchWifiController).GetMethod(nameof(TenantBranchWifiController.GetQr))!;
            var regenerate = typeof(TenantBranchWifiController).GetMethod(nameof(TenantBranchWifiController.RegenerateQr))!;

            Assert.Equal(
                "t/{slug}/api/v1/branches/{branchId:guid}/wifi",
                typeof(TenantBranchWifiController).GetCustomAttribute<RouteAttribute>()!.Template);
            Assert.Equal("qr", Assert.IsType<HttpGetAttribute>(get.GetCustomAttribute<HttpGetAttribute>()).Template);
            Assert.Equal("qr/regenerate", Assert.IsType<HttpPostAttribute>(
                regenerate.GetCustomAttribute<HttpPostAttribute>()).Template);
            Assert.All(
                new[] { get, regenerate },
                action => Assert.Equal(
                    TenantPermissions.WifiManage,
                    Assert.IsType<TenantAuthorizeAttribute>(
                        action.GetCustomAttribute<TenantAuthorizeAttribute>()).Policy));

            var links = new TableQrLinks(
                new Uri("https://app.example.com"),
                new FakeTenantContextAccessor());
            var response = links.CreateWifi(new WifiQrData(BranchId, Code));

            Assert.Equal(BranchId, response.BranchId);
            Assert.Equal($"/t/demo-cafe/w/{Code}", response.Path);
            Assert.Equal($"https://app.example.com/t/demo-cafe/w/{Code}", response.Url);
        }

        [Fact]
        public void TheGuestActionIsAnonymousRateLimitedAndAllowListed()
        {
            var action = typeof(TenantGuestWifiController).GetMethod(
                nameof(TenantGuestWifiController.Get))!;

            Assert.Equal(
                "t/{slug}/api/v1/guest/wifi",
                typeof(TenantGuestWifiController).GetCustomAttribute<RouteAttribute>()!.Template);
            Assert.Equal("{code}", Assert.IsType<HttpGetAttribute>(action.GetCustomAttribute<HttpGetAttribute>()).Template);
            Assert.NotNull(action.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Null(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.True(Assert.IsType<TableQrEndpointAttribute>(
                action.GetCustomAttribute<TableQrEndpointAttribute>()).IsGuest);
            Assert.Contains(
                action.GetCustomAttributes<ProducesResponseTypeAttribute>(),
                response => response.StatusCode == StatusCodes.Status429TooManyRequests);
        }

        private static WifiPackage ActivePackage(string name, decimal price, int sortOrder) =>
            WifiPackage.Create(
                Guid.CreateVersion7(), name, 60, 1m, WifiDataMode.Unlimited, null, price, sortOrder, Now, UserId);

        private static WifiPackage InactivePackage(string name, decimal price, int sortOrder)
        {
            var package = ActivePackage(name, price, sortOrder);
            package.Deactivate(Now, UserId);
            return package;
        }

        private static FakeTenantUser StaffUser(Guid branchId) =>
            new(UserId, new TenantAccountAccess(true, null, true) { BranchId = branchId });

        private static FakeTenantUser OwnerUser() =>
            new(UserId, new TenantAccountAccess(false, null, true));

        private static ITenantContextAccessor TenantAccessor() => new FakeTenantContextAccessor();

        private sealed class FakeTenantContextAccessor : ITenantContextAccessor
        {
            public TenantContext GetRequired() =>
                TenantContext.Create(TenantId, "tenant-secret", true, "demo-cafe");
        }

        private sealed class FakeTenantUser(Guid? userId, TenantAccountAccess? accountAccess) : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId => userId;

            public Guid? TenantId => TenantIdForTests;

            public IReadOnlyCollection<string> Roles => [];

            public Guid? BranchId => AccountAccess?.BranchId;

            public TenantAccountAccess? AccountAccess { get; } = accountAccess;

            private static Guid TenantIdForTests => WifiQrTests.TenantId;
        }

        private sealed class FakeWifiQrStore(Guid branchId, string code) : IWifiQrStore
        {
            public string Code { get; private set; } = code;

            public WifiQrContext? Context { get; set; } = new(
                branchId,
                WifiPolicy.Free,
                BranchWifiNetwork.Create(branchId, "CafeGuest", WifiSecurity.WPA, null, Now, UserId),
                []);

            public int Writes { get; private set; }

            public Task<Result<WifiQrData>> GetOrCreateAsync(
                Guid? branchIdScope,
                Guid requestedBranchId,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                Task.FromResult(branchIdScope is null || branchIdScope == requestedBranchId
                    ? Result.Success(new WifiQrData(requestedBranchId, Code))
                    : Result.Failure<WifiQrData>(WifiErrors.NotFound()));

            public Task<Result<WifiQrData>> RegenerateAsync(
                Guid? branchIdScope,
                Guid requestedBranchId,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                if (branchIdScope is not null && branchIdScope != requestedBranchId)
                {
                    return Task.FromResult(Result.Failure<WifiQrData>(WifiErrors.NotFound()));
                }

                Writes++;
                Code = Replacement;
                return Task.FromResult(Result.Success(new WifiQrData(requestedBranchId, Code)));
            }

            public Task<WifiQrContext?> ResolveAsync(string requestedCode, CancellationToken cancellationToken) =>
                Task.FromResult(requestedCode == Code ? Context : null);
        }
    }
}
