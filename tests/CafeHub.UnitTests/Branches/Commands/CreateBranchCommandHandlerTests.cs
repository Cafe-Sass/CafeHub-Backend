using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Branches.Commands;
using CafeHub.Application.Branches.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;

namespace CafeHub.UnitTests.Branches.Commands
{
    public sealed class CreateBranchCommandHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        private readonly FakeBranchStore store = new();
        private readonly FakeCurrentTenantUser currentTenantUser = new();
        private readonly StubTenantContextAccessor tenantContextAccessor = new(
            TenantContext.Create(TenantId, "tenant-secret-reference", true, "cafe-one"));
        private readonly CreateBranchCommandHandler handler;

        public CreateBranchCommandHandlerTests()
        {
            handler = new CreateBranchCommandHandler(store, currentTenantUser, tenantContextAccessor);
        }

        [Fact]
        public async Task Handle_CreatesAnActiveBranchWithTheCurrentTimeAndActor()
        {
            var result = await handler.Handle(new CreateBranchCommand("Downtown Cafe", "123 Main St", "+1-555-0100", "Mon-Fri 8am-6pm"), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var response = result.Value;
            Assert.Equal("Downtown Cafe", response!.Name);
            Assert.Equal("Active", response.LifecycleStatus);
            Assert.Equal("123 Main St", response.Address);
            Assert.Equal("+1-555-0100", response.ContactPhone);
            Assert.Equal("Mon-Fri 8am-6pm", response.OpeningHoursSummary);
            Assert.NotEqual(Guid.Empty, response.Id);
            Assert.Equal("12345", response.ConcurrencyToken);

            var call = Assert.Single(store.Created);
            Assert.Equal("Downtown Cafe", call.Name);
            Assert.Equal("123 Main St", call.Address);
            Assert.Equal("+1-555-0100", call.ContactPhone);
            Assert.Equal("Mon-Fri 8am-6pm", call.OpeningHoursSummary);
            Assert.Equal(UserId, call.ActorUserId);
        }

        [Fact]
        public async Task Handle_NormalizesEmptyOptionalsToNull()
        {
            var result = await handler.Handle(new CreateBranchCommand("Downtown Cafe", "   ", "", null), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var call = Assert.Single(store.Created);
            Assert.Null(call.Address);
            Assert.Null(call.ContactPhone);
            Assert.Null(call.OpeningHoursSummary);
        }

        [Fact]
        public async Task Handle_WhenTheCurrentTenantUserIsUnavailable_ReturnsUnauthorized()
        {
            currentTenantUser.IsAuthenticated = false;

            var result = await handler.Handle(new CreateBranchCommand("Downtown Cafe", null, null, null), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthErrors.CurrentTenantUserUnavailableCode, result.Error!.Code);
            Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        }

        [Fact]
        public async Task Handle_WritesOneCreatedAuditEntry()
        {
            await handler.Handle(new CreateBranchCommand("Downtown Cafe", null, null, null), CancellationToken.None);

            Assert.Single(store.AuditEntries);
            var entry = Assert.Single(store.AuditEntries);
            Assert.Equal("branches.created.succeeded", entry.Action);
            Assert.Equal("branches", entry.TargetType);
            Assert.Equal(UserId, entry.ActorUserId);
        }

        [Fact]
        public async Task Handle_CreatingManyBranchesInARow_NeverFailsForCountReasons()
        {
            for (var i = 0; i < 200; i++)
            {
                var result = await handler.Handle(
                    new CreateBranchCommand($"Branch {i}", null, null, null),
                    CancellationToken.None);

                Assert.True(result.IsSuccess);
            }

            Assert.Equal(200, store.Created.Count);
        }

        [Fact]
        public void TheHandler_RequiresAllDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new CreateBranchCommandHandler(null!, currentTenantUser, tenantContextAccessor));
            Assert.Throws<ArgumentNullException>(() => new CreateBranchCommandHandler(store, null!, tenantContextAccessor));
            Assert.Throws<ArgumentNullException>(() => new CreateBranchCommandHandler(store, currentTenantUser, null!));
        }

        [Fact]
        public async Task Handle_WithANullCommand_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
        }
    }
}
