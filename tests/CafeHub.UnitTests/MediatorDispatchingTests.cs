using CafeHub.Application;
using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests
{
    public sealed class MediatorDispatchingTests
    {
        [Fact]
        public void AddApplication_MakesIMediatorResolvable()
        {
            using var provider = new ServiceCollection()
                .AddApplication()
                .BuildServiceProvider();

            var mediator = provider.GetRequiredService<IMediator>();

            Assert.NotNull(mediator);
        }

        [Fact]
        public async Task Send_CommandWithoutResponse_ReachesHandler_AndReturnsItsResult()
        {
            var handler = new TestPingCommandHandler();
            using var provider = new ServiceCollection()
                .AddApplication()
                .AddSingleton<IRequestHandler<TestPingCommand, Result>>(handler)
                .BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new TestPingCommand());

            Assert.True(handler.WasHandled);
            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
            Assert.Null(result.Error);
        }

        [Fact]
        public async Task Send_CommandWithResponse_ReturnsItsValue()
        {
            using var provider = new ServiceCollection()
                .AddApplication()
                .AddSingleton<IRequestHandler<TestPingCommandWithResponse, Result<string>>, TestPingCommandWithResponseHandler>()
                .BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new TestPingCommandWithResponse("test-ping-response"));

            Assert.True(result.IsSuccess);
            Assert.Equal("test-ping-response", result.Value);
        }

        [Fact]
        public async Task Send_Query_ReachesHandler_AndReturnsItsValue()
        {
            using var provider = new ServiceCollection()
                .AddApplication()
                .AddSingleton<IRequestHandler<TestBranchCountQuery, Result<int>>, TestBranchCountQueryHandler>()
                .BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new TestBranchCountQuery(4));

            Assert.True(result.IsSuccess);
            Assert.Equal(5, result.Value);
        }

        [Fact]
        public async Task Send_FailedResult_ComesBackUnchanged()
        {
            var expectedError = Error.NotFound("Test.HandlerFailed", "The test handler could not process the request.");
            var handler = new TestFailingCommandHandler(expectedError);
            using var provider = new ServiceCollection()
                .AddApplication()
                .AddSingleton<IRequestHandler<TestFailingCommand, Result>>(handler)
                .BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new TestFailingCommand());

            Assert.True(result.IsFailure);
            Assert.False(result.IsSuccess);
            Assert.Same(expectedError, result.Error);
        }
    }

    public sealed record TestPingCommand : ICommand
    {
    }

    public sealed class TestPingCommandHandler : ICommandHandler<TestPingCommand>
    {
        public bool WasHandled { get; private set; }

        public Task<Result> Handle(TestPingCommand command, CancellationToken cancellationToken)
        {
            WasHandled = true;

            return Task.FromResult(Result.Success());
        }
    }

    public sealed record TestPingCommandWithResponse(string Response) : ICommand<string>
    {
    }

    public sealed class TestPingCommandWithResponseHandler : ICommandHandler<TestPingCommandWithResponse, string>
    {
        public Task<Result<string>> Handle(TestPingCommandWithResponse command, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Success(command.Response));
        }
    }

    public sealed record TestBranchCountQuery(int KnownBranches) : IQuery<int>
    {
    }

    public sealed class TestBranchCountQueryHandler : IQueryHandler<TestBranchCountQuery, int>
    {
        public Task<Result<int>> Handle(TestBranchCountQuery command, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Success(command.KnownBranches + 1));
        }
    }

    public sealed record TestFailingCommand : ICommand
    {
    }

    public sealed class TestFailingCommandHandler : ICommandHandler<TestFailingCommand>
    {
        private readonly Error error;

        public TestFailingCommandHandler(Error error)
        {
            this.error = error;
        }

        public Task<Result> Handle(TestFailingCommand command, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Failure(this.error));
        }
    }
}
