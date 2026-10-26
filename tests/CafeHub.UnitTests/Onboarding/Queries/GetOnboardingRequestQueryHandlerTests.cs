using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Onboarding;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Onboarding.DTOs;
using CafeHub.Application.Onboarding.Queries;
using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Subscriptions;
using CafeHub.UnitTests.Onboarding.Application;
using CafeHub.UnitTests.Plans;

namespace CafeHub.UnitTests.Onboarding.Queries
{
    public sealed class GetOnboardingRequestQueryHandlerTests
    {
        private sealed class FakePaymentTransactionStore : IPaymentTransactionStore
        {
            public int FindByOnboardingRequestIdCallCount { get; private set; }

            public Task<PlatformPaymentTransaction?> FindByIdempotencyKeyAsync(Guid onboardingRequestId, string idempotencyKey, CancellationToken cancellationToken)
            {
                return Task.FromResult<PlatformPaymentTransaction?>(null);
            }

            public Task<PlatformPaymentTransaction?> FindByOnboardingRequestIdAsync(Guid onboardingRequestId, CancellationToken cancellationToken)
            {
                FindByOnboardingRequestIdCallCount++;
                return Task.FromResult<PlatformPaymentTransaction?>(null);
            }

            public Task<bool> KeyExistsAsync(string idempotencyKey, CancellationToken cancellationToken)
            {
                return Task.FromResult(false);
            }

            public Task AddAsync(PlatformPaymentTransaction transaction, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task Handle_UnknownRequest_ReturnsRequestNotFound()
        {
            var store = new FakeOnboardingRequestStore();
            var planRepository = new FakePlanRepository();
            var paymentStore = new FakePaymentTransactionStore();
            var handler = new GetOnboardingRequestQueryHandler(store, planRepository, paymentStore);

            var result = await handler.Handle(new GetOnboardingRequestQuery(Guid.CreateVersion7()), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(OnboardingErrors.RequestNotFoundCode, result.Error!.Code);
            Assert.Equal(ErrorType.NotFound, result.Error.Type);
            Assert.Equal(0, planRepository.FindVersionByIdCallCount);
            Assert.Equal(0, planRepository.FindByIdCallCount);
            Assert.Equal(0, paymentStore.FindByOnboardingRequestIdCallCount);
        }
    }
}
