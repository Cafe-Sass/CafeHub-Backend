namespace CafeHub.WebApi.Controllers
{
    public sealed record SubmitPaymentProofRequest(
        IFormFile? Image,
        decimal ClaimedAmount,
        string? Note);
}
