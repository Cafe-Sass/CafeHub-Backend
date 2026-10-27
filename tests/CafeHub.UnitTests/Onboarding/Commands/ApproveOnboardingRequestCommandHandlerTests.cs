using CafeHub.Application;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Onboarding;
using CafeHub.Application.Onboarding.Commands;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Onboarding.DTOs;
using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Provisioning;
using CafeHub.Domain.Subscriptions;
using CafeHub.Domain.Tenants;
using CafeHub.UnitTests.Onboarding.Application;
using CafeHub.UnitTests.Plans;
using FluentValidation.TestHelper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests.Onboarding.Commands
{
    public sealed class ApproveOnboardingRequestCommandHandlerTests
    {
        private sealed class FakeTenantStore : ITenantStore
        {
            public List<Tenant> Tenants { get; } = new();
            public int AddCallCount { get; private set; }
            public int FindCallCount { get; private set; }
            public int UpdateCallCount { get; private set; }

            public Task<Tenant?> FindByOnboardingRequestIdAsync(
                Guid onboardingRequestId,
                CancellationToken cancellationToken)
            {
                FindCallCount++;
                return Task.FromResult(Tenants.FirstOrDefault(t => t.OnboardingRequestId == onboardingRequestId));
            }

            public Task AddAsync(Tenant tenant, CancellationToken cancellationToken)
            {
                Tenants.Add(tenant);
                AddCallCount++;
                return Task.CompletedTask;
            }

            public Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken)
            {
                UpdateCallCount++;
                return Task.CompletedTask;
            }

            public Task<Tenant?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken)
            {
                return Task.FromResult(Tenants.FirstOrDefault(t => t.Id == tenantId));
            }
        }

        private sealed class FakeSubscriptionRepository : ISubscriptionRepository
        {
            public Subscription? Subscription { get; set; }
            public int FindCallCount { get; private set; }
            public int AddCallCount { get; private set; }

            public Task<Subscription?> FindByOnboardingRequestIdAsync(
                Guid onboardingRequestId,
                CancellationToken cancellationToken)
            {
                FindCallCount++;
                return Task.FromResult(Subscription);
            }

            public Task AddAsync(Subscription subscription, SubscriptionPeriod initialPeriod, CancellationToken cancellationToken)
            {
                AddCallCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class FakeProvisioningJobStore : IProvisioningJobStore
        {
            public List<ProvisioningJob> Jobs { get; } = new();
            public int AddCallCount { get; private set; }
            public int UpdateCallCount { get; private set; }
            public Exception? ThrowOnAdd { get; set; }

            public Task AddAsync(ProvisioningJob job, CancellationToken cancellationToken)
            {
                if (ThrowOnAdd is not null)
                {
                    throw ThrowOnAdd;
                }

                Jobs.Add(job);
                AddCallCount++;
                return Task.CompletedTask;
            }

            public Task<ProvisioningJob?> FindByIdAsync(
                Guid jobId,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(Jobs.FirstOrDefault(j => j.Id == jobId));
            }

            public Task<ProvisioningJob?> FindByTenantIdAsync(
                Guid tenantId,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(Jobs.FirstOrDefault(j => j.TenantId == tenantId));
            }

            public Task UpdateAsync(ProvisioningJob job, CancellationToken cancellationToken)
            {
                var existing = Jobs.FirstOrDefault(j => j.Id == job.Id);
                if (existing is not null)
                {
                    var index = Jobs.IndexOf(existing);
                    Jobs[index] = job;
                }

                UpdateCallCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class FakeCurrentUser : ICurrentUser
        {
            public bool IsAuthenticated { get; set; } = true;
            public Guid? AdminId { get; set; } = Guid.CreateVersion7();
            public IReadOnlyCollection<string> Roles { get; set; } = new[] { "SuperAdmin" };
        }

        private static readonly Guid RequestId = Guid.CreateVersion7();
        private static readonly Guid PlanVersionId = Guid.CreateVersion7();
        private static readonly Guid ReviewerId = Guid.CreateVersion7();

        private static OnboardingRequest PaidRequest()
        {
            var request = OnboardingRequest.CreateForPublicRegistration(
                RequestId,
                PlanVersionId,
                "owner@example.com",
                "Owner Name",
                null,
                "Cafe Name",
                "hash",
                "my-cafe",
                new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));
            request.TransitionTo(OnboardingRequestStatus.PaidAwaitingReview, new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero));
            return request;
        }

        private static Subscription ActiveSubscription()
        {
            return Subscription.Create(
                Guid.CreateVersion7(),
                RequestId,
                null,
                SubscriptionStatus.Active,
                new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));
        }

        private static ApproveOnboardingRequestCommandHandler BuildHandler(
            FakeOnboardingRequestStore store,
            FakeTenantStore tenantStore,
            FakeSubscriptionRepository subscriptionRepository,
            FakeProvisioningJobStore provisioningJobStore,
            FakeMasterUnitOfWork unitOfWork,
            RecordingPlanAuditWriter auditWriter,
            FakeCurrentUser currentUser,
            TimeProvider? timeProvider = null)
        {
            return new ApproveOnboardingRequestCommandHandler(
                store,
                tenantStore,
                subscriptionRepository,
                provisioningJobStore,
                unitOfWork,
                auditWriter,
                currentUser,
                timeProvider ?? TimeProvider.System);
        }

        private static ServiceProvider CreateMediatorProvider(
            FakeOnboardingRequestStore store,
            FakeTenantStore tenantStore,
            FakeSubscriptionRepository subscriptionRepository,
            FakeProvisioningJobStore provisioningJobStore,
            FakeMasterUnitOfWork unitOfWork,
            RecordingPlanAuditWriter auditWriter,
            FakeCurrentUser currentUser)
        {
            return new ServiceCollection()
                .AddApplication()
                .AddSingleton<IOnboardingRequestStore>(store)
                .AddSingleton<ITenantStore>(tenantStore)
                .AddSingleton<ISubscriptionRepository>(subscriptionRepository)
                .AddSingleton<IProvisioningJobStore>(provisioningJobStore)
                .AddSingleton<IMasterUnitOfWork>(unitOfWork)
                .AddSingleton<IPlatformAuditWriter>(auditWriter)
                .AddSingleton<ICurrentUser>(currentUser)
                .AddSingleton(TimeProvider.System)
                .BuildServiceProvider();
        }

        [Fact]
        public async Task Send_Success_UsesOnlyHandlerOwnedMasterTransaction()
        {
            var request = PaidRequest();
            var store = new FakeOnboardingRequestStore(request);
            var tenantStore = new FakeTenantStore();
            var subscriptionRepository = new FakeSubscriptionRepository { Subscription = ActiveSubscription() };
            var provisioningJobStore = new FakeProvisioningJobStore();
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            await using var provider = CreateMediatorProvider(store, tenantStore, subscriptionRepository, provisioningJobStore, unitOfWork, auditWriter, new FakeCurrentUser { AdminId = ReviewerId });

            var result = await provider.GetRequiredService<ISender>().Send(new ApproveOnboardingRequestCommand(RequestId));

            Assert.True(result.IsSuccess);
            Assert.Single(tenantStore.Tenants);
            Assert.Single(provisioningJobStore.Jobs);
            Assert.Single(auditWriter.Entries);
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(1, unitOfWork.CommitCount);
            Assert.Equal(0, unitOfWork.RollbackCount);
        }

        [Fact]
        public async Task Send_MissingSubscription_RollsBackHandlerOwnedMasterTransactionOnce()
        {
            var request = PaidRequest();
            var store = new FakeOnboardingRequestStore(request);
            var tenantStore = new FakeTenantStore();
            var subscriptionRepository = new FakeSubscriptionRepository();
            var provisioningJobStore = new FakeProvisioningJobStore();
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            await using var provider = CreateMediatorProvider(store, tenantStore, subscriptionRepository, provisioningJobStore, unitOfWork, auditWriter, new FakeCurrentUser { AdminId = ReviewerId });

            var result = await provider.GetRequiredService<ISender>().Send(new ApproveOnboardingRequestCommand(RequestId));

            Assert.False(result.IsSuccess);
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(0, unitOfWork.CommitCount);
            Assert.Equal(1, unitOfWork.RollbackCount);
            Assert.Empty(provisioningJobStore.Jobs);
            Assert.Empty(auditWriter.Entries);
        }

        [Fact]
        public async Task Send_HandlerException_RollsBackHandlerOwnedMasterTransactionOnce()
        {
            var request = PaidRequest();
            var expectedException = new InvalidOperationException("Provisioning job persistence failed.");
            var store = new FakeOnboardingRequestStore(request);
            var tenantStore = new FakeTenantStore();
            var subscriptionRepository = new FakeSubscriptionRepository { Subscription = ActiveSubscription() };
            var provisioningJobStore = new FakeProvisioningJobStore { ThrowOnAdd = expectedException };
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            await using var provider = CreateMediatorProvider(store, tenantStore, subscriptionRepository, provisioningJobStore, unitOfWork, auditWriter, new FakeCurrentUser { AdminId = ReviewerId });

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.GetRequiredService<ISender>().Send(new ApproveOnboardingRequestCommand(RequestId)));

            Assert.Same(expectedException, exception);
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(0, unitOfWork.CommitCount);
            Assert.Equal(1, unitOfWork.RollbackCount);
            Assert.Empty(provisioningJobStore.Jobs);
            Assert.Empty(auditWriter.Entries);
        }

        [Fact]
        public async Task Send_RepeatedApproval_IsIdempotentWithoutStartingTransaction()
        {
            var request = PaidRequest();
            request.Approve(ReviewerId, new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero));
            var tenantId = Guid.CreateVersion7();
            var existingTenant = Tenant.Create(tenantId, RequestId, "my-cafe", TenantLifecycleStatus.Provisioning,
                new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero), ReviewerId);
            var store = new FakeOnboardingRequestStore(request);
            var tenantStore = new FakeTenantStore { Tenants = { existingTenant } };
            var provisioningJobStore = new FakeProvisioningJobStore();
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            await using var provider = CreateMediatorProvider(store, tenantStore, new FakeSubscriptionRepository(), provisioningJobStore, unitOfWork, auditWriter, new FakeCurrentUser { AdminId = ReviewerId });

            var result = await provider.GetRequiredService<ISender>().Send(new ApproveOnboardingRequestCommand(RequestId));

            Assert.True(result.IsSuccess);
            Assert.Equal(tenantId, result.Value!.TenantId);
            Assert.Equal(0, unitOfWork.BeginCount);
            Assert.Empty(provisioningJobStore.Jobs);
            Assert.Empty(auditWriter.Entries);
        }

        [Fact]
        public async Task Handle_Success_CreatesTenantLinksSubscriptionCreatesJobAndApproves()
        {
            var request = PaidRequest();
            var subscription = ActiveSubscription();
            var now = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

            var store = new FakeOnboardingRequestStore(request);
            var tenantStore = new FakeTenantStore();
            var subscriptionRepository = new FakeSubscriptionRepository { Subscription = subscription };
            var provisioningJobStore = new FakeProvisioningJobStore();
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var currentUser = new FakeCurrentUser { AdminId = ReviewerId };

            var handler = BuildHandler(store, tenantStore, subscriptionRepository, provisioningJobStore, unitOfWork, auditWriter, currentUser, new FakeTimeProvider(now));

            var result = await handler.Handle(new ApproveOnboardingRequestCommand(RequestId), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var response = result.Value!;
            Assert.Equal(RequestId, response.RequestId);
            Assert.NotEqual(Guid.Empty, response.TenantId);
            Assert.Equal(OnboardingRequestStatus.Approved.ToString(), response.State);

            // One tenant created
            Assert.Single(tenantStore.Tenants);
            Assert.Equal(RequestId, tenantStore.Tenants[0].OnboardingRequestId);
            Assert.Equal(TenantLifecycleStatus.Provisioning, tenantStore.Tenants[0].LifecycleStatus);
            Assert.Equal(ReviewerId, tenantStore.Tenants[0].CreatedBy);
            Assert.Equal(response.TenantId, tenantStore.Tenants[0].Id);

            // Subscription linked to tenant
            Assert.Equal(response.TenantId, subscription.TenantId);

            // One pending provisioning job
            Assert.Single(provisioningJobStore.Jobs);
            Assert.Equal(response.TenantId, provisioningJobStore.Jobs[0].TenantId);
            Assert.Equal(ProvisioningJobStatus.Pending, provisioningJobStore.Jobs[0].Status);

            // Request approved with reviewer and time
            Assert.Equal(OnboardingRequestStatus.Approved, request.Status);
            Assert.Equal(ReviewerId, request.ReviewedBy);
            Assert.Equal(now, request.ReviewedAt);

            // Audit entry written
            Assert.Single(auditWriter.Entries);
            Assert.Equal("onboarding.approved.succeeded", auditWriter.Entries[0].Action);
            Assert.Equal(PlatformAuditActorKind.Admin, auditWriter.Entries[0].ActorKind);
            Assert.Equal(PlatformAuditOutcome.Succeeded, auditWriter.Entries[0].Outcome);

            // Transaction used
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(1, unitOfWork.CommitCount);
            Assert.Equal(0, unitOfWork.RollbackCount);
        }

        [Fact]
        public async Task Handle_RepeatedApproval_ReturnsSameTenantIdAndCreatesNothingNew()
        {
            var request = PaidRequest();
            request.Approve(ReviewerId, new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero));
            var tenantId = Guid.CreateVersion7();
            var existingTenant = Tenant.Create(
                tenantId,
                RequestId,
                "my-cafe",
                TenantLifecycleStatus.Provisioning,
                new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero),
                ReviewerId);

            var store = new FakeOnboardingRequestStore(request);
            var tenantStore = new FakeTenantStore { Tenants = { existingTenant } };
            var subscriptionRepository = new FakeSubscriptionRepository { Subscription = ActiveSubscription() };
            var provisioningJobStore = new FakeProvisioningJobStore();
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var currentUser = new FakeCurrentUser { AdminId = ReviewerId };

            var handler = BuildHandler(store, tenantStore, subscriptionRepository, provisioningJobStore, unitOfWork, auditWriter, currentUser);

            var result = await handler.Handle(new ApproveOnboardingRequestCommand(RequestId), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(tenantId, result.Value!.TenantId);

            // Nothing new created
            Assert.Equal(0, tenantStore.AddCallCount);
            Assert.Equal(0, provisioningJobStore.AddCallCount);
            Assert.Equal(0, unitOfWork.BeginCount);
            Assert.Equal(0, unitOfWork.CommitCount);
        }

        [Fact]
        public async Task Handle_RejectedRequest_ReturnsRequestNotApprovable()
        {
            var request = PaidRequest();
            request.Reject(ReviewerId, new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero), "Reason");

            var store = new FakeOnboardingRequestStore(request);
            var tenantStore = new FakeTenantStore();
            var subscriptionRepository = new FakeSubscriptionRepository { Subscription = ActiveSubscription() };
            var provisioningJobStore = new FakeProvisioningJobStore();
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var currentUser = new FakeCurrentUser { AdminId = ReviewerId };

            var handler = BuildHandler(store, tenantStore, subscriptionRepository, provisioningJobStore, unitOfWork, auditWriter, currentUser);

            var result = await handler.Handle(new ApproveOnboardingRequestCommand(RequestId), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(OnboardingErrors.RequestNotApprovableCode, result.Error!.Code);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);

            // Nothing changed
            Assert.Equal(0, tenantStore.AddCallCount);
            Assert.Equal(0, provisioningJobStore.AddCallCount);
            Assert.Equal(0, unitOfWork.BeginCount);
        }

        [Theory]
        [InlineData(OnboardingRequestStatus.AwaitingPayment)]
        [InlineData(OnboardingRequestStatus.PaymentFailed)]
        [InlineData(OnboardingRequestStatus.Cancelled)]
        public async Task Handle_NotReadyForReview_ReturnsRequestNotApprovable(OnboardingRequestStatus status)
        {
            var request = PaidRequest();
            request.TransitionTo(status, new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero));

            var store = new FakeOnboardingRequestStore(request);
            var tenantStore = new FakeTenantStore();
            var subscriptionRepository = new FakeSubscriptionRepository { Subscription = ActiveSubscription() };
            var provisioningJobStore = new FakeProvisioningJobStore();
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var currentUser = new FakeCurrentUser { AdminId = ReviewerId };

            var handler = BuildHandler(store, tenantStore, subscriptionRepository, provisioningJobStore, unitOfWork, auditWriter, currentUser);

            var result = await handler.Handle(new ApproveOnboardingRequestCommand(RequestId), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(OnboardingErrors.RequestNotApprovableCode, result.Error!.Code);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);

            // Nothing changed
            Assert.Equal(0, tenantStore.AddCallCount);
            Assert.Equal(0, provisioningJobStore.AddCallCount);
            Assert.Equal(0, unitOfWork.BeginCount);
        }

        [Fact]
        public async Task Handle_UnknownRequest_ReturnsRequestNotFound()
        {
            var store = new FakeOnboardingRequestStore();
            var tenantStore = new FakeTenantStore();
            var subscriptionRepository = new FakeSubscriptionRepository { Subscription = ActiveSubscription() };
            var provisioningJobStore = new FakeProvisioningJobStore();
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var currentUser = new FakeCurrentUser { AdminId = ReviewerId };

            var handler = BuildHandler(store, tenantStore, subscriptionRepository, provisioningJobStore, unitOfWork, auditWriter, currentUser);

            var result = await handler.Handle(new ApproveOnboardingRequestCommand(Guid.CreateVersion7()), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(OnboardingErrors.RequestNotFoundCode, result.Error!.Code);
            Assert.Equal(ErrorType.NotFound, result.Error.Type);

            Assert.Equal(0, tenantStore.AddCallCount);
            Assert.Equal(0, provisioningJobStore.AddCallCount);
            Assert.Equal(0, unitOfWork.BeginCount);
        }

        [Fact]
        public void Validator_RejectsEmptyRequestId()
        {
            var validator = new ApproveOnboardingRequestCommandValidator();
            var command = new ApproveOnboardingRequestCommand(Guid.Empty);

            var result = validator.TestValidate(command);

            result.ShouldHaveValidationErrorFor("RequestId");
        }

        private sealed class FakeTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;
            public FakeTimeProvider(DateTimeOffset now) => _now = now;
            public override DateTimeOffset GetUtcNow() => _now;
        }
    }
}
