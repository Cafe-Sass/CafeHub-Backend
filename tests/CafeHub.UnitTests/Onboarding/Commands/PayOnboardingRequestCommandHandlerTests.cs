using System.Linq;

using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Idempotency;
using CafeHub.Application.Onboarding.Commands;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Onboarding.PaymentGateway;
using CafeHub.Application.Plans.Contracts;
using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Plans;
using CafeHub.Domain.Subscriptions;
using CafeHub.UnitTests.Onboarding.Application;
using CafeHub.UnitTests.Plans;

namespace CafeHub.UnitTests.Onboarding.Commands
{
    public sealed class PayOnboardingRequestCommandHandlerTests
    {
        private static readonly Guid PlanVersionId = Guid.CreateVersion7();
        private static readonly Guid RequestId = Guid.CreateVersion7();

        private sealed class FakePaymentGateway : IPaymentGateway
        {
            public PaymentOutcome Outcome { get; set; } = PaymentOutcome.Succeeded;
            public string? FailureCode { get; set; }
            public int ChargeCallCount { get; private set; }
            public PaymentChargeRequest? LastRequest { get; private set; }

            public Task<Result<PaymentChargeResult>> ChargeAsync(PaymentChargeRequest request, CancellationToken cancellationToken)
            {
                ChargeCallCount++;
                LastRequest = request;
                var reference = $"mock_{Guid.CreateVersion7()}";
                if (Outcome == PaymentOutcome.Declined)
                {
                    return Task.FromResult(Result.Success<PaymentChargeResult>(
                        new PaymentChargeResult(PaymentOutcome.Declined, reference, FailureCode ?? "insufficient_funds")));
                }
                return Task.FromResult(Result.Success<PaymentChargeResult>(
                    new PaymentChargeResult(PaymentOutcome.Succeeded, reference, null)));
            }
        }

        private sealed class FakePaymentTransactionStore : IPaymentTransactionStore
        {
            private readonly List<PlatformPaymentTransaction> transactions = new();

            public IReadOnlyList<PlatformPaymentTransaction> Transactions => transactions.AsReadOnly();
            public int AddCallCount { get; private set; }
            public int FindByKeyCallCount { get; private set; }
            public int KeyExistsCallCount { get; private set; }

            public Task<PlatformPaymentTransaction?> FindByIdempotencyKeyAsync(Guid onboardingRequestId, string idempotencyKey, CancellationToken cancellationToken)
            {
                FindByKeyCallCount++;
                return Task.FromResult(transactions.FirstOrDefault(t =>
                    t.OnboardingRequestId == onboardingRequestId && t.IdempotencyKey == idempotencyKey));
            }

            public Task<bool> KeyExistsAsync(string idempotencyKey, CancellationToken cancellationToken)
            {
                KeyExistsCallCount++;
                return Task.FromResult(transactions.Any(t => t.IdempotencyKey == idempotencyKey));
            }

            public Task<PlatformPaymentTransaction?> FindByOnboardingRequestIdAsync(Guid onboardingRequestId, CancellationToken cancellationToken)
            {
                return Task.FromResult(transactions.OrderByDescending(t => t.CreatedAt).FirstOrDefault(t => t.OnboardingRequestId == onboardingRequestId));
            }

            public Task AddAsync(PlatformPaymentTransaction transaction, CancellationToken cancellationToken)
            {
                AddCallCount++;
                transactions.Add(transaction);
                return Task.CompletedTask;
            }

            internal void AddExisting(PlatformPaymentTransaction transaction)
            {
                transactions.Add(transaction);
            }
        }

        private static PayOnboardingRequestCommandHandler BuildHandler(
            FakeOnboardingRequestStore store,
            FakePaymentTransactionStore transactionStore,
            FakePaymentGateway paymentGateway,
            FakePlanRepository planRepository,
            IMasterUnitOfWork unitOfWork,
            RecordingPlanAuditWriter auditWriter,
            DuplicateRegistrationDetectorForTests detector,
            TimeProvider? timeProvider = null)
        {
            return new PayOnboardingRequestCommandHandler(
                store,
                transactionStore,
                paymentGateway,
                planRepository,
                unitOfWork,
                auditWriter,
                timeProvider ?? TimeProvider.System,
                detector);
        }

        private sealed class ThrowingMasterUnitOfWork : IMasterUnitOfWork
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

        private static PayOnboardingRequestCommand ValidCommand(string idempotencyKey = "key-12345678") =>
            new(RequestId) { IdempotencyKey = idempotencyKey };

        private static PlanVersion CreateActiveVersion(Guid planVersionId, decimal price = 49.99m, string currencyCode = "usd")
        {
            var plan = Plan.Create(Guid.CreateVersion7(), "starter", "Starter", true, TestData.CreatedAt);
            return plan.AddVersion(
                planVersionId,
                price,
                currencyCode,
                BillingPeriod.Monthly,
                PlanVersionStatus.Active,
                Array.Empty<PlanEntitlement>(),
                TestData.CreatedAt);
        }

        [Fact]
        public async Task Handle_Success_CreatesTransactionSubscriptionPeriod_AndMovesToPaidAwaitingReview()
        {
            var store = new FakeOnboardingRequestStore(
                OnboardingRequest.CreateForPublicRegistration(
                    RequestId, PlanVersionId, "owner@example.com", "Owner Name",
                    null, "Cafe Name", "hash", "my-cafe", TestData.CreatedAt));
            var transactionStore = new FakePaymentTransactionStore();
            var paymentGateway = new FakePaymentGateway { Outcome = PaymentOutcome.Succeeded };
            var planRepository = new FakePlanRepository { Plans = { CreatePlanWithVersion() } };
            var unitOfWork = new RecordingMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var detector = new DuplicateRegistrationDetectorForTests();

            var handler = BuildHandler(store, transactionStore, paymentGateway, planRepository, unitOfWork, auditWriter, detector);

            var result = await handler.Handle(ValidCommand(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var response = result.Value!;
            Assert.Equal(RequestId, response.RequestId);
            Assert.Equal(OnboardingRequestStatus.PaidAwaitingReview, response.State);
            Assert.Equal(PlatformPaymentTransactionStatus.Succeeded, response.PaymentStatus);

            Assert.Single(store.Requests);
            Assert.Equal(OnboardingRequestStatus.PaidAwaitingReview, store.Requests[0].Status);
            Assert.Single(transactionStore.Transactions);
            Assert.Equal(PlatformPaymentTransactionStatus.Succeeded, transactionStore.Transactions[0].Status);
            Assert.Equal(1, paymentGateway.ChargeCallCount);
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(1, unitOfWork.CommitCount);
            Assert.Equal(0, unitOfWork.RollbackCount);

            var auditEntry = Assert.Single(auditWriter.Entries);
            Assert.Equal("onboarding.payment.succeeded", auditEntry.Action);
            Assert.Equal(PlatformAuditActorKind.System, auditEntry.ActorKind);
            Assert.Equal(PlatformAuditOutcome.Succeeded, auditEntry.Outcome);
        }

        [Fact]
        public async Task Handle_Decline_RecordsFailedTransaction_AndKeepsRequestPayable()
        {
            var store = new FakeOnboardingRequestStore(
                OnboardingRequest.CreateForPublicRegistration(
                    RequestId, PlanVersionId, "owner@example.com", "Owner Name",
                    null, "Cafe Name", "hash", "my-cafe", TestData.CreatedAt));
            var transactionStore = new FakePaymentTransactionStore();
            var paymentGateway = new FakePaymentGateway { Outcome = PaymentOutcome.Declined, FailureCode = "insufficient_funds" };
            var planRepository = new FakePlanRepository { Plans = { CreatePlanWithVersion() } };
            var unitOfWork = new RecordingMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var detector = new DuplicateRegistrationDetectorForTests();

            var handler = BuildHandler(store, transactionStore, paymentGateway, planRepository, unitOfWork, auditWriter, detector);

            var result = await handler.Handle(ValidCommand(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var response = result.Value!;
            Assert.Equal(RequestId, response.RequestId);
            Assert.Equal(OnboardingRequestStatus.AwaitingPayment, response.State);
            Assert.Equal(PlatformPaymentTransactionStatus.Failed, response.PaymentStatus);

            Assert.Single(store.Requests);
            Assert.Equal(OnboardingRequestStatus.AwaitingPayment, store.Requests[0].Status);
            Assert.Single(transactionStore.Transactions);
            Assert.Equal(PlatformPaymentTransactionStatus.Failed, transactionStore.Transactions[0].Status);
            Assert.Equal("insufficient_funds", transactionStore.Transactions[0].FailureCode);
            Assert.Equal(1, paymentGateway.ChargeCallCount);
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(1, unitOfWork.CommitCount);

            var auditEntry = Assert.Single(auditWriter.Entries);
            Assert.Equal("onboarding.payment.failed", auditEntry.Action);
            Assert.Equal("insufficient_funds", auditEntry.Reason);
        }

        [Fact]
        public async Task Handle_Decline_SecondAttemptWithNewKey_CanSucceed()
        {
            var store = new FakeOnboardingRequestStore(
                OnboardingRequest.CreateForPublicRegistration(
                    RequestId, PlanVersionId, "owner@example.com", "Owner Name",
                    null, "Cafe Name", "hash", "my-cafe", TestData.CreatedAt));
            var transactionStore = new FakePaymentTransactionStore();
            var paymentGateway = new FakePaymentGateway { Outcome = PaymentOutcome.Declined };
            var planRepository = new FakePlanRepository { Plans = { CreatePlanWithVersion() } };
            var unitOfWork = new RecordingMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var detector = new DuplicateRegistrationDetectorForTests();

            var handler = BuildHandler(store, transactionStore, paymentGateway, planRepository, unitOfWork, auditWriter, detector);

            var firstResult = await handler.Handle(ValidCommand("key-first"), CancellationToken.None);
            Assert.True(firstResult.IsSuccess);
            Assert.Equal(PlatformPaymentTransactionStatus.Failed, firstResult.Value!.PaymentStatus);

            paymentGateway.Outcome = PaymentOutcome.Succeeded;
            var secondResult = await handler.Handle(ValidCommand("key-second"), CancellationToken.None);
            Assert.True(secondResult.IsSuccess);
            Assert.Equal(PlatformPaymentTransactionStatus.Succeeded, secondResult.Value!.PaymentStatus);
            Assert.Equal(OnboardingRequestStatus.PaidAwaitingReview, secondResult.Value.State);
        }

        [Fact]
        public async Task Handle_Replay_SameKey_ReturnsStoredOutcome_WithoutCallingGateway()
        {
            var existingTransaction = PlatformPaymentTransaction.Create(
                Guid.CreateVersion7(), RequestId, null, "key-12345678",
                49.99m, "usd", PlatformPaymentTransactionStatus.Succeeded,
                TestData.CreatedAt, null, TestData.CreatedAt);
            var store = new FakeOnboardingRequestStore(
                OnboardingRequest.CreateForPublicRegistration(
                    RequestId, PlanVersionId, "owner@example.com", "Owner Name",
                    null, "Cafe Name", "hash", "my-cafe", TestData.CreatedAt));
            var transactionStore = new FakePaymentTransactionStore();
            transactionStore.AddExisting(existingTransaction);
            var paymentGateway = new FakePaymentGateway();
            var planRepository = new FakePlanRepository { Plans = { CreatePlanWithVersion() } };
            var unitOfWork = new RecordingMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var detector = new DuplicateRegistrationDetectorForTests();

            var handler = BuildHandler(store, transactionStore, paymentGateway, planRepository, unitOfWork, auditWriter, detector);

            var result = await handler.Handle(ValidCommand(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var response = result.Value!;
            Assert.Equal(RequestId, response.RequestId);
            Assert.Equal(PlatformPaymentTransactionStatus.Succeeded, response.PaymentStatus);
            Assert.Equal(0, paymentGateway.ChargeCallCount);
            Assert.Equal(0, unitOfWork.BeginCount);
            Assert.Equal(0, transactionStore.AddCallCount);
        }

        [Fact]
        public async Task Handle_KeyReusedForDifferentRequest_ReturnsKeyReused()
        {
            var existingTransaction = PlatformPaymentTransaction.Create(
                Guid.CreateVersion7(), Guid.CreateVersion7(), null, "key-12345678",
                49.99m, "usd", PlatformPaymentTransactionStatus.Succeeded,
                TestData.CreatedAt, null, TestData.CreatedAt);
            var store = new FakeOnboardingRequestStore(
                OnboardingRequest.CreateForPublicRegistration(
                    RequestId, PlanVersionId, "owner@example.com", "Owner Name",
                    null, "Cafe Name", "hash", "my-cafe", TestData.CreatedAt));
            var transactionStore = new FakePaymentTransactionStore();
            transactionStore.AddExisting(existingTransaction);
            var paymentGateway = new FakePaymentGateway();
            var planRepository = new FakePlanRepository { Plans = { CreatePlanWithVersion() } };
            var unitOfWork = new RecordingMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var detector = new DuplicateRegistrationDetectorForTests();

            var handler = BuildHandler(store, transactionStore, paymentGateway, planRepository, unitOfWork, auditWriter, detector);

            var result = await handler.Handle(ValidCommand("key-12345678"), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(IdempotencyErrors.KeyReusedCode, result.Error!.Code);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);
            Assert.Equal(0, paymentGateway.ChargeCallCount);
            Assert.Equal(0, transactionStore.AddCallCount);
        }

        [Fact]
        public async Task Handle_UnknownRequest_ReturnsNotFound()
        {
            var store = new FakeOnboardingRequestStore();
            var transactionStore = new FakePaymentTransactionStore();
            var paymentGateway = new FakePaymentGateway();
            var planRepository = new FakePlanRepository { Plans = { CreatePlanWithVersion() } };
            var unitOfWork = new RecordingMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var detector = new DuplicateRegistrationDetectorForTests();

            var handler = BuildHandler(store, transactionStore, paymentGateway, planRepository, unitOfWork, auditWriter, detector);

            var result = await handler.Handle(ValidCommand(), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal("Onboarding.RequestNotFound", result.Error!.Code);
            Assert.Equal(ErrorType.NotFound, result.Error.Type);
            Assert.Equal(0, paymentGateway.ChargeCallCount);
            Assert.Equal(0, transactionStore.AddCallCount);
        }

        [Fact]
        public async Task Handle_AlreadyPaidRequest_ReturnsNotPayable()
        {
            var paidRequest = OnboardingRequest.CreateForPublicRegistration(
                RequestId, PlanVersionId, "owner@example.com", "Owner Name",
                null, "Cafe Name", "hash", "my-cafe", TestData.CreatedAt);
            paidRequest.TransitionTo(OnboardingRequestStatus.PaidAwaitingReview, TestData.CreatedAt);
            var store = new FakeOnboardingRequestStore(paidRequest);
            var transactionStore = new FakePaymentTransactionStore();
            var paymentGateway = new FakePaymentGateway();
            var planRepository = new FakePlanRepository { Plans = { CreatePlanWithVersion() } };
            var unitOfWork = new RecordingMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var detector = new DuplicateRegistrationDetectorForTests();

            var handler = BuildHandler(store, transactionStore, paymentGateway, planRepository, unitOfWork, auditWriter, detector);

            var result = await handler.Handle(ValidCommand(), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal("Onboarding.RequestNotPayable", result.Error!.Code);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);
            Assert.Equal(0, paymentGateway.ChargeCallCount);
            Assert.Equal(0, transactionStore.AddCallCount);
        }

        [Fact]
        public async Task Handle_ConcurrentIdempotencyKeyViolation_ReturnsStoredOutcome()
        {
            var existingTransaction = PlatformPaymentTransaction.Create(
                Guid.CreateVersion7(), RequestId, null, "key-12345678",
                49.99m, "usd", PlatformPaymentTransactionStatus.Succeeded,
                TestData.CreatedAt, null, TestData.CreatedAt);
            var store = new FakeOnboardingRequestStore(
                OnboardingRequest.CreateForPublicRegistration(
                    RequestId, PlanVersionId, "owner@example.com", "Owner Name",
                    null, "Cafe Name", "hash", "my-cafe", TestData.CreatedAt));
            var transactionStore = new FakePaymentTransactionStore();
            var paymentGateway = new FakePaymentGateway();
            var planRepository = new FakePlanRepository { Plans = { CreatePlanWithVersion() } };
            var unitOfWork = new ThrowingMasterUnitOfWork { ThrowOnCommit = new ThrowingPostgresException() };
            var auditWriter = new RecordingPlanAuditWriter();
            var detector = new DuplicateRegistrationDetectorForTests();

            var handler = BuildHandler(store, transactionStore, paymentGateway, planRepository, unitOfWork, auditWriter, detector);

            var result = await handler.Handle(ValidCommand(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var response = result.Value!;
            Assert.Equal(RequestId, response.RequestId);
            Assert.Equal(PlatformPaymentTransactionStatus.Succeeded, response.PaymentStatus);
            Assert.Equal(1, unitOfWork.RollbackCount);
        }

        [Fact]
        public async Task Handle_OtherCommitFailure_IsNotSwallowed()
        {
            var store = new FakeOnboardingRequestStore(
                OnboardingRequest.CreateForPublicRegistration(
                    RequestId, PlanVersionId, "owner@example.com", "Owner Name",
                    null, "Cafe Name", "hash", "my-cafe", TestData.CreatedAt));
            var transactionStore = new FakePaymentTransactionStore();
            var paymentGateway = new FakePaymentGateway();
            var planRepository = new FakePlanRepository { Plans = { CreatePlanWithVersion() } };
            var unitOfWork = new ThrowingMasterUnitOfWork { ThrowOnCommit = new InvalidOperationException("connection lost") };
            var auditWriter = new RecordingPlanAuditWriter();
            var detector = new DuplicateRegistrationDetectorForTests();

            var handler = BuildHandler(store, transactionStore, paymentGateway, planRepository, unitOfWork, auditWriter, detector);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(ValidCommand(), CancellationToken.None));

            Assert.Equal(1, unitOfWork.RollbackCount);
        }

        [Fact]
        public async Task Handle_AmountComesFromPlanVersion_NotFromCommand()
        {
            var store = new FakeOnboardingRequestStore(
                OnboardingRequest.CreateForPublicRegistration(
                    RequestId, PlanVersionId, "owner@example.com", "Owner Name",
                    null, "Cafe Name", "hash", "my-cafe", TestData.CreatedAt));
            var transactionStore = new FakePaymentTransactionStore();
            var paymentGateway = new FakePaymentGateway();
            var version = CreateActiveVersion(PlanVersionId, 99.99m, "eur");
            var planRepository = new FakePlanRepository { Plans = { CreatePlanWithVersion(version) } };
            var unitOfWork = new RecordingMasterUnitOfWork();
            var auditWriter = new RecordingPlanAuditWriter();
            var detector = new DuplicateRegistrationDetectorForTests();

            var handler = BuildHandler(store, transactionStore, paymentGateway, planRepository, unitOfWork, auditWriter, detector);

            var result = await handler.Handle(ValidCommand(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Single(transactionStore.Transactions);
            Assert.Equal(99.99m, transactionStore.Transactions[0].Amount);
            Assert.Equal("eur", transactionStore.Transactions[0].CurrencyCode);
            Assert.Equal(99.99m, paymentGateway.LastRequest!.Amount);
            Assert.Equal("eur", paymentGateway.LastRequest!.Currency);
        }

        private static Plan CreatePlanWithVersion(PlanVersion? version = null)
        {
            var plan = Plan.Create(Guid.CreateVersion7(), "starter", "Starter", true, TestData.CreatedAt);
            var v = version ?? CreateActiveVersion(PlanVersionId);
            plan.AddVersion(
                v.Id, v.Price, v.CurrencyCode, BillingPeriod.Monthly,
                PlanVersionStatus.Active, Array.Empty<PlanEntitlement>(), TestData.CreatedAt);
            return plan;
        }
    }

    internal static class TestData
    {
        public static readonly DateTimeOffset CreatedAt = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
    }
}
