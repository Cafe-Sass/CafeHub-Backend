using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory;
using CafeHub.Application.Inventory.Alerts;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Inventory.Queries;
using CafeHub.Application.Notifications;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeHub.UnitTests.Inventory
{
    public sealed class InventoryAlertTests
    {
        private static readonly Guid FixtureTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid OtherBranchId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        private static readonly Guid ItemId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        private static readonly Guid BatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly DateOnly Today = new(2026, 11, 26);
        private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

        private static readonly InventoryAlertResponse Response = new(
            Guid.NewGuid(), ItemId, "Coffee Beans", BranchId, null, "LowStock", "Open",
            1.5m, null, Now, null, null);

        [Fact]
        public void LowStock_RaisesAtOrBelowTheThresholdAndStaysQuietAboveIt()
        {
            Assert.True(InventoryAlertRule.IsLowStock(3m, 3m));
            Assert.True(InventoryAlertRule.IsLowStock(0m, 3m));
            Assert.False(InventoryAlertRule.IsLowStock(3.001m, 3m));
        }

        [Fact]
        public void Expiry_ClassifiesExpiredAndNearExpiryBatchesFromTheItemDays()
        {
            Assert.Equal(
                InventoryAlertKind.Expired,
                InventoryAlertRule.ClassifyExpiry(Today.AddDays(-1), Today, 7));
            Assert.Equal(
                InventoryAlertKind.NearExpiry,
                InventoryAlertRule.ClassifyExpiry(Today, Today, 7));
            Assert.Equal(
                InventoryAlertKind.NearExpiry,
                InventoryAlertRule.ClassifyExpiry(Today.AddDays(7), Today, 7));
            Assert.Null(InventoryAlertRule.ClassifyExpiry(Today.AddDays(8), Today, 7));
            Assert.Null(InventoryAlertRule.ClassifyExpiry(null, Today, 7));
            Assert.Equal(
                InventoryAlertKind.NearExpiry,
                InventoryAlertRule.ClassifyExpiry(Today, Today, 0));
            Assert.Null(InventoryAlertRule.ClassifyExpiry(Today.AddDays(1), Today, 0));
        }

        [Fact]
        public void Plan_RaisesOneAlertPerConditionAndIgnoresRepeatedConditions()
        {
            var condition = LowStockCondition();

            var plan = InventoryAlertRule.Plan(
                Array.Empty<InventoryAlert>(), new[] { condition, condition });

            Assert.Equal(condition, Assert.Single(plan.Raise));
            Assert.Empty(plan.Resolve);
        }

        [Fact]
        public void Plan_RepeatedEvaluationOfAnOpenConditionRaisesNothingNew()
        {
            var plan = InventoryAlertRule.Plan(
                new[] { RaiseLowStock() }, new[] { LowStockCondition() });

            Assert.Empty(plan.Raise);
            Assert.Empty(plan.Resolve);
        }

        [Fact]
        public void Plan_RecoveryResolvesTheOpenConditionAndAnExpiryChangeSwapsTheBatchKind()
        {
            var lowStock = RaiseLowStock();
            var nearExpiry = InventoryAlert.Raise(
                Guid.NewGuid(), ItemId, BranchId, BatchId, InventoryAlertKind.NearExpiry, Now, ActorId);

            var plan = InventoryAlertRule.Plan(
                new[] { lowStock, nearExpiry },
                new[] { new InventoryAlertCondition(ItemId, BranchId, BatchId, InventoryAlertKind.Expired) });

            var raised = Assert.Single(plan.Raise);
            Assert.Equal(InventoryAlertKind.Expired, raised.Kind);
            Assert.Equal(2, plan.Resolve.Count);
            Assert.Contains(lowStock, plan.Resolve);
            Assert.Contains(nearExpiry, plan.Resolve);
        }

        [Fact]
        public void Plan_IgnoresResolvedAlertsWhenDecidingToRaiseAgain()
        {
            var resolved = RaiseLowStock();
            Assert.True(resolved.Resolve(Now.AddMinutes(1), ActorId));

            var plan = InventoryAlertRule.Plan(
                new[] { resolved }, new[] { LowStockCondition() });

            Assert.Single(plan.Raise);
            Assert.Empty(plan.Resolve);
        }

        [Fact]
        public void Alert_LowStockTakesNoBatchAndExpiryKindsRequireOne()
        {
            Assert.Null(RaiseLowStock().BatchId);
            Assert.Equal(InventoryAlertState.Open, RaiseLowStock().State);

            Assert.Throws<ArgumentException>(() => InventoryAlert.Raise(
                Guid.NewGuid(), ItemId, BranchId, BatchId, InventoryAlertKind.LowStock, Now, ActorId));
            Assert.Throws<ArgumentException>(() => InventoryAlert.Raise(
                Guid.NewGuid(), ItemId, BranchId, null, InventoryAlertKind.NearExpiry, Now, ActorId));
            Assert.Throws<ArgumentException>(() => InventoryAlert.Raise(
                Guid.NewGuid(), ItemId, BranchId, null, InventoryAlertKind.Expired, Now, ActorId));
            Assert.Throws<ArgumentOutOfRangeException>(() => InventoryAlert.Raise(
                Guid.NewGuid(), ItemId, BranchId, null, (InventoryAlertKind)99, Now, ActorId));
        }

        [Fact]
        public void Alert_ResolveAndMarkNotifiedChangeOnce()
        {
            var alert = RaiseLowStock();

            Assert.True(alert.Resolve(Now.AddHours(1), ActorId));
            Assert.False(alert.Resolve(Now.AddHours(2), ActorId));
            Assert.Equal(InventoryAlertState.Resolved, alert.State);
            Assert.Equal(Now.AddHours(1), alert.ResolvedAt);
            Assert.Equal(Now.AddHours(1), alert.UpdatedAt);

            Assert.True(alert.MarkNotified(Now.AddMinutes(5), ActorId));
            Assert.False(alert.MarkNotified(Now.AddMinutes(6), ActorId));
            Assert.Equal(Now.AddMinutes(5), alert.NotifiedAt);
        }

        [Fact]
        public async Task Notifier_WhenTheSendFailsKeepsTheAlertUnsent()
        {
            var alert = RaiseLowStock();
            var sender = new FakeNotificationSender { Succeeds = false };
            var notifier = new InventoryAlertNotifier(sender, NullLogger<InventoryAlertNotifier>.Instance);

            var sent = await notifier.TrySendAsync(
                alert, FixtureTenantId, "owner@example.test", Values(), CancellationToken.None);

            Assert.False(sent);
            Assert.Null(alert.NotifiedAt);
            var message = Assert.Single(sender.Messages);
            Assert.Equal(NotificationKind.InventoryAlert, message.Kind);
            Assert.Equal(NotificationTemplateKeys.InventoryAlert, message.TemplateKey);
            Assert.Equal("Coffee Beans", message.Values["itemName"]);
            Assert.Equal(BranchId.ToString(), message.Values["branchId"]);
        }

        [Fact]
        public async Task Notifier_WhenTheProviderThrowsKeepsTheAlertUnsent()
        {
            var alert = RaiseLowStock();
            var notifier = new InventoryAlertNotifier(
                new FakeNotificationSender { Throws = true },
                NullLogger<InventoryAlertNotifier>.Instance);

            var sent = await notifier.TrySendAsync(
                alert, FixtureTenantId, "owner@example.test", Values(), CancellationToken.None);

            Assert.False(sent);
            Assert.Null(alert.NotifiedAt);
        }

        [Fact]
        public async Task Notifier_WhenTheSendSucceedsAllowsTheAlertToBeMarkedNotified()
        {
            var alert = RaiseLowStock();
            var notifier = new InventoryAlertNotifier(
                new FakeNotificationSender(), NullLogger<InventoryAlertNotifier>.Instance);

            var sent = await notifier.TrySendAsync(
                alert, FixtureTenantId, "owner@example.test", Values(), CancellationToken.None);

            Assert.True(sent);
            Assert.Null(alert.NotifiedAt);
            Assert.True(alert.MarkNotified(Now.AddMinutes(1), ActorId));
            Assert.Equal(Now.AddMinutes(1), alert.NotifiedAt);
        }

        [Fact]
        public async Task Notifier_WithoutATenantRecipientKeepsTheAlertUnsentWithoutCallingTheProvider()
        {
            var sender = new FakeNotificationSender();
            var notifier = new InventoryAlertNotifier(sender, NullLogger<InventoryAlertNotifier>.Instance);

            Assert.False(await notifier.TrySendAsync(
                RaiseLowStock(), FixtureTenantId, null, Values(), CancellationToken.None));
            Assert.False(await notifier.TrySendAsync(
                RaiseLowStock(), null, "owner@example.test", Values(), CancellationToken.None));
            Assert.Empty(sender.Messages);
        }

        [Fact]
        public void AlertsValidator_EnforcesPagingAndDefinedFilters()
        {
            var validator = new ListInventoryAlertsQueryValidator(new PaginationOptions());

            Assert.True(validator.Validate(new ListInventoryAlertsQuery()).IsValid);
            Assert.False(validator.Validate(new ListInventoryAlertsQuery { Page = 0 }).IsValid);
            Assert.False(validator.Validate(new ListInventoryAlertsQuery { PageSize = 0 }).IsValid);
            Assert.False(validator.Validate(new ListInventoryAlertsQuery { PageSize = 101 }).IsValid);
            Assert.False(validator.Validate(new ListInventoryAlertsQuery { BranchId = Guid.Empty }).IsValid);

            var invalidKind = validator.Validate(
                new ListInventoryAlertsQuery { Kind = (InventoryAlertKind)99 });
            Assert.False(invalidKind.IsValid);
            Assert.Equal(InventoryAlertErrors.InvalidKindFilterCode, invalidKind.Errors[0].ErrorCode);

            var invalidState = validator.Validate(
                new ListInventoryAlertsQuery { State = (InventoryAlertState)99 });
            Assert.False(invalidState.IsValid);
            Assert.Equal(InventoryAlertErrors.InvalidStateFilterCode, invalidState.Errors[0].ErrorCode);
        }

        [Fact]
        public async Task AlertsHandler_ForStaffPinsTheBranchEvaluatesExpiryAndForwardsTheFilters()
        {
            var store = new FakeAlertStore
            {
                Page = new InventoryAlertListPage(new[] { Response }, 1),
            };
            var evaluator = new FakeAlertEvaluator();
            var handler = new ListInventoryAlertsQueryHandler(
                store,
                evaluator,
                StaffUser(BranchId),
                new PaginationOptions { DefaultPageSize = 10 });

            var result = await handler.Handle(
                new ListInventoryAlertsQuery
                {
                    Kind = InventoryAlertKind.NearExpiry,
                    State = InventoryAlertState.Open,
                    IncludeTotalCount = true,
                },
                default);

            Assert.True(result.IsSuccess);
            Assert.Single(result.Value.Items);
            Assert.Equal(10, result.Value.PageSize);
            Assert.Equal(1, result.Value.TotalCount);
            Assert.Equal((BranchId, (Guid?)null), Assert.Single(evaluator.ExpiryRequests));
            var request = Assert.Single(store.Requests);
            Assert.Equal(BranchId, request.BranchId);
            Assert.Equal(InventoryAlertKind.NearExpiry, request.Kind);
            Assert.Equal(InventoryAlertState.Open, request.State);
            Assert.True(request.IncludeTotalCount);
        }

        [Fact]
        public async Task AlertsHandler_ForStaffRefusesAForeignBranchBeforeEvaluatingOrReading()
        {
            var store = new FakeAlertStore();
            var evaluator = new FakeAlertEvaluator();
            var handler = new ListInventoryAlertsQueryHandler(
                store, evaluator, StaffUser(OtherBranchId), new PaginationOptions());

            var result = await handler.Handle(
                new ListInventoryAlertsQuery { BranchId = BranchId }, default);

            Assert.Equal(InventoryBatchErrors.BranchNotFound(), result.Error);
            Assert.Empty(evaluator.ExpiryRequests);
            Assert.Empty(store.Requests);
        }

        [Fact]
        public async Task AlertsHandler_WhenTheEvaluationFailsDoesNotReadTheAlerts()
        {
            var store = new FakeAlertStore();
            var evaluator = new FakeAlertEvaluator
            {
                Failure = Error.Failure("Inventory.AlertEvaluationFailed", "The alert evaluation failed."),
            };
            var handler = new ListInventoryAlertsQueryHandler(
                store, evaluator, Owner(), new PaginationOptions());

            var result = await handler.Handle(new ListInventoryAlertsQuery(), default);

            Assert.Equal("Inventory.AlertEvaluationFailed", result.Error!.Code);
            Assert.Empty(store.Requests);
        }

        private static InventoryAlert RaiseLowStock() =>
            InventoryAlert.Raise(
                Guid.NewGuid(), ItemId, BranchId, null, InventoryAlertKind.LowStock, Now, ActorId);

        private static InventoryAlertCondition LowStockCondition() =>
            new(ItemId, BranchId, null, InventoryAlertKind.LowStock);

        private static IReadOnlyDictionary<string, string> Values() =>
            new Dictionary<string, string>
            {
                ["itemName"] = "Coffee Beans",
                ["branchId"] = BranchId.ToString(),
                ["alertKind"] = InventoryAlertKind.LowStock.ToString(),
                ["quantity"] = "1.5",
            };

        private static FakeTenantUser Owner() => new();

        private static FakeTenantUser StaffUser(Guid branchId) => new()
        {
            BranchId = branchId,
            AccountAccess = new TenantAccountAccess(true, "1", true) { BranchId = branchId },
        };

        private sealed class FakeTenantUser : ICurrentTenantUser
        {
            public bool IsAuthenticated { get; init; } = true;

            public Guid? UserId { get; init; } = ActorId;

            public Guid? TenantId { get; init; } = FixtureTenantId;

            public IReadOnlyCollection<string> Roles { get; init; } = new[] { "Owner" };

            public Guid? BranchId { get; init; }

            public TenantAccountAccess? AccountAccess { get; init; } = new(false, "1", false);
        }

        private sealed class FakeAlertEvaluator : IInventoryAlertEvaluator
        {
            public List<(Guid? BranchId, Guid? ItemId)> ExpiryRequests { get; } = new();

            public Error? Failure { get; init; }

            public Task<Result> EvaluateStockAsync(
                Guid branchId, Guid itemId, CancellationToken cancellationToken) =>
                Task.FromResult(Failure is null ? Result.Success() : Result.Failure(Failure));

            public Task<Result> EvaluateExpiryAsync(
                Guid? branchId, Guid? itemId, CancellationToken cancellationToken)
            {
                ExpiryRequests.Add((branchId, itemId));
                return Task.FromResult(Failure is null ? Result.Success() : Result.Failure(Failure));
            }
        }

        private sealed class FakeAlertStore : IInventoryAlertStore
        {
            public List<InventoryAlertListRequest> Requests { get; } = new();

            public InventoryAlertListPage Page { get; init; } =
                new(Array.Empty<InventoryAlertResponse>(), null);

            public Task<InventoryAlertListPage> ListAsync(
                InventoryAlertListRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(Page);
            }
        }

        private sealed class FakeNotificationSender : INotificationSender
        {
            public bool Succeeds { get; init; } = true;

            public bool Throws { get; init; }

            public List<NotificationMessage> Messages { get; } = new();

            public Task<Result> SendAsync(
                NotificationMessage message, CancellationToken cancellationToken)
            {
                Messages.Add(message);
                if (Throws)
                {
                    throw new InvalidOperationException("The provider failed.");
                }

                return Task.FromResult(Succeeds
                    ? Result.Success()
                    : Result.Failure(Error.Failure(
                        "Notifications.DeliveryFailed", "The notification could not be delivered.")));
            }
        }
    }
}
