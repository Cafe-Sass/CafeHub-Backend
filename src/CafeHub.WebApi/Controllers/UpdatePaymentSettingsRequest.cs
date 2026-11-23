namespace CafeHub.WebApi.Controllers
{
    public sealed record UpdatePaymentSettingsRequest(
        bool OnlineEnabled,
        string? MethodLabel,
        string? PaymentLink,
        string? PaymentAddress,
        string? DisplayName,
        string ConcurrencyToken);
}
