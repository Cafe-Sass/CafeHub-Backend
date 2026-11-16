using System.Linq;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Application.Tables.Commands;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tables.Queries;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using CafeHub.WebApi.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/branches/{branchId:guid}/tables")]
    public sealed class TenantTablesController : ControllerBase
    {
        private readonly ISender sender;
        private readonly ICurrentTenantUser currentTenantUser;
        private readonly ITenantPermissionReader permissions;
        private readonly ICorrelationIdAccessor? correlationIdAccessor;

        public TenantTablesController(
            ISender sender,
            ICurrentTenantUser currentTenantUser,
            ITenantPermissionReader permissions,
            ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
            this.currentTenantUser = currentTenantUser ?? throw new ArgumentNullException(nameof(currentTenantUser));
            this.permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
            this.correlationIdAccessor = correlationIdAccessor;
        }

        [HttpPost]
        [TenantAuthorize(TenantPermissions.TablesManage)]
        public async Task<ActionResult<TableResponse>> Create(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromBody] AddTableRequest request,
            CancellationToken cancellationToken)
        {
            var command = new AddTableCommand(branchId, request.Label);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return CreatedAtAction(
                    nameof(Get),
                    new { slug, branchId, tableId = result.Value.Id },
                    result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("bulk-add")]
        [TenantAuthorize(TenantPermissions.TablesManage)]
        public async Task<ActionResult<TableBulkResult>> BulkAdd(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromBody] AddTablesBulkRequest request,
            CancellationToken cancellationToken)
        {
            var command = new AddTablesBulkCommand(branchId, request.Count);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("bulk-deactivate-highest")]
        [TenantAuthorize(TenantPermissions.TablesManage)]
        public async Task<ActionResult<TableBulkResult>> BulkDeactivateHighest(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromBody] AddTablesBulkRequest request,
            CancellationToken cancellationToken)
        {
            var command = new DeactivateHighestTablesCommand(branchId, request.Count);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet]
        [TenantAuthorize(TenantPolicies.TenantUser)]
        public async Task<ActionResult<OffsetPage<TableResponse>>> List(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromQuery] ListTablesQuery query,
            CancellationToken cancellationToken)
        {
            var listQuery = query with { BranchId = branchId };
            var result = await sender.Send(listQuery, cancellationToken);

            if (result.IsSuccess)
            {
                var canManage = await CanManageTablesAsync(cancellationToken);
                var redactedItems = result.Value.Items.Select(table => new TableResponse
                {
                    Id = table.Id,
                    BranchId = table.BranchId,
                    Number = table.Number,
                    Label = table.Label,
                    Status = table.Status,
                    QrCode = canManage ? table.QrCode : null,
                    CreatedAt = table.CreatedAt,
                    UpdatedAt = table.UpdatedAt,
                    ConcurrencyToken = table.ConcurrencyToken,
                }).ToList();

                var redacted = new OffsetPage<TableResponse>
                {
                    Items = redactedItems,
                    Page = result.Value.Page,
                    PageSize = result.Value.PageSize,
                    TotalCount = result.Value.TotalCount,
                    HasNextPage = result.Value.HasNextPage,
                };
                return Ok(redacted);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{tableId:guid}")]
        [TenantAuthorize(TenantPolicies.TenantUser)]
        public async Task<ActionResult<TableResponse>> Get(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromRoute] Guid tableId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetTableQuery(tableId), cancellationToken);

            if (result.IsSuccess)
            {
                var canManage = await CanManageTablesAsync(cancellationToken);
                return Ok(new TableResponse
                {
                    Id = result.Value.Id,
                    BranchId = result.Value.BranchId,
                    Number = result.Value.Number,
                    Label = result.Value.Label,
                    Status = result.Value.Status,
                    QrCode = canManage ? result.Value.QrCode : null,
                    CreatedAt = result.Value.CreatedAt,
                    UpdatedAt = result.Value.UpdatedAt,
                    ConcurrencyToken = result.Value.ConcurrencyToken,
                });
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPut("{tableId:guid}")]
        [TenantAuthorize(TenantPermissions.TablesManage)]
        public async Task<ActionResult<TableResponse>> Put(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromRoute] Guid tableId,
            [FromBody] UpdateTableRequest request,
            CancellationToken cancellationToken)
        {
            var command = new UpdateTableCommand(tableId, request.Label, request.ConcurrencyToken);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{tableId:guid}/deactivate")]
        [TenantAuthorize(TenantPermissions.TablesManage)]
        public async Task<ActionResult<TableResponse>> Deactivate(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromRoute] Guid tableId,
            [FromBody] BranchLifecycleRequest request,
            CancellationToken cancellationToken)
        {
            var command = new DeactivateTableCommand(tableId, request.ConcurrencyToken);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{tableId:guid}/reactivate")]
        [TenantAuthorize(TenantPermissions.TablesManage)]
        public async Task<ActionResult<TableResponse>> Reactivate(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromRoute] Guid tableId,
            [FromBody] BranchLifecycleRequest request,
            CancellationToken cancellationToken)
        {
            var command = new ReactivateTableCommand(tableId, request.ConcurrencyToken);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{tableId:guid}/qr")]
        [TenantAuthorize(TenantPermissions.TablesManage)]
        [TableQrEndpoint]
        public async Task<ActionResult<TableQrLinkResponse>> GetQr(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromRoute] Guid tableId,
            [FromServices] TableQrLinks links,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetTableQrQuery(branchId, tableId), cancellationToken);
            return result.IsSuccess
                ? Ok(links.Create(result.Value))
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("qr-sheet")]
        [TenantAuthorize(TenantPermissions.TablesManage)]
        [TableQrEndpoint]
        public async Task<ActionResult<IReadOnlyList<TableQrLinkResponse>>> GetQrSheet(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromServices] TableQrLinks links,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetTableQrSheetQuery(branchId), cancellationToken);
            return result.IsSuccess
                ? Ok(result.Value.Select(links.Create).ToArray())
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{tableId:guid}/qr/regenerate")]
        [TenantAuthorize(TenantPermissions.TablesManage)]
        [TableQrEndpoint]
        public async Task<ActionResult<TableQrLinkResponse>> RegenerateQr(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromRoute] Guid tableId,
            [FromBody] BranchLifecycleRequest request,
            [FromServices] TableQrLinks links,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new RegenerateTableQrCommand(branchId, tableId, request.ConcurrencyToken), cancellationToken);
            return result.IsSuccess
                ? Ok(links.Create(result.Value))
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        private async Task<bool> CanManageTablesAsync(CancellationToken cancellationToken)
        {
            if (currentTenantUser.AccountAccess is { IsStaff: false })
            {
                return true;
            }

            var userId = currentTenantUser.UserId;
            if (userId is null || userId == Guid.Empty)
            {
                return false;
            }

            var branchId = currentTenantUser.AccountAccess?.BranchId;
            var grants = await permissions.ReadAsync(userId.Value, cancellationToken, branchId);
            return grants.Contains(TenantPermissions.TablesManage, StringComparer.Ordinal);
        }
    }
}
