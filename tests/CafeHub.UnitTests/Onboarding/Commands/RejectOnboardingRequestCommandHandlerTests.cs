using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Onboarding;
using CafeHub.Application.Onboarding.Commands;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Onboarding.DTOs;
using CafeHub.Domain.Onboarding;
using CafeHub.UnitTests.Onboarding.Application;
using CafeHub.UnitTests.Plans;
using FluentValidation.TestHelper;

namespace CafeHub.UnitTests.Onboarding.Commands
{
    public sealed class RejectOnboardingRequestCommandHandlerTests
    {
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

        private static RejectOnboardingRequestCommandHandler BuildHandler(
            FakeOnboardingRequestStore store,
            FakeMasterUnitOfWork unitOfWork,
            RecordingPlanAuditWriter auditWriter,
            FakeCurrentUser currentUser,
            TimeProvider? timeProvider = null)
        {
            return new RejectOnboardingRequestCommandHandler(
                store,
                unitOfWork,
                auditWriter,
                currentUser,
                timeProvider ?? TimeProvider.System);
        }

        [Fact]
        public async Task Handle_Success_RecordsReviewerTimeReasonAndAudit_NoSideEffects()
        {
            var request = PaidRequest();
            var now = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

            var store = new FakeOnboardingRequestStore(request);
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var currentUser = new FakeCurrentUser { AdminId = ReviewerId };

            var handler = BuildHandler(store, unitOfWork, auditWriter, currentUser, new FakeTimeProvider(now));

            var result = await handler.Handle(
                new RejectOnboardingRequestCommand(RequestId, "  cafes not viable  "),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            var response = result.Value!;
            Assert.Equal(RequestId, response.RequestId);
            Assert.Equal(OnboardingRequestStatus.Rejected.ToString(), response.State);

            // Request rejected with reviewer, time, and trimmed reason
            Assert.Equal(OnboardingRequestStatus.Rejected, request.Status);
            Assert.Equal(ReviewerId, request.ReviewedBy);
            Assert.Equal(now, request.ReviewedAt);
            Assert.Equal("cafes not viable", request.RejectionReason);

            // Audit entry written without the reason text
            Assert.Single(auditWriter.Entries);
            Assert.Equal("onboarding.rejected.succeeded", auditWriter.Entries[0].Action);
            Assert.Equal(PlatformAuditActorKind.Admin, auditWriter.Entries[0].ActorKind);
            Assert.Equal(PlatformAuditOutcome.Succeeded, auditWriter.Entries[0].Outcome);
            Assert.Null(auditWriter.Entries[0].Reason);
            Assert.DoesNotContain("cafes", auditWriter.Entries[0].Action, StringComparison.OrdinalIgnoreCase);

            // Transaction used
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(1, unitOfWork.CommitCount);
            Assert.Equal(0, unitOfWork.RollbackCount);
        }

        [Fact]
        public async Task Handle_EmptyReason_IsAllowed()
        {
            var request = PaidRequest();

            var store = new FakeOnboardingRequestStore(request);
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var currentUser = new FakeCurrentUser { AdminId = ReviewerId };

            var handler = BuildHandler(store, unitOfWork, auditWriter, currentUser);

            var result = await handler.Handle(
                new RejectOnboardingRequestCommand(RequestId, ""),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(string.Empty, request.RejectionReason);
        }

        [Fact]
        public async Task Handle_RepeatedRejection_IsIdempotent()
        {
            var request = PaidRequest();
            request.Reject(ReviewerId, new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero), "First reason");

            var store = new FakeOnboardingRequestStore(request);
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var currentUser = new FakeCurrentUser { AdminId = ReviewerId };

            var handler = BuildHandler(store, unitOfWork, auditWriter, currentUser);

            var result = await handler.Handle(
                new RejectOnboardingRequestCommand(RequestId, "Second reason"),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            // No new update or audit write for idempotent repeat
            Assert.Equal(0, store.UpdateCallCount);
            Assert.Equal(0, unitOfWork.BeginCount);
            Assert.Empty(auditWriter.Entries);
        }

        [Fact]
        public async Task Handle_ApprovedRequest_ReturnsRequestNotRejectable()
        {
            var request = PaidRequest();
            request.Approve(ReviewerId, new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero));

            var store = new FakeOnboardingRequestStore(request);
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var currentUser = new FakeCurrentUser { AdminId = ReviewerId };

            var handler = BuildHandler(store, unitOfWork, auditWriter, currentUser);

            var result = await handler.Handle(
                new RejectOnboardingRequestCommand(RequestId, "Reason"),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(OnboardingErrors.RequestNotRejectableCode, result.Error!.Code);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);

            // Nothing changed
            Assert.Equal(0, store.UpdateCallCount);
            Assert.Equal(0, unitOfWork.BeginCount);
        }

        [Theory]
        [InlineData(OnboardingRequestStatus.AwaitingPayment)]
        [InlineData(OnboardingRequestStatus.PaymentFailed)]
        [InlineData(OnboardingRequestStatus.Cancelled)]
        public async Task Handle_NotReadyForReview_ReturnsRequestNotRejectable(OnboardingRequestStatus status)
        {
            var request = PaidRequest();
            request.TransitionTo(status, new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero));

            var store = new FakeOnboardingRequestStore(request);
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var currentUser = new FakeCurrentUser { AdminId = ReviewerId };

            var handler = BuildHandler(store, unitOfWork, auditWriter, currentUser);

            var result = await handler.Handle(
                new RejectOnboardingRequestCommand(RequestId, "Reason"),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(OnboardingErrors.RequestNotRejectableCode, result.Error!.Code);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);

            // Nothing changed
            Assert.Equal(0, store.UpdateCallCount);
            Assert.Equal(0, unitOfWork.BeginCount);
        }

        [Fact]
        public async Task Handle_UnknownRequest_ReturnsRequestNotFound()
        {
            var store = new FakeOnboardingRequestStore();
            var unitOfWork = new FakeMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var currentUser = new FakeCurrentUser { AdminId = ReviewerId };

            var handler = BuildHandler(store, unitOfWork, auditWriter, currentUser);

            var result = await handler.Handle(
                new RejectOnboardingRequestCommand(Guid.CreateVersion7(), "Reason"),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(OnboardingErrors.RequestNotFoundCode, result.Error!.Code);
            Assert.Equal(ErrorType.NotFound, result.Error.Type);

            Assert.Equal(0, store.UpdateCallCount);
            Assert.Equal(0, unitOfWork.BeginCount);
        }

        [Fact]
        public void Validator_RejectsEmptyRequestId()
        {
            var validator = new RejectOnboardingRequestCommandValidator();
            var command = new RejectOnboardingRequestCommand(Guid.Empty, "Reason");

            var result = validator.TestValidate(command);

            result.ShouldHaveValidationErrorFor("RequestId");
        }

        [Fact]
        public void Validator_RejectsReasonOver500Chars()
        {
            var validator = new RejectOnboardingRequestCommandValidator();
            var longReason = new string('a', 501);
            var command = new RejectOnboardingRequestCommand(RequestId, longReason);

            var result = validator.TestValidate(command);

            result.ShouldHaveValidationErrorFor("Reason");
        }

        [Fact]
        public void Validator_AllowsReasonUpTo500Chars()
        {
            var validator = new RejectOnboardingRequestCommandValidator();
            var maxReason = new string('a', 500);
            var command = new RejectOnboardingRequestCommand(RequestId, maxReason);

            var result = validator.TestValidate(command);

            result.ShouldNotHaveValidationErrorFor("Reason");
        }

        [Fact]
        public void Validator_AllowsEmptyReason()
        {
            var validator = new RejectOnboardingRequestCommandValidator();
            var command = new RejectOnboardingRequestCommand(RequestId, "");

            var result = validator.TestValidate(command);

            result.ShouldNotHaveValidationErrorFor("Reason");
        }

        private sealed class FakeTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;
            public FakeTimeProvider(DateTimeOffset now) => _now = now;
            public override DateTimeOffset GetUtcNow() => _now;
        }
    }
}
