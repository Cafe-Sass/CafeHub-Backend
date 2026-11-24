namespace CafeHub.Application.Billing.DTOs
{
    /// <summary>
    /// The guest-facing proof response: only the payment id, its status, and the claimed amount.
    /// The note, the media key, and the image never leave this use case.
    /// </summary>
    public sealed record SubmitPaymentProofResult(Guid PaymentId, string Status, decimal ClaimedAmount);
}
