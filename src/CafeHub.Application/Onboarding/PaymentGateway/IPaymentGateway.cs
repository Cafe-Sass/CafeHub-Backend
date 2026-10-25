using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Onboarding.PaymentGateway
{
    public enum PaymentOutcome
    {
        Succeeded,
        Declined,
    }

    public sealed record PaymentChargeRequest(
        decimal Amount,
        string Currency,
        string IdempotencyKey,
        string Reference);

    public sealed record PaymentChargeResult(
        PaymentOutcome Outcome,
        string ProviderReference,
        string? FailureCode);

    public interface IPaymentGateway
    {
        Task<Result<PaymentChargeResult>> ChargeAsync(PaymentChargeRequest request, CancellationToken cancellationToken);
    }
}
