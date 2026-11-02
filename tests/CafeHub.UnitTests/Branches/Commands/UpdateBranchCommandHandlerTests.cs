using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Branches;
using CafeHub.Application.Branches.Commands;
using CafeHub.Application.Branches.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;

namespace CafeHub.UnitTests.Branches.Commands
{
    public sealed class UpdateBranchCommandHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        private readonly FakeBranchStore store = new();
        private readonly FakeCurrentTenantUser currentTenantUser = new();
        private readonly StubTenantContextAccessor tenantContextAccessor = new(
            TenantContext.Create(TenantId, "tenant-secret-reference", true, "cafe-one"));
        private readonly UpdateBranchCommandHandler handler;

        public UpdateBranchCommandHandlerTests()
        {
            handler = new UpdateBranchCommandHandler(store, currentTenantUser, tenantContextAccessor);
        }

        [Fact]
        public async Task Handle_ChangesOnlyTheFourEditableFieldsAndLeavesLifecycleStatusUnchanged()
        {
            store.UpdatedResponse = new BranchResponse
            {
                Id = BranchId,
                Name = "Downtown Cafe West",
                LifecycleStatus = "Active",
                Address = "456 Side St",
                ContactPhone = "+1-555-0101",
                OpeningHoursSummary = "Mon-Fri 9am-7pm",
                CreatedAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero),
                ConcurrencyToken = "67891",
            };

            var result = await handler.Handle(
                new UpdateBranchCommand(BranchId, "Downtown Cafe West", "456 Side St", "+1-555-0101", "Mon-Fri 9am-7pm", "12345"),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            var response = result.Value;
            Assert.Equal("Downtown Cafe West", response!.Name);
            Assert.Equal("456 Side St", response.Address);
            Assert.Equal("+1-555-0101", response.ContactPhone);
            Assert.Equal("Mon-Fri 9am-7pm", response.OpeningHoursSummary);
            Assert.Equal("Active", response.LifecycleStatus);

            var call = Assert.Single(store.Updated);
            Assert.Equal(BranchId, call.BranchId);
            Assert.Equal("Downtown Cafe West", call.Name);
            Assert.Equal("456 Side St", call.Address);
            Assert.Equal("+1-555-0101", call.ContactPhone);
            Assert.Equal("Mon-Fri 9am-7pm", call.OpeningHoursSummary);
            Assert.Equal(UserId, call.ActorUserId);
        }

        [Fact]
        public async Task Handle_StaleToken_ReturnsConcurrencyConflictAndDoesNotAudit()
        {
            store.Failure = BranchesErrors.ConcurrencyConflict();

            var result = await handler.Handle(
                new UpdateBranchCommand(BranchId, "Downtown Cafe West", null, null, null, "99999"),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(BranchesErrors.ConcurrencyConflictCode, result.Error!.Code);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);
            Assert.Empty(store.AuditEntries);
        }

        [Fact]
        public async Task Handle_NormalizesEmptyOptionalsToNull()
        {
            var result = await handler.Handle(
                new UpdateBranchCommand(BranchId, "Downtown Cafe West", "   ", "", null, "12345"),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            var call = Assert.Single(store.Updated);
            Assert.Null(call.Address);
            Assert.Null(call.ContactPhone);
            Assert.Null(call.OpeningHoursSummary);
        }

        [Fact]
        public async Task Handle_WhenTheCurrentTenantUserIsUnavailable_ReturnsUnauthorized()
        {
            currentTenantUser.IsAuthenticated = false;

            var result = await handler.Handle(new UpdateBranchCommand(BranchId, "Downtown Cafe West", null, null, null, "12345"), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthErrors.CurrentTenantUserUnavailableCode, result.Error!.Code);
        }

        [Fact]
        public async Task Handle_WritesOneUpdatedAuditEntry()
        {
            await handler.Handle(new UpdateBranchCommand(BranchId, "Downtown Cafe West", null, null, null, "12345"), CancellationToken.None);

            Assert.Single(store.AuditEntries);
            var entry = Assert.Single(store.AuditEntries);
            Assert.Equal("branches.updated.succeeded", entry.Action);
            Assert.Equal("branches", entry.TargetType);
            Assert.Equal(BranchId, entry.TargetId);
            Assert.Equal(UserId, entry.ActorUserId);
        }

        [Fact]
        public void TheHandler_RequiresAllDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new UpdateBranchCommandHandler(null!, currentTenantUser, tenantContextAccessor));
            Assert.Throws<ArgumentNullException>(() => new UpdateBranchCommandHandler(store, null!, tenantContextAccessor));
            Assert.Throws<ArgumentNullException>(() => new UpdateBranchCommandHandler(store, currentTenantUser, null!));
        }

        [Fact]
        public async Task Handle_WithANullCommand_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
        }
    }
}
