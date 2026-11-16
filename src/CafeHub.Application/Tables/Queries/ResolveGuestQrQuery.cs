using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Tables.DTOs;

namespace CafeHub.Application.Tables.Queries
{
    public sealed class ResolveGuestQrQuery(string? code) : IQuery<GuestQrContext>, ISensitiveRequest
    {
        public string? Code { get; } = code;
    }
}
