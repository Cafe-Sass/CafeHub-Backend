using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Billing
{
    internal sealed class BillStore(
        TenantDbContext context,
        IBillPublicReferenceGenerator referenceGenerator,
        ITenantAuditWriter auditWriter) : IBillStore
    {
        private const string TargetType = "bills";

        public async Task<Result<Bill>> ResolveOpenBillAsync(
            Guid branchId,
            Guid tableId,
            DateTimeOffset now,
            Guid? actorId,
            CancellationToken cancellationToken)
        {
            var openBill = await context.Bills
                .Where(bill => bill.TableId == tableId && bill.Status == BillStatus.Open)
                .SingleOrDefaultAsync(cancellationToken);
            if (openBill is not null)
            {
                return Result.Success(openBill);
            }

            var bill = Bill.Create(
                Guid.CreateVersion7(),
                branchId,
                tableId,
                referenceGenerator.Generate(),
                now,
                actorId);
            context.Bills.Add(bill);
            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "bills.opened.succeeded",
                TargetType = TargetType,
                TargetId = bill.Id,
                ActorUserId = actorId,
                Changes = TenantAuditChangeSets.BillOpen(bill),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<Bill>(auditResult.Error!);
            }

            return Result.Success(bill);
        }
    }
}
