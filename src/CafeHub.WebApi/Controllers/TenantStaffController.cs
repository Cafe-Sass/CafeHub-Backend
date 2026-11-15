using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Pagination;
using CafeHub.Application.Staff.Commands;
using CafeHub.Application.Staff.DTOs;
using CafeHub.Application.Staff.Queries;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/staff")]
    public sealed class TenantStaffController : ControllerBase
    {
        private readonly ISender sender;
        private readonly ICorrelationIdAccessor? correlationIdAccessor;

        public TenantStaffController(ISender sender, ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
            this.correlationIdAccessor = correlationIdAccessor;
        }

        [HttpPost]
        [TenantAuthorize(TenantPolicies.TenantOwner)]
        public async Task<ActionResult<StaffResponse>> Create(
            [FromBody] CreateStaffRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CreateStaffCommand(
                request.FullName,
                request.Email,
                request.PhoneNumber,
                request.BranchId,
                request.Password);

            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return CreatedAtAction(nameof(GetById), new { staffId = result.Value.Id }, result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet]
        [TenantAuthorize(TenantPolicies.TenantOwner)]
        public async Task<ActionResult<OffsetPage<StaffResponse>>> List(
            [FromQuery] ListStaffQuery query,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(query, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{staffId:guid}")]
        [TenantAuthorize(TenantPolicies.TenantOwner)]
        public async Task<ActionResult<StaffResponse>> GetById(
            Guid staffId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetStaffByIdQuery(staffId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPut("{staffId:guid}")]
        [TenantAuthorize(TenantPolicies.TenantOwner)]
        public async Task<ActionResult<StaffResponse>> Update(
            Guid staffId,
            [FromBody] UpdateStaffRequest request,
            CancellationToken cancellationToken)
        {
            var command = new UpdateStaffCommand(
                staffId,
                request.FullName,
                request.PhoneNumber,
                request.BranchId,
                request.ConcurrencyToken);

            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{staffId:guid}/deactivate")]
        [TenantAuthorize(TenantPolicies.TenantOwner)]
        public async Task<ActionResult> Deactivate(
            Guid staffId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new DeactivateStaffCommand(staffId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok();
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{staffId:guid}/reactivate")]
        [TenantAuthorize(TenantPolicies.TenantOwner)]
        public async Task<ActionResult> Reactivate(
            Guid staffId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new ReactivateStaffCommand(staffId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok();
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{staffId:guid}/reset-password")]
        [TenantAuthorize(TenantPolicies.TenantOwner)]
        public async Task<ActionResult> ResetPassword(
            Guid staffId,
            [FromBody] ResetStaffPasswordRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new ResetStaffPasswordCommand(staffId, request.NewPassword),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok();
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
