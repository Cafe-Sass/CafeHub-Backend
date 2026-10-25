using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Plans;
using CafeHub.Application.Plans.Commands;
using CafeHub.Application.Plans.DTOs;
using CafeHub.Domain.Plans;
using CafeHub.UnitTests.Plans.Domain;

namespace CafeHub.UnitTests.Plans.Commands
{
    public sealed class CreatePlanCommandHandlerTests
    {
        private const string CreatedAuditAction = "plans.created.succeeded";
        private const string PlanTargetType = "plan";

        private readonly FakePlanRepository repository = new();
        private readonly RecordingPlanAuditWriter auditWriter = new();
        private readonly FakeCurrentUser currentUser;
        private readonly MutableTimeProvider timeProvider;
        private readonly CreatePlanCommandHandler handler;

        public CreatePlanCommandHandlerTests()
        {
            currentUser = new FakeCurrentUser
            {
                IsAuthenticated = true,
                AdminId = PlanTestData.AdminId,
            };
            timeProvider = new MutableTimeProvider { UtcNow = PlanTestData.CreatedAt };
            handler = new CreatePlanCommandHandler(repository, auditWriter, currentUser, timeProvider);
        }

        [Fact]
        public async Task Handle_WithANewKey_CreatesThePlanReturnsTheContractAndWritesTheAuditRecord()
        {
            var command = new CreatePlanCommand("growth", "Growth", false);

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var response = result.Value;
            Assert.NotEqual(Guid.Empty, response.Id);
            Assert.Equal("growth", response.Key);
            Assert.Equal("Growth", response.Name);
            Assert.False(response.IsSelectable);
            Assert.Empty(response.Versions);
            Assert.Equal(PlanTestData.CreatedAt, response.CreatedAt);
            Assert.Equal(PlanTestData.CreatedAt, response.UpdatedAt);

            var plan = Assert.Single(repository.Plans);
            Assert.Equal(response.Id, plan.Id);
            Assert.Equal("growth", plan.Code);
            Assert.Equal("Growth", plan.DisplayName);
            Assert.False(plan.IsSelectable);
            Assert.Equal(PlanTestData.AdminId, plan.CreatedBy);
            Assert.Equal(PlanTestData.AdminId, plan.UpdatedBy);
            Assert.Empty(plan.Versions);
            Assert.Equal(1, repository.KeyExistsCallCount);

            var auditEntry = Assert.Single(auditWriter.Entries);
            Assert.Equal(PlatformAuditActorKind.Admin, auditEntry.ActorKind);
            Assert.Equal(CreatedAuditAction, auditEntry.Action);
            Assert.Equal(PlanTargetType, auditEntry.TargetType);
            Assert.Equal(plan.Id, auditEntry.TargetId);
            Assert.Equal(PlatformAuditOutcome.Succeeded, auditEntry.Outcome);
        }

        [Fact]
        public async Task Handle_WithADuplicateKey_ReturnsConflictWithoutCreatingOrAuditing()
        {
            repository.Add(PlanTestData.CreatePlan(code: "starter"));
            var command = new CreatePlanCommand("starter", "Starter Again", true);

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(PlansErrors.KeyAlreadyExistsCode, result.Error!.Code);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);
            Assert.Equal(PlansErrors.KeyAlreadyExistsMessage, result.Error.Message);
            Assert.Single(repository.Plans);
            Assert.Equal(1, repository.KeyExistsCallCount);
            Assert.Empty(auditWriter.Entries);
        }

        [Fact]
        public async Task Handle_TakesTheCreationTimeFromTheTimeProvider()
        {
            var later = PlanTestData.CreatedAt.AddDays(2);
            timeProvider.UtcNow = later;

            var result = await handler.Handle(new CreatePlanCommand("growth", "Growth", true), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(later, result.Value.CreatedAt);
            Assert.Equal(later, Assert.Single(repository.Plans).CreatedAt);
        }

        [Fact]
        public async Task Handle_WithANullCommand_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
        }

        [Fact]
        public void TheHandler_RequiresEveryDependency()
        {
            Assert.Throws<ArgumentNullException>(() => new CreatePlanCommandHandler(null!, auditWriter, currentUser, timeProvider));
            Assert.Throws<ArgumentNullException>(() => new CreatePlanCommandHandler(repository, null!, currentUser, timeProvider));
            Assert.Throws<ArgumentNullException>(() => new CreatePlanCommandHandler(repository, auditWriter, null!, timeProvider));
            Assert.Throws<ArgumentNullException>(() => new CreatePlanCommandHandler(repository, auditWriter, currentUser, null!));
        }

        [Fact]
        public void TheCommand_IsAMasterTransactionalCommandReturningThePlanResponse()
        {
            Assert.True(typeof(ICommand<PlanResponse>).IsAssignableFrom(typeof(CreatePlanCommand)));
            Assert.True(typeof(IMasterTransactionalCommand).IsAssignableFrom(typeof(CreatePlanCommand)));
        }
    }
}
