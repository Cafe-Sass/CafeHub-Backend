using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Roles;
using CafeHub.UnitTests.Staff;
using CafeHub.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests
{
    public sealed class TenantCreationLocationTests
    {
        [Fact]
        public async Task CreateStaffIncludesTheTenantInTheDetailRoute()
        {
            var branchId = Guid.NewGuid();
            var staff = FakeStaffStore.MakeResponse(Guid.NewGuid(), "Fixture Staff", "staff@example.test", null, branchId);
            var sender = new RecordingSender { Response = Result.Success(staff) };
            var controller = new TenantStaffController(sender);
            var request = new CreateStaffRequest(staff.FullName, staff.Email, null, branchId, "fixture-password");

            var result = await controller.Create("fixture-cafe", request, default);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var routeValues = created.RouteValues;
            Assert.NotNull(routeValues);
            Assert.Equal(nameof(TenantStaffController.GetById), created.ActionName);
            Assert.Equal("fixture-cafe", routeValues["slug"]);
            Assert.Equal(staff.Id, routeValues["staffId"]);
        }

        [Fact]
        public async Task CreateRoleIncludesTheTenantInTheDetailRoute()
        {
            var role = new TenantRoleResponse(Guid.NewGuid(), "Manager", [], "1");
            var sender = new RecordingSender { Response = Result.Success(role) };
            var controller = new TenantRolesController(sender, new NoCorrelation());

            var result = await controller.Create("fixture-cafe", new CreateTenantRole(role.Name, []), default);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var routeValues = created.RouteValues;
            Assert.NotNull(routeValues);
            Assert.Equal(nameof(TenantRolesController.Get), created.ActionName);
            Assert.Equal("fixture-cafe", routeValues["slug"]);
            Assert.Equal(role.Id, routeValues["roleId"]);
        }

        private sealed class NoCorrelation : ICorrelationIdAccessor
        {
            public string? CorrelationId => null;
        }
    }
}
