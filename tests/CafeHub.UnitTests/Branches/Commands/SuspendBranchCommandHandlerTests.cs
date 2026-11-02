using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Branches;
using CafeHub.Application.Branches.Commands;
using CafeHub.Application.Branches.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Branches.Commands
{
    public sealed class SuspendBranchCommandHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        private readonly FakeBranchStore store = new();
        private readonly FakeCurrentTenantUser currentTenantUser = new();
        private readonly StubTenantContextAccessor tenantContextAccessor = new(
            TenantContext.Create(TenantId, "tenant-secret-reference", true, "cafe-one"));
        private readonly SuspendBranchCommandHandler handler;

        public SuspendBranchCommandHandlerTests()
        {
            handler = new SuspendBranchCommandHandler(store, currentTenantUser, tenantContextAccessor);
        }

        [Fact]
        public async Task Handle_SuspendsActiveBranch_ReturnsSuccessAndWritesAudit()
        {
            store.GetByIdResponse = BranchResponse("Active");

            var result = await handler.Handle(
                new SuspendBranchCommand(BranchId, "12345"),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Suspended", result.Value!.LifecycleStatus);
            Assert.Equal(BranchId, Assert.Single(store.LifecycleChanges).BranchId);
            Assert.Equal(BranchLifecycleStatus.Suspended, Assert.Single(store.LifecycleChanges).NewStatus);
            Assert.Equal("branches.suspended.succeeded", Assert.Single(store.LifecycleChanges).AuditAction);
            Assert.Equal(UserId, Assert.Single(store.LifecycleChanges).ActorUserId);
        }

        [Fact]
        public async Task Handle_AlreadySuspended_IsIdempotent_NoSaveNoAudit()
        {
            store.GetByIdResponse = BranchResponse("Suspended");

            var result = await handler.Handle(
                new SuspendBranchCommand(BranchId, "12345"),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Suspended", result.Value!.LifecycleStatus);
            Assert.Empty(store.LifecycleChanges);
        }

        [Fact]
        public async Task Handle_RetiredBranch_ReturnsIllegalTransition_NoAudit()
        {
            store.GetByIdResponse = BranchResponse("Retired");

            var result = await handler.Handle(
                new SuspendBranchCommand(BranchId, "12345"),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(BranchesErrors.IllegalTransitionCode, result.Error!.Code);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);
            Assert.Empty(store.LifecycleChanges);
        }

        [Fact]
        public async Task Handle_StaleToken_ReturnsConcurrencyConflict_NoAudit()
        {
            store.GetByIdResponse = BranchResponse("Active");
            store.Failure = BranchesErrors.ConcurrencyConflict();

            var result = await handler.Handle(
                new SuspendBranchCommand(BranchId, "99999"),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(BranchesErrors.ConcurrencyConflictCode, result.Error!.Code);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);
            Assert.Empty(store.LifecycleChanges);
        }

        [Fact]
        public async Task Handle_NonNumericToken_ReturnsValidationFieldError_NoAudit()
        {
            store.GetByIdResponse = BranchResponse("Active");

            var result = await handler.Handle(
                new SuspendBranchCommand(BranchId, "abc"),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.Validation, result.Error!.Type);
            Assert.Empty(store.LifecycleChanges);
        }

        [Fact]
        public async Task Handle_MissingId_ReturnsNotFound()
        {
            store.GetByIdResponse = null;

            var result = await handler.Handle(
                new SuspendBranchCommand(Guid.NewGuid(), "12345"),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(BranchesErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(ErrorType.NotFound, result.Error.Type);
            Assert.Empty(store.LifecycleChanges);
        }

        [Fact]
        public async Task Handle_WhenTheCurrentTenantUserIsUnavailable_ReturnsUnauthorized()
        {
            currentTenantUser.IsAuthenticated = false;
            store.GetByIdResponse = BranchResponse("Active");

            var result = await handler.Handle(
                new SuspendBranchCommand(BranchId, "12345"),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthErrors.CurrentTenantUserUnavailableCode, result.Error!.Code);
            Assert.Empty(store.LifecycleChanges);
        }

        [Fact]
        public void TheHandler_RequiresAllDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new SuspendBranchCommandHandler(null!, currentTenantUser, tenantContextAccessor));
            Assert.Throws<ArgumentNullException>(() => new SuspendBranchCommandHandler(store, null!, tenantContextAccessor));
            Assert.Throws<ArgumentNullException>(() => new SuspendBranchCommandHandler(store, currentTenantUser, null!));
        }

        [Fact]
        public async Task Handle_WithANullCommand_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
        }

        private static BranchResponse BranchResponse(string lifecycleStatus)
        {
            return new BranchResponse
            {
                Id = BranchId,
                Name = "Branch",
                LifecycleStatus = lifecycleStatus,
                Address = null,
                ContactPhone = null,
                OpeningHoursSummary = null,
                CreatedAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                ConcurrencyToken = "12345",
            };
        }
    }
}
