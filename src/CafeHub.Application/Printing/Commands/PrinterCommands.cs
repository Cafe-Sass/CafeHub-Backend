using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Printing.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Printing.Commands
{
    public sealed record RegisterPrinterCommand(
        Guid BranchId,
        Guid? StationId,
        string? Name,
        PrinterProfile Profile,
        string? ConnectionKind) : ICommand<PrinterResponse>, ITenantTransactionalCommand;

    public sealed record UpdatePrinterCommand(
        Guid PrinterId,
        Guid BranchId,
        Guid? StationId,
        string? Name,
        PrinterProfile Profile,
        string? ConnectionKind,
        string ConcurrencyToken) : ICommand<PrinterResponse>, ITenantTransactionalCommand;

    public sealed record SetPrinterStatusCommand(
        Guid PrinterId,
        Guid BranchId,
        PrinterStatus Status,
        string ConcurrencyToken) : ICommand<PrinterResponse>, ITenantTransactionalCommand;

    public sealed record SetPrintRouteCommand(
        Guid BranchId,
        Guid? StationId,
        PrintDocumentType DocumentType,
        Guid PrinterId) : ICommand<PrintRouteResponse>, ITenantTransactionalCommand;
}
