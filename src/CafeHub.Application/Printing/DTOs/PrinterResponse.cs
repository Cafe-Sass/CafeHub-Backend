using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Printing.DTOs
{
    public sealed record PrinterResponse(
        Guid Id,
        Guid BranchId,
        Guid? StationId,
        string Name,
        string Profile,
        string ConnectionKind,
        string Status,
        string ConcurrencyToken)
    {
        public static PrinterResponse From(Printer printer, string concurrencyToken) =>
            new(
                printer.Id,
                printer.BranchId,
                printer.StationId,
                printer.Name,
                printer.Profile.ToString(),
                printer.ConnectionKind,
                printer.Status.ToString(),
                concurrencyToken);
    }

    public sealed record PrintRouteResponse(
        Guid Id,
        Guid BranchId,
        Guid? StationId,
        string DocumentType,
        Guid PrinterId)
    {
        public static PrintRouteResponse From(PrintRoute route) =>
            new(
                route.Id,
                route.BranchId,
                route.StationId,
                route.DocumentType.ToString(),
                route.PrinterId);
    }
}
