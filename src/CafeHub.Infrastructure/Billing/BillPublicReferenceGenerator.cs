using System.Security.Cryptography;
using CafeHub.Application.Billing.Contracts;

namespace CafeHub.Infrastructure.Billing
{
    internal sealed class BillPublicReferenceGenerator : IBillPublicReferenceGenerator
    {
        private const int ReferenceBytes = 16;

        public string Generate()
        {
            var buffer = new byte[ReferenceBytes];
            RandomNumberGenerator.Fill(buffer);
            return Convert.ToBase64String(buffer)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}
