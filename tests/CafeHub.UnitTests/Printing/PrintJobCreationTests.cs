using System.Text.Json;
using CafeHub.Application.Printing;
using CafeHub.Application.Printing.Contracts;
using CafeHub.Domain.Tenants;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeHub.UnitTests.Printing
{
    public sealed class PrintJobCreationTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid StationOne = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid StationTwo = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid PrinterOne = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly Guid PrinterTwo = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly Guid BillId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-25T09:00:00Z");

        [Fact]
        public async Task OrderTicket_CreatesOneJobPerRoutedStationPrinter()
        {
            var order = OrderWithItems(Item(StationOne, 1, "Latte"), Item(StationTwo, 2, "Croissant"));
            var store = new FakePrintJobStore
            {
                StationRoutes =
                [
                    new PrintRouteTarget(PrinterOne, StationOne, 48),
                    new PrintRouteTarget(PrinterTwo, StationTwo, 32),
                ],
            };

            await Creator(store).EnqueueOrderTicketAsync(order, default);

            Assert.Equal(2, store.Added.Count);
            Assert.Equal(
                [PrinterOne, PrinterTwo],
                store.Added.Select(job => job.PrinterId).OrderBy(id => id).ToArray());
            Assert.All(store.Added, job =>
            {
                Assert.Equal(BranchId, job.BranchId);
                Assert.Equal(PrintDocumentType.OrderTicket, job.DocumentType);
                Assert.Equal("Order", job.SourceType);
                Assert.Equal(order.Id, job.SourceId);
                Assert.Equal(PrintJobStatus.Queued, job.Status);
                Assert.Equal(0, job.Attempts);
            });
        }

        [Fact]
        public async Task OrderTicket_SplitsItemsAcrossStations()
        {
            var order = OrderWithItems(
                Item(StationOne, 1, "Latte"),
                Item(StationOne, 2, "Mocha"),
                Item(StationTwo, 3, "Croissant"));
            var store = new FakePrintJobStore
            {
                StationRoutes =
                [
                    new PrintRouteTarget(PrinterOne, StationOne, 48),
                    new PrintRouteTarget(PrinterTwo, StationTwo, 48),
                ],
            };

            await Creator(store).EnqueueOrderTicketAsync(order, default);

            var first = Payload(store.Added.Single(job => job.PrinterId == PrinterOne));
            var second = Payload(store.Added.Single(job => job.PrinterId == PrinterTwo));
            Assert.Contains(Lines(first), line => line.Contains("Latte", StringComparison.Ordinal));
            Assert.Contains(Lines(first), line => line.Contains("Mocha", StringComparison.Ordinal));
            Assert.DoesNotContain(Lines(first), line => line.Contains("Croissant", StringComparison.Ordinal));
            Assert.Contains(Lines(second), line => line.Contains("Croissant", StringComparison.Ordinal));
            Assert.DoesNotContain(Lines(second), line => line.Contains("Latte", StringComparison.Ordinal));
        }

        [Fact]
        public async Task OrderTicket_WithoutARouteCreatesNoJob()
        {
            var order = OrderWithItems(Item(StationOne, 1, "Latte"));
            var store = new FakePrintJobStore();

            await Creator(store).EnqueueOrderTicketAsync(order, default);

            Assert.Empty(store.Added);
        }

        [Fact]
        public async Task OrderTicket_ARepeatedEventCreatesNoSecondJob()
        {
            var order = OrderWithItems(Item(StationOne, 1, "Latte"));
            var dedupeKey = PrintJob.BuildDedupeKey(PrintDocumentType.OrderTicket, order.Id, PrinterOne);
            var store = new FakePrintJobStore
            {
                StationRoutes = [new PrintRouteTarget(PrinterOne, StationOne, 48)],
                ExistingDedupeKeys = new HashSet<string>(StringComparer.Ordinal) { dedupeKey },
            };

            await Creator(store).EnqueueOrderTicketAsync(order, default);

            Assert.Empty(store.Added);
        }

        [Fact]
        public async Task Receipt_CreatesOneBranchWideJob()
        {
            var receipt = ReceiptWithLine();
            var store = new FakePrintJobStore
            {
                BranchRoute = new PrintRouteTarget(PrinterOne, null, 48),
            };

            await Creator(store).EnqueueReceiptAsync(receipt, default);

            var job = Assert.Single(store.Added);
            Assert.Equal(PrinterOne, job.PrinterId);
            Assert.Equal(PrintDocumentType.Receipt, job.DocumentType);
            Assert.Equal("Bill", job.SourceType);
            Assert.Equal(BillId, job.SourceId);
            Assert.Equal(
                PrintJob.BuildDedupeKey(PrintDocumentType.Receipt, BillId, PrinterOne),
                job.DedupeKey);
        }

        [Fact]
        public async Task Receipt_ARepeatedEventCreatesNoSecondJob()
        {
            var receipt = ReceiptWithLine();
            var store = new FakePrintJobStore
            {
                BranchRoute = new PrintRouteTarget(PrinterOne, null, 48),
                ExistingDedupeKeys = new HashSet<string>(StringComparer.Ordinal)
                {
                    PrintJob.BuildDedupeKey(PrintDocumentType.Receipt, BillId, PrinterOne),
                },
            };

            await Creator(store).EnqueueReceiptAsync(receipt, default);

            Assert.Empty(store.Added);
        }

        [Fact]
        public async Task Receipt_PayloadExcludesWifiCodesAndPaymentProof()
        {
            var receipt = ReceiptWithLine(wifiCode: "WIFI-CODE-1234");
            var store = new FakePrintJobStore
            {
                BranchRoute = new PrintRouteTarget(PrinterOne, null, 48),
            };

            await Creator(store).EnqueueReceiptAsync(receipt, default);

            var payload = Assert.Single(store.Added).DocumentPayload;
            Assert.DoesNotContain("WIFI-CODE-1234", payload, StringComparison.Ordinal);
            Assert.DoesNotContain("Proof", payload, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Cash", payload, StringComparison.Ordinal);
        }

        [Fact]
        public void Payload_TruncatesLongProductNamesWithAMarker()
        {
            var order = OrderWithItems(Item(StationOne, 1, new string('L', OrderItem.MaximumNameLength)));

            var payload = PrintDocumentComposer.ComposeOrderTicket(order, order.Items, 32);

            var itemLine = Lines(Payload(payload))
                .Single(line => line.StartsWith("1x ", StringComparison.Ordinal));
            Assert.Equal(32, itemLine.Length);
            Assert.EndsWith("...", itemLine, StringComparison.Ordinal);
        }

        private static PrintJobCreator Creator(FakePrintJobStore store) =>
            new(store, new FixedClock(Now), NullLogger<PrintJobCreator>.Instance);

        private static PrintDocumentPayload Payload(PrintJob job) =>
            Payload(job.DocumentPayload);

        private static PrintDocumentPayload Payload(string payload) =>
            JsonSerializer.Deserialize<PrintDocumentPayload>(payload)
            ?? throw new InvalidOperationException("The payload did not deserialize.");

        private static string[] Lines(PrintDocumentPayload payload) =>
            payload.Lines.Select(line => line.Text).ToArray();

        private static Order OrderWithItems(params OrderItem[] items) =>
            Order.Create(
                Guid.CreateVersion7(),
                BranchId,
                Guid.Parse("77777777-7777-7777-7777-777777777777"),
                7,
                new DateOnly(2026, 11, 25),
                OrderServiceType.DineIn,
                OrderSource.Guest,
                "Sara",
                "device-identifier-0001",
                null,
                "Abcdefghijklmnopqrstuv",
                "idempotency-key",
                null,
                Now.AddMinutes(-5),
                Now.AddMinutes(5),
                items,
                null);

        private static OrderItem Item(Guid? stationId, int lineNumber, string productName) =>
            OrderItem.Create(
                Guid.CreateVersion7(),
                lineNumber,
                Guid.NewGuid(),
                Guid.NewGuid(),
                stationId,
                productName,
                "Regular",
                4.50m,
                1,
                [],
                Now.AddMinutes(-5),
                null);

        private static Receipt ReceiptWithLine(string? wifiCode = null) =>
            Receipt.Issue(
                Guid.CreateVersion7(),
                BillId,
                BranchId,
                "R-20261125-1",
                Now,
                4.50m,
                4.50m,
                0m,
                [
                    ReceiptLine.Create(
                        Guid.CreateVersion7(),
                        7,
                        "Sara",
                        "Latte",
                        "Regular",
                        null,
                        1,
                        4.50m,
                        4.50m,
                        Now,
                        wifiCode),
                ],
                [ReceiptPayment.Create(Guid.CreateVersion7(), PaymentMethod.Cash, 4.50m, Now, Now)],
                null);

        private sealed class FakePrintJobStore : IPrintJobStore
        {
            public IReadOnlyList<PrintRouteTarget> StationRoutes { get; init; } = [];

            public PrintRouteTarget? BranchRoute { get; init; }

            public IReadOnlySet<string> ExistingDedupeKeys { get; init; } =
                new HashSet<string>(StringComparer.Ordinal);

            public List<PrintJob> Added { get; } = [];

            public Task<IReadOnlyList<PrintRouteTarget>> FindStationRoutesAsync(
                Guid branchId,
                PrintDocumentType documentType,
                IReadOnlyCollection<Guid> stationIds,
                CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<PrintRouteTarget>>(
                    StationRoutes
                        .Where(route => route.StationId is { } stationId && stationIds.Contains(stationId))
                        .ToArray());

            public Task<PrintRouteTarget?> FindBranchRouteAsync(
                Guid branchId,
                PrintDocumentType documentType,
                CancellationToken cancellationToken) =>
                Task.FromResult(BranchRoute);

            public Task<IReadOnlySet<string>> FindExistingDedupeKeysAsync(
                Guid branchId,
                IReadOnlyCollection<string> dedupeKeys,
                CancellationToken cancellationToken) =>
                Task.FromResult(ExistingDedupeKeys);

            public void Add(PrintJob job) => Added.Add(job);
        }

        private sealed class FixedClock(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }
    }
}
