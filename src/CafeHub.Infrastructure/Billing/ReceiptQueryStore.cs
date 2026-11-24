using CafeHub.Application.Billing.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Billing
{
    internal sealed class ReceiptQueryStore(TenantDbContext context) : IReceiptQueryStore
    {
        public async Task<Receipt?> FindByIdAsync(
            Guid? branchId,
            Guid receiptId,
            CancellationToken cancellationToken)
        {
            var receipts = Snapshots().Where(receipt => receipt.Id == receiptId);
            if (branchId is { } branch)
            {
                receipts = receipts.Where(receipt => context.Bills.Any(
                    bill => bill.Id == receipt.BillId && bill.BranchId == branch));
            }

            return await receipts.SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<Receipt?> FindByBillAsync(
            Guid? branchId,
            Guid billId,
            CancellationToken cancellationToken)
        {
            var receipts = Snapshots().Where(receipt => receipt.BillId == billId);
            if (branchId is { } branch)
            {
                receipts = receipts.Where(receipt => context.Bills.Any(
                    bill => bill.Id == receipt.BillId && bill.BranchId == branch));
            }

            return await receipts.SingleOrDefaultAsync(cancellationToken);
        }

        public Task<Receipt?> FindByBillReferenceAsync(
            string billReference,
            CancellationToken cancellationToken) =>
            Snapshots()
                .Where(receipt => context.Bills.Any(
                    bill => bill.Id == receipt.BillId && bill.PublicReference == billReference))
                .SingleOrDefaultAsync(cancellationToken);

        private IQueryable<Receipt> Snapshots() =>
            context.Receipts.AsNoTracking()
                .Include(receipt => receipt.Lines)
                .Include(receipt => receipt.Payments);
    }
}
