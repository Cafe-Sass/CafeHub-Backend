using CafeHub.Application.Billing;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Billing.Queries;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Billing
{
    public sealed class ReceiptQueryTests
    {
        private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid BranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid OtherBranchId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-24T12:00:00Z");
        private const string Reference = "Abcdefghijklmnopqrstuv";

        [Fact]
        public void TheDetailGroupsTheStoredLinesAndListsEveryPayment()
        {
            var receipt = SampleReceipt();

            var detail = ReceiptDetail.From(receipt);

            Assert.Equal("R-20261124-1", detail.ReceiptNumber);
            Assert.Equal(Now, detail.IssuedAt);
            Assert.Equal(10.00m, detail.TotalAmount);
            Assert.Equal(10.00m, detail.PaidAmount);
            Assert.Equal(0m, detail.RemainingAmount);
            Assert.Equal(new[] { "Omar", "Sara" }, detail.Groups.Select(group => group.GuestDisplayName));
            Assert.All(detail.Groups, group => Assert.Equal(5.00m, group.TotalAmount));
            var sara = detail.Groups[1].Lines[0];
            Assert.Equal("Latte", sara.ProductName);
            Assert.Equal("Regular", sara.SizeName);
            Assert.Equal("Shot", sara.Addons);
            Assert.Equal(2, sara.Quantity);
            Assert.Equal(2.50m, sara.UnitPrice);
            Assert.Equal(5.00m, sara.LineTotal);
            Assert.Equal(new[] { "Cash", "Online" }, detail.Payments.Select(payment => payment.Method));
            Assert.Equal(new[] { 4.00m, 6.00m }, detail.Payments.Select(payment => payment.Amount));
            Assert.DoesNotContain(
                typeof(ReceiptDetail).GetProperties(),
                property => property.Name.Contains("Id", StringComparison.Ordinal));
        }

        [Fact]
        public async Task StaffReadsResolveInsideTheCallersBranchScope()
        {
            var receipt = SampleReceipt();
            var store = new FakeReceiptQueryStore(receipt);
            var handler = new GetReceiptQueryHandler(store, StaffUser());

            var found = await handler.Handle(new GetReceiptQuery(receipt.Id), default);
            var foreign = await handler.Handle(new GetReceiptQuery(Guid.CreateVersion7()), default);
            var byBill = await new GetBillReceiptQueryHandler(store, StaffUser()).Handle(
                new GetBillReceiptQuery(receipt.BillId), default);

            Assert.True(found.IsSuccess, found.Error?.Code);
            Assert.Equal(BranchId, store.LastBranch);
            Assert.False(foreign.IsSuccess);
            Assert.Equal(ReceiptErrors.NotFoundCode, foreign.Error!.Code);
            Assert.True(byBill.IsSuccess, byBill.Error?.Code);
            Assert.Equal(ReceiptErrors.NotFoundCode, (await new GetReceiptQueryHandler(store, StaffUser(OtherBranchId))
                .Handle(new GetReceiptQuery(receipt.Id), default)).Error!.Code);
        }

        [Fact]
        public async Task TheOwnerMayReadAnyBranchAndAnUnscopedCallerIsForbidden()
        {
            var receipt = SampleReceipt();
            var store = new FakeReceiptQueryStore(receipt);

            var owner = await new GetReceiptQueryHandler(store, OwnerUser()).Handle(
                new GetReceiptQuery(receipt.Id), default);
            var noBranch = await new GetReceiptQueryHandler(store, BrokenUser()).Handle(
                new GetReceiptQuery(receipt.Id), default);

            Assert.True(owner.IsSuccess, owner.Error?.Code);
            Assert.Null(store.LastBranch);
            Assert.False(noBranch.IsSuccess);
            Assert.Equal("Identity.Forbidden", noBranch.Error!.Code);
        }

        [Fact]
        public async Task TheGuestReadNeedsAFullyPaidBillWithAValidReference()
        {
            var receipt = SampleReceipt();
            var store = new FakeReceiptQueryStore(receipt);
            var handler = new GetGuestReceiptQueryHandler(store);

            var malformed = await handler.Handle(new GetGuestReceiptQuery("short"), default);
            var unknown = await handler.Handle(new GetGuestReceiptQuery("Zyxwvutsrqponmlkjihgfe"), default);
            var found = await handler.Handle(new GetGuestReceiptQuery(Reference), default);

            Assert.False(malformed.IsSuccess);
            Assert.Equal(ReceiptErrors.NotFoundCode, malformed.Error!.Code);
            Assert.False(unknown.IsSuccess);
            Assert.Equal(ReceiptErrors.NotFoundCode, unknown.Error!.Code);
            Assert.True(found.IsSuccess, found.Error?.Code);
            Assert.Equal("R-20261124-1", found.Value!.ReceiptNumber);
        }

        private static Receipt SampleReceipt()
        {
            var bill = Bill.Create(Guid.CreateVersion7(), BranchId, Guid.CreateVersion7(), Reference, Now, null);
            ReceiptLine[] lines =
            [
                ReceiptLine.Create(
                    Guid.CreateVersion7(), 1, "Sara", "Latte", "Regular", "Shot", 2, 2.50m, 5.00m, Now),
                ReceiptLine.Create(
                    Guid.CreateVersion7(), 2, "Omar", "Tea", "Large", null, 1, 5.00m, 5.00m, Now),
            ];
            ReceiptPayment[] payments =
            [
                ReceiptPayment.Create(
                    Guid.CreateVersion7(), PaymentMethod.Cash, 4.00m, Now.AddMinutes(-2), Now),
                ReceiptPayment.Create(
                    Guid.CreateVersion7(), PaymentMethod.Online, 6.00m, Now.AddMinutes(-1), Now),
            ];

            return Receipt.Issue(
                Guid.CreateVersion7(),
                bill.Id,
                bill.BranchId,
                "R-20261124-1",
                Now,
                10.00m,
                10.00m,
                0m,
                lines,
                payments,
                UserId);
        }

        private static FakeTenantUser StaffUser(Guid? branchId = null) =>
            new(
                UserId,
                new TenantAccountAccess(true, null, true) { BranchId = branchId ?? BranchId },
                TenantId);

        private static FakeTenantUser OwnerUser() =>
            new(UserId, new TenantAccountAccess(false, null, true), TenantId);

        private static FakeTenantUser BrokenUser() =>
            new(UserId, new TenantAccountAccess(true, null, false), TenantId);

        private sealed class FakeTenantUser(Guid? userId, TenantAccountAccess? accountAccess, Guid tenantId)
            : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId => userId;

            public Guid? TenantId => tenantId;

            public IReadOnlyCollection<string> Roles => [];

            public Guid? BranchId => AccountAccess?.BranchId;

            public TenantAccountAccess? AccountAccess { get; } = accountAccess;
        }

        private sealed class FakeReceiptQueryStore(Receipt receipt) : IReceiptQueryStore
        {
            public Guid? LastBranch { get; private set; }

            public Task<Receipt?> FindByIdAsync(
                Guid? branchId,
                Guid receiptId,
                CancellationToken cancellationToken)
            {
                LastBranch = branchId;
                return Task.FromResult<Receipt?>(Matches(receipt, branchId, receiptId));
            }

            public Task<Receipt?> FindByBillAsync(
                Guid? branchId,
                Guid billId,
                CancellationToken cancellationToken)
            {
                LastBranch = branchId;
                return Task.FromResult<Receipt?>(Matches(receipt, branchId, receipt.Id)
                    is { } stored && stored.BillId == billId ? stored : null);
            }

            public Task<Receipt?> FindByBillReferenceAsync(
                string billReference,
                CancellationToken cancellationToken) =>
                Task.FromResult<Receipt?>(billReference == Reference ? receipt : null);

            private static Receipt? Matches(Receipt stored, Guid? branchId, Guid receiptId) =>
                stored.Id == receiptId && (branchId is null || branchId == stored.BranchId)
                    ? stored
                    : null;
        }
    }
}
