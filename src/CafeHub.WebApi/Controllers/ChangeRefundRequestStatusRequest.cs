namespace CafeHub.WebApi.Controllers
{
    public sealed record ChangeRefundRequestStatusRequest(string? Status, string? Note);
}
