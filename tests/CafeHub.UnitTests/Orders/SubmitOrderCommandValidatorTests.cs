using CafeHub.Application.Idempotency;
using CafeHub.Application.Orders;
using CafeHub.Domain.Tenants;
using FluentValidation.Results;

namespace CafeHub.UnitTests.Orders
{
    public sealed class SubmitOrderCommandValidatorTests
    {
        private const string Code = "Abcdefghijklmnopqrstuv";

        private static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid SizeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid AddonId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private readonly SubmitOrderCommandValidator validator = new();

        [Fact]
        public void AShapeValidGuestSubmissionPasses()
        {
            Assert.True(Validate(Valid()).IsValid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("short")]
        [InlineData("bad key with spaces")]
        public void AnInvalidIdempotencyKeyIsRefused(string? key)
        {
            var result = Validate(Valid() with { IdempotencyKey = key });

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.ErrorMessage == IdempotencyErrors.InvalidKeyMessage);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("too-short")]
        [InlineData("Abcdefghijklmnopqrstuv!")]
        public void AnInvalidQrCodeIsRefused(string? code)
        {
            Assert.False(Validate(Valid() with { Code = code }).IsValid);
        }

        [Fact]
        public void ZeroAndFiftyOneLinesAreRefusedWhileFiftyPass()
        {
            Assert.False(Validate(Valid() with { Lines = [] }).IsValid);
            Assert.False(Validate(Valid() with { Lines = Enumerable.Repeat(Line(), 51).ToArray() }).IsValid);
            Assert.True(Validate(Valid() with { Lines = Enumerable.Repeat(Line(), 50).ToArray() }).IsValid);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(51)]
        public void QuantitiesOutsideTheRangeAreRefused(int quantity)
        {
            Assert.False(Validate(Valid() with { Lines = [Line() with { Quantity = quantity }] }).IsValid);
        }

        [Fact]
        public void ElevenAddonsAreRefusedWhileTenPass()
        {
            var eleven = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToArray();

            Assert.False(Validate(Valid() with
            {
                Lines = [Line() with { AddonIds = eleven, ExpectedAddonPrices = eleven.Select(_ => 1m).ToArray() }],
            }).IsValid);

            var ten = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();
            Assert.True(Validate(Valid() with
            {
                Lines = [Line() with { AddonIds = ten, ExpectedAddonPrices = ten.Select(_ => 1m).ToArray() }],
            }).IsValid);
        }

        [Fact]
        public void DuplicateOrEmptyAddonIdsAreRefused()
        {
            Assert.False(Validate(Valid() with
            {
                Lines = [Line() with { AddonIds = [AddonId, AddonId], ExpectedAddonPrices = [1m, 1m] }],
            }).IsValid);

            Assert.False(Validate(Valid() with
            {
                Lines = [Line() with { AddonIds = [Guid.Empty], ExpectedAddonPrices = [1m] }],
            }).IsValid);
        }

        [Fact]
        public void AnAddonWithoutItsExpectedPriceIsRefused()
        {
            Assert.False(Validate(Valid() with
            {
                Lines = [Line() with { AddonIds = [AddonId], ExpectedAddonPrices = [] }],
            }).IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ABlankGuestNameIsRefused(string name)
        {
            Assert.False(Validate(Valid() with { GuestDisplayName = name }).IsValid);
        }

        [Fact]
        public void TheGuestNameLengthBoundaryIsSixtyFourCharacters()
        {
            Assert.True(Validate(Valid() with { GuestDisplayName = new string('a', 64) }).IsValid);
            Assert.False(Validate(Valid() with { GuestDisplayName = new string('a', 65) }).IsValid);
        }

        [Fact]
        public void ControlCharactersAndDigitsInTheGuestNameAreRefused()
        {
            Assert.False(Validate(Valid() with { GuestDisplayName = "Sa\u0007ra" }).IsValid);
            Assert.False(Validate(Valid() with { GuestDisplayName = "Sara 01001234567" }).IsValid);
        }

        [Fact]
        public void AnInvalidGuestDeviceIdIsRefusedAndAWildcardFreeOnePasses()
        {
            Assert.False(Validate(Valid() with { GuestDeviceId = "short" }).IsValid);
            Assert.False(Validate(Valid() with { GuestDeviceId = new string('a', 65) }).IsValid);
            Assert.True(Validate(Valid() with { GuestDeviceId = "device-1234567890_ab" }).IsValid);
            Assert.True(Validate(Valid() with { GuestDeviceId = null }).IsValid);
        }

        [Fact]
        public void NegativeOrOverPreciseExpectedPricesAreRefused()
        {
            Assert.False(Validate(Valid() with { Lines = [Line() with { ExpectedUnitPrice = -1m }] }).IsValid);
            Assert.False(Validate(Valid() with { Lines = [Line() with { ExpectedUnitPrice = 1.005m }] }).IsValid);
            Assert.False(Validate(Valid() with { ExpectedTotal = -1m }).IsValid);
        }

        [Fact]
        public void AStaffActorOnAGuestSubmissionIsRefused()
        {
            Assert.False(Validate(Valid() with { ActingStaffUserId = Guid.NewGuid() }).IsValid);
            Assert.True(Validate(Valid() with
            {
                Source = OrderSource.Captain,
                ActingStaffUserId = Guid.NewGuid(),
            }).IsValid);
        }

        [Fact]
        public void AnUnknownServiceTypeIsRefused()
        {
            Assert.False(Validate(Valid() with { ServiceType = (OrderServiceType)99 }).IsValid);
        }

        private ValidationResult Validate(SubmitOrderCommand command) => validator.Validate(command);

        private static SubmitOrderCommand Valid() => new()
        {
            IdempotencyKey = "idempotency-key-1234",
            Code = Code,
            ServiceType = OrderServiceType.DineIn,
            GuestDisplayName = "Sara",
            Lines = [Line()],
        };

        private static SubmitOrderLine Line() => new(ProductId, SizeId, [AddonId], 2, 10m, [2m]);
    }
}
