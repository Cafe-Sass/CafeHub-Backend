using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Dashboard.DTOs;
using CafeHub.Application.Dashboard.Queries;
using CafeHub.Application.Identity;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/dashboard")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public sealed class TenantDashboardController(
        ISender sender,
        ICorrelationIdAccessor? correlationIdAccessor = null) : ControllerBase
    {
        [HttpGet]
        [TenantAuthorize(TenantPermissions.ReportsView)]
        [ProducesResponseType<DashboardResponse>(StatusCodes.Status200OK, "application/json")]
        public async Task<ActionResult<DashboardResponse>> Get(
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] Guid? branchId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new GetDashboardQuery
                {
                    From = from ?? default,
                    To = to ?? default,
                    BranchId = branchId,
                },
                cancellationToken);

            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("leakage")]
        [TenantAuthorize(TenantPermissions.ReportsView)]
        [ProducesResponseType<DashboardLeakageResponse>(StatusCodes.Status200OK, "application/json")]
        public async Task<ActionResult<DashboardLeakageResponse>> GetLeakage(
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] Guid? branchId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new GetDashboardLeakageQuery
                {
                    From = from ?? default,
                    To = to ?? default,
                    BranchId = branchId,
                },
                cancellationToken);

            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{metric}")]
        [TenantAuthorize(TenantPermissions.ReportsView)]
        [ProducesResponseType<DashboardMetricResponse>(StatusCodes.Status200OK, "application/json")]
        public async Task<ActionResult<DashboardMetricResponse>> GetMetric(
            [FromRoute] string metric,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] Guid? branchId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new GetDashboardMetricQuery
                {
                    Metric = metric,
                    From = from ?? default,
                    To = to ?? default,
                    BranchId = branchId,
                },
                cancellationToken);

            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
