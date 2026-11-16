using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu.Commands;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Menu.Queries;
using CafeHub.Application.Pagination;
using CafeHub.WebApi.Authentication;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/menu/categories")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public sealed class TenantMenuCategoriesController(
        ISender sender, IAuthorizationService authorization, ICorrelationIdAccessor correlationIdAccessor) : ControllerBase
    {
        [HttpPost]
        [TenantAuthorize(TenantPermissions.MenuManage)]
        [ProducesResponseType<MenuCategoryResponse>(StatusCodes.Status201Created, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<MenuCategoryResponse>> Create(
            [FromRoute] string slug, [FromBody] CreateMenuCategoryRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new CreateMenuCategoryCommand(request.ParentId, request.Name, request.SortOrder), cancellationToken);
            return result.IsSuccess
                ? CreatedAtAction(nameof(Get), new { slug, id = result.Value.Id }, result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet]
        [TenantAuthorize(TenantPolicies.TenantUser)]
        [ProducesResponseType<OffsetPage<MenuCategoryTreeNodeResponse>>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<OffsetPage<MenuCategoryTreeNodeResponse>>> GetTree(
            [FromQuery] OffsetPageRequest paging, CancellationToken cancellationToken, [FromQuery] bool includeInactive = false)
        {
            if (includeInactive && !(await authorization.AuthorizeAsync(User, HttpContext, TenantPermissions.MenuManage)).Succeeded)
            {
                return Forbid(TenantAuthenticationSchemes.Bearer);
            }

            var result = await sender.Send(new GetMenuCategoryTreeQuery(includeInactive, paging), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{id:guid}")]
        [TenantAuthorize(TenantPolicies.TenantUser)]
        [ProducesResponseType<MenuCategoryResponse>(StatusCodes.Status200OK, "application/json")]
        public async Task<ActionResult<MenuCategoryResponse>> Get([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetMenuCategoryQuery(id), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPut("{id:guid}")]
        [TenantAuthorize(TenantPermissions.MenuManage)]
        [ProducesResponseType<MenuCategoryResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<MenuCategoryResponse>> Update(
            [FromRoute] Guid id, [FromBody] UpdateMenuCategoryRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new UpdateMenuCategoryCommand(id, request.Name, request.SortOrder, request.ConcurrencyToken), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{id:guid}/deactivate")]
        [TenantAuthorize(TenantPermissions.MenuManage)]
        [ProducesResponseType<MenuCategoryResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<MenuCategoryResponse>> Deactivate(
            [FromRoute] Guid id, [FromBody] MenuCategoryLifecycleRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new DeactivateMenuCategoryCommand(id, request.ConcurrencyToken), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{id:guid}/reactivate")]
        [TenantAuthorize(TenantPermissions.MenuManage)]
        [ProducesResponseType<MenuCategoryResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<MenuCategoryResponse>> Reactivate(
            [FromRoute] Guid id, [FromBody] MenuCategoryLifecycleRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new ReactivateMenuCategoryCommand(id, request.ConcurrencyToken), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
