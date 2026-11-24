namespace CafeHub.WebApi.Controllers
{
    public sealed record RegisterPrinterRequest(
        string? Name,
        Guid? StationId,
        string? Profile,
        string? ConnectionKind);

    public sealed record UpdatePrinterRequest(
        string? Name,
        Guid? StationId,
        string? Profile,
        string? ConnectionKind,
        string? ConcurrencyToken);

    public sealed record PrinterLifecycleRequest(string? ConcurrencyToken);

    public sealed record SetPrintRouteRequest(
        Guid? StationId,
        string? DocumentType,
        Guid PrinterId);
}
