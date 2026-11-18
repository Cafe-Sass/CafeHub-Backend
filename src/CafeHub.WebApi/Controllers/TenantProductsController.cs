using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu;
using CafeHub.Application.Menu.Commands;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Menu.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;
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
    [Route("t/{slug}/api/v1/menu/products")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public sealed class TenantProductsController(
        ISender sender,
        IAuthorizationService authorization,
        ICorrelationIdAccessor correlationIdAccessor) : ControllerBase
    {
        [HttpPost]
        [TenantAuthorize(TenantPermissions.MenuManage)]
        [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<ProductResponse>> Create(
            [FromRoute] string slug,
            [FromBody] CreateProductRequest request,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<ProductKind>(request.Kind, true, out var kind))
            {
                return Result.Failure<ProductResponse>(ProductErrors.InvalidKind())
                    .ToProblemResult(HttpContext, correlationIdAccessor);
            }

            var result = await sender.Send(
                new CreateProductCommand(
                    request.CategoryId,
                    null,
                    request.Name,
                    request.Description,
                    request.Ingredients,
                    kind,
                    request.Caffeine,
                    request.PreparationMinutes,
                    request.SortOrder,
                    request.Sizes.Select(size => new ProductSizeInput(size.Id, size.Name, size.Price, size.IsDefault)).ToList(),
                    request.Addons.Select(addon => new ProductAddonInput(addon.Id, addon.Name, addon.Price)).ToList(),
                    request.Tags),
                cancellationToken);
            return result.IsSuccess
                ? CreatedAtAction(nameof(Get), new { slug, id = result.Value.Id }, result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        [TenantAuthorize(TenantPolicies.TenantUser)]
        [ProducesResponseType<OffsetPage<ProductListItemResponse>>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<OffsetPage<ProductListItemResponse>>> List(
            [FromRoute] string slug,
            [FromQuery] ListProductsQuery request,
            CancellationToken cancellationToken)
        {
            var canManage = (await authorization.AuthorizeAsync(User, HttpContext, TenantPermissions.MenuManage)).Succeeded;
            if (request.IncludeHiddenCategories && !canManage)
            {
                return Result.Failure<OffsetPage<ProductListItemResponse>>(AuthErrors.Forbidden())
                    .ToProblemResult(HttpContext, correlationIdAccessor);
            }

            var status = canManage ? request.Status : ProductStatus.Active;

            var result = await sender.Send(request with { Status = status }, cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        [TenantAuthorize(TenantPolicies.TenantUser)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK, "application/json")]
        public async Task<ActionResult<ProductResponse>> Get(
            [FromRoute] string slug,
            [FromRoute] Guid id,
            CancellationToken cancellationToken,
            [FromQuery] bool includeHiddenCategories = false)
        {
            var canManage = (await authorization.AuthorizeAsync(User, HttpContext, TenantPermissions.MenuManage)).Succeeded;
            if (includeHiddenCategories && !canManage)
            {
                return Result.Failure<ProductResponse>(AuthErrors.Forbidden())
                    .ToProblemResult(HttpContext, correlationIdAccessor);
            }

            var result = await sender.Send(new GetProductQuery(id, includeHiddenCategories), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToProblemResult(HttpContext, correlationIdAccessor);
            }

            if (!canManage && result.Value.Status != nameof(ProductStatus.Active))
            {
                return Result.Failure<ProductResponse>(ProductErrors.NotFound())
                    .ToProblemResult(HttpContext, correlationIdAccessor);
            }

            return Ok(result.Value);
        }

        [HttpPut("{id:guid}")]
        [TenantAuthorize(TenantPermissions.MenuManage)]
        [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<ProductResponse>> Update(
            [FromRoute] string slug,
            [FromRoute] Guid id,
            [FromBody] UpdateProductRequest request,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<ProductKind>(request.Kind, true, out var kind))
            {
                return Result.Failure<ProductResponse>(ProductErrors.InvalidKind())
                    .ToProblemResult(HttpContext, correlationIdAccessor);
            }

            var result = await sender.Send(
                new UpdateProductCommand(
                    id,
                    request.CategoryId,
                    null,
                    request.Name,
                    request.Description,
                    request.Ingredients,
                    kind,
                    request.Caffeine,
                    request.PreparationMinutes,
                    request.SortOrder,
                    request.Sizes.Select(size => new ProductSizeInput(size.Id, size.Name, size.Price, size.IsDefault)).ToList(),
                    request.Addons.Select(addon => new ProductAddonInput(addon.Id, addon.Name, addon.Price)).ToList(),
                    request.Tags,
                    request.ConcurrencyToken),
                cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{id:guid}/deactivate")]
        [TenantAuthorize(TenantPermissions.MenuManage)]
        [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<ProductResponse>> Deactivate(
            [FromRoute] string slug,
            [FromRoute] Guid id,
            [FromBody] ProductLifecycleRequest request,
            CancellationToken cancellationToken,
            [FromQuery] bool includeHiddenCategories = false)
        {
            if (includeHiddenCategories && !(await authorization.AuthorizeAsync(User, HttpContext, TenantPermissions.MenuManage)).Succeeded)
            {
                return Result.Failure<ProductResponse>(AuthErrors.Forbidden()).ToProblemResult(HttpContext, correlationIdAccessor);
            }

            var result = await sender.Send(
                new DeactivateProductCommand(id, request.ConcurrencyToken, includeHiddenCategories), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{id:guid}/reactivate")]
        [TenantAuthorize(TenantPermissions.MenuManage)]
        [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<ProductResponse>> Reactivate(
            [FromRoute] string slug,
            [FromRoute] Guid id,
            [FromBody] ProductLifecycleRequest request,
            CancellationToken cancellationToken,
            [FromQuery] bool includeHiddenCategories = false)
        {
            if (includeHiddenCategories && !(await authorization.AuthorizeAsync(User, HttpContext, TenantPermissions.MenuManage)).Succeeded)
            {
                return Result.Failure<ProductResponse>(AuthErrors.Forbidden()).ToProblemResult(HttpContext, correlationIdAccessor);
            }

            var result = await sender.Send(
                new ReactivateProductCommand(id, request.ConcurrencyToken, includeHiddenCategories), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
