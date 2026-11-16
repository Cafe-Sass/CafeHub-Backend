using CafeHub.Application.Abstractions.Behaviors;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Tables;
using CafeHub.Application.Tables.Commands;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tables.Queries;
using CafeHub.Application.Tenancy;
using CafeHub.UnitTests.Branches;

namespace CafeHub.UnitTests.Tables
{
    public sealed class TableQrHandlerTests
    {
        private const string Code = "Abcdefghijklmnopqrstuv";
        private readonly Guid branchId = Guid.NewGuid();
        private readonly Guid tableId = Guid.NewGuid();
        private readonly FakeTableStore store = new();
        private readonly FakeTask42Access user = new();

        [Fact]
        public async Task ManagementHandlers_DenyAStaffMemberFromAnotherBranchBeforeReadingOrWriting()
        {
            user.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = Guid.NewGuid() };
            var read = await new GetTableQrQueryHandler(store, user).Handle(new(branchId, tableId), default);
            var sheet = await new GetTableQrSheetQueryHandler(store, user).Handle(new(branchId), default);
            var rotation = await new RegenerateTableQrCommandHandler(store, user, Tenant())
                .Handle(new(branchId, tableId, "7"), default);

            Assert.Equal(TablesErrors.NotFound(), read.Error);
            Assert.Equal(TablesErrors.NotFound(), sheet.Error);
            Assert.Equal(TablesErrors.NotFound(), rotation.Error);
            Assert.Empty(store.QrReads);
            Assert.Empty(store.QrSheetReads);
            Assert.Empty(store.QrRotations);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ManagementHandlers_PassTheRouteBranchAndActorForAuthorizedScope(bool staff)
        {
            user.AccountAccess = new TenantAccountAccess(staff, null, true) { BranchId = staff ? branchId : null };
            var table = new TableQrData(tableId, 4, null, Code, 8);
            store.QrData = table;
            store.QrSheet = [];

            var read = await new GetTableQrQueryHandler(store, user).Handle(new(branchId, tableId), default);
            var sheet = await new GetTableQrSheetQueryHandler(store, user).Handle(new(branchId), default);
            var rotation = await new RegenerateTableQrCommandHandler(store, user, Tenant())
                .Handle(new(branchId, tableId, "7"), default);

            Assert.Same(table, read.Value);
            Assert.Empty(sheet.Value);
            Assert.Same(table, rotation.Value);
            Assert.Equal((branchId, tableId), Assert.Single(store.QrReads));
            Assert.Equal(branchId, Assert.Single(store.QrSheetReads));
            Assert.Equal((branchId, tableId, "7", user.UserId!.Value), Assert.Single(store.QrRotations));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("too-short")]
        [InlineData("Abcdefghijklmnopqrstu/")]
        [InlineData("Abcdefghijklmnopqrstué")]
        public async Task Resolve_MalformedCodeReturnsNotFoundWithoutReadingTables(string? code)
        {
            var handler = new ResolveGuestQrQueryHandler(store, Tenant());

            var result = await handler.Handle(new(code), default);

            Assert.Equal(GuestQrErrors.NotFound(), result.Error);
            Assert.Empty(store.GuestQrReads);
        }

        [Fact]
        public async Task Resolve_PassesTheExactCaseAndReturnsOnlyGuestContext()
        {
            store.GuestContext = new GuestQrContext("Cafe", branchId, "Downtown", tableId, 4);

            var result = await new ResolveGuestQrQueryHandler(store, Tenant()).Handle(new(Code), default);

            Assert.Same(store.GuestContext, result.Value);
            Assert.Equal(Code, Assert.Single(store.GuestQrReads));
        }

        [Fact]
        public async Task Resolve_UnknownAndInactiveCafeUseTheSameError()
        {
            var unknown = await new ResolveGuestQrQueryHandler(store, Tenant()).Handle(new(Code), default);
            store.GuestQrReads.Clear();
            var inactive = await new ResolveGuestQrQueryHandler(store, Tenant(active: false)).Handle(new(Code), default);

            Assert.Equal(GuestQrErrors.NotFound(), unknown.Error);
            Assert.Equal(unknown.Error, inactive.Error);
            Assert.Empty(store.GuestQrReads);
        }

        [Fact]
        public async Task Resolve_LogPreservesExceptionTypeWithoutMessageOrPayload()
        {
            var logger = new FakeLogger<LoggingBehavior<ResolveGuestQrQuery, Result<GuestQrContext>>>();
            var behavior = new LoggingBehavior<ResolveGuestQrQuery, Result<GuestQrContext>>(logger);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                behavior.Handle(new(Code), _ => throw new InvalidOperationException(Code), default));

            var entry = Assert.Single(logger.Entries);
            Assert.Null(entry.Exception);
            Assert.Contains(nameof(InvalidOperationException), entry.Message);
            Assert.DoesNotContain(Code, entry.Message);
        }

        private StubTenantContextAccessor Tenant(bool active = true) =>
            new(TenantContext.Create(user.TenantId!.Value, "test-reference", active, "cafe-one"));
    }
}
