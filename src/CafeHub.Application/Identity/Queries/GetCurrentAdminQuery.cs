using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Identity.DTOs;

namespace CafeHub.Application.Identity.Queries
{
    public sealed record GetCurrentAdminQuery : IQuery<CurrentAdminResponse>;
}
