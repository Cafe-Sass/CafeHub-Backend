using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Billing
{
    public sealed class BillTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid TableId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-23T10:00:00Z");
        private const string Reference = "Abcdefghijklmnopqrstuv";

        [Fact]
        public void Create_AValidBillOpensWithTheReferenceAndAuditTimes()
        {
            var actor = Guid.CreateVersion7();

            var bill = Bill.Create(Guid.CreateVersion7(), BranchId, TableId, Reference, Now, actor);

            Assert.Equal(BillStatus.Open, bill.Status);
            Assert.Equal(BranchId, bill.BranchId);
            Assert.Equal(TableId, bill.TableId);
            Assert.Equal(Reference, bill.PublicReference);
            Assert.Equal(Now, bill.OpenedAt);
            Assert.Equal(Now, bill.CreatedAt);
            Assert.Equal(Now, bill.UpdatedAt);
            Assert.Equal(actor, bill.CreatedBy);
            Assert.Equal(actor, bill.UpdatedBy);
            Assert.Null(bill.ClosedAt);
            Assert.Null(bill.ClosedByUserId);
            Assert.Null(bill.CloseReason);
        }

        [Fact]
        public void Create_RejectsAReferenceThatIsNot22Base64UrlCharacters()
        {
            Assert.Throws<ArgumentException>(
                () => Bill.Create(Guid.CreateVersion7(), BranchId, TableId, "short", Now, null));
            Assert.Throws<ArgumentException>(
                () => Bill.Create(Guid.CreateVersion7(), BranchId, TableId, "Abcdefghijklmnopqrstu+", Now, null));
            Assert.Throws<ArgumentException>(
                () => Bill.Create(Guid.Empty, BranchId, TableId, Reference, Now, null));
            Assert.Throws<ArgumentException>(
                () => Bill.Create(Guid.CreateVersion7(), Guid.Empty, TableId, Reference, Now, null));
            Assert.Throws<ArgumentException>(
                () => Bill.Create(Guid.CreateVersion7(), BranchId, Guid.Empty, Reference, Now, null));
        }

        [Fact]
        public void TheBillTotalReconcilesToTheSumOfAcceptedOrderTotals()
        {
            var acceptedOne = PendingOrder(10.00m);
            var acceptedTwo = PendingOrder(4.50m);
            acceptedOne.Accept(Guid.CreateVersion7(), Now);
            acceptedTwo.Accept(Guid.CreateVersion7(), Now);

            var total = Bill.ResolveTotalAmount([acceptedOne, acceptedTwo]);

            Assert.Equal(14.50m, total);
        }

        [Fact]
        public void TheBillTotalIgnoresOrdersThatAreNotAccepted()
        {
            var accepted = PendingOrder(10.00m);
            accepted.Accept(Guid.CreateVersion7(), Now);
            var pending = PendingOrder(5.00m);
            var rejected = PendingOrder(3.00m);
            rejected.Reject(Guid.CreateVersion7(), "Out of oat milk", Now);
            var expired = PendingOrder(2.00m);
            expired.Expire(Now.AddMinutes(10));

            var total = Bill.ResolveTotalAmount([accepted, pending, rejected, expired]);

            Assert.Equal(10.00m, total);
        }

        private static Order PendingOrder(decimal total) =>
            PendingOrder(
                total,
                OrderItem.Create(
                    Guid.CreateVersion7(),
                    1,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    "Latte",
                    "Regular",
                    total,
                    1,
                    [],
                    Now.AddMinutes(-5),
                    null));

        private static Order PendingOrder(decimal total, OrderItem item) =>
            Order.Create(
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
                "idempotency-key",
                null,
                Now.AddMinutes(-5),
                Now.AddMinutes(5),
                [item],
                null);
    }
}
