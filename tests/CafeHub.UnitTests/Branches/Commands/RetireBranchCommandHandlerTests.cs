using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Branches;
using CafeHub.Application.Branches.Commands;
using CafeHub.Application.Branches.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Branches.Commands
{
    public sealed class RetireBranchCommandHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        private readonly FakeBranchStore store = new();
        private readonly FakeCurrentTenantUser currentTenantUser = new();
        private readonly StubTenantContextAccessor tenantContextAccessor = new(
            TenantContext.Create(TenantId, "tenant-secret-reference", true, "cafe-one"));
        private readonly RetireBranchCommandHandler handler;

        public RetireBranchCommandHandlerTests()
        {
            handler = new RetireBranchCommandHandler(store, currentTenantUser, tenantContextAccessor);
        }

        [Fact]
        public async Task Handle_RetiresActiveBranch_ReturnsSuccessAndWritesAudit()
        {
            store.GetByIdResponse = BranchResponse("Active");

            var result = await handler.Handle(
                new RetireBranchCommand(BranchId, "12345"),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Retired", result.Value!.LifecycleStatus);
            Assert.Equal(BranchId, Assert.Single(store.LifecycleChanges).BranchId);
            Assert.Equal(BranchLifecycleStatus.Retired, Assert.Single(store.LifecycleChanges).NewStatus);
            Assert.Equal("branches.retired.succeeded", Assert.Single(store.LifecycleChanges).AuditAction);
            Assert.Equal(UserId, Assert.Single(store.LifecycleChanges).ActorUserId);
        }

        [Fact]
        public async Task Handle_RetiresSuspendedBranch_ReturnsSuccessAndWritesAudit()
        {
            store.GetByIdResponse = BranchResponse("Suspended");

            var result = await handler.Handle(
                new RetireBranchCommand(BranchId, "12345"),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Retired", result.Value!.LifecycleStatus);
            Assert.Equal("branches.retired.succeeded", Assert.Single(store.LifecycleChanges).AuditAction);
        }

        [Fact]
        public async Task Handle_AlreadyRetired_IsIdempotent_NoSaveNoAudit()
        {
            store.GetByIdResponse = BranchResponse("Retired");

            var result = await handler.Handle(
                new RetireBranchCommand(BranchId, "12345"),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Retired", result.Value!.LifecycleStatus);
            Assert.Empty(store.LifecycleChanges);
        }

        [Fact]
        public async Task Handle_StaleToken_ReturnsConcurrencyConflict_NoAudit()
        {
            store.GetByIdResponse = BranchResponse("Active");
            store.Failure = BranchesErrors.ConcurrencyConflict();

            var result = await handler.Handle(
                new RetireBranchCommand(BranchId, "99999"),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(BranchesErrors.ConcurrencyConflictCode, result.Error!.Code);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);
            Assert.Empty(store.LifecycleChanges);
        }

        [Fact]
        public async Task Handle_MissingId_ReturnsNotFound()
        {
            store.GetByIdResponse = null;

            var result = await handler.Handle(
                new RetireBranchCommand(Guid.NewGuid(), "12345"),
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
                new RetireBranchCommand(BranchId, "12345"),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthErrors.CurrentTenantUserUnavailableCode, result.Error!.Code);
            Assert.Empty(store.LifecycleChanges);
        }

        [Fact]
        public void TheHandler_RequiresAllDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new RetireBranchCommandHandler(null!, currentTenantUser, tenantContextAccessor));
            Assert.Throws<ArgumentNullException>(() => new RetireBranchCommandHandler(store, null!, tenantContextAccessor));
            Assert.Throws<ArgumentNullException>(() => new RetireBranchCommandHandler(store, currentTenantUser, null!));
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
