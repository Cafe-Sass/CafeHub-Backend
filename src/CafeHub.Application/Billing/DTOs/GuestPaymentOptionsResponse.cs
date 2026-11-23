namespace CafeHub.Application.Billing.DTOs
{
    /// <summary>
    /// The guest-facing payment options for one table QR: what the cafe accepts online and what the
    /// frontend turns into a payment QR. It carries no audit data, no identifiers beyond the QR's cafe,
    /// and never explains whether settings exist.
    /// </summary>
    public sealed record GuestPaymentOptionsResponse(
        bool OnlineEnabled,
        string MethodLabel,
        string? PaymentLink,
        string? PaymentAddress,
        string? DisplayName);
}
