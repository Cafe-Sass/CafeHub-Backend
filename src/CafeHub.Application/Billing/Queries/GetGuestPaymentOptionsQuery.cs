using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Tables;

namespace CafeHub.Application.Billing.Queries
{
    public sealed class GetGuestPaymentOptionsQuery(string? code) : IQuery<GuestPaymentOptionsResponse>, ISensitiveRequest
    {
        public string? Code { get; } = code;
    }
}
