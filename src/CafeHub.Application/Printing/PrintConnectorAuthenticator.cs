using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Printing.Contracts;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Printing
{
    /// <summary>
    /// Authenticates a presented connector key. A malformed, unknown, revoked, or mismatched key all return
    /// the same <c>Printers.ConnectorUnauthorized</c> result, so a caller cannot tell the cases apart.
    /// </summary>
    public sealed class PrintConnectorAuthenticator(IPrintConnectorStore store)
    {
        public async Task<Result<PrintConnectorAccess>> AuthenticateAsync(
            string? presentedKey,
            CancellationToken cancellationToken)
        {
            if (!ConnectorKeys.IsWellFormed(presentedKey))
            {
                return Result.Failure<PrintConnectorAccess>(PrintingErrors.ConnectorUnauthorized());
            }

            var presentedHash = ConnectorKeys.Hash(presentedKey!);
            var connector = await store.FindByKeyHashAsync(presentedHash, cancellationToken);
            if (connector is null
                || !ConnectorKeys.Matches(connector.KeyHash, presentedHash)
                || connector.Status != PrintConnectorStatus.Active)
            {
                return Result.Failure<PrintConnectorAccess>(PrintingErrors.ConnectorUnauthorized());
            }

            return Result.Success(connector);
        }
    }
}
