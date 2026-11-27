using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Idempotency;
using CafeHub.Application.Media;
using CafeHub.Application.Media.Contracts;
using CafeHub.Application.Refunds;
using CafeHub.Application.Refunds.Commands;
using CafeHub.Application.Refunds.Contracts;
using CafeHub.Application.Refunds.DTOs;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.UnitTests.Media;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeHub.UnitTests.Refunds
{
    public sealed class GuestRefundRequestSubmissionTests
    {
        private const string BillReference = "Abcdefghijklmnopqrstuv";
        private const string IdempotencyKey = "refund-key-0001";
        private const long MaximumMediaBytes = 1024;
        private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid BranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid OwnerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly DateTimeOffset Now = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);
        private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
        private static readonly byte[] Pdf = "%PDF-1.7\n"u8.ToArray();

        [Fact]
        public async Task Submit_CreatesOpenRequestWithTenantScopedRefundEvidence()
        {
            var store = Store();
            var media = new FakeMediaStorage();
            var upload = Upload(media);

            var result = await Handler(store, upload, media).Handle(Command(Pdf), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(new SubmitGuestRefundRequestResult("ZYXWVUTSRQPONMLKJIHGFE", "Open"), result.Value);
            var request = Assert.Single(store.AddedRequests);
            Assert.Equal(RefundRequestStatus.Open, request.Status);
            var attachment = Assert.Single(request.Attachments);
            Assert.Equal("application/pdf", attachment.ContentType);
            Assert.True(MediaKey.TryParse(attachment.MediaKey, out var key));
            Assert.Equal(TenantId, key!.Scope.TenantId);
            var use = Assert.Single(upload.Uses);
            Assert.Equal(MediaPurpose.RefundEvidence, use.Purpose);
            Assert.Equal(TenantId, use.Scope.TenantId);
            Assert.Equal(1, media.Count);
        }

        [Fact]
        public async Task Submit_RepeatReturnsTheExistingRequestWithoutUploadingEvidence()
        {
            var store = Store();
            var existing = CreateRequest(store.Submission!.Bill, store.Submission.Payment!, RefundRequestStatus.Approved);
            store.Submission = store.Submission with { ExistingRequest = existing };
            var media = new FakeMediaStorage();
            var upload = Upload(media);

            var result = await Handler(store, upload, media).Handle(Command(Png), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(new SubmitGuestRefundRequestResult(existing.PublicReference, "Approved"), result.Value);
            Assert.Empty(store.AddedRequests);
            Assert.Empty(upload.Uses);
            Assert.True(media.IsEmpty);
        }

        [Fact]
        public async Task Submit_ReusedKeyOnAnotherBillIsRefused()
        {
            var store = Store();
            store.Submission = store.Submission! with { IdempotencyKeyUsedByAnotherBill = true };
            var media = new FakeMediaStorage();
            var upload = Upload(media);

            var result = await Handler(store, upload, media).Handle(Command(Png), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(IdempotencyErrors.KeyReused().Code, result.Error!.Code);
            Assert.Empty(upload.Uses);
            Assert.True(media.IsEmpty);
            Assert.Empty(store.AddedRequests);
        }

        [Fact]
        public async Task Submit_RejectsTheFourthOpenRequestBeforeUploadingEvidence()
        {
            var store = Store(openRequestCount: 3);
            var media = new FakeMediaStorage();
            var upload = Upload(media);

            var result = await Handler(store, upload, media).Handle(Command(Png), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(RefundRequestErrors.OpenRequestLimitReachedCode, result.Error!.Code);
            Assert.Empty(upload.Uses);
            Assert.Empty(store.AddedRequests);
        }

        [Fact]
        public async Task Submit_RejectsAClaimAboveThePaymentAmountAfterApprovedRefunds()
        {
            var store = Store(approvedRefundedAmount: 80m);
            var media = new FakeMediaStorage();
            var upload = Upload(media);

            var result = await Handler(store, upload, media).Handle(Command(Png, claimedAmount: 20.01m), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(RefundRequestErrors.AmountAboveRefundableCode, result.Error!.Code);
            Assert.Empty(upload.Uses);
            Assert.Empty(store.AddedRequests);
        }

        [Fact]
        public async Task Submit_RejectsAPaymentAwaitingReviewWithoutUploadingEvidence()
        {
            var store = Store();
            var pendingPayment = Payment.SubmitOnlineProof(
                Guid.CreateVersion7(),
                store.Submission!.Bill.Id,
                100m,
                "media-reference",
                null,
                "pending-key-0001",
                Now);
            store.Submission = store.Submission with { Payment = pendingPayment };
            var media = new FakeMediaStorage();
            var upload = Upload(media);

            var result = await Handler(store, upload, media).Handle(Command(Png), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(RefundRequestErrors.NotFoundCode, result.Error!.Code);
            Assert.Empty(upload.Uses);
            Assert.Empty(store.AddedRequests);
        }

        [Fact]
        public async Task Submit_RejectsPaymentFromAnotherBillWithoutUploadingEvidence()
        {
            var store = Store();
            var otherBill = Bill.Create(Guid.CreateVersion7(), BranchId, null, "ZYXWVUTSRQPONMLKJIHGFE", Now, null);
            var payment = Payment.ConfirmCash(
                Guid.CreateVersion7(),
                otherBill.Id,
                null,
                100m,
                OwnerId,
                "cash-key-0002",
                Now,
                OwnerId);
            store.Submission = store.Submission! with { Payment = payment };
            var media = new FakeMediaStorage();
            var upload = Upload(media);

            var result = await Handler(store, upload, media).Handle(Command(Png), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(RefundRequestErrors.NotFoundCode, result.Error!.Code);
            Assert.Empty(upload.Uses);
            Assert.Empty(store.AddedRequests);
        }

        [Fact]
        public async Task Submit_InvalidEvidenceRemovesFilesAlreadyUploadedForTheRequest()
        {
            var store = Store();
            var media = new FakeMediaStorage();
            var upload = Upload(media);
            var command = Command(Png) with
            {
                Evidence = [new(new MemoryStream(Png)), new(new MemoryStream([1, 2, 3]))],
            };

            var result = await Handler(store, upload, media).Handle(command, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(MediaErrors.InvalidFileCode, result.Error!.Code);
            Assert.Equal(2, upload.Uses.Count);
            Assert.True(media.IsEmpty);
            Assert.Empty(store.AddedRequests);
        }

        [Fact]
        public async Task Submit_IdempotencyRaceDeletesNewEvidenceAndReturnsTheStoredRequest()
        {
            var store = Store();
            var existing = CreateRequest(store.Submission!.Bill, store.Submission.Payment!, RefundRequestStatus.Open);
            store.DuplicateOnAdd = existing;
            var media = new FakeMediaStorage();
            var upload = Upload(media);

            var result = await Handler(store, upload, media).Handle(Command(Png), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(existing.PublicReference, result.Value!.PublicReference);
            Assert.True(media.IsEmpty);
        }

        [Fact]
        public void Validator_RejectsBadIdempotencyAmountAndTooManyEvidenceFiles()
        {
            var validator = new SubmitGuestRefundRequestCommandValidator();
            var command = Command(Png);

            validator.TestValidate(command with { IdempotencyKey = null })
                .ShouldHaveValidationErrorFor(value => value.IdempotencyKey);
            validator.TestValidate(command with { ClaimedAmount = 0m })
                .ShouldHaveValidationErrorFor(value => value.ClaimedAmount);
            validator.TestValidate(command with
            {
                Evidence = Enumerable.Range(0, RefundRequest.MaximumAttachmentCount + 1)
                        .Select(_ => new RefundRequestEvidence(new MemoryStream(Png)))
                        .ToArray(),
            })
                .ShouldHaveValidationErrorFor(value => value.Evidence);
        }

        private static SubmitGuestRefundRequestCommandHandler Handler(
            FakeRefundRequestStore store,
            RecordingUploadService upload,
            FakeMediaStorage media) =>
            new(
                store,
                upload,
                media,
                new FakeBillPublicReferenceGenerator(),
                new FakeTenantContextAccessor(TenantContext.Create(TenantId, "tenant-secret-ref", true)),
                new FixedTimeProvider(Now),
                NullLogger<SubmitGuestRefundRequestCommandHandler>.Instance);

        private static RecordingUploadService Upload(FakeMediaStorage media) =>
            new(new MediaUploadService(media, MaximumMediaBytes));

        private static FakeRefundRequestStore Store(
            decimal approvedRefundedAmount = 0m,
            int openRequestCount = 0)
        {
            var bill = Bill.Create(Guid.CreateVersion7(), BranchId, null, BillReference, Now, null);
            var payment = Payment.ConfirmCash(
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                bill.Id,
                null,
                100m,
                OwnerId,
                "cash-key-0001",
                Now,
                OwnerId);
            return new FakeRefundRequestStore
            {
                Submission = new RefundRequestSubmissionContext(
                    bill,
                    payment,
                    approvedRefundedAmount,
                    openRequestCount,
                    null,
                    false),
            };
        }

        private static RefundRequest CreateRequest(Bill bill, Payment payment, RefundRequestStatus status)
        {
            var request = RefundRequest.Create(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                bill,
                payment,
                "QWERTYUIOPASDFGHJKLZXC",
                "refund-key-0002",
                "Delayed service",
                null,
                10m,
                0m,
                null,
                Now);
            if (status == RefundRequestStatus.Approved)
            {
                request.TransitionTo(Guid.CreateVersion7(), status, OwnerId, "Reviewed", Now);
            }

            return request;
        }

        private static SubmitGuestRefundRequestCommand Command(byte[] content, decimal claimedAmount = 20m) =>
            new()
            {
                BillReference = BillReference,
                PaymentId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                ClaimedAmount = claimedAmount,
                Reason = "Delayed service",
                Details = "The order arrived late.",
                GuestDisplayName = "Guest",
                Evidence = [new(new MemoryStream(content))],
                IdempotencyKey = IdempotencyKey,
            };

        private sealed class FakeRefundRequestStore : IRefundRequestStore
        {
            public RefundRequestSubmissionContext? Submission { get; set; }

            public RefundRequest? DuplicateOnAdd { get; set; }

            public List<RefundRequest> AddedRequests { get; } = [];

            public Task<IReadOnlyList<GuestRefundablePayment>?> ListConfirmedPaymentsForBillAsync(
                string billReference,
                CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<GuestRefundRequestStatus?> FindGuestStatusAsync(
                string publicReference,
                CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<RefundRequestSubmissionContext?> FindForSubmissionAsync(
                string billReference,
                Guid paymentId,
                string idempotencyKey,
                CancellationToken cancellationToken) => Task.FromResult(Submission);

            public Task<Result<RefundRequestInsertResult>> AddAsync(
                RefundRequest request,
                CancellationToken cancellationToken)
            {
                AddedRequests.Add(request);
                return Task.FromResult(Result.Success(new RefundRequestInsertResult(
                    DuplicateOnAdd ?? request,
                    DuplicateOnAdd is not null)));
            }

            public Task<RefundRequestListPage> ListAsync(
                RefundRequestListRequest request,
                CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<RefundRequest?> FindAsync(
                Guid? branchId,
                Guid refundRequestId,
                CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<RefundRequestAttachment?> FindAttachmentAsync(
                Guid? branchId,
                Guid refundRequestId,
                Guid attachmentId,
                CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<bool> IsAssignableStaffAsync(
                Guid branchId,
                Guid userId,
                CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<Result> SaveAsync(
                RefundRequest request,
                CancellationToken cancellationToken) => throw new NotSupportedException();
        }

        private sealed class RecordingUploadService(IMediaUploadService inner) : IMediaUploadService
        {
            public List<(MediaScope Scope, MediaPurpose Purpose)> Uses { get; } = [];

            public Task<Result<MediaUploadResult>> UploadAsync(
                MediaScope scope,
                MediaPurpose purpose,
                Stream content,
                CancellationToken cancellationToken)
            {
                Uses.Add((scope, purpose));
                return inner.UploadAsync(scope, purpose, content, cancellationToken);
            }
        }

        private sealed class FakeBillPublicReferenceGenerator : IBillPublicReferenceGenerator
        {
            public string Generate() => "ZYXWVUTSRQPONMLKJIHGFE";
        }

        private sealed class FakeTenantContextAccessor(TenantContext context) : ITenantContextAccessor
        {
            public TenantContext GetRequired() => context;
        }

        private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }
    }
}
