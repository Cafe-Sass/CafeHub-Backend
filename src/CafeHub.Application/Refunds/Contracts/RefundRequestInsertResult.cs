using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Refunds.Contracts
{
    public sealed record RefundRequestInsertResult(RefundRequest Request, bool AlreadyExisted);
}
