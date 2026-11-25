using CafeHub.Application.Printing.Contracts;
using CafeHub.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace CafeHub.Application.Printing
{
    /// <summary>
    /// Turns accepted orders and issued receipts into queued print jobs inside the caller's unit of work.
    /// A missing route or any failure is reported and skipped, so printing never blocks the business event.
    /// </summary>
    public sealed class PrintJobCreator(
        IPrintJobStore store,
        TimeProvider timeProvider,
        ILogger<PrintJobCreator> logger) : IPrintJobQueue
    {
        private const string OrderSourceType = "Order";
        private const string BillSourceType = "Bill";

        public async Task EnqueueOrderTicketAsync(Order order, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(order);

            try
            {
                var stationIds = order.Items
                    .Where(item => item.StationId is not null)
                    .Select(item => item.StationId!.Value)
                    .Distinct()
                    .ToArray();
                if (stationIds.Length == 0)
                {
                    return;
                }

                var routes = await store.FindStationRoutesAsync(
                    order.BranchId,
                    PrintDocumentType.OrderTicket,
                    stationIds,
                    cancellationToken);
                if (routes.Count == 0)
                {
                    logger.LogInformation(
                        "No order ticket print route is configured for branch {BranchId} and order {OrderId}.",
                        order.BranchId,
                        order.Id);
                    return;
                }

                var routesByStation = routes
                    .Where(route => route.StationId is not null)
                    .GroupBy(route => route.StationId!.Value)
                    .ToDictionary(group => group.Key, group => group.First());

                var candidates = order.Items
                    .Where(item => item.StationId is { } stationId && routesByStation.ContainsKey(stationId))
                    .GroupBy(item => routesByStation[item.StationId!.Value].PrinterId)
                    .Select(group => new
                    {
                        Route = routesByStation[group.First().StationId!.Value],
                        Items = group.OrderBy(item => item.LineNumber).ToArray(),
                    })
                    .ToArray();
                if (candidates.Length == 0)
                {
                    logger.LogInformation(
                        "No order ticket print route matches the stations of order {OrderId}.",
                        order.Id);
                    return;
                }

                var dedupeKeys = candidates
                    .Select(candidate => PrintJob.BuildDedupeKey(
                        PrintDocumentType.OrderTicket,
                        order.Id,
                        candidate.Route.PrinterId))
                    .ToArray();
                var existing = await store.FindExistingDedupeKeysAsync(
                    order.BranchId,
                    dedupeKeys,
                    cancellationToken);
                var now = timeProvider.GetUtcNow();

                foreach (var candidate in candidates)
                {
                    var dedupeKey = PrintJob.BuildDedupeKey(
                        PrintDocumentType.OrderTicket,
                        order.Id,
                        candidate.Route.PrinterId);
                    if (existing.Contains(dedupeKey))
                    {
                        continue;
                    }

                    store.Add(PrintJob.Create(
                        Guid.CreateVersion7(),
                        order.BranchId,
                        candidate.Route.PrinterId,
                        PrintDocumentType.OrderTicket,
                        OrderSourceType,
                        order.Id,
                        PrintDocumentComposer.ComposeOrderTicket(order, candidate.Items, candidate.Route.LineWidth),
                        now));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Creating the order ticket print jobs failed for order {OrderId}.",
                    order.Id);
            }
        }

        public async Task EnqueueReceiptAsync(Receipt receipt, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(receipt);

            try
            {
                var route = await store.FindBranchRouteAsync(
                    receipt.BranchId,
                    PrintDocumentType.Receipt,
                    cancellationToken);
                if (route is null)
                {
                    logger.LogInformation(
                        "No receipt print route is configured for branch {BranchId} and bill {BillId}.",
                        receipt.BranchId,
                        receipt.BillId);
                    return;
                }

                var dedupeKey = PrintJob.BuildDedupeKey(
                    PrintDocumentType.Receipt,
                    receipt.BillId,
                    route.PrinterId);
                var existing = await store.FindExistingDedupeKeysAsync(
                    receipt.BranchId,
                    [dedupeKey],
                    cancellationToken);
                if (existing.Contains(dedupeKey))
                {
                    return;
                }

                store.Add(PrintJob.Create(
                    Guid.CreateVersion7(),
                    receipt.BranchId,
                    route.PrinterId,
                    PrintDocumentType.Receipt,
                    BillSourceType,
                    receipt.BillId,
                    PrintDocumentComposer.ComposeReceipt(receipt, route.LineWidth),
                    timeProvider.GetUtcNow()));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Creating the receipt print job failed for bill {BillId}.",
                    receipt.BillId);
            }
        }
    }
}
