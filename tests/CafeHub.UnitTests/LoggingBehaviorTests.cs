using CafeHub.Application;
using CafeHub.Application.Abstractions.Behaviors;
using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CafeHub.UnitTests
{
    public sealed class LoggingBehaviorTests
    {
        [Fact]
        public async Task Handle_Success_LogsOneInformationEntry_WithRequestNameAndDuration()
        {
            var logger = new FakeLogger<LoggingBehavior<TestPingCommand, Result>>();
            var behavior = new LoggingBehavior<TestPingCommand, Result>(logger);
            RequestHandlerDelegate<Result> next = (_) => Task.FromResult(Result.Success());

            var result = await behavior.Handle(new TestPingCommand(), next, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Null(entry.Exception);
            Assert.Contains("TestPingCommand", entry.Message);
            var values = StructuredState(entry.State);
            Assert.Equal("TestPingCommand", values.Single(value => value.Key == "RequestName").Value);
            Assert.True(RecordedDuration(values) >= TimeSpan.Zero);
        }

        [Fact]
        public async Task Handle_FailedResult_LogsOneWarningEntry_WithRequestNameAndErrorCodeAndErrorType()
        {
            var expectedError = Error.Conflict("Test.Conflict", "The continuation returned a conflict.");
            var logger = new FakeLogger<LoggingBehavior<TestPingCommand, Result>>();
            var behavior = new LoggingBehavior<TestPingCommand, Result>(logger);
            RequestHandlerDelegate<Result> next = (_) => Task.FromResult(Result.Failure(expectedError));

            var result = await behavior.Handle(new TestPingCommand(), next, CancellationToken.None);

            Assert.True(result.IsFailure);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Null(entry.Exception);
            var values = StructuredState(entry.State);
            Assert.Equal("TestPingCommand", values.Single(value => value.Key == "RequestName").Value);
            Assert.Equal("Test.Conflict", values.Single(value => value.Key == "ErrorCode").Value);
            Assert.Equal(ErrorType.Conflict, values.Single(value => value.Key == "ErrorType").Value);
            Assert.True(RecordedDuration(values) >= TimeSpan.Zero);
            Assert.Contains("Test.Conflict", entry.Message);
        }

        [Fact]
        public async Task Handle_ThrownException_LogsSingleErrorEntry_AndRethrowsSameInstance()
        {
            var expectedException = new TestPipelineException("The pipeline continuation threw.");
            var logger = new FakeLogger<LoggingBehavior<TestPingCommand, Result>>();
            var behavior = new LoggingBehavior<TestPingCommand, Result>(logger);
            RequestHandlerDelegate<Result> next = (_) => throw expectedException;

            var caughtException = await Assert.ThrowsAsync<TestPipelineException>(() => behavior.Handle(new TestPingCommand(), next, CancellationToken.None));

            Assert.Same(expectedException, caughtException);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Same(expectedException, entry.Exception);
            Assert.Contains("TestPingCommand", entry.Message);
            Assert.Contains(nameof(TestPipelineException), entry.Message);
        }

        [Fact]
        public async Task Handle_NeverLogsRequestPayload_OrSecretValues()
        {
            var accessToken = "sk-cafehub-e2f1a9c3b7d5";
            var logger = new FakeLogger<LoggingBehavior<TestSecretCommand, Result>>();
            var behavior = new LoggingBehavior<TestSecretCommand, Result>(logger);
            RequestHandlerDelegate<Result> succeedingNext = (_) => Task.FromResult(Result.Success());
            RequestHandlerDelegate<Result> failingNext = (_) => Task.FromResult(Result.Failure(Error.Conflict("Test.Conflict", "The continuation returned a conflict.")));

            await behavior.Handle(new TestSecretCommand(accessToken), succeedingNext, CancellationToken.None);
            await behavior.Handle(new TestSecretCommand(accessToken), failingNext, CancellationToken.None);

            Assert.Equal(2, logger.Entries.Count);
            foreach (var entry in logger.Entries)
            {
                Assert.DoesNotContain(accessToken, entry.Message);
                Assert.DoesNotContain(accessToken, $"{entry.State}");
            }
        }

        [Fact]
        public async Task Send_ValidationFailureThroughRegisteredPipeline_ProducesOneWarningLogEntry()
        {
            var handler = new TestPingCommandHandler();
            var validator = new TestValidator<TestPingCommand>().WithFailure("Name", "A name is required.");
            var logger = new FakeLogger<LoggingBehavior<TestPingCommand, Result>>();
            using var provider = new ServiceCollection()
                .AddApplication()
                .AddSingleton<IRequestHandler<TestPingCommand, Result>>(handler)
                .AddSingleton<IValidator<TestPingCommand>>(validator)
                .AddSingleton<ILogger<LoggingBehavior<TestPingCommand, Result>>>(logger)
                .BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new TestPingCommand());

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorType.Validation, result.Error!.Type);
            Assert.False(handler.WasHandled);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("TestPingCommand", entry.Message);
            Assert.Contains("Validation.Failed", entry.Message);
        }

        private static IReadOnlyList<KeyValuePair<string, object?>> StructuredState(object? state)
        {
            Assert.NotNull(state);

            return Assert.IsAssignableFrom<IReadOnlyList<KeyValuePair<string, object?>>>(state);
        }

        private static TimeSpan RecordedDuration(IReadOnlyList<KeyValuePair<string, object?>> values)
        {
            return Assert.IsType<TimeSpan>(values.Single(value => value.Key == "Duration").Value);
        }
    }

    public sealed class FakeLogger<TCategoryName> : ILogger<TCategoryName>
    {
        public List<FakeLogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new FakeLogEntry(logLevel, formatter(state, exception), exception, state));
        }
    }

    public sealed record FakeLogEntry(LogLevel Level, string Message, Exception? Exception, object? State);

    public sealed record TestSecretCommand(string AccessToken) : ICommand
    {
    }

    public sealed class TestPipelineException(string message) : Exception(message)
    {
    }
}
