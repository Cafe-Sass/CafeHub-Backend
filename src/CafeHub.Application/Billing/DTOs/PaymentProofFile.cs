namespace CafeHub.Application.Billing.DTOs
{
    public sealed record PaymentProofFile(Stream Content, string ContentType);
}
