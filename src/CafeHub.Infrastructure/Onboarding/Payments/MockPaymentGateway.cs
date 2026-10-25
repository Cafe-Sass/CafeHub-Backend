using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Onboarding.PaymentGateway;

namespace CafeHub.Infrastructure.Onboarding.Payments
{
    public sealed class MockPaymentGateway : IPaymentGateway
    {
        private readonly MockPaymentOptions _options;

        public MockPaymentGateway(MockPaymentOptions options)
        {
            _options = options;
        }

        public Task<Result<PaymentChargeResult>> ChargeAsync(PaymentChargeRequest request, CancellationToken cancellationToken)
        {
            if (string.Equals(_options.Outcome, "Decline", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(
                    Result.Success<PaymentChargeResult>(
                        new PaymentChargeResult(
                            PaymentOutcome.Declined,
                            $"mock_{Guid.CreateVersion7()}",
                            "insufficient_funds")));
            }

            return Task.FromResult(
                Result.Success<PaymentChargeResult>(
                    new PaymentChargeResult(
                        PaymentOutcome.Succeeded,
                        $"mock_{Guid.CreateVersion7()}",
                        null)));
        }
    }
}
