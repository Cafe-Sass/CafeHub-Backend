using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Printing.Contracts;
using CafeHub.Application.Printing.DTOs;

namespace CafeHub.Application.Printing.Commands
{
    public sealed class CreatePrintConnectorCommandHandler(
        IPrintConnectorStore store,
        ICurrentTenantUser currentUser)
        : ICommandHandler<CreatePrintConnectorCommand, PrintConnectorCreatedResponse>
    {
        public Task<Result<PrintConnectorCreatedResponse>> Handle(
            CreatePrintConnectorCommand command,
            CancellationToken cancellationToken) =>
            PrinterCommandScope.RunAsync(
                currentUser,
                command.BranchId,
                actorId => CreateAsync(command, actorId, cancellationToken));

        private async Task<Result<PrintConnectorCreatedResponse>> CreateAsync(
            CreatePrintConnectorCommand command,
            Guid actorId,
            CancellationToken cancellationToken)
        {
            var key = ConnectorKeys.Generate();
            var created = await store.CreateAsync(
                command.BranchId,
                command.Name!,
                ConnectorKeys.Hash(key),
                actorId,
                cancellationToken);
            if (created.IsFailure)
            {
                return Result.Failure<PrintConnectorCreatedResponse>(created.Error!);
            }

            var connector = created.Value;
            return Result.Success(new PrintConnectorCreatedResponse(
                connector.Id,
                connector.BranchId,
                connector.Name,
                connector.Status,
                connector.CreatedAt,
                key));
        }
    }

    public sealed class RevokePrintConnectorCommandHandler(
        IPrintConnectorStore store,
        ICurrentTenantUser currentUser)
        : ICommandHandler<RevokePrintConnectorCommand, PrintConnectorResponse>
    {
        public Task<Result<PrintConnectorResponse>> Handle(
            RevokePrintConnectorCommand command,
            CancellationToken cancellationToken) =>
            PrinterCommandScope.RunAsync(
                currentUser,
                command.BranchId,
                actorId => store.RevokeAsync(
                    command.ConnectorId,
                    command.BranchId,
                    actorId,
                    cancellationToken));
    }
}
