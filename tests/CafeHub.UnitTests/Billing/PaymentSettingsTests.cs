using System.Text.Json;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing;
using CafeHub.Application.Billing.Commands;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Billing.Queries;
using CafeHub.Application.Identity;
using CafeHub.Application.Tables;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;

namespace CafeHub.UnitTests.Billing
{
    public sealed class PaymentSettingsTests
    {
        private static readonly Guid ActorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-23T22:00:00Z");

        [Fact]
        public void Update_EnablingWithoutLinkOrAddressIsRefused()
        {
            var settings = CafePaymentSettings.Create(Guid.CreateVersion7(), Now);

            Assert.Throws<ArgumentException>(
                () => settings.Update(true, null, null, null, null, Now, ActorId));

            Assert.Throws<ArgumentException>(
                () => settings.Update(true, null, "   ", "", null, Now, ActorId));
        }

        [Fact]
        public void Update_EnablingWithEitherDestinationIsAccepted()
        {
            var byLink = CafePaymentSettings.Create(Guid.CreateVersion7(), Now);
            byLink.Update(true, null, "https://ipay.example.com/cafe", null, null, Now, ActorId);
            Assert.True(byLink.OnlineEnabled);

            var byAddress = CafePaymentSettings.Create(Guid.CreateVersion7(), Now);
            byAddress.Update(true, null, null, "01012345678@instapay", null, Now, ActorId);
            Assert.True(byAddress.OnlineEnabled);
        }

        [Theory]
        [InlineData("http://ipay.example.com/cafe")]
        [InlineData("ftp://ipay.example.com/cafe")]
        [InlineData("ipay.example.com/cafe")]
        [InlineData("//ipay.example.com/cafe")]
        public void Update_APaymentLinkOutsideTheHttpsShapeIsRefused(string paymentLink)
        {
            var settings = CafePaymentSettings.Create(Guid.CreateVersion7(), Now);

            Assert.Throws<ArgumentException>(
                () => settings.Update(true, null, paymentLink, null, null, Now, ActorId));
        }

        [Fact]
        public void Update_AnAbsoluteHttpsLinkUpToFiveHundredTwelveCharactersIsAccepted()
        {
            var settings = CafePaymentSettings.Create(Guid.CreateVersion7(), Now);
            var longest = "https://ipay.example.com/pay/" + new string('a', 483);

            settings.Update(true, null, longest, null, null, Now, ActorId);

            Assert.Equal(longest, settings.PaymentLink);
            Assert.Throws<ArgumentException>(
                () => settings.Update(true, null, longest + "a", null, null, Now, ActorId));
        }

        [Theory]
        [InlineData("short")]
        [InlineData("tab\tinside")]
        public void Update_TheMethodLabelIsFreeTextUpToThirtyTwoCharacters(string methodLabel)
        {
            var settings = CafePaymentSettings.Create(Guid.CreateVersion7(), Now);
            var valid = methodLabel.Trim();

            if (valid.Length is >= 1 and <= CafePaymentSettings.MaximumMethodLabelLength
                && !valid.Any(char.IsControl))
            {
                settings.Update(false, methodLabel, null, null, null, Now, ActorId);
                Assert.Equal(valid, settings.MethodLabel);
            }
            else
            {
                Assert.Throws<ArgumentException>(
                    () => settings.Update(false, methodLabel, null, null, null, Now, ActorId));
            }
        }

        [Fact]
        public void Update_TheMethodLabelDefaultsToInstaPay()
        {
            var settings = CafePaymentSettings.Create(Guid.CreateVersion7(), Now);

            settings.Update(true, null, "https://ipay.example.com/cafe", null, null, Now, ActorId);

            Assert.Equal(CafePaymentSettings.DefaultMethodLabel, settings.MethodLabel);
        }

        [Fact]
        public void Update_ControlCharactersAndOverLengthDestinationsAreRefused()
        {
            var settings = CafePaymentSettings.Create(Guid.CreateVersion7(), Now);

            Assert.Throws<ArgumentException>(
                () => settings.Update(true, null, null, "address\nwith\nnewline", null, Now, ActorId));
            Assert.Throws<ArgumentException>(
                () => settings.Update(true, null, null, new string('a', 129), null, Now, ActorId));
            Assert.Throws<ArgumentException>(
                () => settings.Update(true, null, null, null, "name\u0007bell", Now, ActorId));
            Assert.Throws<ArgumentException>(
                () => settings.Update(true, null, null, null, new string('a', 129), Now, ActorId));
        }

        [Fact]
        public void Validator_EnablingWithoutLinkOrAddressIsRefusedAndShapesAreEnforced()
        {
            var validator = new UpdatePaymentSettingsCommandValidator();

            Assert.False(validator.Validate(new UpdatePaymentSettingsCommand(
                true, null, null, null, null, "1")).IsValid);
            Assert.False(validator.Validate(new UpdatePaymentSettingsCommand(
                true, null, "http://ipay.example.com/cafe", null, null, "1")).IsValid);
            Assert.False(validator.Validate(new UpdatePaymentSettingsCommand(
                true, null, null, new string('a', 129), null, "1")).IsValid);
            Assert.False(validator.Validate(new UpdatePaymentSettingsCommand(
                true, new string('a', 33), null, "address@instapay", null, "1")).IsValid);
            Assert.True(validator.Validate(new UpdatePaymentSettingsCommand(
                true, "InstaPay", "https://ipay.example.com/cafe", null, "Cafe Payment", "1")).IsValid);
            Assert.True(validator.Validate(new UpdatePaymentSettingsCommand(
                false, null, null, null, null, "1")).IsValid);
        }

        [Fact]
        public async Task Update_TheHandlerRequiresTheAuthenticatedUserAndForwardsToTheWriter()
        {
            var writer = new FakeWriter { Response = Result.Success(SampleResponse("7")) };

            var failure = await new UpdatePaymentSettingsCommandHandler(writer, AnonymousUser())
                .Handle(new UpdatePaymentSettingsCommand(true, null, null, "address@instapay", null, "1"), default);
            Assert.False(failure.IsSuccess);
            Assert.Equal("Identity.Forbidden", failure.Error!.Code);

            var success = await new UpdatePaymentSettingsCommandHandler(writer, StaffUser())
                .Handle(new UpdatePaymentSettingsCommand(true, "InstaPay", "https://ipay.example.com/cafe", null, null, "1"), default);

            Assert.True(success.IsSuccess, success.Error?.Code);
            var command = Assert.Single(writer.Received);
            Assert.True(command.OnlineEnabled);
            Assert.Equal("https://ipay.example.com/cafe", command.PaymentLink);
            Assert.Equal(ActorId, command.ActorUserId);
        }

        [Fact]
        public async Task Update_AStaleTokenConflictPassesThroughUnchanged()
        {
            var writer = new FakeWriter
            {
                Response = Result.Failure<PaymentSettingsResponse>(PaymentSettingsErrors.ConcurrencyConflict()),
            };

            var result = await new UpdatePaymentSettingsCommandHandler(writer, StaffUser())
                .Handle(new UpdatePaymentSettingsCommand(true, null, null, "address@instapay", null, "5"), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(PaymentSettingsErrors.ConcurrencyConflictCode, result.Error!.Code);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);
        }

        [Fact]
        public void TheUpdateAuditSnapshotHoldsTheChangedFieldsAndNeverSensitiveData()
        {
            var settings = CafePaymentSettings.Create(Guid.CreateVersion7(), Now);
            settings.Update(false, "InstaPay", null, "01012345678@instapay", "Cafe", Now, ActorId);

            var changes = TenantAuditChangeSets.PaymentSettingsUpdate(
                settings,
                true,
                "InstaPay",
                "https://ipay.example.com/cafe",
                "01012345678@instapay",
                null);

            Assert.Contains(changes.Fields, field => field.Name == nameof(CafePaymentSettings.OnlineEnabled)
                && field.Before == "False" && field.After == "True");
            Assert.Contains(changes.Fields, field => field.Name == nameof(CafePaymentSettings.PaymentLink)
                && field.Before is null && field.After == "https://ipay.example.com/cafe");

            var snapshot = TenantAuditSnapshotSerializer.Create("cafe_payment_settings", changes);

            Assert.True(snapshot.IsSuccess, snapshot.Error?.Code);
            var afterState = snapshot.Value.AfterState!;
            Assert.Contains("https://ipay.example.com/cafe", afterState, StringComparison.Ordinal);
            Assert.DoesNotContain("token", afterState, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", afterState, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GuestOptions_AnInvalidCodeNeverReachesTheStoreAndIsTheUniformNotFound()
        {
            var store = new FakeReader();

            foreach (var reference in new[] { null, "", "short", "Abcdefghijklmnopqrstu+" })
            {
                var result = await new GetGuestPaymentOptionsQueryHandler(store, ActiveTenantContext())
                    .Handle(new GetGuestPaymentOptionsQuery(reference), default);

                Assert.False(result.IsSuccess);
                Assert.Equal(GuestQrErrors.NotFoundCode, result.Error!.Code);
            }

            Assert.Equal(0, store.OptionReads);
        }

        [Fact]
        public async Task GuestOptions_AnUnknownCodeIsTheUniformNotFound()
        {
            var store = new FakeReader();
            var handler = new GetGuestPaymentOptionsQueryHandler(store, ActiveTenantContext());

            var result = await handler.Handle(new GetGuestPaymentOptionsQuery("Abcdefghijklmnopqrstuv"), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(GuestQrErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(1, store.OptionReads);
        }

        [Fact]
        public async Task GuestOptions_TheResponseCarriesTheOptionFieldsAndNeverAuditData()
        {
            var store = new FakeReader
            {
                Options = new GuestPaymentOptionsResponse(
                    true,
                    "InstaPay",
                    "https://ipay.example.com/cafe",
                    "01012345678@instapay",
                    "Cafe Payment"),
            };
            var handler = new GetGuestPaymentOptionsQueryHandler(store, ActiveTenantContext());

            var result = await handler.Handle(new GetGuestPaymentOptionsQuery("Abcdefghijklmnopqrstuv"), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.True(result.Value!.OnlineEnabled);
            Assert.Equal("InstaPay", result.Value.MethodLabel);
            Assert.Equal("https://ipay.example.com/cafe", result.Value.PaymentLink);
            Assert.Equal("01012345678@instapay", result.Value.PaymentAddress);
            Assert.Equal("Cafe Payment", result.Value.DisplayName);

            var json = JsonSerializer.Serialize(result.Value);
            Assert.DoesNotContain("audit", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("concurrency", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("actor", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GuestOptions_AnInactiveTenantContextIsTheUniformNotFound()
        {
            var store = new FakeReader();

            var result = await new GetGuestPaymentOptionsQueryHandler(store, InactiveTenantContext())
                .Handle(new GetGuestPaymentOptionsQuery("Abcdefghijklmnopqrstuv"), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(GuestQrErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(0, store.OptionReads);
        }

        private static PaymentSettingsResponse SampleResponse(string token) =>
            new(true, "InstaPay", "https://ipay.example.com/cafe", null, null, token);

        private static FakeCurrentTenantUser StaffUser() => new(ActorId, true);

        private static FakeCurrentTenantUser AnonymousUser() => new(null, false);

        private static FakeTenantContextAccessor ActiveTenantContext() =>
            new(TenantContext.Create(Guid.NewGuid(), "secret-reference", true));

        private static FakeTenantContextAccessor InactiveTenantContext() =>
            new(TenantContext.Create(Guid.NewGuid(), "secret-reference", false));

        private sealed class FakeWriter : ICafePaymentSettingsWriter
        {
            public List<(bool OnlineEnabled, string? MethodLabel, string? PaymentLink, string? PaymentAddress, string? DisplayName, Guid ActorUserId, string ConcurrencyToken)> Received { get; } = [];

            public Result<PaymentSettingsResponse> Response { get; set; } =
                Result.Failure<PaymentSettingsResponse>(PaymentSettingsErrors.NotFound());

            public Task<Result<PaymentSettingsResponse>> UpdateAsync(
                bool onlineEnabled,
                string? methodLabel,
                string? paymentLink,
                string? paymentAddress,
                string? displayName,
                Guid actorUserId,
                string concurrencyToken,
                CancellationToken cancellationToken)
            {
                Received.Add((onlineEnabled, methodLabel, paymentLink, paymentAddress, displayName, actorUserId, concurrencyToken));
                return Task.FromResult(Response);
            }
        }

        private sealed class FakeReader : ICafePaymentSettingsReader
        {
            public GuestPaymentOptionsResponse? Options { get; set; }

            public int OptionReads { get; private set; }

            public Task<PaymentSettingsResponse?> GetAsync(CancellationToken cancellationToken) =>
                throw new NotSupportedException("The guest flow must not read the settings response.");

            public Task<GuestPaymentOptionsResponse?> FindOptionsByQrCodeAsync(
                string qrCode,
                CancellationToken cancellationToken)
            {
                OptionReads++;
                return Task.FromResult(Options);
            }
        }

        private sealed class FakeCurrentTenantUser(Guid? userId, bool isAuthenticated) : ICurrentTenantUser
        {
            public bool IsAuthenticated => isAuthenticated;

            public Guid? UserId => userId;

            public Guid? TenantId => Guid.NewGuid();

            public IReadOnlyCollection<string> Roles => [];

            public Guid? BranchId => null;

            public TenantAccountAccess? AccountAccess => null;
        }

        private sealed class FakeTenantContextAccessor(TenantContext context) : ITenantContextAccessor
        {
            public TenantContext GetRequired() => context;
        }
    }
}
