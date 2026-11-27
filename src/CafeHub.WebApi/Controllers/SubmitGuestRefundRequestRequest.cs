using Microsoft.AspNetCore.Http;

namespace CafeHub.WebApi.Controllers
{
    public sealed record SubmitGuestRefundRequestRequest(
        Guid PaymentId,
        decimal ClaimedAmount,
        string? Reason,
        string? Details,
        string? GuestDisplayName,
        IFormFile[]? Evidence);
}
