using CafeHub.Application;
using CafeHub.Application.Abstractions.Behaviors;
using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests
{
    public sealed class TransactionBehaviorTests
    {
        [Fact]
        public async Task Handle_MasterMarkedCommand_Succeeds_BeginOnce_CommitOnce_NoRollback()
        {
            var unitOfWork = new RecordingMasterUnitOfWork();
            var behavior = new MasterTransactionBehavior<TestMasterMarkedCommand, Result>(unitOfWork);
            RequestHandlerDelegate<Result> next = (_) => Task.FromResult(Result.Success());

            var result = await behavior.Handle(new TestMasterMarkedCommand(), next, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(1, unitOfWork.CommitCount);
            Assert.Equal(0, unitOfWork.RollbackCount);
        }

        [Fact]
        public async Task Handle_MasterMarkedCommand_FailedResult_RollbackOnce_NoCommit_ReturnsSameFailure()
        {
            var expectedError = Error.Conflict("Test.Conflict", "The continuation returned a conflict.");
            var unitOfWork = new RecordingMasterUnitOfWork();
            var behavior = new MasterTransactionBehavior<TestMasterMarkedCommand, Result>(unitOfWork);
            RequestHandlerDelegate<Result> next = (_) => Task.FromResult(Result.Failure(expectedError));

            var result = await behavior.Handle(new TestMasterMarkedCommand(), next, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Same(expectedError, result.Error);
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(0, unitOfWork.CommitCount);
            Assert.Equal(1, unitOfWork.RollbackCount);
        }

        [Fact]
        public async Task Handle_MasterMarkedCommand_Exception_RollbackOnce_NoCommit_RethrowsSameException()
        {
            var expectedException = new TestPipelineException("The pipeline continuation threw.");
            var unitOfWork = new RecordingMasterUnitOfWork();
            var behavior = new MasterTransactionBehavior<TestMasterMarkedCommand, Result>(unitOfWork);
            RequestHandlerDelegate<Result> next = (_) => throw expectedException;

            var caughtException = await Assert.ThrowsAsync<TestPipelineException>(() => behavior.Handle(new TestMasterMarkedCommand(), next, CancellationToken.None));

            Assert.Same(expectedException, caughtException);
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(0, unitOfWork.CommitCount);
            Assert.Equal(1, unitOfWork.RollbackCount);
        }

        [Fact]
        public async Task Handle_TenantMarkedCommand_Succeeds_BeginOnce_CommitOnce_NoRollback()
        {
            var unitOfWork = new RecordingTenantUnitOfWork();
            var behavior = new TenantTransactionBehavior<TestTenantMarkedCommand, Result>(unitOfWork);
            RequestHandlerDelegate<Result> next = (_) => Task.FromResult(Result.Success());

            var result = await behavior.Handle(new TestTenantMarkedCommand(), next, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(1, unitOfWork.CommitCount);
            Assert.Equal(0, unitOfWork.RollbackCount);
        }

        [Fact]
        public async Task Send_MasterMarkedCommand_TouchesOnlyMasterUnitOfWork()
        {
            var masterUnitOfWork = new RecordingMasterUnitOfWork();
            var tenantUnitOfWork = new RecordingTenantUnitOfWork();
            using var provider = new ServiceCollection()
                .AddApplication()
                .AddSingleton<IRequestHandler<TestMasterMarkedCommand, Result>>(new TestMasterMarkedCommandHandler())
                .AddSingleton<IMasterUnitOfWork>(masterUnitOfWork)
                .AddSingleton<ITenantUnitOfWork>(tenantUnitOfWork)
                .BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new TestMasterMarkedCommand());

            Assert.True(result.IsSuccess);
            Assert.Equal(1, masterUnitOfWork.BeginCount);
            Assert.Equal(1, masterUnitOfWork.CommitCount);
            Assert.Equal(0, masterUnitOfWork.RollbackCount);
            Assert.Equal(0, tenantUnitOfWork.BeginCount);
            Assert.Equal(0, tenantUnitOfWork.CommitCount);
            Assert.Equal(0, tenantUnitOfWork.RollbackCount);
        }

        [Fact]
        public async Task Send_TenantMarkedCommand_TouchesOnlyTenantUnitOfWork()
        {
            var masterUnitOfWork = new RecordingMasterUnitOfWork();
            var tenantUnitOfWork = new RecordingTenantUnitOfWork();
            using var provider = new ServiceCollection()
                .AddApplication()
                .AddSingleton<IRequestHandler<TestTenantMarkedCommand, Result>>(new TestTenantMarkedCommandHandler())
                .AddSingleton<IMasterUnitOfWork>(masterUnitOfWork)
                .AddSingleton<ITenantUnitOfWork>(tenantUnitOfWork)
                .BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new TestTenantMarkedCommand());

            Assert.True(result.IsSuccess);
            Assert.Equal(1, tenantUnitOfWork.BeginCount);
            Assert.Equal(1, tenantUnitOfWork.CommitCount);
            Assert.Equal(0, tenantUnitOfWork.RollbackCount);
            Assert.Equal(0, masterUnitOfWork.BeginCount);
            Assert.Equal(0, masterUnitOfWork.CommitCount);
            Assert.Equal(0, masterUnitOfWork.RollbackCount);
        }

        [Fact]
        public async Task Send_Query_And_UnmarkedCommand_AreNeverWrapped()
        {
            var masterUnitOfWork = new RecordingMasterUnitOfWork();
            var tenantUnitOfWork = new RecordingTenantUnitOfWork();
            using var provider = new ServiceCollection()
                .AddApplication()
                .AddSingleton<IRequestHandler<TestBranchCountQuery, Result<int>>, TestBranchCountQueryHandler>()
                .AddSingleton<IRequestHandler<TestPingCommand, Result>>(new TestPingCommandHandler())
                .AddSingleton<IMasterUnitOfWork>(masterUnitOfWork)
                .AddSingleton<ITenantUnitOfWork>(tenantUnitOfWork)
                .BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var queryResult = await mediator.Send(new TestBranchCountQuery(4));
            var commandResult = await mediator.Send(new TestPingCommand());

            Assert.True(queryResult.IsSuccess);
            Assert.True(commandResult.IsSuccess);
            Assert.Equal(0, masterUnitOfWork.BeginCount);
            Assert.Equal(0, masterUnitOfWork.CommitCount);
            Assert.Equal(0, masterUnitOfWork.RollbackCount);
            Assert.Equal(0, tenantUnitOfWork.BeginCount);
            Assert.Equal(0, tenantUnitOfWork.CommitCount);
            Assert.Equal(0, tenantUnitOfWork.RollbackCount);
        }
    }

    public abstract class RecordingUnitOfWork : IUnitOfWork
    {
        public int BeginCount { get; private set; }

        public int CommitCount { get; private set; }

        public int RollbackCount { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken)
        {
            BeginCount++;

            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken)
        {
            CommitCount++;

            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken)
        {
            RollbackCount++;

            return Task.CompletedTask;
        }
    }

    public sealed class RecordingMasterUnitOfWork : RecordingUnitOfWork, IMasterUnitOfWork
    {
    }

    public sealed class RecordingTenantUnitOfWork : RecordingUnitOfWork, ITenantUnitOfWork
    {
    }

    public sealed record TestMasterMarkedCommand : ICommand, IMasterTransactionalCommand
    {
    }

    public sealed record TestTenantMarkedCommand : ICommand, ITenantTransactionalCommand
    {
    }

    public sealed class TestMasterMarkedCommandHandler : ICommandHandler<TestMasterMarkedCommand>
    {
        public Task<Result> Handle(TestMasterMarkedCommand command, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Success());
        }
    }

    public sealed class TestTenantMarkedCommandHandler : ICommandHandler<TestTenantMarkedCommand>
    {
        public Task<Result> Handle(TestTenantMarkedCommand command, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Success());
        }
    }
}
