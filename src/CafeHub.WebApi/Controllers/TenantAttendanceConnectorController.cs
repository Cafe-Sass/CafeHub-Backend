using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Commands;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/connector/attendance")]
    [AllowAnonymous]
    [ServiceFilter(typeof(AttendanceDeviceKeyAuthorizationFilter))]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public sealed class TenantAttendanceConnectorController(
        ISender sender,
        ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpPost("events")]
        [ProducesResponseType<AttendanceDeviceEventsResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<AttendanceDeviceEventsResponse>> Events(
            [FromBody] AttendanceDeviceEventsRequest request,
            CancellationToken cancellationToken)
        {
            var device = HttpContext.GetAttendanceDevice();
            var events = request.Events?
                .Select(item => new AttendanceDeviceEventInput(
                    item.EventId, item.Code, item.OccurredAt, item.Direction))
                .ToArray();
            var result = await sender.Send(
                new IngestAttendanceDeviceEventsCommand(device.Id, device.BranchId, events),
                cancellationToken);

            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
