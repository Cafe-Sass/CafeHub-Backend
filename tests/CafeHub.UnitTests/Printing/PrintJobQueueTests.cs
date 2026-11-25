using System.Reflection;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Printing;
using CafeHub.Application.Printing.Commands;
using CafeHub.Application.Printing.Contracts;
using CafeHub.Application.Printing.DTOs;
using CafeHub.Application.Printing.Queries;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Printing
{
    public sealed class PrintJobQueueTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OtherBranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid PrinterId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid JobId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly Guid ConnectorId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly Guid SourceId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-25T09:00:00Z");

        [Fact]
        public async Task ClaimReturnsOneJobAndSetsTheLease()
        {
            var job = QueuedJob();
            var jobs = new FakeJobStore(Now) { NextJob = job };
            var connectors = new FakeConnectorStore();
            var handler = new ClaimPrintJobCommandHandler(jobs, connectors, new FixedClock(Now));

            var result = await handler.Handle(new ClaimPrintJobCommand(ConnectorId, BranchId), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.NotNull(result.Value);
            Assert.Equal(JobId, result.Value!.Id);
            Assert.Equal(BranchId, jobs.LastClaimedBranch);
            Assert.Equal(TimeSpan.FromSeconds(60), jobs.LastLease);
            Assert.Equal(PrintJobStatus.Claimed, job.Status);
            Assert.Equal(Now.AddSeconds(60), job.LeaseExpiresAt);
            Assert.Equal(ConnectorId, connectors.LastTouchedConnectorId);
        }

        [Fact]
        public async Task ClaimReturnsNothingWhenTheQueueIsEmpty()
        {
            var jobs = new FakeJobStore(Now);
            var handler = new ClaimPrintJobCommandHandler(jobs, new FakeConnectorStore(), new FixedClock(Now));

            var result = await handler.Handle(new ClaimPrintJobCommand(ConnectorId, BranchId), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Null(result.Value);
        }

        [Fact]
        public void AnExpiredLeaseReturnsTheJobToTheQueue()
        {
            var job = QueuedJob();
            job.TryClaim(Now, PrintJob.DefaultLease);

            Assert.False(job.RequeueExpiredLease(Now.AddSeconds(59)));
            Assert.True(job.RequeueExpiredLease(Now.AddSeconds(61)));
            Assert.Equal(PrintJobStatus.Queued, job.Status);
            Assert.Null(job.LeaseExpiresAt);
        }

        [Fact]
        public async Task TwoConnectorsNeverClaimTheSameJob()
        {
            var job = QueuedJob();
            var jobs = new FakeJobStore(Now) { NextJob = job };
            var connectorStore = new FakeConnectorStore();
            var clock = new FixedClock(Now);
            var handler = new ClaimPrintJobCommandHandler(jobs, connectorStore, clock);

            var first = await handler.Handle(new ClaimPrintJobCommand(ConnectorId, BranchId), default);
            var second = await handler.Handle(new ClaimPrintJobCommand(ConnectorId, BranchId), default);

            Assert.NotNull(first.Value);
            Assert.Null(second.Value);
            Assert.Equal(2, jobs.ClaimCalls);
            Assert.Equal(1, jobs.Claims);
        }

        [Fact]
        public void AJobIsNeverSucceededWithoutAReport()
        {
            var job = QueuedJob();

            Assert.False(job.ReportSucceeded(Now));
            Assert.Equal(PrintJobStatus.Queued, job.Status);

            job.TryClaim(Now, PrintJob.DefaultLease);
            Assert.Equal(PrintJobStatus.Claimed, job.Status);
            Assert.NotEqual(PrintJobStatus.Succeeded, job.Status);

            Assert.True(job.ReportSucceeded(Now.AddSeconds(1)));
            Assert.Equal(PrintJobStatus.Succeeded, job.Status);
            Assert.Null(job.LeaseExpiresAt);
        }

        [Fact]
        public void CancellingAClaimedJobNeverMarksItPrinted()
        {
            var job = QueuedJob();
            job.TryClaim(Now, PrintJob.DefaultLease);

            Assert.True(job.Cancel(Now.AddSeconds(1)));

            Assert.Equal(PrintJobStatus.Cancelled, job.Status);
            Assert.False(job.ReportSucceeded(Now.AddSeconds(2)));
            Assert.Equal(PrintJobStatus.Cancelled, job.Status);
        }

        [Fact]
        public void FailuresBelowTheBoundReturnTheJobToQueued()
        {
            var job = QueuedJob();

            job.TryClaim(Now, PrintJob.DefaultLease);
            Assert.True(job.ReportFailed(PrintFailureCodes.PaperOut, Now.AddSeconds(1)));
            Assert.Equal(PrintJobStatus.Queued, job.Status);
            Assert.Equal(1, job.Attempts);
            Assert.Equal(PrintFailureCodes.PaperOut, job.LastErrorCode);
            Assert.Null(job.LeaseExpiresAt);

            job.TryClaim(Now.AddMinutes(1), PrintJob.DefaultLease);
            Assert.True(job.ReportFailed(PrintFailureCodes.Unknown, Now.AddMinutes(1)));
            Assert.Equal(PrintJobStatus.Queued, job.Status);
            Assert.Equal(2, job.Attempts);
        }

        [Fact]
        public void TheThirdFailureMarksTheJobFailed()
        {
            var job = QueuedJob();

            for (var attempt = 1; attempt <= PrintJob.DefaultMaxAttempts; attempt++)
            {
                var claimedAt = Now.AddMinutes(attempt);
                job.TryClaim(claimedAt, PrintJob.DefaultLease);
                Assert.True(job.ReportFailed(PrintFailureCodes.PrinterOffline, claimedAt));
            }

            Assert.Equal(PrintJobStatus.Failed, job.Status);
            Assert.Equal(PrintJob.DefaultMaxAttempts, job.Attempts);
            Assert.Equal(PrintFailureCodes.PrinterOffline, job.LastErrorCode);
        }

        [Fact]
        public void RequeueingAFailedJobResetsAttemptsAndTheLastError()
        {
            var job = QueuedJob();
            while (job.Attempts < job.MaxAttempts)
            {
                job.TryClaim(Now, PrintJob.DefaultLease);
                job.ReportFailed(PrintFailureCodes.PayloadRejected, Now);
            }

            Assert.Equal(PrintJobStatus.Failed, job.Status);
            Assert.True(job.Requeue(Now));

            Assert.Equal(PrintJobStatus.Queued, job.Status);
            Assert.Equal(0, job.Attempts);
            Assert.Null(job.LastErrorCode);
            Assert.Null(job.LeaseExpiresAt);
            Assert.False(job.Requeue(Now));
        }

        [Fact]
        public void TheClosedFailureCodeListIsEnforced()
        {
            Assert.Equal(
                new[] { "PrinterOffline", "PaperOut", "PayloadRejected", "Unknown" },
                PrintFailureCodes.All);
            Assert.False(PrintFailureCodes.IsSupported("PaperJam"));

            var job = QueuedJob();
            job.TryClaim(Now, PrintJob.DefaultLease);
            Assert.False(job.ReportFailed("PaperJam", Now));
            Assert.Equal(0, job.Attempts);

            var validator = new ReportPrintJobFailedCommandValidator();
            validator.TestValidate(new ReportPrintJobFailedCommand(JobId, BranchId, "PaperOut"))
                .ShouldNotHaveAnyValidationErrors();
            validator.TestValidate(new ReportPrintJobFailedCommand(JobId, BranchId, "PaperJam"))
                .ShouldHaveValidationErrorFor(command => command.FailureCode);
            validator.TestValidate(new ReportPrintJobFailedCommand(JobId, BranchId, null))
                .ShouldHaveValidationErrorFor(command => command.FailureCode);
        }

        [Fact]
        public async Task StaffJobFiltersAreParsedAndUnknownOnesAreRefused()
        {
            var store = new FakeJobStore(Now);
            var handler = new ListPrintJobsQueryHandler(store, StaffUser());

            var filtered = await handler.Handle(
                new ListPrintJobsQuery(null, "failed", PrinterId, "receipt"),
                default);
            var unknownStatus = await handler.Handle(
                new ListPrintJobsQuery(null, "printed", null, null),
                default);
            var unknownType = await handler.Handle(
                new ListPrintJobsQuery(null, null, null, "Invoice"),
                default);

            Assert.True(filtered.IsSuccess, filtered.Error?.Code);
            Assert.Equal(PrintJobStatus.Failed, store.LastStatus);
            Assert.Equal(PrinterId, store.LastPrinterId);
            Assert.Equal(PrintDocumentType.Receipt, store.LastDocumentType);
            Assert.Equal(PrintingErrors.InvalidJobFilterCode, unknownStatus.Error!.Code);
            Assert.Equal(PrintingErrors.InvalidJobFilterCode, unknownType.Error!.Code);
        }

        [Fact]
        public async Task TheOwnerMustPassABranchAndStaffArePinnedToTheirBranch()
        {
            var store = new FakeJobStore(Now);
            var ownerMissing = await new ListPrintJobsQueryHandler(store, OwnerUser())
                .Handle(new ListPrintJobsQuery(null, null, null, null), default);
            var staffList = await new ListPrintJobsQueryHandler(store, StaffUser())
                .Handle(new ListPrintJobsQuery(OtherBranchId, null, null, null), default);

            Assert.Equal(PrintingErrors.BranchRequiredCode, ownerMissing.Error!.Code);
            Assert.True(staffList.IsSuccess, staffList.Error?.Code);
            Assert.Equal(BranchId, store.LastListedBranch);
        }

        [Fact]
        public async Task AnotherBranchesStaffCannotControlAJob()
        {
            var store = new FakeJobStore(Now);
            var handler = new CancelPrintJobCommandHandler(store, StaffUser(OtherBranchId));

            var result = await handler.Handle(new CancelPrintJobCommand(JobId, BranchId), default);

            Assert.Equal(PrintingErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(0, store.CancelCalls);
        }

        [Fact]
        public async Task RequeueingAFailedJobIsDelegatedOnce()
        {
            var store = new FakeJobStore(Now);
            var handler = new RequeuePrintJobCommandHandler(store, StaffUser());

            var result = await handler.Handle(new RequeuePrintJobCommand(JobId, BranchId), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(1, store.RequeueCalls);
        }

        [Fact]
        public void TheJobAuditSnapshotHoldsOnlyTheAllowListedFields()
        {
            var snapshot = TenantAuditSnapshotSerializer.Create(
                "print_jobs",
                TenantAuditChanges.Update(
                [
                    new("Status", "Failed", "Queued"),
                    new("Attempts", "3", "0"),
                    new("LastErrorCode", "PaperOut", null),
                    new("Key", null, "never"),
                ]));

            Assert.True(snapshot.IsSuccess, snapshot.Error?.Code);
            var afterState = snapshot.Value.AfterState!;
            Assert.Contains("\"Status\":\"Queued\"", afterState, StringComparison.Ordinal);
            Assert.Contains("\"Attempts\":\"0\"", afterState, StringComparison.Ordinal);
            Assert.DoesNotContain("never", afterState, StringComparison.Ordinal);
        }

        [Fact]
        public void TheJobEndpointsRequirePrintersManageAndTheExpectedRoutes()
        {
            Assert.Equal(
                "t/{slug}/api/v1/branches/{branchId:guid}/print-jobs",
                typeof(TenantPrintJobsController).GetCustomAttribute<RouteAttribute>()!.Template);

            foreach (var actionName in new[]
            {
                nameof(TenantPrintJobsController.List),
                nameof(TenantPrintJobsController.Cancel),
                nameof(TenantPrintJobsController.Requeue),
            })
            {
                var action = typeof(TenantPrintJobsController).GetMethod(actionName)!;
                Assert.Equal(
                    TenantPermissions.PrintersManage,
                    Assert.IsType<TenantAuthorizeAttribute>(
                        action.GetCustomAttribute<TenantAuthorizeAttribute>()).Policy);
                Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            }

            Assert.Equal(
                "t/{slug}/api/v1/branches/{branchId:guid}/print-connectors",
                typeof(TenantPrintConnectorsController).GetCustomAttribute<RouteAttribute>()!.Template);
            foreach (var actionName in new[]
            {
                nameof(TenantPrintConnectorsController.List),
                nameof(TenantPrintConnectorsController.Create),
                nameof(TenantPrintConnectorsController.Revoke),
            })
            {
                var action = typeof(TenantPrintConnectorsController).GetMethod(actionName)!;
                Assert.Equal(
                    TenantPermissions.PrintersManage,
                    Assert.IsType<TenantAuthorizeAttribute>(
                        action.GetCustomAttribute<TenantAuthorizeAttribute>()).Policy);
            }
        }

        private static PrintJob QueuedJob() =>
            PrintJob.Create(
                JobId,
                BranchId,
                PrinterId,
                PrintDocumentType.OrderTicket,
                "Order",
                SourceId,
                "{\"documentType\":\"OrderTicket\",\"sourceId\":\"66666666-6666-6666-6666-666666666666\",\"lines\":[]}",
                Now);

        private static FakeTenantUser StaffUser(Guid? branchId = null) =>
            new(new TenantAccountAccess(true, null, true) { BranchId = branchId ?? BranchId });

        private static FakeTenantUser OwnerUser() =>
            new(new TenantAccountAccess(false, null, true));

        private sealed class FakeTenantUser(TenantAccountAccess accountAccess) : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId => Guid.Parse("77777777-7777-7777-7777-777777777777");

            public Guid? TenantId => Guid.Parse("88888888-8888-8888-8888-888888888888");

            public IReadOnlyCollection<string> Roles => [];

            public Guid? BranchId => accountAccess.BranchId;

            public TenantAccountAccess? AccountAccess => accountAccess;
        }

        private sealed class FixedClock(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private sealed class FakeConnectorStore : IPrintConnectorStore
        {
            public Guid? LastTouchedConnectorId { get; private set; }

            public Task TouchLastSeenAsync(Guid connectorId, DateTimeOffset now, CancellationToken cancellationToken)
            {
                LastTouchedConnectorId = connectorId;
                return Task.CompletedTask;
            }

            public Task<PrintConnectorAccess?> FindByKeyHashAsync(
                string keyHash,
                CancellationToken cancellationToken) =>
                Task.FromResult<PrintConnectorAccess?>(null);

            public Task<Result<PrintConnectorResponse>> CreateAsync(
                Guid branchId,
                string name,
                string keyHash,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<IReadOnlyList<PrintConnectorResponse>> ListAsync(
                Guid branchId,
                CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<PrintConnectorResponse>>([]);

            public Task<Result<PrintConnectorResponse>> RevokeAsync(
                Guid connectorId,
                Guid branchId,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();
        }

        private sealed class FakeJobStore(DateTimeOffset now) : IPrintJobOperationsStore
        {
            public PrintJob? NextJob { get; set; }

            public Guid? LastClaimedBranch { get; private set; }

            public TimeSpan LastLease { get; private set; }

            public Guid? LastListedBranch { get; private set; }

            public PrintJobStatus? LastStatus { get; private set; }

            public Guid? LastPrinterId { get; private set; }

            public PrintDocumentType? LastDocumentType { get; private set; }

            public int ClaimCalls { get; private set; }

            public int Claims { get; private set; }

            public int CancelCalls { get; private set; }

            public int RequeueCalls { get; private set; }

            public Task<PrintJob?> ClaimNextAsync(
                Guid branchId,
                TimeSpan lease,
                CancellationToken cancellationToken)
            {
                ClaimCalls++;
                LastClaimedBranch = branchId;
                LastLease = lease;
                var job = NextJob;
                NextJob = null;
                if (job is null || job.BranchId != branchId || !job.TryClaim(now, lease))
                {
                    return Task.FromResult<PrintJob?>(null);
                }

                Claims++;
                return Task.FromResult<PrintJob?>(job);
            }

            public Task<Result<PrintJobResponse>> ReportSucceededAsync(
                Guid jobId,
                Guid branchId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<Result<PrintJobResponse>> ReportFailedAsync(
                Guid jobId,
                Guid branchId,
                string failureCode,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<Result<PrintJobResponse>> CancelAsync(
                Guid jobId,
                Guid branchId,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                CancelCalls++;
                var job = NextJob ?? QueuedJob();
                return Task.FromResult(Result.Success(PrintJobResponse.From(job)));
            }

            public Task<Result<PrintJobResponse>> RequeueAsync(
                Guid jobId,
                Guid branchId,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                RequeueCalls++;
                return Task.FromResult(Result.Success(PrintJobResponse.From(QueuedJob())));
            }

            public Task<IReadOnlyList<PrintJobResponse>> ListAsync(
                Guid branchId,
                PrintJobStatus? status,
                Guid? printerId,
                PrintDocumentType? documentType,
                CancellationToken cancellationToken)
            {
                LastListedBranch = branchId;
                LastStatus = status;
                LastPrinterId = printerId;
                LastDocumentType = documentType;
                return Task.FromResult<IReadOnlyList<PrintJobResponse>>([]);
            }
        }
    }
}
