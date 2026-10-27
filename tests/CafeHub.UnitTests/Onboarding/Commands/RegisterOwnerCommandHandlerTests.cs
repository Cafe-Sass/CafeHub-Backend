using CafeHub.Application;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Onboarding;
using CafeHub.Application.Onboarding.Commands;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Plans.Contracts;
using CafeHub.Domain.Onboarding;
using CafeHub.UnitTests.Onboarding.Application;
using CafeHub.UnitTests.Plans;
using FluentValidation.TestHelper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests.Onboarding.Commands
{
    public sealed class RegisterOwnerCommandHandlerTests
    {
        private static readonly Guid PlanVersionId = Guid.CreateVersion7();

        private sealed class FakeOwnerPasswordHasher : IOwnerPasswordHasher
        {
            public int HashCallCount { get; private set; }

            public string Hash(string password)
            {
                HashCallCount++;
                return $"hash:{password.Length}";
            }
        }

        private sealed class FakeMasterUnitOfWork : IMasterUnitOfWork
        {
            public int BeginCount { get; private set; }
            public int CommitCount { get; private set; }
            public int RollbackCount { get; private set; }
            public Exception? ThrowOnCommit { get; set; }

            public Task BeginTransactionAsync(CancellationToken cancellationToken)
            {
                BeginCount++;
                return Task.CompletedTask;
            }

            public Task CommitTransactionAsync(CancellationToken cancellationToken)
            {
                CommitCount++;
                if (ThrowOnCommit is not null)
                {
                    throw ThrowOnCommit;
                }

                return Task.CompletedTask;
            }

            public Task RollbackTransactionAsync(CancellationToken cancellationToken)
            {
                RollbackCount++;
                return Task.CompletedTask;
            }
        }

        private static readonly OnboardingRequest ExistingRequest = OnboardingRequest.CreateForPublicRegistration(
            Guid.CreateVersion7(),
            PlanVersionId,
            "owner@example.com",
            "Owner Name",
            null,
            "Cafe Name",
            "existing-hash",
            "my-cafe",
            new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

        private static OnboardingRequest RequestInState(OnboardingRequestStatus status)
        {
            var request = OnboardingRequest.CreateForPublicRegistration(
                Guid.CreateVersion7(),
                PlanVersionId,
                "owner@example.com",
                "Owner Name",
                null,
                "Cafe Name",
                "existing-hash",
                "my-cafe",
                new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));
            request.Status = status;
            return request;
        }

        private static RegisterOwnerCommandHandler BuildHandler(
            FakeOnboardingRequestStore store,
            FakeSlugAvailability slugAvailability,
            FakeOwnerPasswordHasher passwordHasher,
            FakePlanRepository planRepository,
            FakeMasterUnitOfWork unitOfWork,
            RecordingPlanAuditWriter auditWriter,
            TimeProvider? timeProvider = null)
        {
            return new RegisterOwnerCommandHandler(
                store,
                slugAvailability,
                new DuplicateRegistrationDetectorForTests(),
                passwordHasher,
                planRepository,
                unitOfWork,
                auditWriter,
                timeProvider ?? TimeProvider.System);
        }

        private static RegisterOwnerCommand ValidCommand() => new(
            "Owner.Name@Example.com",
            "Owner Name",
            "1-555-0100",
            "Cafe Name",
            "Str0ngP@ssw0rd!",
            "my-cafe",
            PlanVersionId);

        [Fact]
        public async Task Handle_CreatesRequestWithHashAndReturnsReference()
        {
            var store = new FakeOnboardingRequestStore();
            var slugAvailability = new FakeSlugAvailability();
            var passwordHasher = new FakeOwnerPasswordHasher();
            var planRepository = new FakePlanRepository { PlanVersionsExist = true };
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();

            var handler = BuildHandler(store, slugAvailability, passwordHasher, planRepository, unitOfWork, auditWriter);

            var result = await handler.Handle(ValidCommand(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotEqual(Guid.Empty, result.Value!.RequestId);
            Assert.Equal(OnboardingRequestStatus.AwaitingPayment, result.Value.State);
            Assert.False(result.Value.IsDuplicate);

            Assert.Single(store.Requests);
            Assert.Equal("owner.name@example.com", store.Requests[0].OwnerEmail);
            Assert.False(string.IsNullOrEmpty(store.Requests[0].OwnerPasswordHash));
            Assert.NotEqual("Str0ngP@ssw0rd!", store.Requests[0].OwnerPasswordHash);
            Assert.Equal("my-cafe", store.Requests[0].TenantSlug);

            Assert.Equal(1, passwordHasher.HashCallCount);

            Assert.Single(auditWriter.Entries);
            Assert.Equal(PlatformAuditActorKind.System, auditWriter.Entries[0].ActorKind);
            Assert.Equal("onboarding.registered.succeeded", auditWriter.Entries[0].Action);
            Assert.Equal(PlatformAuditOutcome.Succeeded, auditWriter.Entries[0].Outcome);

            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(1, unitOfWork.CommitCount);
            Assert.Equal(0, unitOfWork.RollbackCount);
        }

        [Fact]
        public async Task Send_RegistrationCommand_UsesOnlyTheHandlerOwnedMasterTransaction()
        {
            var store = new FakeOnboardingRequestStore();
            var slugAvailability = new FakeSlugAvailability();
            var passwordHasher = new FakeOwnerPasswordHasher();
            var planRepository = new FakePlanRepository { PlanVersionsExist = true };
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();

            using var provider = CreateMediatorProvider(
                store, slugAvailability, passwordHasher, planRepository, unitOfWork, auditWriter);
            var result = await provider.GetRequiredService<IMediator>().Send(ValidCommand());

            Assert.True(result.IsSuccess);
            Assert.Single(store.Requests);
            Assert.Single(auditWriter.Entries);
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(1, unitOfWork.CommitCount);
            Assert.Equal(0, unitOfWork.RollbackCount);
        }

        [Fact]
        public async Task Send_RegistrationCommitFailure_RollsBackOnceAndRethrows()
        {
            var store = new FakeOnboardingRequestStore();
            var slugAvailability = new FakeSlugAvailability();
            var passwordHasher = new FakeOwnerPasswordHasher();
            var planRepository = new FakePlanRepository { PlanVersionsExist = true };
            var expectedException = new InvalidOperationException("commit failed");
            var unitOfWork = new FakeMasterUnitOfWork { ThrowOnCommit = expectedException };
            var auditWriter = new RecordingPlanAuditWriter();

            using var provider = CreateMediatorProvider(
                store, slugAvailability, passwordHasher, planRepository, unitOfWork, auditWriter);

            var actualException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.GetRequiredService<IMediator>().Send(ValidCommand()));

            Assert.Same(expectedException, actualException);
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(1, unitOfWork.CommitCount);
            Assert.Equal(1, unitOfWork.RollbackCount);
        }

        private static ServiceProvider CreateMediatorProvider(
            FakeOnboardingRequestStore store,
            FakeSlugAvailability slugAvailability,
            FakeOwnerPasswordHasher passwordHasher,
            FakePlanRepository planRepository,
            FakeMasterUnitOfWork unitOfWork,
            RecordingPlanAuditWriter auditWriter)
        {
            return new ServiceCollection()
                .AddApplication()
                .AddSingleton<IOnboardingRequestStore>(store)
                .AddSingleton<ISlugAvailability>(slugAvailability)
                .AddSingleton<IDuplicateRegistrationDetector>(new DuplicateRegistrationDetectorForTests())
                .AddSingleton<IOwnerPasswordHasher>(passwordHasher)
                .AddSingleton<IPlanRepository>(planRepository)
                .AddSingleton<IMasterUnitOfWork>(unitOfWork)
                .AddSingleton<IPlatformAuditWriter>(auditWriter)
                .AddSingleton(TimeProvider.System)
                .BuildServiceProvider();
        }

        [Theory]
        [InlineData(OnboardingRequestStatus.AwaitingPayment)]
        [InlineData(OnboardingRequestStatus.PaymentFailed)]
        [InlineData(OnboardingRequestStatus.PaidAwaitingReview)]
        [InlineData(OnboardingRequestStatus.Approved)]
        [InlineData(OnboardingRequestStatus.Rejected)]
        [InlineData(OnboardingRequestStatus.Cancelled)]
        public async Task Handle_DuplicateEmailDifferentCasing_ReturnsExistingReference(OnboardingRequestStatus status)
        {
            var existing = RequestInState(status);
            var store = new FakeOnboardingRequestStore(existing);
            var slugAvailability = new FakeSlugAvailability();
            var passwordHasher = new FakeOwnerPasswordHasher();
            var planRepository = new FakePlanRepository { PlanVersionsExist = true };
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();

            var handler = BuildHandler(store, slugAvailability, passwordHasher, planRepository, unitOfWork, auditWriter);

            var command = new RegisterOwnerCommand(
                "OWNER@EXAMPLE.COM", "Owner Name", "+1-555-0100", "Cafe Name",
                "Str0ngP@ssw0rd!", "my-cafe", PlanVersionId);
            var result = await handler.Handle(command, CancellationToken.None);

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"Handler failed: {result.Error?.Code} - {result.Error?.Message}");
            }

            Assert.Equal(status, result.Value!.State);
            Assert.True(result.Value.IsDuplicate);

            Assert.Single(store.Requests);
            Assert.Equal(0, store.AddCallCount);
            Assert.Equal(0, passwordHasher.HashCallCount);
            Assert.Equal(0, unitOfWork.BeginCount);
            Assert.Equal(0, unitOfWork.CommitCount);
        }

        [Fact]
        public async Task Handle_UnknownPlanVersion_ReturnsNotFound()
        {
            var store = new FakeOnboardingRequestStore();
            var slugAvailability = new FakeSlugAvailability();
            var passwordHasher = new FakeOwnerPasswordHasher();
            var planRepository = new FakePlanRepository { PlanVersionsExist = false };
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();

            var handler = BuildHandler(store, slugAvailability, passwordHasher, planRepository, unitOfWork, auditWriter);

            var result = await handler.Handle(ValidCommand(), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal("Onboarding.PlanVersionNotFound", result.Error!.Code);
            Assert.Empty(store.Requests);
            Assert.Equal(0, unitOfWork.BeginCount);
        }

        [Fact]
        public async Task Handle_InvalidSlug_ReturnsSlugInvalid()
        {
            var store = new FakeOnboardingRequestStore();
            var slugAvailability = new FakeSlugAvailability();
            var passwordHasher = new FakeOwnerPasswordHasher();
            var planRepository = new FakePlanRepository { PlanVersionsExist = true };
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();

            var handler = BuildHandler(store, slugAvailability, passwordHasher, planRepository, unitOfWork, auditWriter);

            var command = new RegisterOwnerCommand(
                "Owner.Name@Example.com", "Owner Name", "+1-555-0100", "Cafe Name",
                "Str0ngP@ssw0rd!", "Admin", PlanVersionId);
            var result = await handler.Handle(command, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal("Onboarding.SlugInvalid", result.Error!.Code);
            Assert.Equal(ErrorType.Validation, result.Error.Type);
            Assert.Empty(store.Requests);
        }

        [Fact]
        public async Task Handle_TakenSlug_ReturnsConflict()
        {
            var store = new FakeOnboardingRequestStore();
            var slugAvailability = new FakeSlugAvailability { IsAvailable = false };
            var passwordHasher = new FakeOwnerPasswordHasher();
            var planRepository = new FakePlanRepository { PlanVersionsExist = true };
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();

            var handler = BuildHandler(store, slugAvailability, passwordHasher, planRepository, unitOfWork, auditWriter);

            var result = await handler.Handle(ValidCommand(), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal("Onboarding.SlugTaken", result.Error!.Code);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);
            Assert.Empty(store.Requests);
        }

        [Fact]
        public async Task Handle_ConcurrentGuardViolation_ReturnsExistingRequest()
        {
            var existing = ExistingRequest;
            var store = new FakeOnboardingRequestStore(existing) { SkipFirstFind = true };
            var slugAvailability = new FakeSlugAvailability();
            var passwordHasher = new FakeOwnerPasswordHasher();
            var planRepository = new FakePlanRepository { PlanVersionsExist = true };
            var unitOfWork = new FakeMasterUnitOfWork { ThrowOnCommit = new ThrowingPostgresException() };
            var auditWriter = new RecordingPlanAuditWriter();

            var handler = BuildHandler(store, slugAvailability, passwordHasher, planRepository, unitOfWork, auditWriter);

            var result = await handler.Handle(new RegisterOwnerCommand(
                "OWNER@EXAMPLE.COM", "Owner Name", "+1-555-0100", "Cafe Name",
                "Str0ngP@ssw0rd!", "my-cafe", PlanVersionId), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(existing.Status, result.Value!.State);
            Assert.True(result.Value.IsDuplicate);
            Assert.Equal(1, unitOfWork.RollbackCount);
        }

        [Fact]
        public async Task Handle_OtherDatabaseError_IsNotSwallowed()
        {
            var store = new FakeOnboardingRequestStore();
            var slugAvailability = new FakeSlugAvailability();
            var passwordHasher = new FakeOwnerPasswordHasher();
            var planRepository = new FakePlanRepository { PlanVersionsExist = true };
            var unitOfWork = new FakeMasterUnitOfWork { ThrowOnCommit = new InvalidOperationException("connection lost") };
            var auditWriter = new RecordingPlanAuditWriter();

            var handler = BuildHandler(store, slugAvailability, passwordHasher, planRepository, unitOfWork, auditWriter);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(ValidCommand(), CancellationToken.None));

            Assert.Equal(1, unitOfWork.RollbackCount);
        }

        [Fact]
        public void Validator_RejectsWeakPassword_WithoutEchoingIt()
        {
            var validator = new RegisterOwnerCommandValidator();
            var command = ValidCommand() with { Password = "weak" };

            var result = validator.TestValidate(command);

            result.ShouldHaveValidationErrorFor("Password");
            Assert.DoesNotContain("weak", result.Errors.Select(e => e.ErrorMessage), StringComparer.Ordinal);
        }

        [Fact]
        public void Validator_RejectsBadEmail()
        {
            var validator = new RegisterOwnerCommandValidator();
            var command = ValidCommand() with { OwnerEmail = "not-an-email" };

            var result = validator.TestValidate(command);

            result.ShouldHaveValidationErrorFor("OwnerEmail");
        }

        [Fact]
        public void Validator_RejectsOversizedName()
        {
            var validator = new RegisterOwnerCommandValidator();
            var command = ValidCommand() with { OwnerFullName = new string('a', 257) };

            var result = validator.TestValidate(command);

            result.ShouldHaveValidationErrorFor("OwnerFullName");
        }

        [Fact]
        public void Validator_DoesNotEchoPasswordInAnyError()
        {
            var validator = new RegisterOwnerCommandValidator();
            var command = ValidCommand() with
            {
                OwnerEmail = "bad",
                OwnerFullName = "",
                CafeName = "",
                Password = "",
                Slug = "",
                PlanVersionId = Guid.Empty
            };

            var result = validator.TestValidate(command);

            Assert.DoesNotContain(result.Errors, e => e.ErrorMessage.Contains("Str0ngP@ssw0rd!"));
        }
    }

    internal sealed class FakeSlugAvailability : ISlugAvailability
    {
        public bool IsAvailable { get; set; } = true;

        public Task<bool> IsAvailableAsync(string slug, CancellationToken cancellationToken)
        {
            return Task.FromResult(IsAvailable);
        }
    }

    internal sealed class DuplicateRegistrationDetectorForTests : IDuplicateRegistrationDetector
    {
        public bool IsPublicEmailGuardViolation(Exception exception)
        {
            return exception is ThrowingPostgresException;
        }

        public bool IsSlugTakenViolation(Exception exception)
        {
            return false;
        }

        public bool IsIdempotencyKeyViolation(Exception exception)
        {
            return exception is ThrowingPostgresException;
        }
    }

    internal sealed class ThrowingPostgresException : Exception
    {
        public ThrowingPostgresException() : base("duplicate key") { }
    }
}
