using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master.Onboarding
{
    internal sealed class PaymentTransactionStore(MasterDbContext context) : IPaymentTransactionStore
    {
        public Task<PlatformPaymentTransaction?> FindByIdempotencyKeyAsync(Guid onboardingRequestId, string idempotencyKey, CancellationToken cancellationToken)
        {
            return context.PlatformPaymentTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(transaction =>
                    transaction.OnboardingRequestId == onboardingRequestId
                    && transaction.IdempotencyKey == idempotencyKey, cancellationToken);
        }

        public Task AddAsync(PlatformPaymentTransaction transaction, CancellationToken cancellationToken)
        {
            context.PlatformPaymentTransactions.Add(transaction);
            return Task.CompletedTask;
        }
    }
}
