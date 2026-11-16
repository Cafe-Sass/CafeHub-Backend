using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Tables.DTOs;

namespace CafeHub.Application.Tables.Commands
{
    public sealed record RegenerateTableQrCommand(Guid BranchId, Guid TableId, string ConcurrencyToken)
        : ICommand<TableQrData>, ITenantTransactionalCommand, ISensitiveRequest;
}
