using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Commands;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Attendance.Queries;
using CafeHub.Application.Identity;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/attendance/devices")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public sealed class TenantAttendanceDevicesController(
        ISender sender,
        ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpGet]
        [TenantAuthorize(TenantPermissions.AttendanceManage)]
        [ProducesResponseType<IReadOnlyList<AttendanceDeviceResponse>>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<IReadOnlyList<AttendanceDeviceResponse>>> List(
            [FromQuery] Guid? branchId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetAttendanceDevicesQuery(branchId), cancellationToken);

            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost]
        [TenantAuthorize(TenantPermissions.AttendanceManage)]
        [ProducesResponseType<AttendanceDeviceCreatedResponse>(StatusCodes.Status201Created, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<AttendanceDeviceCreatedResponse>> Create(
            [FromRoute] string slug,
            [FromBody] CreateAttendanceDeviceRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new CreateAttendanceDeviceCommand(request.BranchId, request.Name), cancellationToken);

            return result.IsSuccess
                ? CreatedAtAction(
                    nameof(List),
                    new { slug, branchId = result.Value.BranchId },
                    result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{deviceId:guid}/revoke")]
        [TenantAuthorize(TenantPermissions.AttendanceManage)]
        [ProducesResponseType<AttendanceDeviceResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<AttendanceDeviceResponse>> Revoke(
            [FromRoute] Guid deviceId,
            [FromBody] AttendanceDeviceScopeRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new RevokeAttendanceDeviceCommand(deviceId, request.BranchId), cancellationToken);

            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPut("codes")]
        [TenantAuthorize(TenantPermissions.AttendanceManage)]
        [ProducesResponseType<AttendanceDeviceCodeResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<AttendanceDeviceCodeResponse>> MapCode(
            [FromBody] MapAttendanceDeviceCodeRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new MapAttendanceDeviceCodeCommand(
                    request.BranchId, request.StaffId, request.DeviceCode),
                cancellationToken);

            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpDelete("codes/{staffId:guid}")]
        [TenantAuthorize(TenantPermissions.AttendanceManage)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<IActionResult> UnmapCode(
            [FromRoute] Guid staffId,
            [FromQuery] Guid branchId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new UnmapAttendanceDeviceCodeCommand(branchId, staffId), cancellationToken);

            return result.IsSuccess
                ? NoContent()
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
