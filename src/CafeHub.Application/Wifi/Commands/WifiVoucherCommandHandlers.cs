using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.Commands
{
    public sealed class ImportWifiVouchersCommandHandler(
        IWifiVoucherStore vouchers,
        IWifiPackageStore packages,
        ICurrentTenantUser currentUser,
        TimeProvider timeProvider)
        : ICommandHandler<ImportWifiVouchersCommand, WifiVoucherImportResult>
    {
        public async Task<Result<WifiVoucherImportResult>> Handle(
            ImportWifiVouchersCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<WifiVoucherImportResult>(AuthErrors.Forbidden());
            }

            var package = await packages.GetAsync(command.PackageId, cancellationToken);
            if (package is null)
            {
                return Result.Failure<WifiVoucherImportResult>(WifiErrors.PackageNotFound());
            }

            var batch = WifiVoucherBatch.Parse(command.Codes);
            var existing = await vouchers.FindExistingCodesAsync(
                command.PackageId,
                batch.ValidCodes,
                cancellationToken);
            var addedCodes = batch.ValidCodes
                .Where(code => !existing.Contains(code))
                .ToArray();
            var counts = new WifiVoucherImportResult(
                addedCodes.Length,
                batch.DuplicateCodes + (batch.ValidCodes.Count - addedCodes.Length),
                batch.InvalidCodes);
            var now = timeProvider.GetUtcNow();
            var rows = addedCodes
                .Select(code => WifiVoucher.Create(
                    Guid.CreateVersion7(),
                    command.PackageId,
                    code,
                    now,
                    actorId))
                .ToArray();

            var saved = await vouchers.InsertImportedAsync(
                command.PackageId,
                rows,
                counts,
                actorId,
                cancellationToken);
            return saved.IsFailure
                ? Result.Failure<WifiVoucherImportResult>(saved.Error!)
                : Result.Success(counts);
        }
    }

    public sealed class VoidWifiVoucherCommandHandler(
        IWifiVoucherStore vouchers,
        IWifiPackageStore packages,
        ICurrentTenantUser currentUser)
        : ICommandHandler<VoidWifiVoucherCommand>
    {
        public async Task<Result> Handle(
            VoidWifiVoucherCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure(AuthErrors.Forbidden());
            }

            var package = await packages.GetAsync(command.PackageId, cancellationToken);
            if (package is null)
            {
                return Result.Failure(WifiErrors.PackageNotFound());
            }

            return await vouchers.VoidAsync(
                command.PackageId,
                command.Code!.Trim(),
                actorId,
                cancellationToken);
        }
    }
}
