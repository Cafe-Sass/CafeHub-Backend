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
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Printing
{
    public sealed class PrinterRegistryTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OtherBranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid StationId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid PrinterId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-24T12:00:00Z");

        [Fact]
        public void TheProfileSetIsClosedAndCarriesWidthAndCodePage()
        {
            Assert.Equal(
                new[] { PrinterProfile.Escpos80mm, PrinterProfile.Escpos58mm },
                PrinterProfiles.All.Select(definition => definition.Profile));
            Assert.Equal(32, PrinterProfiles.All.Single(definition => definition.Profile == PrinterProfile.Escpos58mm).LineWidth);
            Assert.Equal(48, PrinterProfiles.All.Single(definition => definition.Profile == PrinterProfile.Escpos80mm).LineWidth);
            Assert.All(PrinterProfiles.All, definition => Assert.Equal("CP437", definition.CodePage));
            Assert.False(PrinterProfiles.TryGet((PrinterProfile)(-1), out _));
        }

        [Fact]
        public void UnknownProfilesAndBadTextAreRefused()
        {
            var validator = new RegisterPrinterCommandValidator();
            validator.TestValidate(ValidRegister()).ShouldNotHaveAnyValidationErrors();
            validator.TestValidate(ValidRegister() with { Profile = (PrinterProfile)(-1) })
                .ShouldHaveValidationErrorFor(command => command.Profile);
            validator.TestValidate(ValidRegister() with { Name = " " })
                .ShouldHaveValidationErrorFor(command => command.Name);
            validator.TestValidate(ValidRegister() with { Name = new string('a', 65) })
                .ShouldHaveValidationErrorFor(command => command.Name);
            validator.TestValidate(ValidRegister() with { ConnectionKind = "bad\nkind" })
                .ShouldHaveValidationErrorFor(command => command.ConnectionKind);
            validator.TestValidate(ValidRegister() with { StationId = Guid.Empty })
                .ShouldHaveValidationErrorFor(command => command.StationId);

            Assert.Throws<ArgumentOutOfRangeException>(() => Printer.Create(
                Guid.CreateVersion7(), BranchId, null, "Front", (PrinterProfile)(-1), "Network", Now, null));
        }

        [Fact]
        public async Task ARouteToAnInactivePrinterIsRefused()
        {
            var store = new FakePrinterStore
            {
                Printer = PrinterResponse("Inactive"),
            };
            var handler = new SetPrintRouteCommandHandler(store, StaffUser());

            var result = await handler.Handle(
                new SetPrintRouteCommand(BranchId, StationId, PrintDocumentType.OrderTicket, PrinterId),
                default);

            Assert.Equal(PrintingErrors.PrinterInactiveCode, result.Error!.Code);
            Assert.Equal(0, store.RouteWrites);
        }

        [Fact]
        public async Task TheActivePrinterRouteIsDelegated()
        {
            var store = new FakePrinterStore
            {
                Printer = PrinterResponse("Active"),
                Route = new PrintRouteResponse(
                    Guid.CreateVersion7(), BranchId, StationId, "OrderTicket", PrinterId),
            };
            var handler = new SetPrintRouteCommandHandler(store, StaffUser());

            var result = await handler.Handle(
                new SetPrintRouteCommand(BranchId, StationId, PrintDocumentType.OrderTicket, PrinterId),
                default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(1, store.RouteWrites);
            Assert.Equal(BranchId, store.LastBranch);
        }

        [Fact]
        public async Task AnotherBranchesStaffGetsTheUniformNotFound()
        {
            var store = new FakePrinterStore();
            var user = StaffUser(OtherBranchId);

            var register = await new RegisterPrinterCommandHandler(store, user)
                .Handle(ValidRegister(), default);
            var route = await new SetPrintRouteCommandHandler(store, user)
                .Handle(new SetPrintRouteCommand(BranchId, null, PrintDocumentType.Receipt, PrinterId), default);

            Assert.Equal(PrintingErrors.NotFoundCode, register.Error!.Code);
            Assert.Equal(PrintingErrors.NotFoundCode, route.Error!.Code);
            Assert.Equal(0, store.Writes);
            Assert.Equal(0, store.RouteWrites);
        }

        [Fact]
        public async Task TheOwnerMustPassABranchToListAndStaffUseTheirScope()
        {
            var handlers = new
            {
                Printers = new ListPrintersQueryHandler(new FakePrinterStore(), StaffUser()),
                Routes = new ListPrintRoutesQueryHandler(new FakePrinterStore(), StaffUser()),
            };

            var staffList = await handlers.Printers.Handle(new ListPrintersQuery(null), default);
            var ownerMissing = await new ListPrintersQueryHandler(new FakePrinterStore(), OwnerUser())
                .Handle(new ListPrintersQuery(null), default);

            Assert.True(staffList.IsSuccess, staffList.Error?.Code);
            Assert.Equal(PrintingErrors.BranchRequiredCode, ownerMissing.Error!.Code);
        }

        [Fact]
        public void TheAuditSnapshotHoldsOnlyTheAllowListedPrinterFields()
        {
            var snapshot = TenantAuditSnapshotSerializer.Create(
                "printers",
                TenantAuditChanges.Update(
                [
                    new("Name", "Old", "New"),
                    new("Status", "Active", "Inactive"),
                    new("ConnectionKind", "USB", "Network"),
                    new("Secret", null, "never"),
                ]));

            Assert.True(snapshot.IsSuccess, snapshot.Error?.Code);
            var afterState = snapshot.Value.AfterState!;
            Assert.Contains("\"Name\":\"New\"", afterState, StringComparison.Ordinal);
            Assert.Contains("\"Status\":\"Inactive\"", afterState, StringComparison.Ordinal);
            Assert.DoesNotContain("never", afterState, StringComparison.Ordinal);
        }

        [Fact]
        public void EndpointsRequirePrintersManageAndUseThePrinterRoutes()
        {
            Assert.Equal(
                "t/{slug}/api/v1/branches/{branchId:guid}/printers",
                typeof(TenantPrintersController).GetCustomAttribute<RouteAttribute>()!.Template);
            Assert.Equal(
                "t/{slug}/api/v1/branches/{branchId:guid}/print-routes",
                typeof(TenantPrintRoutesController).GetCustomAttribute<RouteAttribute>()!.Template);

            var actions = new[]
            {
                typeof(TenantPrintersController).GetMethod(nameof(TenantPrintersController.List))!,
                typeof(TenantPrintersController).GetMethod(nameof(TenantPrintersController.Register))!,
                typeof(TenantPrintersController).GetMethod(nameof(TenantPrintersController.Update))!,
                typeof(TenantPrintersController).GetMethod(nameof(TenantPrintersController.Deactivate))!,
                typeof(TenantPrintersController).GetMethod(nameof(TenantPrintersController.Reactivate))!,
                typeof(TenantPrintRoutesController).GetMethod(nameof(TenantPrintRoutesController.List))!,
                typeof(TenantPrintRoutesController).GetMethod(nameof(TenantPrintRoutesController.Set))!,
            };
            Assert.All(actions, action => Assert.Equal(
                TenantPermissions.PrintersManage,
                Assert.IsType<TenantAuthorizeAttribute>(
                    action.GetCustomAttribute<TenantAuthorizeAttribute>()).Policy));

            Assert.Equal("printers.manage", TenantPermissions.PrintersManage);
            Assert.Contains(TenantPermissions.PrintersManage, TenantPermissions.All);
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.PrintersManage));
            Assert.Equal(PrinterProfile.Escpos58mm, TenantPrintersController.ParseProfile("escpos58mm"));
            Assert.Equal(
                PrintDocumentType.Receipt,
                TenantPrintRoutesController.ParseDocumentType("receipt"));
        }

        private static RegisterPrinterCommand ValidRegister() =>
            new(BranchId, StationId, "Front counter", PrinterProfile.Escpos80mm, "Network");

        private static PrinterResponse PrinterResponse(string status) =>
            new(PrinterId, BranchId, StationId, "Front counter", "Escpos80mm", "Network", status, "1");

        private static FakeTenantUser StaffUser(Guid? branchId = null) =>
            new(Guid.NewGuid(), new TenantAccountAccess(true, null, true) { BranchId = branchId ?? BranchId });

        private static FakeTenantUser OwnerUser() =>
            new(Guid.NewGuid(), new TenantAccountAccess(false, null, true));

        private sealed class FakeTenantUser(Guid? userId, TenantAccountAccess? accountAccess) : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId => userId;

            public Guid? TenantId => Guid.Parse("55555555-5555-5555-5555-555555555555");

            public IReadOnlyCollection<string> Roles => [];

            public Guid? BranchId => AccountAccess?.BranchId;

            public TenantAccountAccess? AccountAccess { get; } = accountAccess;
        }

        private sealed class FakePrinterStore : IPrinterStore
        {
            public PrinterResponse? Printer { get; set; }

            public PrintRouteResponse? Route { get; set; }

            public int Writes { get; private set; }

            public int RouteWrites { get; private set; }

            public Guid? LastBranch { get; private set; }

            public Task<Result<PrinterResponse>> RegisterAsync(
                Guid branchId,
                Guid? stationId,
                string name,
                PrinterProfile profile,
                string connectionKind,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                Writes++;
                return Task.FromResult(Result.Success(
                    Printer ?? PrinterResponse("Active") with { BranchId = branchId }));
            }

            public Task<Result<PrinterResponse>> UpdateAsync(
                Guid printerId,
                Guid branchId,
                Guid? stationId,
                string name,
                PrinterProfile profile,
                string connectionKind,
                string concurrencyToken,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                Writes++;
                return Task.FromResult(Result.Success(Printer!));
            }

            public Task<Result<PrinterResponse>> SetStatusAsync(
                Guid printerId,
                Guid branchId,
                PrinterStatus status,
                string concurrencyToken,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                Writes++;
                return Task.FromResult(Result.Success(Printer!));
            }

            public Task<PrinterResponse?> FindPrinterAsync(
                Guid printerId,
                Guid branchId,
                CancellationToken cancellationToken) =>
                Task.FromResult(PrinterId == printerId ? Printer : null);

            public Task<IReadOnlyList<PrinterResponse>> ListAsync(
                Guid branchId,
                CancellationToken cancellationToken)
            {
                LastBranch = branchId;
                return Task.FromResult<IReadOnlyList<PrinterResponse>>(
                    Printer is null ? [] : [Printer]);
            }

            public Task<Result<PrintRouteResponse>> SetRouteAsync(
                Guid branchId,
                Guid? stationId,
                PrintDocumentType documentType,
                Guid printerId,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                RouteWrites++;
                LastBranch = branchId;
                return Task.FromResult(Result.Success(Route!));
            }

            public Task<IReadOnlyList<PrintRouteResponse>> ListRoutesAsync(
                Guid branchId,
                CancellationToken cancellationToken)
            {
                LastBranch = branchId;
                return Task.FromResult<IReadOnlyList<PrintRouteResponse>>(
                    Route is null ? [] : [Route]);
            }
        }
    }
}
