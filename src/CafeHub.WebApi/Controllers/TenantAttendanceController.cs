using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance;
using CafeHub.Application.Attendance.Commands;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Attendance.Queries;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/attendance")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public sealed class TenantAttendanceController(
        ISender sender,
        ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpGet("settings")]
        [TenantAuthorize(TenantPermissions.AttendanceManage)]
        [ProducesResponseType<AttendanceSettingsResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<AttendanceSettingsResponse>> GetSettings(
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetAttendanceSettingsQuery(), cancellationToken);

            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPut("settings")]
        [TenantAuthorize(TenantPermissions.AttendanceManage)]
        [ProducesResponseType<AttendanceSettingsResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<AttendanceSettingsResponse>> UpdateSettings(
            [FromBody] UpdateAttendanceSettingsRequest request,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<AttendanceMode>(request.Mode, true, out var mode))
            {
                return Result.Failure<AttendanceSettingsResponse>(AttendanceErrors.InvalidMode())
                    .ToProblemResult(HttpContext, correlationIdAccessor);
            }

            var result = await sender.Send(
                new UpdateAttendanceSettingsCommand(mode, request.ConcurrencyToken),
                cancellationToken);

            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("branches/{branchId:guid}/qr")]
        [TenantAuthorize(TenantPermissions.AttendanceQrManage)]
        [ProducesResponseType<AttendanceQrSessionResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<AttendanceQrSessionResponse>> IssueQr(
            [FromRoute] Guid branchId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new IssueAttendanceQrCommand(branchId), cancellationToken);

            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("scan")]
        [TenantAuthorize(TenantPolicies.TenantUser)]
        [ProducesResponseType<AttendanceScanResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<AttendanceScanResponse>> Scan(
            [FromBody] AttendanceScanRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new ScanAttendanceCommand(request.Token), cancellationToken);

            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet]
        [TenantAuthorize(TenantPermissions.AttendanceView)]
        [ProducesResponseType<OffsetPage<AttendanceHistoryItem>>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<OffsetPage<AttendanceHistoryItem>>> History(
            [FromQuery] GetAttendanceHistoryQuery request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(request, cancellationToken);

            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
