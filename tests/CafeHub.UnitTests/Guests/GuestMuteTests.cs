using System.Reflection;
using System.Text.Json;
using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Guests;
using CafeHub.Application.Guests.Commands;
using CafeHub.Application.Guests.Contracts;
using CafeHub.Application.Identity;
using CafeHub.Application.Orders;
using CafeHub.Application.Orders.Commands;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Orders.DTOs;
using CafeHub.Application.Orders.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Application.Stations.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using MediatR;

namespace CafeHub.UnitTests.Guests
{
    public sealed class GuestMuteTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OtherBranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid OrderId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly Guid MuteId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-23T10:00:00Z");
        private const string DeviceId = "device-0123456789abcdef";

        [Fact]
        public void Mute_AMuteIsActiveUntilItExpiresOrIsLifted()
        {
            var mute = NewMute(expiryHours: 2);

            Assert.True(mute.IsActive(Now));
            Assert.True(mute.IsActive(Now.AddHours(1)));
            Assert.False(mute.IsActive(Now.AddHours(2)));
            Assert.Equal(Now.AddHours(2), mute.ExpiresAt);

            Assert.True(mute.Lift(UserId, Now.AddMinutes(5)));
            Assert.False(mute.IsActive(Now.AddMinutes(6)));
            Assert.Equal(Now.AddMinutes(5), mute.LiftedAt);
            Assert.Equal(UserId, mute.LiftedByUserId);
            Assert.False(mute.Lift(UserId, Now.AddMinutes(7)));
        }

        [Fact]
        public void Mute_TheDefaultExpiryIsTwentyFourHoursAndTheMaximumIsSevenDays()
        {
            Assert.Equal(24, GuestMute.DefaultExpiryHours);
            Assert.Equal(168, GuestMute.MaximumExpiryHours);
            Assert.Equal(Now.AddHours(24), GuestMute.ResolveExpiry(Now, null));
            Assert.Equal(Now.AddHours(6), GuestMute.ResolveExpiry(Now, 6));
            Assert.Throws<ArgumentOutOfRangeException>(() => GuestMute.Create(
                Guid.CreateVersion7(), BranchId, OrderId, DeviceId, UserId, "Spam", Now, 169, UserId));
            Assert.Throws<ArgumentException>(() => GuestMute.Create(
                Guid.CreateVersion7(), BranchId, OrderId, "short", UserId, "Spam", Now, null, UserId));
            Assert.Throws<ArgumentException>(() => GuestMute.Create(
                Guid.CreateVersion7(), BranchId, OrderId, DeviceId, UserId, "ab", Now, null, UserId));
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(2, false)]
        [InlineData(3, true)]
        [InlineData(4, true)]
        public void Suggestion_ThreeRejectedOrdersWithinTheWindowSuggestAMute(int rejectedOrders, bool expected)
        {
            Assert.Equal(3, GuestMute.MuteSuggestionThreshold);
            Assert.Equal(24, GuestMute.RejectionWindowHours);
            Assert.Equal(expected, GuestMute.ShouldSuggestMute(rejectedOrders));
        }

        [Fact]
        public void Mute_TheRecordCarriesNoNetworkAddressField()
        {
            var properties = typeof(GuestMute).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .ToArray();

            Assert.DoesNotContain(properties, name => name.Contains("Ip", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(properties, name => name.Contains("Address", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task Mute_AStaffMemberMutesTheOrderDeviceAndGetsTheStoredMute()
        {
            var store = new FakeGuestMuteStore { Target = new GuestMuteTarget(OrderId, BranchId, 12, DeviceId) };
            var handler = CreateMuteHandler(store, StaffUser(BranchId));

            var result = await handler.Handle(new MuteGuestDeviceCommand
            {
                OrderId = OrderId,
                Reason = " Sending repeated fake orders ",
            }, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(BranchId, store.OrderScope);
            var created = Assert.Single(store.Created);
            Assert.Equal(BranchId, created.BranchId);
            Assert.Equal(OrderId, created.OrderId);
            Assert.Equal(DeviceId, created.DeviceId);
            Assert.Equal(UserId, created.MutedByUserId);
            Assert.Equal("Sending repeated fake orders", created.Reason);
            Assert.Equal(Now.AddHours(24), created.ExpiresAt);
            Assert.Equal(12, result.Value!.OrderNumber);
            Assert.Null(result.Value.LiftedAt);
        }

        [Fact]
        public async Task Mute_AnExplicitExpiryIsHonoured()
        {
            var store = new FakeGuestMuteStore { Target = new GuestMuteTarget(OrderId, BranchId, 12, DeviceId) };
            var handler = CreateMuteHandler(store, StaffUser(BranchId));

            var result = await handler.Handle(
                new MuteGuestDeviceCommand { OrderId = OrderId, Reason = "Spam", ExpiryHours = 48 },
                default);

            Assert.True(result.IsSuccess);
            Assert.Equal(Now.AddHours(48), Assert.Single(store.Created).ExpiresAt);
        }

        [Fact]
        public async Task Mute_AnOrderWithoutADeviceIsRefused()
        {
            var store = new FakeGuestMuteStore { Target = new GuestMuteTarget(OrderId, BranchId, 12, null) };
            var handler = CreateMuteHandler(store, StaffUser(BranchId));

            var result = await handler.Handle(
                new MuteGuestDeviceCommand { OrderId = OrderId, Reason = "Spam" }, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(GuestMuteErrors.DeviceRequiredCode, result.Error!.Code);
            Assert.Empty(store.Created);
        }

        [Fact]
        public async Task Mute_AForeignOrderReturnsTheUniformNotFound()
        {
            var store = new FakeGuestMuteStore { Target = null };
            var handler = CreateMuteHandler(store, StaffUser(BranchId));

            var result = await handler.Handle(
                new MuteGuestDeviceCommand { OrderId = OrderId, Reason = "Spam" }, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(GuestMuteErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(BranchId, store.OrderScope);
            Assert.Empty(store.Created);
        }

        [Fact]
        public async Task Mute_TheOwnerMayMuteAcrossBranches()
        {
            var store = new FakeGuestMuteStore
            {
                Target = new GuestMuteTarget(OrderId, OtherBranchId, 12, DeviceId),
            };
            var handler = CreateMuteHandler(store, OwnerUser());

            var result = await handler.Handle(
                new MuteGuestDeviceCommand { OrderId = OrderId, Reason = "Spam" }, default);

            Assert.True(result.IsSuccess);
            Assert.Null(store.OrderScope);
            Assert.Equal(OtherBranchId, Assert.Single(store.Created).BranchId);
        }

        [Fact]
        public async Task Mute_AStaffMemberWithoutAnActiveBranchIsForbidden()
        {
            var store = new FakeGuestMuteStore { Target = new GuestMuteTarget(OrderId, BranchId, 12, DeviceId) };
            var handler = CreateMuteHandler(store, new FakeTenantUser
            {
                UserId = UserId,
                AccountAccess = new TenantAccountAccess(true, null, false),
            });

            var result = await handler.Handle(
                new MuteGuestDeviceCommand { OrderId = OrderId, Reason = "Spam" }, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthErrors.Forbidden().Code, result.Error!.Code);
            Assert.Empty(store.Created);
        }

        [Fact]
        public async Task Mute_RepeatingTheMuteReturnsTheExistingRecordAndDoesNotCreateAnother()
        {
            var existing = Entry(liftedAt: null);
            var store = new FakeGuestMuteStore
            {
                Target = new GuestMuteTarget(OrderId, BranchId, 12, DeviceId),
                Active = existing,
            };
            var handler = CreateMuteHandler(store, StaffUser(BranchId));

            var result = await handler.Handle(
                new MuteGuestDeviceCommand { OrderId = OrderId, Reason = "Spam again", ExpiryHours = 96 },
                default);

            Assert.True(result.IsSuccess);
            Assert.Equal(existing.Id, result.Value!.Id);
            Assert.Equal(existing.ExpiresAt, result.Value.ExpiresAt);
            Assert.Empty(store.Created);
        }

        [Theory]
        [InlineData("")]
        [InlineData("ab")]
        [InlineData("line\nbreak")]
        public void Mute_TheValidatorRefusesAnInvalidReason(string reason)
        {
            var validator = new MuteGuestDeviceCommandValidator();

            var result = validator.Validate(new MuteGuestDeviceCommand { OrderId = OrderId, Reason = reason });

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == nameof(MuteGuestDeviceCommand.Reason));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(169)]
        public void Mute_TheValidatorRefusesAnExpiryOutsideTheAllowedRange(int expiryHours)
        {
            var validator = new MuteGuestDeviceCommandValidator();

            var result = validator.Validate(new MuteGuestDeviceCommand
            {
                OrderId = OrderId,
                Reason = "Spam",
                ExpiryHours = expiryHours,
            });

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == nameof(MuteGuestDeviceCommand.ExpiryHours));
        }

        [Fact]
        public void Mute_TheAuditSnapshotCarriesNoDeviceIdentifier()
        {
            var mute = NewMute(expiryHours: 4);

            var snapshot = TenantAuditSnapshotSerializer.Create(
                "guest_mutes",
                TenantAuditChangeSets.GuestMuteCreate(mute));

            Assert.True(snapshot.IsSuccess);
            var afterState = snapshot.Value.AfterState!;
            Assert.DoesNotContain(DeviceId, afterState, StringComparison.Ordinal);
            Assert.DoesNotContain("device", afterState, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Reason", afterState, StringComparison.Ordinal);
            Assert.Contains("ExpiresAt", afterState, StringComparison.Ordinal);
            Assert.Contains(mute.BranchId.ToString(), afterState, StringComparison.Ordinal);
        }

        [Fact]
        public void Unmute_TheAuditSnapshotRecordsOnlyTheLift()
        {
            var mute = NewMute(expiryHours: 4);
            mute.Lift(UserId, Now.AddMinutes(10));

            var snapshot = TenantAuditSnapshotSerializer.Create(
                "guest_mutes",
                TenantAuditChangeSets.GuestMuteLift(mute));

            Assert.True(snapshot.IsSuccess);
            Assert.DoesNotContain("device", snapshot.Value.AfterState!, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("LiftedAt", snapshot.Value.AfterState!, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Unmute_SetsTheLiftTimeAndTheLiftingUser()
        {
            var store = new FakeGuestMuteStore { Entry = Entry(liftedAt: null) };
            var handler = CreateUnmuteHandler(store, StaffUser(BranchId));

            var result = await handler.Handle(new UnmuteGuestDeviceCommand { MuteId = MuteId }, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(Now, result.Value!.LiftedAt);
            var lift = Assert.Single(store.Lifts);
            Assert.Equal(MuteId, lift.MuteId);
            Assert.Equal(UserId, lift.UserId);
            Assert.Equal(Now, lift.Now);
        }

        [Fact]
        public async Task Unmute_AnAlreadyLiftedMuteIsANoOp()
        {
            var store = new FakeGuestMuteStore { Entry = Entry(liftedAt: Now.AddMinutes(-30)) };
            var handler = CreateUnmuteHandler(store, StaffUser(BranchId));

            var result = await handler.Handle(new UnmuteGuestDeviceCommand { MuteId = MuteId }, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(Now.AddMinutes(-30), result.Value!.LiftedAt);
            Assert.Empty(store.Lifts);
        }

        [Fact]
        public async Task Unmute_AnExpiredMuteIsANoOp()
        {
            var store = new FakeGuestMuteStore { Entry = Entry(liftedAt: null) with { ExpiresAt = Now.AddHours(-1) } };
            var handler = CreateUnmuteHandler(store, StaffUser(BranchId));

            var result = await handler.Handle(new UnmuteGuestDeviceCommand { MuteId = MuteId }, default);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value!.LiftedAt);
            Assert.Empty(store.Lifts);
        }

        [Fact]
        public async Task Unmute_AForeignMuteReturnsTheUniformNotFound()
        {
            var store = new FakeGuestMuteStore { Entry = null };
            var handler = CreateUnmuteHandler(store, StaffUser(BranchId));

            var result = await handler.Handle(new UnmuteGuestDeviceCommand { MuteId = MuteId }, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(GuestMuteErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(BranchId, store.MuteScope);
            Assert.Empty(store.Lifts);
        }

        [Fact]
        public async Task Feed_IncludeMutedDefaultsToFalseAndIsForwardedToTheStore()
        {
            var store = new FakeOrderFeedStore();
            var handler = CreateFeedHandler(store, StaffUser(BranchId));

            var defaultFeed = await handler.Handle(new GetOrderFeedQuery(), default);
            var inclusiveFeed = await handler.Handle(new GetOrderFeedQuery { IncludeMuted = true }, default);

            Assert.True(defaultFeed.IsSuccess);
            Assert.False(store.Requests[0].IncludeMuted);
            Assert.True(inclusiveFeed.IsSuccess);
            Assert.True(store.Requests[1].IncludeMuted);
        }

        [Fact]
        public async Task Feed_TheMuteSuggestionReachesTheFeedItem()
        {
            var entry = new OrderFeedEntry(
                Guid.CreateVersion7(),
                12,
                7,
                "DineIn",
                "Rejected",
                "Sara",
                Now.AddMinutes(-5),
                Now.AddMinutes(5),
                Now,
                42.50m,
                [])
            {
                SuggestMute = true,
            };
            var store = new FakeOrderFeedStore { Entries = [entry] };
            var handler = CreateFeedHandler(store, StaffUser(BranchId));

            var result = await handler.Handle(new GetOrderFeedQuery(), default);

            Assert.True(result.IsSuccess);
            Assert.True(Assert.Single(result.Value!.Items).SuggestMute);
        }

        [Fact]
        public void Feed_AnItemWithoutRejectionsCarriesNoSuggestion()
        {
            var entry = new OrderFeedEntry(
                Guid.CreateVersion7(),
                12,
                7,
                "DineIn",
                "Pending",
                "Sara",
                Now.AddMinutes(-5),
                Now.AddMinutes(5),
                Now,
                42.50m,
                []);

            var item = OrderFeedItem.From(entry);

            Assert.False(item.SuggestMute);
            Assert.DoesNotContain("device", JsonSerializer.Serialize(item), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Permissions_TheGuestMuteCodesAreGrantableToStaff()
        {
            Assert.Equal("guests.mute", TenantPermissions.GuestsMute);
            Assert.Equal("guests.mute.audit", TenantPermissions.GuestsMuteAudit);
            Assert.Contains(TenantPermissions.GuestsMute, TenantPermissions.All);
            Assert.Contains(TenantPermissions.GuestsMuteAudit, TenantPermissions.All);
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.GuestsMute));
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.GuestsMuteAudit));
            Assert.False(TenantPermissions.IsOwnerOnly(TenantPermissions.GuestsMute));
        }

        private static MuteGuestDeviceCommandHandler CreateMuteHandler(FakeGuestMuteStore store, FakeTenantUser user) =>
            new(store, user, new FixedClock());

        private static UnmuteGuestDeviceCommandHandler CreateUnmuteHandler(FakeGuestMuteStore store, FakeTenantUser user) =>
            new(store, user, new FixedClock());

        private static GetOrderFeedQueryHandler CreateFeedHandler(FakeOrderFeedStore store, FakeTenantUser user) =>
            new(new NoSender(), store, user, new FakeStationScope(), new PaginationOptions
            {
                DefaultPageSize = 20,
                MaxPageSize = 100,
            });

        private static FakeTenantUser StaffUser(Guid branchId) => new()
        {
            UserId = UserId,
            BranchId = branchId,
            AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = branchId },
        };

        private static FakeTenantUser OwnerUser() => new()
        {
            UserId = Guid.CreateVersion7(),
            AccountAccess = new TenantAccountAccess(false, null, false),
        };

        private static GuestMute NewMute(int? expiryHours) => GuestMute.Create(
            Guid.CreateVersion7(),
            BranchId,
            OrderId,
            DeviceId,
            UserId,
            "Spam",
            Now,
            expiryHours,
            UserId);

        private static GuestMuteEntry Entry(DateTimeOffset? liftedAt) => new(
            MuteId,
            BranchId,
            OrderId,
            12,
            UserId,
            "Spam",
            Now.AddHours(-1),
            Now.AddHours(23),
            liftedAt,
            liftedAt is null ? null : UserId);

        private sealed class FixedClock : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => Now;
        }

        private sealed class FakeTenantUser : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId { get; init; }

            public Guid? TenantId { get; init; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

            public IReadOnlyCollection<string> Roles => [];

            public Guid? BranchId { get; init; }

            public TenantAccountAccess? AccountAccess { get; init; }
        }

        private sealed class FakeGuestMuteStore : IGuestMuteStore
        {
            public GuestMuteTarget? Target { get; init; }

            public GuestMuteEntry? Active { get; init; }

            public GuestMuteEntry? Entry { get; init; }

            public List<GuestMute> Created { get; } = [];

            public List<(Guid MuteId, Guid UserId, DateTimeOffset Now)> Lifts { get; } = [];

            public Guid? OrderScope { get; private set; }

            public Guid? MuteScope { get; private set; }

            public Task<GuestMuteTarget?> FindOrderAsync(
                Guid? branchId, Guid orderId, CancellationToken cancellationToken)
            {
                OrderScope = branchId;
                return Task.FromResult(Target);
            }

            public Task<GuestMuteEntry?> FindActiveAsync(
                Guid branchId, string deviceId, DateTimeOffset now, CancellationToken cancellationToken) =>
                Task.FromResult(Active);

            public Task<GuestMuteEntry?> FindAsync(
                Guid? branchId, Guid muteId, CancellationToken cancellationToken)
            {
                MuteScope = branchId;
                return Task.FromResult(Entry);
            }

            public Task<GuestMuteListPage> ListAsync(
                GuestMuteListRequest request, CancellationToken cancellationToken) =>
                Task.FromResult(new GuestMuteListPage([], null));

            public Task<IReadOnlyList<GuestMuteSummaryEntry>> ListSummaryAsync(
                GuestMuteSummaryRequest request, CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<GuestMuteSummaryEntry>>([]);

            public Task<string?> FindUserNameAsync(Guid userId, CancellationToken cancellationToken) =>
                Task.FromResult<string?>("Sara");

            public Task<Result> CreateAsync(GuestMute mute, CancellationToken cancellationToken)
            {
                Created.Add(mute);
                return Task.FromResult(Result.Success());
            }

            public Task<Result> LiftAsync(
                Guid muteId, Guid liftedByUserId, DateTimeOffset now, CancellationToken cancellationToken)
            {
                Lifts.Add((muteId, liftedByUserId, now));
                return Task.FromResult(Result.Success());
            }
        }

        private sealed class FakeOrderFeedStore : IOrderFeedStore
        {
            public List<OrderFeedListRequest> Requests { get; } = [];

            public IReadOnlyList<OrderFeedEntry> Entries { get; init; } = [];

            public Task<OrderFeedPage> ListFeedAsync(
                OrderFeedListRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(new OrderFeedPage(Entries, null));
            }

            public Task<OrderFeedEntry?> FindByIdAsync(
                Guid? branchId, Guid orderId, CancellationToken cancellationToken) =>
                Task.FromResult<OrderFeedEntry?>(null);
        }

        private sealed class FakeStationScope : IStationScope
        {
            public Task<IReadOnlyList<Guid>?> GetVisibleStationIdsAsync(
                Guid userId, CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<Guid>?>(null);
        }

        private sealed class NoSender : ISender
        {
            public Task<TResponse> Send<TResponse>(
                IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
                Task.FromResult((TResponse)(object)Result.Success<IReadOnlyList<Guid>>([]));

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
                where TRequest : IRequest =>
                Task.CompletedTask;

            public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
                Task.FromResult<object?>(null);

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
                IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public IAsyncEnumerable<object?> CreateStream(
                object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
        }
    }
}
