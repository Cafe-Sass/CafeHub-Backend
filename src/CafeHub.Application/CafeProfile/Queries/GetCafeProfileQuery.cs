using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.CafeProfile.DTOs;

namespace CafeHub.Application.CafeProfile.Queries
{
    /// <summary>
    /// Reads the signed-in tenant's cafe profile. Read-only: no data changes.
    /// </summary>
    public sealed record GetCafeProfileQuery : IQuery<CafeProfileResponse>;
}
