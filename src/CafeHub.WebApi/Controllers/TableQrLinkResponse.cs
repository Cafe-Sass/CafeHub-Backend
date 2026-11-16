namespace CafeHub.WebApi.Controllers
{
    public sealed record TableQrLinkResponse(
        Guid TableId,
        int Number,
        string? Label,
        string Path,
        string Url,
        string ConcurrencyToken);
}
