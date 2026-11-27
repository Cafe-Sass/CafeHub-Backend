namespace CafeHub.Application.Refunds.Commands
{
    public sealed record SubmitGuestRefundRequestResult(string PublicReference, string Status);
}
