namespace CafeHub.WebApi.Controllers
{
    public sealed record WifiQrLinkResponse(Guid BranchId, string Path, string Url);
}
