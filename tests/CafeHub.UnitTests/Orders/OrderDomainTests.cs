using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Orders
{
    public sealed class OrderDomainTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
        private static readonly Guid Actor = Guid.NewGuid();
        private const string Reference = "Abcdefghijklmnopqrstuv";
        private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [Fact]
        public void StatusAndServiceTypeAreClosedSetsSupportedByTheSchema()
        {
            Assert.Equal(
                new[] { "Pending", "Accepted", "Rejected", "Expired" },
                Enum.GetNames<OrderStatus>());
            Assert.Equal(new[] { "DineIn", "Takeaway" }, Enum.GetNames<OrderServiceType>());
            Assert.Equal(new[] { "Guest", "Captain" }, Enum.GetNames<OrderSource>());
        }

        [Fact]
        public void Create_ComputesTheTotalFromTheSizesAndAddOns()
        {
            var order = CreateOrder();

            Assert.Equal(OrderStatus.Pending, order.Status);
            Assert.Equal(OrderSource.Guest, order.Source);
            Assert.Equal(2, order.Items.Count);
            Assert.Equal(22m, order.Items[0].LineTotal);
            Assert.Equal(22m, order.Items[1].LineTotal);
            Assert.Equal(44m, order.TotalAmount);
            Assert.True(order.TotalAmount >= 0m);
        }

        [Theory]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("Sara", true)]
        public void GuestDisplayName_Boundaries(string name, bool expected)
        {
            Assert.Equal(expected, Order.IsValidGuestDisplayName(name));
        }

        [Fact]
        public void GuestDisplayName_LengthAndControlCharactersAreRefused()
        {
            Assert.True(Order.IsValidGuestDisplayName(new string('a', Order.MaximumGuestDisplayNameLength)));
            Assert.False(Order.IsValidGuestDisplayName(new string('a', Order.MaximumGuestDisplayNameLength + 1)));
            Assert.False(Order.IsValidGuestDisplayName("Sa\u0007ra"));
            Assert.Throws<ArgumentException>(() => CreateOrder(guestName: new string('a', 65)));
            Assert.Throws<ArgumentException>(() => CreateOrder(guestName: "Sa\u0007ra"));
        }

        [Fact]
        public void GuestDeviceId_IsOptionalAndBounded()
        {
            Assert.True(Order.IsValidGuestDeviceId(null) == false);
            Assert.True(Order.IsValidGuestDeviceId(new string('a', 16)));
            Assert.True(Order.IsValidGuestDeviceId(new string('a', 64)));
            Assert.False(Order.IsValidGuestDeviceId(new string('a', 15)));
            Assert.False(Order.IsValidGuestDeviceId(new string('a', 65)));
            Assert.False(Order.IsValidGuestDeviceId("device with spaces"));
            Assert.Null(CreateOrder(guestDeviceId: null).GuestDeviceId);
            Assert.Throws<ArgumentException>(() => CreateOrder(guestDeviceId: "short"));
        }

        [Fact]
        public void PublicReference_IsTwentyTwoBase64UrlCharacters()
        {
            Assert.True(Order.IsValidPublicReference(Reference));
            Assert.False(Order.IsValidPublicReference(new string('a', 21)));
            Assert.False(Order.IsValidPublicReference("Abcdefghijklmnopqrstu+"));
            Assert.Throws<ArgumentException>(() => CreateOrder(publicReference: "short"));
        }

        [Fact]
        public void IdempotencyKeyAndRequestHash_AreBoundedAndHexOnly()
        {
            Assert.True(Order.IsValidIdempotencyKey(new string('k', 128)));
            Assert.False(Order.IsValidIdempotencyKey(new string('k', 129)));
            Assert.False(Order.IsValidIdempotencyKey(" "));
            Assert.True(Order.IsValidRequestHash(null));
            Assert.True(Order.IsValidRequestHash(Hash));
            Assert.False(Order.IsValidRequestHash(Hash.ToUpperInvariant()));
            Assert.False(Order.IsValidRequestHash("abc"));
        }

        [Fact]
        public void Create_RefusesClosedSetAndBoundaryViolations()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateOrder(orderNumber: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateOrder(serviceType: (OrderServiceType)9));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateOrder(source: (OrderSource)9));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateOrder(deadline: Now));
        }

        [Fact]
        public void Create_RefusesAnOrderLineLimitsAndDuplicateLines()
        {
            Assert.Throws<ArgumentException>(
                () => CreateOrder(items: Array.Empty<OrderItem>()));
            Assert.Throws<ArgumentException>(
                () => CreateOrder(items: Enumerable.Range(1, Order.MaximumItemCount + 1)
                    .Select(line => CreateItem(line)).ToArray()));
            Assert.Equal(
                Order.MaximumItemCount,
                CreateOrder(items: Enumerable.Range(1, Order.MaximumItemCount)
                    .Select(line => CreateItem(line)).ToArray()).Items.Count);

            var duplicateLines = new[] { CreateItem(1), CreateItem(1) };
            Assert.Throws<ArgumentException>(() => CreateOrder(items: duplicateLines));
        }

        [Fact]
        public void GuestOrder_CannotNameThePlacingUser()
        {
            Assert.Throws<ArgumentException>(() => CreateOrder(placedByUserId: Guid.NewGuid()));
            var captainOrder = CreateOrder(source: OrderSource.Captain, placedByUserId: Actor);
            Assert.Equal(Actor, captainOrder.PlacedByUserId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(51)]
        public void OrderItem_QuantityBoundariesAreRefused(int quantity)
        {
            Assert.False(OrderItem.IsValidQuantity(quantity));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateItem(1, quantity: quantity));
        }

        [Fact]
        public void OrderItem_RefusesNegativePricesAndTooManyAddons()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateItem(1, unitPrice: -1m));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateItem(1, unitPrice: 1.005m));
            Assert.Throws<ArgumentException>(() => CreateItem(
                1,
                addons: Enumerable.Range(1, OrderItem.MaximumAddonCount + 1)
                    .Select(_ => CreateAddon()).ToArray()));
            Assert.Equal(
                OrderItem.MaximumAddonCount,
                CreateItem(1, addons: Enumerable.Range(1, OrderItem.MaximumAddonCount)
                    .Select(_ => CreateAddon()).ToArray()).Addons.Count);
        }

        [Fact]
        public void OrderItem_RefusesInvalidSnapshotLabels()
        {
            Assert.True(OrderItem.IsValidProductName(new string('a', OrderItem.MaximumNameLength)));
            Assert.False(OrderItem.IsValidProductName(new string('a', OrderItem.MaximumNameLength + 1)));
            Assert.False(OrderItem.IsValidProductName("Latte\u0001"));
            Assert.True(OrderItem.IsValidSizeName(new string('a', OrderItem.MaximumSizeNameLength)));
            Assert.False(OrderItem.IsValidSizeName(new string('a', OrderItem.MaximumSizeNameLength + 1)));
            Assert.Throws<ArgumentException>(() => CreateItem(1, productName: " "));
        }

        [Fact]
        public void OrderItemAddon_RefusesInvalidPricesAndNames()
        {
            Assert.True(OrderItemAddon.IsValidPrice(0m));
            Assert.False(OrderItemAddon.IsValidPrice(-0.01m));
            Assert.True(OrderItemAddon.IsValidName("Milk"));
            Assert.False(OrderItemAddon.IsValidName(new string('a', 49)));
            Assert.Throws<ArgumentOutOfRangeException>(() => OrderItemAddon.Create(
                Guid.NewGuid(), null, "Milk", -1m, Now, null));
            Assert.Throws<ArgumentException>(() => OrderItemAddon.Create(
                Guid.NewGuid(), null, " ", 1m, Now, null));
        }

        private static Order CreateOrder(
            int orderNumber = 1,
            OrderServiceType serviceType = OrderServiceType.DineIn,
            OrderSource source = OrderSource.Guest,
            string guestName = "Sara",
            string? guestDeviceId = "device-identifier-0001",
            Guid? placedByUserId = null,
            string publicReference = Reference,
            DateTimeOffset? deadline = null,
            IReadOnlyCollection<OrderItem>? items = null)
        {
            return Order.Create(
                Guid.CreateVersion7(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                orderNumber,
                new DateOnly(2026, 11, 22),
                serviceType,
                source,
                guestName,
                guestDeviceId,
                placedByUserId,
                publicReference,
                "idempotency-key",
                Hash,
                Now,
                deadline ?? Now.AddMinutes(5),
                items ?? new[] { CreateItem(1), CreateItem(2) },
                source == OrderSource.Captain ? Actor : null);
        }

        private static OrderItem CreateItem(
            int lineNumber,
            decimal unitPrice = 10m,
            int quantity = 2,
            string productName = "Latte",
            IReadOnlyCollection<OrderItemAddon>? addons = null)
        {
            return OrderItem.Create(
                Guid.CreateVersion7(),
                lineNumber,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                productName,
                "Regular",
                unitPrice,
                quantity,
                addons ?? new[] { CreateAddon() },
                Now,
                null);
        }

        private static OrderItemAddon CreateAddon()
        {
            return OrderItemAddon.Create(Guid.CreateVersion7(), Guid.NewGuid(), "Milk", 2m, Now, null);
        }
    }
}
