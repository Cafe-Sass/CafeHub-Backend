using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing;
using CafeHub.Application.Billing.Commands;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;

namespace CafeHub.UnitTests.Billing
{
    public sealed class BillCloseTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OtherBranchId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid TableId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-23T18:00:00Z");
        private const string Reference = "Abcdefghijklmnopqrstuv";

        [Fact]
        public async Task Close_RecordsTheReasonTheRemainingAmountAndTheActor()
        {
            var bill = OpenBill();
            var order = AcceptedOrder(bill, 14.50m);
            var paid = CashPayment(bill, 4.00m);
            var store = new FakeBillStore(new BillSettlement(bill, [order], [paid]));

            var result = await new CloseBillCommandHandler(store, StaffUser(), new FixedClock(Now))
                .Handle(new CloseBillCommand { BillId = bill.Id, Reason = "  Guest left early  " }, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(BillStatus.Closed, bill.Status);
            Assert.Equal("Guest left early", bill.CloseReason);
            Assert.Equal(UserId, bill.ClosedByUserId);
            Assert.Equal(Now, bill.ClosedAt);
            Assert.Equal("Closed", result.Value!.Status);
            Assert.Equal("Guest left early", result.Value.CloseReason);
            Assert.Equal(10.50m, result.Value.RemainingAmount);

            var saved = Assert.Single(store.SavedCloses);
            Assert.Equal(bill.Id, saved.Bill.Id);
            Assert.Equal(10.50m, saved.RemainingAmount);
            Assert.Equal(UserId, saved.ActorId);
        }

        [Fact]
        public async Task Close_AnAlreadyClosedBillIsIdempotentAndWritesNothing()
        {
            var bill = OpenBill();
            bill.Close("Staff shift ended", UserId, Now.AddMinutes(-10));
            var store = new FakeBillStore(new BillSettlement(bill, [], []));

            var result = await new CloseBillCommandHandler(store, StaffUser(), new FixedClock(Now))
                .Handle(new CloseBillCommand { BillId = bill.Id, Reason = "Second attempt" }, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal("Staff shift ended", bill.CloseReason);
            Assert.Equal(Now.AddMinutes(-10), bill.ClosedAt);
            Assert.Empty(store.SavedCloses);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("ab")]
        [InlineData("line\nbreak")]
        public void Close_AReasonOutsideTheShapeIsRefusedByTheValidator(string? reason)
        {
            var result = new CloseBillCommandValidator().Validate(new CloseBillCommand
            {
                BillId = Guid.CreateVersion7(),
                Reason = reason,
            });

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == nameof(CloseBillCommand.Reason));
        }

        [Fact]
        public void Close_AReasonOfThreeToTwoHundredFiftySixCharactersIsAccepted()
        {
            var validator = new CloseBillCommandValidator();

            Assert.True(validator.Validate(new CloseBillCommand { BillId = Guid.CreateVersion7(), Reason = "abc" }).IsValid);
            Assert.True(
                validator.Validate(
                    new CloseBillCommand { BillId = Guid.CreateVersion7(), Reason = new string('a', 256) }).IsValid);
            Assert.False(
                validator.Validate(
                    new CloseBillCommand { BillId = Guid.CreateVersion7(), Reason = new string('a', 257) }).IsValid);
            Assert.False(validator.Validate(new CloseBillCommand { BillId = Guid.Empty, Reason = "abc" }).IsValid);
        }

        [Fact]
        public async Task Close_StaffReadOnlyTheirBranchAndAForeignBillIsTheUniformNotFound()
        {
            var bill = OpenBill();
            var store = new FakeBillStore(new BillSettlement(bill, [], []));

            var foreign = await new CloseBillCommandHandler(store, StaffUser(OtherBranchId), new FixedClock(Now))
                .Handle(new CloseBillCommand { BillId = bill.Id, Reason = "Guest left" }, default);

            Assert.False(foreign.IsSuccess);
            Assert.Equal(BillingErrors.NotFoundCode, foreign.Error!.Code);
            Assert.Equal(BillStatus.Open, bill.Status);
            Assert.Empty(store.SavedCloses);

            var owner = await new CloseBillCommandHandler(store, StaffUser(null, isStaff: false), new FixedClock(Now))
                .Handle(new CloseBillCommand { BillId = bill.Id, Reason = "Guest left" }, default);

            Assert.True(owner.IsSuccess, owner.Error?.Code);
            Assert.Null(store.LastBranchId);
        }

        [Fact]
        public async Task Close_TheRemainingAmountIgnoresOrdersThatAreNotAccepted()
        {
            var bill = OpenBill();
            var accepted = AcceptedOrder(bill, 10.00m);
            var pending = PendingOrder(bill, 3.00m);
            var store = new FakeBillStore(new BillSettlement(bill, [accepted, pending], []));

            var result = await new CloseBillCommandHandler(store, StaffUser(), new FixedClock(Now))
                .Handle(new CloseBillCommand { BillId = bill.Id, Reason = "Cash drawer closed" }, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(10.00m, result.Value!.TotalAmount);
            Assert.Equal(10.00m, Assert.Single(store.SavedCloses).RemainingAmount);
        }

        [Fact]
        public void TheOpenAuditSnapshotIsSupportedForBills()
        {
            var bill = OpenBill();

            var snapshot = TenantAuditSnapshotSerializer.Create("bills", TenantAuditChangeSets.BillOpen(bill));

            Assert.True(snapshot.IsSuccess, snapshot.Error?.Code);
            Assert.Contains(bill.PublicReference, snapshot.Value.AfterState!, StringComparison.Ordinal);
        }

        [Fact]
        public void TheCloseAuditSnapshotCarriesTheStatusTheReasonAndTheRemainingAmountOnly()
        {
            var bill = OpenBill();
            bill.Close("Guest left early", UserId, Now);

            var snapshot = TenantAuditSnapshotSerializer.Create(
                "bills",
                TenantAuditChangeSets.BillClose(bill, 10.50m));

            Assert.True(snapshot.IsSuccess, snapshot.Error?.Code);
            var afterState = snapshot.Value.AfterState!;
            Assert.Contains("\"Status\":\"Closed\"", afterState, StringComparison.Ordinal);
            Assert.Contains("Guest left early", afterState, StringComparison.Ordinal);
            Assert.Contains("\"RemainingAmount\":\"10.50\"", afterState, StringComparison.Ordinal);
            Assert.DoesNotContain("device", afterState, StringComparison.OrdinalIgnoreCase);
        }

        private static FakeTenantUser StaffUser(Guid? branchId = null, bool isStaff = true) =>
            new(
                UserId,
                isStaff
                    ? new TenantAccountAccess(true, null, true) { BranchId = branchId ?? BillCloseTests.BranchId }
                    : new TenantAccountAccess(false, null, true));

        private static Bill OpenBill() =>
            Bill.Create(Guid.CreateVersion7(), BranchId, TableId, Reference, Now, UserId);

        private static Order AcceptedOrder(Bill bill, decimal totalAmount)
        {
            var order = PendingOrder(bill, totalAmount);
            order.Accept(UserId, Now);
            return order;
        }

        private static Order PendingOrder(Bill bill, decimal totalAmount)
        {
            var order = Order.Create(
                Guid.CreateVersion7(),
                BranchId,
                TableId,
                1,
                new DateOnly(2026, 11, 23),
                OrderServiceType.DineIn,
                OrderSource.Guest,
                "Sara",
                "device-identifier-0001",
                null,
                Reference,
                $"idempotency-key-{Guid.CreateVersion7():N}",
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
            order.AttachToBill(bill.Id);
            return order;
        }

        private static Payment CashPayment(Bill bill, decimal amount) =>
            Payment.ConfirmCash(
                Guid.CreateVersion7(),
                bill.Id,
                null,
                amount,
                UserId,
                "cash-key-0001",
                Now,
                UserId);

        private sealed class FakeBillStore(BillSettlement? settlement) : IBillStore
        {
            public List<(Bill Bill, decimal RemainingAmount, Guid ActorId)> SavedCloses { get; } = [];

            public Guid? LastBranchId { get; private set; }

            public Task<Result<Bill>> ResolveOpenBillAsync(
                Guid branchId,
                Guid tableId,
                DateTimeOffset now,
                Guid? actorId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The close flow must not open a bill.");

            public Task<BillSettlement?> FindForCloseAsync(
                Guid? branchId,
                Guid billId,
                CancellationToken cancellationToken)
            {
                LastBranchId = branchId;
                if (settlement is null
                    || settlement.Bill.Id != billId
                    || (branchId is { } branch && settlement.Bill.BranchId != branch))
                {
                    return Task.FromResult<BillSettlement?>(null);
                }

                return Task.FromResult<BillSettlement?>(settlement);
            }

            public Task<Result> SaveCloseAsync(
                Bill bill,
                decimal remainingAmount,
                Guid actorId,
                CancellationToken cancellationToken)
            {
                SavedCloses.Add((bill, remainingAmount, actorId));
                return Task.FromResult(Result.Success());
            }
        }

        private sealed class FixedClock(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
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
