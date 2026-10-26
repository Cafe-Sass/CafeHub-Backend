using CafeHub.Application.Notifications;
using CafeHub.Infrastructure.Notifications;
using Microsoft.Extensions.Logging;

namespace CafeHub.UnitTests.Notifications.Infrastructure
{
    public sealed class LoggingNotificationSenderTests
    {
        private sealed class CapturingLogger : ILogger<LoggingNotificationSender>
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                Entries.Add((logLevel, formatter(state, exception)));
            }
        }

        private static readonly Guid TenantId = Guid.CreateVersion7();

        [Fact]
        public async Task SendAsync_SucceedsAndLogsKindAndTenantIdOnly()
        {
            var logger = new CapturingLogger();
            var sender = new LoggingNotificationSender(logger);
            var message = NotificationMessage.TenantReady(
                TenantId,
                "owner@example.com",
                "Cafe Name",
                "my-cafe");

            var result = await sender.SendAsync(message, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Contains(TenantId.ToString(), entry.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(NotificationKind.TenantReady), entry.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SendAsync_LogsNoRecipientEmailOrValues()
        {
            var logger = new CapturingLogger();
            var sender = new LoggingNotificationSender(logger);
            var message = NotificationMessage.TenantReady(
                TenantId,
                "owner@example.com",
                "Cafe Name",
                "my-cafe");

            await sender.SendAsync(message, CancellationToken.None);

            var entry = Assert.Single(logger.Entries);
            Assert.DoesNotContain("owner@example.com", entry.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Cafe Name", entry.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("my-cafe", entry.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SendAsync_RejectsNullMessage()
        {
            var sender = new LoggingNotificationSender(new CapturingLogger());

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => sender.SendAsync(null!, CancellationToken.None));
        }
    }
}
