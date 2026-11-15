using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Staff;
using CafeHub.Application.Staff.Commands;
using CafeHub.Application.Tenancy;

namespace CafeHub.UnitTests.Staff.Commands
{
    public sealed class CreateStaffCommandHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        private readonly FakeStaffStore store = new();
        private readonly FakeStaffCurrentTenantUser currentUser = new();
        private readonly StubTenantContextAccessor contextAccessor = new(
            TenantContext.Create(TenantId, "tenant-secret-reference", true, "cafe-one"));
        private readonly CreateStaffCommandHandler handler;

        public CreateStaffCommandHandlerTests()
        {
            handler = new CreateStaffCommandHandler(store, currentUser, contextAccessor);
        }

        [Fact]
        public async Task Handle_CreatesStaffAndReturnsResponse()
        {
            var command = new CreateStaffCommand("Jane Doe", "jane@test.com", "+966500000001", BranchId, "Secret123!");

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var call = Assert.Single(store.Created);
            Assert.Equal("Jane Doe", call.FullName);
            Assert.Equal("jane@test.com", call.Email);
            Assert.Equal("+966500000001", call.PhoneNumber);
            Assert.Equal(BranchId, call.BranchId);
            Assert.Equal("Secret123!", call.RawPassword);
            Assert.Equal(UserId, call.ActorUserId);
        }

        [Fact]
        public async Task Handle_TrimsWhitespaceFromNameAndEmail()
        {
            var command = new CreateStaffCommand("  Jane Doe  ", "  jane@test.com  ", null, BranchId, "Secret123!");

            await handler.Handle(command, CancellationToken.None);

            var call = Assert.Single(store.Created);
            Assert.Equal("Jane Doe", call.FullName);
            Assert.Equal("jane@test.com", call.Email);
        }

        [Fact]
        public async Task Handle_NormalizesWhitespacePhoneToNull()
        {
            var command = new CreateStaffCommand("Jane Doe", "jane@test.com", "   ", BranchId, "Secret123!");

            await handler.Handle(command, CancellationToken.None);

            var call = Assert.Single(store.Created);
            Assert.Null(call.PhoneNumber);
        }

        [Fact]
        public async Task Handle_ReturnsFailure_WhenStoreReturnsEmailConflict()
        {
            store.Failure = StaffErrors.EmailConflict();
            var command = new CreateStaffCommand("Jane Doe", "jane@test.com", null, BranchId, "Secret123!");

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(StaffErrors.EmailConflictCode, result.Error.Code);
        }

        [Fact]
        public async Task Handle_ReturnsFailure_WhenNotAuthenticated()
        {
            currentUser.IsAuthenticated = false;
            var command = new CreateStaffCommand("Jane Doe", "jane@test.com", null, BranchId, "Secret123!");

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Empty(store.Created);
        }

        [Fact]
        public async Task Handle_ReturnsFailure_WhenTenantMismatch()
        {
            currentUser.TenantId = Guid.NewGuid();
            var command = new CreateStaffCommand("Jane Doe", "jane@test.com", null, BranchId, "Secret123!");

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Empty(store.Created);
        }
    }

    internal sealed class StubTenantContextAccessor(TenantContext tenantContext) : ITenantContextAccessor
    {
        public TenantContext GetRequired() => tenantContext;
    }
}
