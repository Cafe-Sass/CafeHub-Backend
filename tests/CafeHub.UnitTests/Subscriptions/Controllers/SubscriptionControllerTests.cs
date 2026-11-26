using System.Reflection;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Application.Subscriptions;
using CafeHub.Application.Subscriptions.Commands;
using CafeHub.Application.Subscriptions.Contracts;
using CafeHub.Application.Subscriptions.DTOs;
using CafeHub.Application.Subscriptions.Queries;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Subscriptions.Controllers
{
    public sealed class SubscriptionControllerTests
    {
        private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OtherTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid OwnerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid RenewalId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        [Fact]
        public void OwnerEndpoints_UseTheDocumentedRouteAndOwnerOnlyPermission()
        {
            var route = typeof(TenantSubscriptionController).GetCustomAttribute<RouteAttribute>();
            Assert.Equal("t/{slug}/api/v1/subscription", route?.Template);
            Assert.NotNull(typeof(TenantSubscriptionController).GetCustomAttribute<ApiControllerAttribute>());
            Assert.Equal("subscription.manage", TenantPermissions.SubscriptionManage);
            Assert.True(TenantPermissions.IsOwnerOnly(TenantPermissions.SubscriptionManage));
            Assert.False(TenantPermissions.IsStaffGrant(TenantPermissions.SubscriptionManage));

            AssertAction(nameof(TenantSubscriptionController.Get), typeof(HttpGetAttribute), null);
            AssertAction(nameof(TenantSubscriptionController.History), typeof(HttpGetAttribute), "history");
            AssertAction(nameof(TenantSubscriptionController.Approve), typeof(HttpPostAttribute), "renewal/{renewalId:guid}/approve");
            AssertAction(nameof(TenantSubscriptionController.Decline), typeof(HttpPostAttribute), "renewal/{renewalId:guid}/decline");
            AssertAction(nameof(TenantSubscriptionController.SetNextPlan), typeof(HttpPutAttribute), "next-plan");
        }

        [Fact]
        public async Task OwnerQueries_AreScopedToTheAuthenticatedTenantAndForwardPaging()
        {
            var overview = new SubscriptionOverviewResponse(
                Guid.NewGuid(), TenantId, "Active", "Active", Guid.NewGuid(), "starter", "Starter", 1,
                10m, "usd", 1, "Active", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(30));
            var history = OffsetPage<SubscriptionHistoryItem>.Create(
                [
                    HistoryItem(DateTimeOffset.UnixEpoch.AddDays(2)),
                    HistoryItem(DateTimeOffset.UnixEpoch.AddDays(1)),
                ],
                2,
                5,
                7);
            var sender = new RecordingSender { Response = Result.Success(overview) };
            var controller = CreateTenantController(sender, TenantId);

            var overviewResult = await controller.Get(default);
            Assert.Same(overview, Assert.IsType<OkObjectResult>(overviewResult.Result).Value);
            Assert.Equal(TenantId, Assert.IsType<GetSubscriptionOverviewQuery>(sender.LastRequest).TenantKey);

            sender.Response = Result.Success(history);
            var historyResult = await controller.History(2, 5, true, default);
            Assert.Same(history, Assert.IsType<OkObjectResult>(historyResult.Result).Value);
            var query = Assert.IsType<GetSubscriptionHistoryQuery>(sender.LastRequest);
            Assert.Equal(TenantId, query.TenantKey);
            Assert.Equal(2, query.Page);
            Assert.Equal(5, query.PageSize);
            Assert.True(query.IncludeTotalCount);
            Assert.Equal(history.Items, ((OffsetPage<SubscriptionHistoryItem>)((OkObjectResult)historyResult.Result!).Value!).Items);

            await controller.History(null, null, false, default);
            Assert.Equal(1, Assert.IsType<GetSubscriptionHistoryQuery>(sender.LastRequest).Page);
        }

        [Fact]
        public async Task OwnerHistory_MapsMissingSubscriptionToNotFound()
        {
            var sender = new RecordingSender
            {
                Response = Result.Failure<OffsetPage<SubscriptionHistoryItem>>(SubscriptionErrors.NotFound()),
            };
            var controller = CreateTenantController(sender, TenantId);

            var result = await controller.History(1, null, false, default);

            var problem = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
            Assert.Equal("Subscription.NotFound", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
        }

        [Fact]
        public async Task OwnerActions_ForwardOnlyCurrentTenantAndActorScope()
        {
            var response = new SubscriptionRenewalResponse(
                RenewalId, Guid.NewGuid(), "Pending", Guid.NewGuid(), 12m, "usd",
                DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(7), null, null, null);
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateTenantController(sender, TenantId);

            await controller.Approve(RenewalId, default);
            var approve = Assert.IsType<ApproveRenewalRequestCommand>(sender.LastRequest);
            Assert.Equal(RenewalId, approve.RenewalRequestId);
            Assert.Equal(TenantId, approve.TenantKey);
            Assert.Equal(OwnerId.ToString(), approve.ActorReference);

            await controller.Decline(RenewalId, default);
            var decline = Assert.IsType<DeclineRenewalRequestCommand>(sender.LastRequest);
            Assert.Equal(TenantId, decline.TenantKey);
            Assert.Equal(OwnerId.ToString(), decline.ActorReference);

            await controller.SetNextPlan(new SetNextPlanRequest(Guid.NewGuid()), default);
            var setPlan = Assert.IsType<SetOwnerNextPlanCommand>(sender.LastRequest);
            Assert.Equal(TenantId, setPlan.TenantKey);
            Assert.Equal(OwnerId.ToString(), setPlan.ActorReference);

            sender.Response = Result.Failure<SubscriptionRenewalResponse>(
                SubscriptionErrors.RenewalNotApprovable());
            var conflict = await controller.Approve(RenewalId, default);
            var problem = Assert.IsType<ObjectResult>(conflict.Result);
            Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
            Assert.Equal(
                "Subscription.RenewalNotApprovable",
                Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
        }

        [Fact]
        public async Task MissingTenantContext_ReturnsNotFoundWithoutDispatching()
        {
            var sender = new RecordingSender();
            var controller = CreateTenantController(sender, null);

            var result = await controller.Get(default);

            Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(result.Result).StatusCode);
            Assert.Null(sender.LastRequest);
        }

        [Fact]
        public async Task AdminEndpoints_RequireSuperAdminAndUseTheRequestedTenant()
        {
            var route = typeof(AdminTenantSubscriptionController).GetCustomAttribute<RouteAttribute>();
            Assert.Equal("api/v1/admin/tenants/{tenantId:guid}/subscription", route?.Template);
            var authorization = typeof(AdminTenantSubscriptionController)
                .GetCustomAttribute<AuthorizeAttribute>();
            Assert.Equal(PlatformAuthorizationPolicies.SuperAdmin, authorization?.Policy);
            Assert.Null(typeof(AdminTenantSubscriptionController).GetCustomAttribute<AllowAnonymousAttribute>());

            var overview = new SubscriptionOverviewResponse(
                Guid.NewGuid(), OtherTenantId, "Suspended", "Active", null, null, null, null, null, null,
                null, null, null, null);
            var page = OffsetPage<SubscriptionHistoryItem>.Create([], 1, 20, 0);
            var sender = new RecordingSender { Response = Result.Success(overview) };
            var controller = CreateAdminController(sender);

            var overviewResult = await controller.Get(OtherTenantId, default);
            Assert.Same(overview, Assert.IsType<OkObjectResult>(overviewResult.Result).Value);
            Assert.Equal("Suspended", overview.TenantStatus);
            Assert.Equal(OtherTenantId, Assert.IsType<GetSubscriptionOverviewQuery>(sender.LastRequest).TenantKey);

            sender.Response = Result.Success(page);
            var historyResult = await controller.History(OtherTenantId, 3, 10, true, default);
            Assert.Same(page, Assert.IsType<OkObjectResult>(historyResult.Result).Value);
            var query = Assert.IsType<GetSubscriptionHistoryQuery>(sender.LastRequest);
            Assert.Equal(OtherTenantId, query.TenantKey);
            Assert.Equal(3, query.Page);
            Assert.Equal(10, query.PageSize);
            Assert.True(query.IncludeTotalCount);

            await controller.History(OtherTenantId, null, null, false, default);
            Assert.Equal(1, Assert.IsType<GetSubscriptionHistoryQuery>(sender.LastRequest).Page);
        }

        [Fact]
        public async Task HistoryQueryHandler_ReturnsNotFoundWhenNoSubscriptionExists()
        {
            var store = new RecordingQueryStore();
            var handler = new GetSubscriptionHistoryQueryHandler(store, new PaginationOptions());

            var result = await handler.Handle(new GetSubscriptionHistoryQuery(TenantId), default);

            Assert.True(result.IsFailure);
            Assert.Equal("Subscription.NotFound", result.Error?.Code);
            Assert.Equal(TenantId, store.TenantId);
        }

        [Fact]
        public async Task HistoryQueryHandler_PreservesPageMetadataAndStableNewestFirstOrder()
        {
            var newest = HistoryItem(DateTimeOffset.UnixEpoch.AddDays(2));
            var oldest = HistoryItem(DateTimeOffset.UnixEpoch.AddDays(1));
            var page = OffsetPage<SubscriptionHistoryItem>.Create([newest, oldest], 2, 5, 7);
            var store = new RecordingQueryStore { History = page };
            var handler = new GetSubscriptionHistoryQueryHandler(
                store,
                new PaginationOptions { DefaultPageSize = 10, MaxPageSize = 50 });

            var result = await handler.Handle(
                new GetSubscriptionHistoryQuery(TenantId, 2, 5, true), default);

            Assert.True(result.IsSuccess);
            Assert.Same(page, result.Value);
            Assert.Equal(TenantId, store.TenantId);
            Assert.Equal(2, store.Page);
            Assert.Equal(5, store.PageSize);
            Assert.True(store.IncludeTotalCount);
            Assert.Equal([newest, oldest], result.Value.Items);
        }

        [Fact]
        public void HistoryQueryValidator_RejectsOffsetsThatOverflowTheStoreSkip()
        {
            var validator = new GetSubscriptionHistoryQueryValidator(new PaginationOptions());

            var result = validator.Validate(new GetSubscriptionHistoryQuery(
                TenantId, int.MaxValue, 100, false));

            Assert.Contains(result.Errors, error => error.ErrorCode == "Pagination.PageInvalid");
        }

        private static SubscriptionHistoryItem HistoryItem(DateTimeOffset occurredAt) =>
            new(Guid.NewGuid(), "Active", "Expired", Guid.NewGuid(), 1, OwnerId.ToString(), null, occurredAt);

        private static TenantSubscriptionController CreateTenantController(
            ISender sender,
            Guid? tenantId) =>
            new(sender, new FakeCurrentTenantUser(tenantId), new TestCorrelationIdAccessor())
            {
                ControllerContext = ControllerContext(),
            };

        private static AdminTenantSubscriptionController CreateAdminController(ISender sender) =>
            new(sender, new TestCorrelationIdAccessor())
            {
                ControllerContext = ControllerContext(),
            };

        private static ControllerContext ControllerContext() => new()
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-subscription" },
        };

        private static void AssertAction(string name, Type verbType, string? template)
        {
            var method = typeof(TenantSubscriptionController).GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"The {name} action was not found.");
            var verb = method.GetCustomAttributes().Single(attribute => verbType.IsInstanceOfType(attribute));
            var actualTemplate = verb switch
            {
                HttpGetAttribute get => get.Template,
                HttpPostAttribute post => post.Template,
                HttpPutAttribute put => put.Template,
                _ => throw new InvalidOperationException("The action uses an unsupported HTTP verb."),
            };

            Assert.Equal(template, actualTemplate);
            Assert.Equal(
                TenantPermissions.SubscriptionManage,
                Assert.IsType<TenantAuthorizeAttribute>(method.GetCustomAttribute<TenantAuthorizeAttribute>()).Policy);
            Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
        }

        private sealed class FakeCurrentTenantUser(Guid? tenantId) : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId => OwnerId;

            public Guid? TenantId => tenantId;

            public IReadOnlyCollection<string> Roles => [TenantRoles.Owner];

            public Guid? BranchId => null;
        }

        private sealed class RecordingSender : ISender
        {
            public object? Response { get; set; }

            public object? LastRequest { get; private set; }

            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                LastRequest = request;
                return Task.FromResult((TResponse)Response!);
            }

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
                where TRequest : IRequest =>
                throw new NotSupportedException();

            public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
                IStreamRequest<TResponse> request,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public IAsyncEnumerable<object?> CreateStream(
                object request,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
        }

        private sealed class RecordingQueryStore : ISubscriptionQueryStore
        {
            public Guid? TenantId { get; private set; }

            public int Page { get; private set; }

            public int PageSize { get; private set; }

            public bool IncludeTotalCount { get; private set; }

            public OffsetPage<SubscriptionHistoryItem>? History { get; set; }

            public Task<SubscriptionOverviewResponse?> FindOverviewAsync(
                Guid tenantId,
                CancellationToken cancellationToken) =>
                Task.FromResult<SubscriptionOverviewResponse?>(null);

            public Task<OffsetPage<SubscriptionHistoryItem>?> ListHistoryAsync(
                Guid tenantId,
                int page,
                int pageSize,
                bool includeTotalCount,
                CancellationToken cancellationToken)
            {
                TenantId = tenantId;
                Page = page;
                PageSize = pageSize;
                IncludeTotalCount = includeTotalCount;
                return Task.FromResult(History);
            }
        }

        private sealed class TestCorrelationIdAccessor : CafeHub.Application.Abstractions.Correlation.ICorrelationIdAccessor
        {
            public string? CorrelationId => "correlation-subscription";
        }
    }
}
