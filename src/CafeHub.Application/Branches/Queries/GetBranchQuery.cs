using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Branches.DTOs;

namespace CafeHub.Application.Branches.Queries
{
    public sealed record GetBranchQuery(Guid BranchId) : IQuery<BranchResponse>;
}
