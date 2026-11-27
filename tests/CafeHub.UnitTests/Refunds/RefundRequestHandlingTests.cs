using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Application.Refunds;
using CafeHub.Application.Refunds.Commands;
using CafeHub.Application.Refunds.Contracts;
using CafeHub.Application.Refunds.DTOs;
using CafeHub.Application.Refunds.Queries;
using CafeHub.Domain.Tenants;
using FluentValidation.TestHelper;

namespace CafeHub.UnitTests.Refunds
{
    public sealed class RefundRequestHandlingTests
    {
        private static readonly Guid BranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid OtherBranchId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly Guid OwnerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid StaffId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        private static readonly DateTimeOffset Now = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Assignment_AllowsTheOwnerAndOnlyStaffOfTheRequestBranch()
        {
            Assert.True(RefundRequestAssignment.IsAllowed(TenantUserKind.Owner, null, BranchId));
            Assert.True(RefundRequestAssignment.IsAllowed(TenantUserKind.Owner, OtherBranchId, BranchId));
            Assert.True(RefundRequestAssignment.IsAllowed(TenantUserKind.Staff, BranchId, BranchId));
            Assert.False(RefundRequestAssignment.IsAllowed(TenantUserKind.Staff, OtherBranchId, BranchId));
            Assert.False(RefundRequestAssignment.IsAllowed(TenantUserKind.Staff, null, BranchId));
        }

        [Fact]
        public async Task List_ForcesTheStaffBranchAndPassesEveryFilterAndThePage()
        {
            var store = new FakeRefundRequestStore
            {
                ListPage = new RefundRequestListPage(
                    [ListItem(CreateRequest())],
                    null),
            };
            var handler = ListHandler(store, new TenantAccountAccess(true, "staff", true) { BranchId = BranchId });

            var result = await handler.Handle(new GetRefundRequestsQuery
            {
                Page = 2,
                PageSize = 5,
                IncludeTotalCount = true,
                BranchId = null,
                Status = RefundRequestStatus.Approved,
                AssignedUserId = StaffId,
                BillId = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            }, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(5, result.Value!.PageSize);
            Assert.Equal(2, result.Value.Page);
            Assert.Single(result.Value.Items);
            var request = Assert.IsType<RefundRequestListRequest>(store.LastListRequest);
            Assert.Equal(BranchId, request.BranchId);
            Assert.Equal(RefundRequestStatus.Approved, request.Status);
            Assert.Equal(StaffId, request.AssignedUserId);
            Assert.Equal(Guid.Parse("77777777-7777-7777-7777-777777777777"), request.BillId);
            Assert.True(request.IncludeTotalCount);
        }

        [Fact]
        public async Task List_RefusesAForeignBranchFilter()
        {
            var store = new FakeRefundRequestStore();
            var handler = ListHandler(store, new TenantAccountAccess(true, "staff", true) { BranchId = BranchId });

            var result = await handler.Handle(new GetRefundRequestsQuery { BranchId = OtherBranchId }, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(RefundRequestErrors.NotFoundCode, result.Error!.Code);
            Assert.Null(store.LastListRequest);
        }

        [Fact]
        public async Task List_LetsTheOwnerReadEveryBranchWhenNoFilterIsGiven()
        {
            var store = new FakeRefundRequestStore();
            var handler = ListHandler(store, new TenantAccountAccess(false, null, false));

            var result = await handler.Handle(new GetRefundRequestsQuery(), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Null(store.LastListRequest!.BranchId);
        }

        [Fact]
        public async Task Detail_ReturnsTheEventsAndAttachmentMetadataWithoutMediaKeys()
        {
            var request = CreateRequest();
            var attachment = RefundRequestAttachment.Create(
                Guid.CreateVersion7(),
                request.Id,
                "tenant-secret-media-key",
                "application/pdf",
                12,
                new string('a', 64),
                Now);
            Assert.True(request.AddAttachment(attachment));
            var store = new FakeRefundRequestStore { Request = request };
            var handler = new GetRefundRequestQueryHandler(store, CurrentUser(BranchId));

            var result = await handler.Handle(new GetRefundRequestQuery(request.Id), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            var detail = result.Value!;
            Assert.Equal(nameof(RefundRequestStatus.Open), detail.Status);
            var eventView = Assert.Single(detail.Events);
            Assert.Equal(nameof(RefundRequestEventKind.Submitted), eventView.Kind);
            var attachmentView = Assert.Single(detail.Attachments);
            Assert.Equal("application/pdf", attachmentView.ContentType);
            Assert.Equal(
                new[]
                {
                    nameof(RefundRequestAttachmentView.Id),
                    nameof(RefundRequestAttachmentView.ContentType),
                    nameof(RefundRequestAttachmentView.SizeBytes),
                    nameof(RefundRequestAttachmentView.CreatedAt),
                },
                typeof(RefundRequestAttachmentView).GetProperties().Select(property => property.Name).ToArray());
            Assert.Equal(BranchId, store.RequestBranchId);
        }

        [Fact]
        public async Task Detail_FromAnotherBranchIsTheUniformNotFound()
        {
            var store = new FakeRefundRequestStore { Request = null };
            var handler = new GetRefundRequestQueryHandler(
                store,
                CurrentUser(new TenantAccountAccess(true, "staff", true) { BranchId = BranchId }));

            var result = await handler.Handle(new GetRefundRequestQuery(Guid.CreateVersion7()), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(RefundRequestErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(BranchId, store.RequestBranchId);
        }

        [Fact]
        public async Task Assign_AcceptsABranchStaffMemberAppendsAnEventAndAuditsOnlyTheAssignee()
        {
            var request = CreateRequest();
            var store = new FakeRefundRequestStore { Request = request, Assignable = true };
            var audit = new RecordingAuditWriter();
            var handler = AssignHandler(store, audit, BranchId);

            var result = await handler.Handle(
                new AssignRefundRequestCommand { RefundRequestId = request.Id, AssignedUserId = StaffId },
                default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(new AssignRefundRequestResult(request.Id, StaffId, "Open"), result.Value);
            Assert.Equal(StaffId, request.AssignedUserId);
            Assert.Equal(1, store.SaveCalls);
            var appended = request.Events.Last();
            Assert.Equal(RefundRequestEventKind.Assigned, appended.Kind);
            Assert.Equal(StaffId, appended.ActorUserId);
            var entry = Assert.Single(audit.Entries);
            Assert.Equal("refund_requests.assigned.succeeded", entry.Action);
            Assert.Equal("refund_requests", entry.TargetType);
            var change = Assert.Single(entry.Changes!.Fields);
            Assert.Equal("assigned_user_id", change.Name);
            Assert.Null(change.Before);
            Assert.Equal(StaffId.ToString(), change.After);
        }

        [Fact]
        public async Task Assign_RejectsAUserOutsideTheRequestBranchWithoutSavingOrAuditing()
        {
            var request = CreateRequest();
            var store = new FakeRefundRequestStore { Request = request, Assignable = false };
            var audit = new RecordingAuditWriter();
            var handler = AssignHandler(store, audit, BranchId);

            var result = await handler.Handle(
                new AssignRefundRequestCommand { RefundRequestId = request.Id, AssignedUserId = StaffId },
                default);

            Assert.False(result.IsSuccess);
            Assert.Equal(RefundRequestErrors.AssignedStaffNotFoundCode, result.Error!.Code);
            Assert.Null(request.AssignedUserId);
            Assert.Equal(0, store.SaveCalls);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Status_ApprovalRequiresANoteAndWritesAnEventAndAStatusOnlyAudit()
        {
            var request = CreateRequest();
            var store = new FakeRefundRequestStore { Request = request };
            var audit = new RecordingAuditWriter();
            var handler = StatusHandler(store, audit, BranchId);

            var missingNote = await handler.Handle(
                new ChangeRefundRequestStatusCommand
                {
                    RefundRequestId = request.Id,
                    Status = "Approved",
                    Note = "   ",
                },
                default);
            Assert.False(missingNote.IsSuccess);
            Assert.Equal(RefundRequestErrors.StatusConflictCode, missingNote.Error!.Code);
            Assert.Empty(audit.Entries);

            var result = await handler.Handle(
                new ChangeRefundRequestStatusCommand
                {
                    RefundRequestId = request.Id,
                    Status = "Approved",
                    Note = "Refund agreed",
                },
                default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(new ChangeRefundRequestStatusResult(request.Id, "Approved", "Refund agreed", Now), result.Value);
            Assert.Equal(RefundRequestStatus.Approved, request.Status);
            var appended = request.Events.Last();
            Assert.Equal(RefundRequestEventKind.StatusChanged, appended.Kind);
            Assert.Equal(RefundRequestStatus.Open, appended.FromStatus);
            Assert.Equal(RefundRequestStatus.Approved, appended.ToStatus);
            var entry = Assert.Single(audit.Entries);
            Assert.Equal("refund_requests.status_changed.succeeded", entry.Action);
            var change = Assert.Single(entry.Changes!.Fields);
            Assert.Equal("status", change.Name);
            Assert.Equal("Open", change.Before);
            Assert.Equal("Approved", change.After);
        }

        [Fact]
        public async Task Status_ApprovalLeavesTheBillAndPaymentUntouched()
        {
            var bill = Bill();
            var payment = CafeHub.Domain.Tenants.Payment.ConfirmCash(
                Guid.CreateVersion7(),
                bill.Id,
                null,
                100m,
                OwnerId,
                "cash-key-0001",
                Now,
                OwnerId);
            var request = RefundRequest.Create(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                bill,
                payment,
                "QWERTYUIOPASDFGHJKLZXC",
                "refund-key-0002",
                "Delayed service",
                null,
                20m,
                0m,
                null,
                Now);
            var store = new FakeRefundRequestStore { Request = request };
            var handler = StatusHandler(store, new RecordingAuditWriter(), BranchId);

            var result = await handler.Handle(
                new ChangeRefundRequestStatusCommand
                {
                    RefundRequestId = request.Id,
                    Status = "Approved",
                    Note = "Refund agreed",
                },
                default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(BillStatus.Open, bill.Status);
            Assert.Equal(PaymentStatus.Confirmed, payment.Status);
            Assert.Equal(100m, payment.Amount);
            Assert.Equal(request.PaymentId, payment.Id);
            Assert.Equal(1, store.SaveCalls);
        }

        [Fact]
        public async Task Status_IsRefusedAfterADecisionEvenWithAValidNote()
        {
            var request = CreateRequest();
            Assert.True(request.TransitionTo(Guid.CreateVersion7(), RefundRequestStatus.Rejected, OwnerId, "Not eligible", Now));
            var store = new FakeRefundRequestStore { Request = request };
            var audit = new RecordingAuditWriter();
            var handler = StatusHandler(store, audit, BranchId);

            var result = await handler.Handle(
                new ChangeRefundRequestStatusCommand
                {
                    RefundRequestId = request.Id,
                    Status = "Approved",
                    Note = "Second decision",
                },
                default);

            Assert.False(result.IsSuccess);
            Assert.Equal(RefundRequestErrors.StatusConflictCode, result.Error!.Code);
            Assert.Equal(RefundRequestStatus.Rejected, request.Status);
            Assert.Equal(0, store.SaveCalls);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Status_LostConcurrencyRaceReturnsConflictAndDoesNotReportSuccess()
        {
            var request = CreateRequest();
            var store = new FakeRefundRequestStore
            {
                Request = request,
                SaveFailure = Result.Failure(RefundRequestErrors.ConcurrencyConflict()),
            };
            var handler = StatusHandler(store, new RecordingAuditWriter(), BranchId);

            var result = await handler.Handle(
                new ChangeRefundRequestStatusCommand
                {
                    RefundRequestId = request.Id,
                    Status = "InReview",
                },
                default);

            Assert.False(result.IsSuccess);
            Assert.Equal(RefundRequestErrors.ConcurrencyConflictCode, result.Error!.Code);
            Assert.Equal(1, store.SaveCalls);
        }

        [Fact]
        public void Validator_RejectsTheOpenTargetAndADecisionWithoutANote()
        {
            var validator = new ChangeRefundRequestStatusCommandValidator();

            validator.TestValidate(Command("Open", null))
                .ShouldHaveValidationErrorFor(command => command.Status);
            validator.TestValidate(Command("Approved", null))
                .ShouldHaveValidationErrorFor(command => command.Note);
            validator.TestValidate(Command("Rejected", " "))
                .ShouldHaveValidationErrorFor(command => command.Note);
            validator.TestValidate(Command("InReview", null)).ShouldNotHaveAnyValidationErrors();
            validator.TestValidate(Command("approved", "Agreed")).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public async Task Attachment_FromAnotherBranchIsTheUniformNotFound()
        {
            var store = new FakeRefundRequestStore { Attachment = null };
            var handler = new GetRefundRequestAttachmentQueryHandler(
                store,
                new ThrowingMediaStorage(),
                CurrentUser(BranchId),
                new StubTenantContext(Guid.CreateVersion7()));

            var result = await handler.Handle(
                new GetRefundRequestAttachmentQuery(Guid.CreateVersion7(), Guid.CreateVersion7()),
                default);

            Assert.False(result.IsSuccess);
            Assert.Equal(RefundRequestErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(BranchId, store.RequestBranchId);
        }

        private static GetRefundRequestsQueryHandler ListHandler(
            FakeRefundRequestStore store,
            TenantAccountAccess access) =>
            new(store, CurrentUser(access), new PaginationOptions());

        private static AssignRefundRequestCommandHandler AssignHandler(
            FakeRefundRequestStore store,
            RecordingAuditWriter audit,
            Guid branchId) =>
            new(store, audit, CurrentUser(branchId), new FixedTimeProvider(Now));

        private static ChangeRefundRequestStatusCommandHandler StatusHandler(
            FakeRefundRequestStore store,
            RecordingAuditWriter audit,
            Guid branchId) =>
            new(store, audit, CurrentUser(branchId), new FixedTimeProvider(Now));

        private static ChangeRefundRequestStatusCommand Command(string status, string? note) =>
            new()
            {
                RefundRequestId = Guid.CreateVersion7(),
                Status = status,
                Note = note,
            };

        private static FakeCurrentUser CurrentUser(Guid branchId) =>
            new(StaffId, new TenantAccountAccess(true, "staff", true) { BranchId = branchId });

        private static FakeCurrentUser CurrentUser(TenantAccountAccess access) => new(OwnerId, access);

        private static Bill Bill() =>
            CafeHub.Domain.Tenants.Bill.Create(Guid.CreateVersion7(), BranchId, null, "Abcdefghijklmnopqrstuv", Now, null);

        private static RefundRequestListItem ListItem(RefundRequest request) =>
            new(
                request.Id,
                request.BillId,
                request.PaymentId,
                request.BranchId,
                request.PublicReference,
                request.Status.ToString(),
                request.Reason,
                request.ClaimedAmount,
                request.GuestDisplayName,
                request.AssignedUserId,
                request.CreatedAt,
                request.UpdatedAt);

        private static RefundRequest CreateRequest()
        {
            var bill = Bill();
            var payment = CafeHub.Domain.Tenants.Payment.ConfirmCash(
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                bill.Id,
                null,
                100m,
                OwnerId,
                "cash-key-0001",
                Now,
                OwnerId);
            return RefundRequest.Create(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                bill,
                payment,
                "QWERTYUIOPASDFGHJKLZXC",
                "refund-key-0002",
                "Delayed service",
                "The order arrived late.",
                20m,
                0m,
                "Guest",
                Now);
        }

        private sealed class FakeCurrentUser(Guid? userId, TenantAccountAccess? access) : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId => userId;

            public Guid? TenantId => Guid.Parse("11111111-1111-1111-1111-111111111111");

            public IReadOnlyCollection<string> Roles => [];

            public Guid? BranchId => access?.BranchId;

            public TenantAccountAccess? AccountAccess => access;
        }

        private sealed class RecordingAuditWriter : ITenantAuditWriter
        {
            public List<TenantAuditEntry> Entries { get; } = [];

            public Task<Result> WriteAsync(TenantAuditEntry entry, CancellationToken cancellationToken)
            {
                Entries.Add(entry);
                return Task.FromResult(Result.Success());
            }
        }

        private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private sealed class StubTenantContext(Guid tenantId) : CafeHub.Application.Tenancy.ITenantContextAccessor
        {
            public CafeHub.Application.Tenancy.TenantContext GetRequired() =>
                CafeHub.Application.Tenancy.TenantContext.Create(tenantId, "tenant-secret-ref", true);
        }

        private sealed class ThrowingMediaStorage : CafeHub.Application.Media.Contracts.IMediaStorage
        {
            public Task<Result<CafeHub.Application.Media.Contracts.MediaStorageResult>> SaveAsync(
                CafeHub.Application.Media.MediaKey key,
                Stream content,
                CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<Stream?> OpenAsync(
                CafeHub.Application.Media.MediaKey key,
                CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<bool> ExistsAsync(
                CafeHub.Application.Media.MediaKey key,
                CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<bool> DeleteAsync(
                CafeHub.Application.Media.MediaKey key,
                CancellationToken cancellationToken) => throw new NotSupportedException();
        }

        private sealed class FakeRefundRequestStore : IRefundRequestStore
        {
            public RefundRequestListPage ListPage { get; set; } = new([], null);

            public RefundRequestListRequest? LastListRequest { get; private set; }

            public RefundRequest? Request { get; set; }

            public RefundRequestAttachment? Attachment { get; set; }

            public bool Assignable { get; set; }

            public Result? SaveFailure { get; set; }

            public int SaveCalls { get; private set; }

            public Guid? RequestBranchId { get; private set; }

            public Task<RefundRequestListPage> ListAsync(
                RefundRequestListRequest request,
                CancellationToken cancellationToken)
            {
                LastListRequest = request;
                return Task.FromResult(ListPage);
            }

            public Task<RefundRequest?> FindAsync(
                Guid? branchId,
                Guid refundRequestId,
                CancellationToken cancellationToken)
            {
                RequestBranchId = branchId;
                return Task.FromResult(Request);
            }

            public Task<RefundRequestAttachment?> FindAttachmentAsync(
                Guid? branchId,
                Guid refundRequestId,
                Guid attachmentId,
                CancellationToken cancellationToken)
            {
                RequestBranchId = branchId;
                return Task.FromResult(Attachment);
            }

            public Task<bool> IsAssignableStaffAsync(
                Guid branchId,
                Guid userId,
                CancellationToken cancellationToken) => Task.FromResult(Assignable);

            public Task<Result> SaveAsync(RefundRequest request, CancellationToken cancellationToken)
            {
                SaveCalls++;
                return Task.FromResult(SaveFailure ?? Result.Success());
            }

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
                CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<Result<RefundRequestInsertResult>> AddAsync(
                RefundRequest request,
                CancellationToken cancellationToken) => throw new NotSupportedException();
        }
    }
}
