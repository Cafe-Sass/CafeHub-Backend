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
    public sealed class CreatePlanVersionCommandHandlerTests
    {
        private const string CreatedAuditAction = "plans.version_created.succeeded";
        private const string PlanTargetType = "plan";

        private readonly FakePlanRepository repository = new();
        private readonly RecordingPlanAuditWriter auditWriter = new();
        private readonly FakeCurrentUser currentUser;
        private readonly MutableTimeProvider timeProvider;
        private readonly CreatePlanVersionCommandHandler handler;

        public CreatePlanVersionCommandHandlerTests()
        {
            currentUser = new FakeCurrentUser
            {
                IsAuthenticated = true,
                AdminId = PlanTestData.AdminId,
            };
            timeProvider = new MutableTimeProvider { UtcNow = PlanTestData.CreatedAt };
            handler = new CreatePlanVersionCommandHandler(repository, auditWriter, currentUser, timeProvider);
        }

        private static CreatePlanVersionCommand CreateCommand(
            Guid planId,
            decimal price = 29.99m,
            string currency = "eur",
            string period = "Annual",
            IReadOnlyList<PlanEntitlementRequest>? entitlements = null)
        {
            return new CreatePlanVersionCommand(
                planId,
                price,
                currency,
                period,
                entitlements ?? new[] { new PlanEntitlementRequest("max_tables", "20") });
        }

        [Fact]
        public async Task Handle_ForAKnownPlan_AppendsTheNextVersionAndReturnsTheContract()
        {
            var plan = PlanTestData.CreatePlan();
            PlanTestData.AddVersion(plan, price: 19.99m, currencyCode: "usd", billingPeriod: BillingPeriod.Monthly);
            repository.Add(plan);
            var command = CreateCommand(
                plan.Id,
                entitlements: new[]
                {
                    new PlanEntitlementRequest("max_tables", "20"),
                    new PlanEntitlementRequest("reporting.enabled", "true"),
                });

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var response = result.Value;
            Assert.Equal(plan.Id, response.Id);
            Assert.Equal(2, response.Versions.Count);
            var latest = response.Versions[0];
            Assert.Equal(2, latest.Number);
            Assert.Equal(29.99m, latest.Price);
            Assert.Equal("eur", latest.Currency);
            Assert.Equal("Annual", latest.Period);
            Assert.Equal(PlanTestData.CreatedAt, latest.CreatedAt);
            Assert.Equal(PlanTestData.CreatedAt, latest.UpdatedAt);
            Assert.Equal(new[] { "max_tables", "reporting.enabled" }, latest.Entitlements.Select(item => item.Key));
            Assert.Equal(new[] { "20", "true" }, latest.Entitlements.Select(item => item.Value));
            Assert.Equal(1, repository.FindByIdCallCount);

            var auditEntry = Assert.Single(auditWriter.Entries);
            Assert.Equal(PlatformAuditActorKind.Admin, auditEntry.ActorKind);
            Assert.Equal(CreatedAuditAction, auditEntry.Action);
            Assert.Equal(PlanTargetType, auditEntry.TargetType);
            Assert.Equal(plan.Id, auditEntry.TargetId);
            Assert.Equal(PlatformAuditOutcome.Succeeded, auditEntry.Outcome);
        }

        [Fact]
        public async Task Handle_LeavesTheEarlierVersionUnchanged()
        {
            var plan = PlanTestData.CreatePlan();
            var firstVersionId = Guid.NewGuid();
            var firstVersion = PlanTestData.AddVersion(
                plan,
                versionId: firstVersionId,
                price: 19.99m,
                currencyCode: "usd",
                billingPeriod: BillingPeriod.Monthly,
                entitlements: new[] { PlanTestData.CreateEntitlement(firstVersionId) });
            var entitlementSnapshot = firstVersion.Entitlements
                .Select(entitlement => (entitlement.Key, entitlement.Value))
                .ToArray();
            repository.Add(plan);

            await handler.Handle(CreateCommand(plan.Id), CancellationToken.None);

            Assert.Equal(19.99m, firstVersion.Price);
            Assert.Equal("usd", firstVersion.CurrencyCode);
            Assert.Equal(BillingPeriod.Monthly, firstVersion.BillingPeriod);
            Assert.Equal(PlanVersionStatus.Active, firstVersion.Status);
            Assert.Equal(PlanTestData.CreatedAt, firstVersion.CreatedAt);
            Assert.Equal(PlanTestData.CreatedAt, firstVersion.UpdatedAt);
            Assert.Equal(entitlementSnapshot, firstVersion.Entitlements.Select(entitlement => (entitlement.Key, entitlement.Value)));
            Assert.Equal(new[] { 1, 2 }, plan.Versions.Select(version => version.VersionNumber));
        }

        [Fact]
        public async Task Handle_CreatesTheVersionAsActiveWithTheCurrentAdministratorAndTime()
        {
            var plan = PlanTestData.CreatePlan();
            repository.Add(plan);
            var later = PlanTestData.CreatedAt.AddHours(3);
            timeProvider.UtcNow = later;

            var result = await handler.Handle(CreateCommand(plan.Id), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var version = Assert.Single(plan.Versions);
            Assert.Equal(PlanVersionStatus.Active, version.Status);
            Assert.Equal(PlanTestData.AdminId, version.CreatedBy);
            Assert.Equal(PlanTestData.AdminId, version.UpdatedBy);
            Assert.Equal(later, version.CreatedAt);
            Assert.Equal(later, version.UpdatedAt);
            Assert.Equal(later, version.Entitlements.Single().CreatedAt);
            Assert.Equal(version.Id, version.Entitlements.Single().PlanVersionId);
        }

        [Fact]
        public async Task Handle_AssignsSequentialVersionNumbersStartingAtOne()
        {
            var plan = PlanTestData.CreatePlan();
            repository.Add(plan);

            var firstResult = await handler.Handle(CreateCommand(plan.Id, price: 10m), CancellationToken.None);
            var secondResult = await handler.Handle(CreateCommand(plan.Id, price: 20m), CancellationToken.None);

            Assert.True(firstResult.IsSuccess);
            Assert.True(secondResult.IsSuccess);
            Assert.Equal(new[] { 1 }, firstResult.Value.Versions.Select(version => version.Number));
            Assert.Equal(new[] { 2, 1 }, secondResult.Value.Versions.Select(version => version.Number));
            Assert.Equal(new[] { 1, 2 }, plan.Versions.Select(version => version.VersionNumber));
        }

        [Fact]
        public async Task Handle_ForAnUnknownPlan_ReturnsNotFoundWithoutAuditing()
        {
            var result = await handler.Handle(CreateCommand(Guid.NewGuid()), CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(PlansErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(ErrorType.NotFound, result.Error.Type);
            Assert.Equal(PlansErrors.NotFoundMessage, result.Error.Message);
            Assert.Empty(auditWriter.Entries);
            Assert.Empty(repository.Plans);
            Assert.Equal(1, repository.FindByIdCallCount);
        }

        [Fact]
        public async Task Handle_WithNoEntitlements_CreatesAVersionWithoutEntitlements()
        {
            var plan = PlanTestData.CreatePlan();
            repository.Add(plan);

            var result = await handler.Handle(
                CreateCommand(plan.Id, entitlements: Array.Empty<PlanEntitlementRequest>()),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(Assert.Single(plan.Versions).Entitlements);
        }

        [Fact]
        public async Task Handle_WithANullCommand_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
        }

        [Fact]
        public void TheHandler_RequiresEveryDependency()
        {
            Assert.Throws<ArgumentNullException>(() => new CreatePlanVersionCommandHandler(null!, auditWriter, currentUser, timeProvider));
            Assert.Throws<ArgumentNullException>(() => new CreatePlanVersionCommandHandler(repository, null!, currentUser, timeProvider));
            Assert.Throws<ArgumentNullException>(() => new CreatePlanVersionCommandHandler(repository, auditWriter, null!, timeProvider));
            Assert.Throws<ArgumentNullException>(() => new CreatePlanVersionCommandHandler(repository, auditWriter, currentUser, null!));
        }

        [Fact]
        public void TheCommand_IsAMasterTransactionalCommandReturningThePlanResponse()
        {
            Assert.True(typeof(ICommand<PlanResponse>).IsAssignableFrom(typeof(CreatePlanVersionCommand)));
            Assert.True(typeof(IMasterTransactionalCommand).IsAssignableFrom(typeof(CreatePlanVersionCommand)));
        }
    }
}
