using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Printing.DTOs;

namespace CafeHub.Application.Printing.Commands
{
    public sealed record CreatePrintConnectorCommand(
        Guid BranchId,
        string? Name) : ICommand<PrintConnectorCreatedResponse>, ITenantTransactionalCommand;

    public sealed record RevokePrintConnectorCommand(
        Guid ConnectorId,
        Guid BranchId) : ICommand<PrintConnectorResponse>, ITenantTransactionalCommand;
}
