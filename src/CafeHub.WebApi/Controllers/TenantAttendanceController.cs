using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance;
using CafeHub.Application.Attendance.Commands;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Attendance.Queries;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/attendance")]
    public sealed class TenantAttendanceController(
        ISender sender,
        ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpGet("settings")]
        [TenantAuthorize(TenantPermissions.AttendanceManage)]
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
        public async Task<ActionResult<AttendanceQrSessionResponse>> IssueQr(
            [FromRoute] Guid branchId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new IssueAttendanceQrCommand(branchId), cancellationToken);

            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
