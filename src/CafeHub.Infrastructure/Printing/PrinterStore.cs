using System.Globalization;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Printing;
using CafeHub.Application.Printing.Contracts;
using CafeHub.Application.Printing.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Printing
{
    internal sealed class PrinterStore(
        TenantDbContext context,
        ITenantAuditWriter auditWriter,
        TimeProvider timeProvider) : IPrinterStore
    {
        private const string PrinterTargetType = "printers";
        private const string RouteTargetType = "print_routes";
        private const string RouteIndex = "uq_print_routes_branch_station_document";

        public async Task<Result<PrinterResponse>> RegisterAsync(
            Guid branchId,
            Guid? stationId,
            string name,
            PrinterProfile profile,
            string connectionKind,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            if (!await BranchExistsAsync(branchId, cancellationToken))
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.NotFound());
            }

            if (!await StationInBranchAsync(stationId, branchId, cancellationToken))
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.InvalidStation());
            }

            var now = timeProvider.GetUtcNow();
            var printer = Printer.Create(
                Guid.CreateVersion7(),
                branchId,
                stationId,
                name,
                profile,
                connectionKind,
                now,
                actorUserId);
            context.Printers.Add(printer);

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "printers.created.succeeded",
                TargetType = PrinterTargetType,
                TargetId = printer.Id,
                ActorUserId = actorUserId,
                Changes = TenantAuditChanges.Create(new[]
                {
                    new TenantAuditChanges.FieldChange(nameof(Printer.Name), null, printer.Name),
                    new TenantAuditChanges.FieldChange(
                        nameof(Printer.StationId), null, printer.StationId?.ToString()),
                    new TenantAuditChanges.FieldChange(
                        nameof(Printer.Profile), null, printer.Profile.ToString()),
                    new TenantAuditChanges.FieldChange(
                        nameof(Printer.ConnectionKind), null, printer.ConnectionKind),
                    new TenantAuditChanges.FieldChange(
                        nameof(Printer.Status), null, printer.Status.ToString()),
                }),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<PrinterResponse>(auditResult.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success(ToResponse(printer));
        }

        public async Task<Result<PrinterResponse>> UpdateAsync(
            Guid printerId,
            Guid branchId,
            Guid? stationId,
            string name,
            PrinterProfile profile,
            string connectionKind,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var printer = await LoadInBranchAsync(printerId, branchId, cancellationToken);
            if (printer is null)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.NotFound());
            }

            if (!TryParseVersion(concurrencyToken, out var version))
            {
                return Result.Failure<PrinterResponse>(InvalidToken());
            }

            if (GetVersion(printer) != version)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.ConcurrencyConflict());
            }

            if (!await StationInBranchAsync(stationId, branchId, cancellationToken))
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.InvalidStation());
            }

            var old = new
            {
                printer.Name,
                printer.StationId,
                printer.Profile,
                printer.ConnectionKind,
            };
            if (!printer.Update(
                    stationId,
                    name,
                    profile,
                    connectionKind,
                    timeProvider.GetUtcNow(),
                    actorUserId))
            {
                return Result.Success(ToResponse(printer, version));
            }

            context.Entry(printer).Property<uint>("xmin").OriginalValue = version;
            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "printers.updated.succeeded",
                TargetType = PrinterTargetType,
                TargetId = printer.Id,
                ActorUserId = actorUserId,
                Changes = TenantAuditChanges.Update(new[]
                    {
                        new TenantAuditChanges.FieldChange(nameof(Printer.Name), old.Name, printer.Name),
                        new TenantAuditChanges.FieldChange(
                            nameof(Printer.StationId), old.StationId?.ToString(), printer.StationId?.ToString()),
                        new TenantAuditChanges.FieldChange(
                            nameof(Printer.Profile), old.Profile.ToString(), printer.Profile.ToString()),
                        new TenantAuditChanges.FieldChange(
                            nameof(Printer.ConnectionKind), old.ConnectionKind, printer.ConnectionKind),
                    }
                    .Where(field => field.HasChanged)),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<PrinterResponse>(auditResult.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.ConcurrencyConflict());
            }

            return Result.Success(ToResponse(printer));
        }

        public async Task<Result<PrinterResponse>> SetStatusAsync(
            Guid printerId,
            Guid branchId,
            PrinterStatus status,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var printer = await LoadInBranchAsync(printerId, branchId, cancellationToken);
            if (printer is null)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.NotFound());
            }

            if (!TryParseVersion(concurrencyToken, out var version))
            {
                return Result.Failure<PrinterResponse>(InvalidToken());
            }

            if (GetVersion(printer) != version)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.ConcurrencyConflict());
            }

            var previous = printer.Status;
            var changed = status == PrinterStatus.Active
                ? printer.Reactivate(timeProvider.GetUtcNow(), actorUserId)
                : printer.Deactivate(timeProvider.GetUtcNow(), actorUserId);
            if (!changed)
            {
                return Result.Success(ToResponse(printer, version));
            }

            context.Entry(printer).Property<uint>("xmin").OriginalValue = version;
            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = status == PrinterStatus.Active
                    ? "printers.reactivated.succeeded"
                    : "printers.deactivated.succeeded",
                TargetType = PrinterTargetType,
                TargetId = printer.Id,
                ActorUserId = actorUserId,
                Changes = TenantAuditChanges.Update(new[]
                {
                    new TenantAuditChanges.FieldChange(
                        nameof(Printer.Status), previous.ToString(), printer.Status.ToString()),
                }),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<PrinterResponse>(auditResult.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.ConcurrencyConflict());
            }

            return Result.Success(ToResponse(printer));
        }

        public async Task<PrinterResponse?> FindPrinterAsync(
            Guid printerId,
            Guid branchId,
            CancellationToken cancellationToken)
        {
            var printer = await context.Printers.AsNoTracking()
                .FirstOrDefaultAsync(
                    candidate => candidate.Id == printerId && candidate.BranchId == branchId,
                    cancellationToken);
            if (printer is null)
            {
                return null;
            }

            var version = await context.Printers.AsNoTracking()
                .Where(candidate => candidate.Id == printerId)
                .Select(candidate => EF.Property<uint>(candidate, "xmin"))
                .SingleAsync(cancellationToken);
            return ToResponse(printer, version);
        }

        public async Task<IReadOnlyList<PrinterResponse>> ListAsync(
            Guid branchId,
            CancellationToken cancellationToken)
        {
            var rows = await context.Printers.AsNoTracking()
                .Where(printer => printer.BranchId == branchId)
                .OrderBy(printer => printer.Name)
                .ThenBy(printer => printer.Id)
                .Select(printer => new { Printer = printer, Version = EF.Property<uint>(printer, "xmin") })
                .ToListAsync(cancellationToken);

            return rows
                .Select(row => ToResponse(row.Printer, row.Version))
                .ToArray();
        }

        public async Task<Result<PrintRouteResponse>> SetRouteAsync(
            Guid branchId,
            Guid? stationId,
            PrintDocumentType documentType,
            Guid printerId,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            if (!await BranchExistsAsync(branchId, cancellationToken))
            {
                return Result.Failure<PrintRouteResponse>(PrintingErrors.NotFound());
            }

            if (!await StationInBranchAsync(stationId, branchId, cancellationToken))
            {
                return Result.Failure<PrintRouteResponse>(PrintingErrors.InvalidStation());
            }

            var printerInBranch = await context.Printers.AsNoTracking()
                .AnyAsync(
                    printer => printer.Id == printerId
                        && printer.BranchId == branchId
                        && printer.Status == PrinterStatus.Active,
                    cancellationToken);
            if (!printerInBranch)
            {
                return Result.Failure<PrintRouteResponse>(PrintingErrors.PrinterInactive());
            }

            var route = await context.PrintRoutes
                .FirstOrDefaultAsync(
                    candidate => candidate.BranchId == branchId
                        && candidate.StationId == stationId
                        && candidate.DocumentType == documentType,
                    cancellationToken);
            var now = timeProvider.GetUtcNow();
            if (route is null)
            {
                route = PrintRoute.Create(
                    Guid.CreateVersion7(),
                    branchId,
                    stationId,
                    documentType,
                    printerId,
                    now,
                    actorUserId);
                context.PrintRoutes.Add(route);
                var created = await auditWriter.WriteAsync(new TenantAuditEntry
                {
                    Action = "print_routes.set.succeeded",
                    TargetType = RouteTargetType,
                    TargetId = route.Id,
                    ActorUserId = actorUserId,
                    Changes = TenantAuditChanges.Create(new[]
                    {
                        new TenantAuditChanges.FieldChange(
                            nameof(PrintRoute.StationId), null, route.StationId?.ToString()),
                        new TenantAuditChanges.FieldChange(
                            nameof(PrintRoute.DocumentType), null, route.DocumentType.ToString()),
                        new TenantAuditChanges.FieldChange(
                            nameof(PrintRoute.PrinterId), null, route.PrinterId.ToString()),
                    }),
                }, cancellationToken);
                if (created.IsFailure)
                {
                    return Result.Failure<PrintRouteResponse>(created.Error!);
                }

                try
                {
                    await context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException exception) when (IsRouteConflict(exception))
                {
                    return Result.Failure<PrintRouteResponse>(PrintingErrors.RouteConflict());
                }

                return Result.Success(PrintRouteResponse.From(route));
            }

            var previousPrinterId = route.PrinterId;
            if (!route.UpdatePrinter(printerId, now, actorUserId))
            {
                return Result.Success(PrintRouteResponse.From(route));
            }

            var updated = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "print_routes.set.succeeded",
                TargetType = RouteTargetType,
                TargetId = route.Id,
                ActorUserId = actorUserId,
                Changes = TenantAuditChanges.Update(new[]
                {
                    new TenantAuditChanges.FieldChange(
                        nameof(PrintRoute.PrinterId),
                        previousPrinterId.ToString(),
                        route.PrinterId.ToString()),
                }),
            }, cancellationToken);
            if (updated.IsFailure)
            {
                return Result.Failure<PrintRouteResponse>(updated.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success(PrintRouteResponse.From(route));
        }

        public async Task<IReadOnlyList<PrintRouteResponse>> ListRoutesAsync(
            Guid branchId,
            CancellationToken cancellationToken) =>
            await context.PrintRoutes.AsNoTracking()
                .Where(route => route.BranchId == branchId)
                .OrderBy(route => route.DocumentType)
                .ThenBy(route => route.StationId)
                .Select(route => new PrintRouteResponse(
                    route.Id,
                    route.BranchId,
                    route.StationId,
                    route.DocumentType.ToString(),
                    route.PrinterId))
                .ToListAsync(cancellationToken);

        private async Task<Printer?> LoadInBranchAsync(
            Guid printerId,
            Guid branchId,
            CancellationToken cancellationToken) =>
            await context.Printers.FirstOrDefaultAsync(
                printer => printer.Id == printerId && printer.BranchId == branchId,
                cancellationToken);

        private Task<bool> BranchExistsAsync(Guid branchId, CancellationToken cancellationToken) =>
            context.Branches.AsNoTracking().AnyAsync(branch => branch.Id == branchId, cancellationToken);

        private async Task<bool> StationInBranchAsync(
            Guid? stationId,
            Guid branchId,
            CancellationToken cancellationToken) =>
            stationId is null
            || await context.Stations.AsNoTracking().AnyAsync(
                station => station.Id == stationId && station.BranchId == branchId,
                cancellationToken);

        private PrinterResponse ToResponse(Printer printer, uint? version = null)
        {
            var token = version ?? GetVersion(printer);
            return PrinterResponse.From(printer, token.ToString(CultureInfo.InvariantCulture));
        }

        private uint GetVersion(Printer printer) =>
            context.Entry(printer).Property<uint>("xmin").CurrentValue;

        private static bool TryParseVersion(string? value, out uint version) =>
            uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out version);

        private static Error InvalidToken() =>
            ValidationError.Create(
                [new FieldError("concurrencyToken", "The concurrency token is not a valid unsigned integer.")]);

        private static bool IsRouteConflict(DbUpdateException exception) =>
            exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName == RouteIndex;
    }
}
