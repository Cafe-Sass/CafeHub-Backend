using System.Reflection;
using System.Threading.RateLimiting;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Printing;
using CafeHub.Application.Printing.Commands;
using CafeHub.Application.Printing.Contracts;
using CafeHub.Application.Printing.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using CafeHub.WebApi.RateLimiting;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CafeHub.UnitTests.Printing
{
    public sealed class PrintConnectorTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OtherBranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid ConnectorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid PrinterId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly Guid JobId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-25T10:00:00Z");

        [Fact]
        public async Task AValidKeyAuthenticatesItsConnectorAndBranch()
        {
            var key = ConnectorKeys.Generate();
            var store = new FakeConnectorStore(Access(key, PrintConnectorStatus.Active));

            var result = await new PrintConnectorAuthenticator(store).AuthenticateAsync(key, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(ConnectorId, result.Value.Id);
            Assert.Equal(BranchId, result.Value.BranchId);
        }

        [Fact]
        public async Task MalformedUnknownAndRevokedKeysAreRefusedUniformly()
        {
            var key = ConnectorKeys.Generate();
            var active = new FakeConnectorStore(Access(key, PrintConnectorStatus.Active));
            var revoked = new FakeConnectorStore(Access(key, PrintConnectorStatus.Revoked));
            var authenticator = new PrintConnectorAuthenticator(active);

            var refusals = new[]
            {
                await authenticator.AuthenticateAsync(null, default),
                await authenticator.AuthenticateAsync(string.Empty, default),
                await authenticator.AuthenticateAsync("not-a-connector-key", default),
                await authenticator.AuthenticateAsync(new string('a', ConnectorKeys.KeyLength), default),
                await new PrintConnectorAuthenticator(revoked).AuthenticateAsync(key, default),
            };

            Assert.All(refusals, refusal =>
            {
                Assert.False(refusal.IsSuccess);
                Assert.Equal(PrintingErrors.ConnectorUnauthorizedCode, refusal.Error!.Code);
                Assert.Equal(ErrorType.Unauthorized, refusal.Error.Type);
            });
        }

        [Fact]
        public async Task AKeyOnlyReachesJobsInItsOwnBranch()
        {
            var job = PrintJob.Create(
                JobId,
                OtherBranchId,
                PrinterId,
                PrintDocumentType.OrderTicket,
                "Order",
                Guid.Parse("66666666-6666-6666-6666-666666666666"),
                "{\"lines\":[]}",
                Now);
            var jobs = new FakeJobStore(job);

            var ownBranch = await new ClaimPrintJobCommandHandler(jobs, new FakeConnectorStore(null), new FixedClock(Now))
                .Handle(new ClaimPrintJobCommand(ConnectorId, BranchId), default);
            var otherBranch = await new ClaimPrintJobCommandHandler(jobs, new FakeConnectorStore(null), new FixedClock(Now))
                .Handle(new ClaimPrintJobCommand(ConnectorId, OtherBranchId), default);

            Assert.Null(ownBranch.Value);
            Assert.NotNull(otherBranch.Value);
            Assert.Equal(OtherBranchId, otherBranch.Value!.BranchId);
        }

        [Fact]
        public async Task TheKeyIsReturnedOnceAndOnlyItsHashIsStored()
        {
            var store = new FakeConnectorStore(null);
            var handler = new CreatePrintConnectorCommandHandler(store, StaffUser());

            var result = await handler.Handle(
                new CreatePrintConnectorCommand(BranchId, "Kitchen PC"),
                default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            var created = result.Value;
            Assert.True(ConnectorKeys.IsWellFormed(created.Key));
            Assert.Equal(ConnectorKeys.Hash(created.Key), store.LastKeyHash);
            Assert.NotEqual(created.Key, store.LastKeyHash);
            Assert.Equal(ConnectorKeys.KeyHashLength, store.LastKeyHash!.Length);
            Assert.DoesNotContain(
                "KeyHash",
                typeof(PrintConnectorCreatedResponse).GetProperties().Select(property => property.Name));
        }

        [Fact]
        public void ConnectorKeysAreRandomAndWellFormed()
        {
            var first = ConnectorKeys.Generate();
            var second = ConnectorKeys.Generate();

            Assert.NotEqual(first, second);
            Assert.True(ConnectorKeys.IsWellFormed(first));
            Assert.True(ConnectorKeys.IsWellFormed(first.ToUpperInvariant()));
            Assert.False(ConnectorKeys.IsWellFormed(null));
            Assert.False(ConnectorKeys.IsWellFormed(first[..^1]));
            Assert.Equal(ConnectorKeys.KeyHashLength, ConnectorKeys.Hash(first).Length);
            Assert.True(ConnectorKeys.Matches(ConnectorKeys.Hash(first), ConnectorKeys.Hash(first)));
            Assert.False(ConnectorKeys.Matches(ConnectorKeys.Hash(first), ConnectorKeys.Hash(second)));
        }

        [Fact]
        public void ConnectorCreationRequiresTheNameAndBranch()
        {
            var validator = new CreatePrintConnectorCommandValidator();
            validator.TestValidate(new CreatePrintConnectorCommand(BranchId, "Kitchen PC"))
                .ShouldNotHaveAnyValidationErrors();
            validator.TestValidate(new CreatePrintConnectorCommand(Guid.Empty, "Kitchen PC"))
                .ShouldHaveValidationErrorFor(command => command.BranchId);
            validator.TestValidate(new CreatePrintConnectorCommand(BranchId, " "))
                .ShouldHaveValidationErrorFor(command => command.Name);
            validator.TestValidate(new CreatePrintConnectorCommand(BranchId, new string('a', 65)))
                .ShouldHaveValidationErrorFor(command => command.Name);

            var revoke = new RevokePrintConnectorCommandValidator();
            revoke.TestValidate(new RevokePrintConnectorCommand(ConnectorId, BranchId))
                .ShouldNotHaveAnyValidationErrors();
            revoke.TestValidate(new RevokePrintConnectorCommand(Guid.Empty, BranchId))
                .ShouldHaveValidationErrorFor(command => command.ConnectorId);
        }

        [Fact]
        public async Task AnotherBranchesStaffCannotRegisterAConnector()
        {
            var store = new FakeConnectorStore(null);
            var handler = new CreatePrintConnectorCommandHandler(store, StaffUser(OtherBranchId));

            var result = await handler.Handle(
                new CreatePrintConnectorCommand(BranchId, "Kitchen PC"),
                default);

            Assert.Equal(PrintingErrors.NotFoundCode, result.Error!.Code);
            Assert.Null(store.LastKeyHash);
        }

        [Fact]
        public void TheConnectorAuditNeverHoldsTheKeyOrItsHash()
        {
            var snapshot = TenantAuditSnapshotSerializer.Create(
                "print_connectors",
                TenantAuditChanges.Create(
                [
                    new("Name", null, "Kitchen PC"),
                    new("Status", null, "Active"),
                    new("BranchId", null, BranchId.ToString()),
                    new("KeyHash", null, ConnectorKeys.Hash(ConnectorKeys.Generate())),
                ]));

            Assert.True(snapshot.IsSuccess, snapshot.Error?.Code);
            var afterState = snapshot.Value.AfterState!;
            Assert.Contains("\"Name\":\"Kitchen PC\"", afterState, StringComparison.Ordinal);
            Assert.Contains("\"Status\":\"Active\"", afterState, StringComparison.Ordinal);
            Assert.DoesNotContain("KeyHash", afterState, StringComparison.Ordinal);
        }

        [Fact]
        public void TheConnectorEndpointsAreAnonymousToTheUserPipelineAndRateLimitedPerKey()
        {
            var controller = typeof(TenantPrintConnectorController);
            Assert.Equal(
                "t/{slug}/api/v1/connector/print",
                controller.GetCustomAttribute<RouteAttribute>()!.Template);
            Assert.NotNull(controller.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Null(controller.GetCustomAttribute<TenantAuthorizeAttribute>());

            var serviceFilter = Assert.IsType<ServiceFilterAttribute>(
                controller.GetCustomAttribute<ServiceFilterAttribute>());
            Assert.Equal(typeof(ConnectorKeyAuthorizationFilter), serviceFilter.ServiceType);
            Assert.Null(controller.GetCustomAttribute<EnableRateLimitingAttribute>());

            foreach (var actionName in new[]
            {
                nameof(TenantPrintConnectorController.Claim),
                nameof(TenantPrintConnectorController.Succeeded),
                nameof(TenantPrintConnectorController.Failed),
            })
            {
                var action = controller.GetMethod(actionName)!;
                Assert.Null(action.GetCustomAttribute<TenantAuthorizeAttribute>());
                Assert.NotNull(action.GetCustomAttribute<HttpPostAttribute>());
            }

            Assert.Equal("X-Connector-Key", ConnectorKeyAuthorizationFilter.HeaderName);
        }

        [Fact]
        public void ThePerKeyLimiterRejectsAfterTheBudgetAndKeepsKeysSeparate()
        {
            using var limiter = new ConnectorRateLimiter();
            var firstKey = ConnectorRateLimiting.GetPartitionKey(Context(ConnectorKeys.Generate()));
            var secondKey = ConnectorRateLimiting.GetPartitionKey(Context(ConnectorKeys.Generate()));
            for (var attempt = 0; attempt < ConnectorRateLimiting.PermitLimit; attempt++)
            {
                using var lease = limiter.Acquire(firstKey);
                Assert.True(lease.IsAcquired);
            }

            using var rejected = limiter.Acquire(firstKey);
            using var otherKey = limiter.Acquire(secondKey);

            Assert.False(rejected.IsAcquired);
            Assert.True(otherKey.IsAcquired);
        }

        [Fact]
        public void TheConnectorRateLimitPartitionIsPerKeyAndNeverTheRawKey()
        {
            var first = ConnectorKeys.Generate();
            var second = ConnectorKeys.Generate();
            var firstContext = Context(first);
            var secondContext = Context(second);
            var missingContext = new DefaultHttpContext();

            var firstKey = ConnectorRateLimiting.GetPartitionKey(firstContext);
            var secondKey = ConnectorRateLimiting.GetPartitionKey(secondContext);
            var unknown = ConnectorRateLimiting.GetPartitionKey(missingContext);

            Assert.NotEqual(firstKey, secondKey);
            Assert.NotEqual(first, firstKey);
            Assert.Equal(ConnectorRateLimiting.UnknownPartitionKey, unknown);
            Assert.Equal(ConnectorKeys.Hash(first), firstKey);
        }

        private static DefaultHttpContext Context(string key)
        {
            var context = new DefaultHttpContext();
            context.Request.Headers[ConnectorKeyAuthorizationFilter.HeaderName] = key;
            return context;
        }

        private static PrintConnectorAccess Access(string key, PrintConnectorStatus status) =>
            new(ConnectorId, BranchId, "Kitchen PC", ConnectorKeys.Hash(key), status);

        private static FakeTenantUser StaffUser(Guid? branchId = null) =>
            new(new TenantAccountAccess(true, null, true) { BranchId = branchId ?? BranchId });

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

        private sealed class FakeConnectorStore(PrintConnectorAccess? access) : IPrintConnectorStore
        {
            public string? LastKeyHash { get; private set; }

            public Task<PrintConnectorAccess?> FindByKeyHashAsync(
                string keyHash,
                CancellationToken cancellationToken) =>
                Task.FromResult(access is not null && access.KeyHash == keyHash ? access : null);

            public Task TouchLastSeenAsync(
                Guid connectorId,
                DateTimeOffset now,
                CancellationToken cancellationToken) =>
                Task.CompletedTask;

            public Task<Result<PrintConnectorResponse>> CreateAsync(
                Guid branchId,
                string name,
                string keyHash,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                LastKeyHash = keyHash;
                return Task.FromResult(Result.Success(new PrintConnectorResponse(
                    ConnectorId,
                    branchId,
                    name.Trim(),
                    PrintConnectorStatus.Active.ToString(),
                    null,
                    Now)));
            }

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

        private sealed class FakeJobStore(PrintJob? job) : IPrintJobOperationsStore
        {
            public Task<PrintJob?> ClaimNextAsync(
                Guid branchId,
                TimeSpan lease,
                CancellationToken cancellationToken)
            {
                if (job is null || job.BranchId != branchId || !job.TryClaim(Now, lease))
                {
                    return Task.FromResult<PrintJob?>(null);
                }

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
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<Result<PrintJobResponse>> RequeueAsync(
                Guid jobId,
                Guid branchId,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<IReadOnlyList<PrintJobResponse>> ListAsync(
                Guid branchId,
                PrintJobStatus? status,
                Guid? printerId,
                PrintDocumentType? documentType,
                CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<PrintJobResponse>>([]);
        }
    }
}
