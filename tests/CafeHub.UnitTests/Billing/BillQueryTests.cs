using CafeHub.Application.Billing;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Billing.Queries;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Billing
{
    public sealed class BillQueryTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OtherBranchId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid TableId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-23T18:00:00Z");
        private const string Reference = "Abcdefghijklmnopqrstuv";

        [Fact]
        public async Task Detail_GroupsTheOrdersByGuestDisplayNameWithPerPersonTotalsPaidAndRemaining()
        {
            var bill = OpenBill();
            var saraLatte = AcceptedOrder(bill, "Sara", 1, 10.00m);
            var saraCake = AcceptedOrder(bill, "Sara", 2, 4.50m);
            var omar = AcceptedOrder(bill, "Omar", 3, 6.00m);
            var payments = new[] { CashPayment(bill, 4.00m, saraLatte.Id) };
            var store = new FakeBillQueryStore
            {
                Detail = new BillSettlement(bill, [saraLatte, saraCake, omar], payments),
            };

            var result = await new GetBillQueryHandler(store, StaffUser())
                .Handle(new GetBillQuery(bill.Id), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            var detail = result.Value!;
            Assert.Equal(20.50m, detail.TotalAmount);
            Assert.Equal(4.00m, detail.PaidAmount);
            Assert.Equal(16.50m, detail.RemainingAmount);
            Assert.Equal("Cash", detail.SettlementType);
            Assert.Equal(["Omar", "Sara"], detail.Groups.Select(group => group.GuestDisplayName));

            var sara = detail.Groups.Single(group => group.GuestDisplayName == "Sara");
            Assert.Equal(14.50m, sara.TotalAmount);
            Assert.Equal(4.00m, sara.PaidAmount);
            Assert.Equal(10.50m, sara.RemainingAmount);
            Assert.Equal([1, 2], sara.Orders.Select(order => order.OrderNumber));
            Assert.Equal(4.00m, sara.Orders.Single(order => order.OrderNumber == 1).PaidAmount);

            var omarGroup = detail.Groups.Single(group => group.GuestDisplayName == "Omar");
            Assert.Equal(0m, omarGroup.PaidAmount);
            Assert.Equal(6.00m, omarGroup.RemainingAmount);
        }

        [Fact]
        public async Task Detail_OrdersWithoutANameGroupUnderTheNeutralLabel()
        {
            var bill = OpenBill();
            var named = AcceptedOrder(bill, "Sara", 1, 5.00m);
            var unnamed = AcceptedOrder(bill, "Placeholder", 2, 7.00m);
            WithBlankGuestName(unnamed);
            var store = new FakeBillQueryStore
            {
                Detail = new BillSettlement(bill, [named, unnamed], []),
            };

            var result = await new GetBillQueryHandler(store, StaffUser())
                .Handle(new GetBillQuery(bill.Id), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            var neutral = Assert.Single(result.Value!.Groups, group => group.GuestDisplayName == BillDetail.NeutralGuestName);
            Assert.Equal(7.00m, neutral.TotalAmount);
            Assert.Equal(12.00m, result.Value.TotalAmount);
        }

        [Fact]
        public async Task Detail_ABillLevelPaymentReducesTheBillButNoPerson()
        {
            var bill = OpenBill();
            var order = AcceptedOrder(bill, "Sara", 1, 10.00m);
            var store = new FakeBillQueryStore
            {
                Detail = new BillSettlement(bill, [order], [CashPayment(bill, 4.00m, null)]),
            };

            var result = await new GetBillQueryHandler(store, StaffUser())
                .Handle(new GetBillQuery(bill.Id), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(4.00m, result.Value!.PaidAmount);
            Assert.Equal(6.00m, result.Value.RemainingAmount);
            var group = Assert.Single(result.Value.Groups);
            Assert.Equal(0m, group.PaidAmount);
            Assert.Equal(10.00m, group.RemainingAmount);
        }

        [Fact]
        public async Task Detail_TheSettlementTypeIsDerivedFromTheConfirmedMethodsAndNullBeforeMoney()
        {
            var bill = OpenBill();
            var order = AcceptedOrder(bill, "Sara", 1, 10.00m);

            var unpaid = await new GetBillQueryHandler(
                    new FakeBillQueryStore { Detail = new BillSettlement(bill, [order], []) },
                    StaffUser())
                .Handle(new GetBillQuery(bill.Id), default);
            Assert.Null(unpaid.Value!.SettlementType);

            var hybrid = await new GetBillQueryHandler(
                    new FakeBillQueryStore
                    {
                        Detail = new BillSettlement(
                            bill,
                            [order],
                            [CashPayment(bill, 2.00m, null), OnlinePayment(bill, PaymentStatus.Confirmed)]),
                    },
                    StaffUser())
                .Handle(new GetBillQuery(bill.Id), default);
            Assert.Equal("Hybrid", hybrid.Value!.SettlementType);

            var pendingOnly = await new GetBillQueryHandler(
                    new FakeBillQueryStore
                    {
                        Detail = new BillSettlement(
                            bill,
                            [order],
                            [OnlinePayment(bill, PaymentStatus.PendingReview)]),
                    },
                    StaffUser())
                .Handle(new GetBillQuery(bill.Id), default);
            Assert.Null(pendingOnly.Value!.SettlementType);
            Assert.Equal(0m, pendingOnly.Value.PaidAmount);
        }

        [Fact]
        public async Task Detail_StaffReadsOnlyTheirBranchAndAForeignBillIsTheUniformNotFound()
        {
            var bill = OpenBill();
            var store = new FakeBillQueryStore { Detail = new BillSettlement(bill, [], []) };

            var result = await new GetBillQueryHandler(store, StaffUser())
                .Handle(new GetBillQuery(bill.Id), default);
            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(BranchId, store.LastBranchId);

            var foreign = await new GetBillQueryHandler(store, StaffUser(branchId: OtherBranchId))
                .Handle(new GetBillQuery(bill.Id), default);
            Assert.False(foreign.IsSuccess);
            Assert.Equal(BillingErrors.NotFoundCode, foreign.Error!.Code);

            var owner = await new GetBillQueryHandler(store, StaffUser(isStaff: false))
                .Handle(new GetBillQuery(bill.Id), default);
            Assert.True(owner.IsSuccess, owner.Error?.Code);
            Assert.Null(store.LastBranchId);
        }

        [Fact]
        public async Task List_StaffAreFixedToTheirBranchAndAMismatchedBranchIsNotFound()
        {
            var store = new FakeBillQueryStore();
            var handler = new GetBillsQueryHandler(store, StaffUser(), Pagination());

            var result = await handler.Handle(new GetBillsQuery(), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(BranchId, store.LastRequest!.BranchId);

            var mismatch = await handler.Handle(new GetBillsQuery { BranchId = OtherBranchId }, default);
            Assert.False(mismatch.IsSuccess);
            Assert.Equal(BillingErrors.NotFoundCode, mismatch.Error!.Code);
        }

        [Fact]
        public async Task List_TheOwnerMustPassABranch()
        {
            var store = new FakeBillQueryStore();
            var handler = new GetBillsQueryHandler(store, StaffUser(isStaff: false), Pagination());

            var missing = await handler.Handle(new GetBillsQuery(), default);
            Assert.False(missing.IsSuccess);
            Assert.Equal(BillingErrors.BranchRequiredCode, missing.Error!.Code);
            Assert.Null(store.LastRequest);

            var scoped = await handler.Handle(new GetBillsQuery { BranchId = OtherBranchId }, default);
            Assert.True(scoped.IsSuccess, scoped.Error?.Code);
            Assert.Equal(OtherBranchId, store.LastRequest!.BranchId);
        }

        [Fact]
        public async Task List_PagesTheBillsAndPassesTheTableAndStatusFilters()
        {
            var store = new FakeBillQueryStore
            {
                ListPage = new BillListPage(
                    [
                        Entry(1, 10.00m, 4.00m),
                        Entry(2, 5.00m, 0m),
                        Entry(3, 1.00m, 1.00m),
                    ],
                    TotalCount: 7),
            };

            var result = await new GetBillsQueryHandler(store, StaffUser(), Pagination())
                .Handle(
                    new GetBillsQuery
                    {
                        Page = 2,
                        PageSize = 2,
                        IncludeTotalCount = true,
                        TableId = TableId,
                        Status = BillStatus.Open,
                    },
                    default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            var request = store.LastRequest!;
            Assert.Equal(2, request.Page);
            Assert.Equal(2, request.PageSize);
            Assert.True(request.IncludeTotalCount);
            Assert.Equal(TableId, request.TableId);
            Assert.Equal(BillStatus.Open, request.Status);

            var page = result.Value!;
            Assert.Equal(2, page.Items.Count);
            Assert.True(page.HasNextPage);
            Assert.Equal(7, page.TotalCount);
            Assert.Equal(6.00m, page.Items[0].RemainingAmount);
        }

        private static PaginationOptions Pagination() => new() { DefaultPageSize = 20, MaxPageSize = 100 };

        private static FakeTenantUser StaffUser(Guid? branchId = null, bool isStaff = true) =>
            new(
                UserId,
                isStaff
                    ? new TenantAccountAccess(true, null, true) { BranchId = branchId ?? BillQueryTests.BranchId }
                    : new TenantAccountAccess(false, null, true));

        private static Bill OpenBill() =>
            Bill.Create(Guid.CreateVersion7(), BranchId, TableId, Reference, Now, UserId);

        private static Order AcceptedOrder(Bill bill, string guestDisplayName, int orderNumber, decimal totalAmount)
        {
            var order = Order.Create(
                Guid.CreateVersion7(),
                BranchId,
                TableId,
                orderNumber,
                new DateOnly(2026, 11, 23),
                OrderServiceType.DineIn,
                OrderSource.Guest,
                guestDisplayName,
                "device-identifier-0001",
                null,
                Reference,
                $"idempotency-key-{orderNumber}",
                null,
                Now.AddMinutes(-5),
                Now.AddMinutes(5),
                [
                    OrderItem.Create(
                        Guid.CreateVersion7(),
                        1,
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        null,
                        "Latte",
                        "Regular",
                        totalAmount,
                        1,
                        [],
                        Now.AddMinutes(-5),
                        null),
                ],
                null);
            order.Accept(UserId, Now);
            order.AttachToBill(bill.Id);
            return order;
        }

        private static Payment CashPayment(Bill bill, decimal amount, Guid? orderId) =>
            Payment.ConfirmCash(
                Guid.CreateVersion7(),
                bill.Id,
                orderId,
                amount,
                UserId,
                $"cash-key-{Guid.CreateVersion7():N}",
                Now,
                UserId);

        private static Payment OnlinePayment(Bill bill, PaymentStatus status)
        {
            var payment = Payment.ConfirmCash(
                Guid.CreateVersion7(),
                bill.Id,
                null,
                1.00m,
                UserId,
                $"online-key-{Guid.CreateVersion7():N}",
                Now,
                UserId);
            typeof(Payment).GetProperty(nameof(Payment.Method))!.SetValue(payment, PaymentMethod.Online);
            typeof(Payment).GetProperty(nameof(Payment.Status))!.SetValue(payment, status);
            return payment;
        }

        private static void WithBlankGuestName(Order order) =>
            typeof(Order).GetProperty(nameof(Order.GuestDisplayName))!.SetValue(order, "  ");

        private static BillListEntry Entry(int sequence, decimal totalAmount, decimal paidAmount) =>
            new(
                Guid.CreateVersion7(),
                BranchId,
                TableId,
                sequence,
                BillStatus.Open,
                Reference,
                Now.AddMinutes(-sequence),
                null,
                totalAmount,
                paidAmount);

        private sealed class FakeBillQueryStore : IBillQueryStore
        {
            public BillListPage ListPage { get; set; } = new([], null);

            public BillSettlement? Detail { get; set; }

            public BillListRequest? LastRequest { get; private set; }

            public Guid? LastBranchId { get; private set; }

            public Task<BillListPage> ListAsync(BillListRequest request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(ListPage);
            }

            public Task<BillSettlement?> FindDetailAsync(
                Guid? branchId,
                Guid billId,
                CancellationToken cancellationToken)
            {
                LastBranchId = branchId;
                var settlement = Detail;
                if (settlement is null || settlement.Bill.Id != billId)
                {
                    return Task.FromResult<BillSettlement?>(null);
                }

                return Task.FromResult<BillSettlement?>(
                    branchId is { } branch && settlement.Bill.BranchId != branch ? null : settlement);
            }
        }

        private sealed class FakeTenantUser(Guid? userId, TenantAccountAccess? accountAccess) : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId => userId;

            public Guid? TenantId => BranchId;

            public IReadOnlyCollection<string> Roles => [];

            public Guid? BranchId => AccountAccess?.BranchId;

            public TenantAccountAccess? AccountAccess { get; } = accountAccess;
        }
    }
}
