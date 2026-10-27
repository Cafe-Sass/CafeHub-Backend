using System.Reflection;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Onboarding.Commands;
using CafeHub.Application.Onboarding.PaymentGateway;
using CafeHub.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests
{
    public sealed class PaymentIdempotencyApiTests
    {
        private const string ValidKey = "demo-key-1234";

        [Fact]
        public async Task Pay_PassesHeaderValueToTheActualCommand()
        {
            var requestId = Guid.CreateVersion7();
            var sender = new RecordingSender
            {
                Response = Result.Success(new PaymentOutcomeResponse(
                    requestId,
                    CafeHub.Domain.Onboarding.OnboardingRequestStatus.AwaitingPayment,
                    CafeHub.Domain.Subscriptions.PlatformPaymentTransactionStatus.Initiated)),
            };
            var controller = new OnboardingRequestsController(sender);

            await controller.Pay(requestId, ValidKey, CancellationToken.None);

            var command = Assert.IsType<PayOnboardingRequestCommand>(sender.LastRequest);
            Assert.Equal(requestId, command.RequestId);
            Assert.Equal(ValidKey, command.IdempotencyKey);
        }

        [Fact]
        public void Pay_DeclaresRequiredIdempotencyHeaderForBindingAndOpenApi()
        {
            var action = typeof(OnboardingRequestsController).GetMethod(nameof(OnboardingRequestsController.Pay));
            Assert.NotNull(action);
            var parameter = action!.GetParameters().Single(item => item.Name == "idempotencyKey");

            var header = parameter.GetCustomAttribute<FromHeaderAttribute>();
            Assert.NotNull(header);
            Assert.Equal("Idempotency-Key", header!.Name);
            Assert.NotNull(parameter.GetCustomAttribute<BindRequiredAttribute>());
        }

        [Fact]
        public void ApiExplorer_DescribesPaymentIdempotencyHeaderAsRequired()
        {
            var services = new ServiceCollection()
                .AddLogging()
                .AddControllers()
                .AddApplicationPart(typeof(OnboardingRequestsController).Assembly)
                .Services;
            using var provider = services.BuildServiceProvider();
            var paymentDescription = provider
                .GetRequiredService<IApiDescriptionGroupCollectionProvider>()
                .ApiDescriptionGroups.Items
                .SelectMany(group => group.Items)
                .Single(description => description.HttpMethod == "POST"
                    && description.RelativePath is not null
                    && description.RelativePath.EndsWith("/{id}/payments", StringComparison.Ordinal));
            var idempotencyHeader = Assert.Single(paymentDescription.ParameterDescriptions,
                parameter => parameter.Name == "Idempotency-Key");

            Assert.Equal(BindingSource.Header, idempotencyHeader.Source);
            Assert.True(idempotencyHeader.IsRequired);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("short", false)]
        [InlineData("contains spaces", false)]
        [InlineData(ValidKey, true)]
        public void PaymentCommandValidator_RequiresAValidIdempotencyKey(string? key, bool expectedValid)
        {
            var command = new PayOnboardingRequestCommand(Guid.CreateVersion7())
            {
                IdempotencyKey = key,
            };

            var result = new PayOnboardingRequestCommandValidator().Validate(command);

            Assert.Equal(expectedValid, result.IsValid);
        }

        [Fact]
        public async Task Pay_RepeatedHeaderValue_IsPassedUnchangedOnEachAttempt()
        {
            var requestId = Guid.CreateVersion7();
            var sender = new RecordingSender
            {
                Response = Result.Success(new PaymentOutcomeResponse(
                    requestId,
                    CafeHub.Domain.Onboarding.OnboardingRequestStatus.AwaitingPayment,
                    CafeHub.Domain.Subscriptions.PlatformPaymentTransactionStatus.Initiated)),
            };
            var controller = new OnboardingRequestsController(sender);

            await controller.Pay(requestId, ValidKey, CancellationToken.None);
            var first = Assert.IsType<PayOnboardingRequestCommand>(sender.LastRequest);
            await controller.Pay(requestId, ValidKey, CancellationToken.None);
            var second = Assert.IsType<PayOnboardingRequestCommand>(sender.LastRequest);

            Assert.Equal(first.IdempotencyKey, second.IdempotencyKey);
            Assert.Equal(ValidKey, second.IdempotencyKey);
        }
    }
}
