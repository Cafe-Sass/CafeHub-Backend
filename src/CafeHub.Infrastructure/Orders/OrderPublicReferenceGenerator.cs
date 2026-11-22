using System.Security.Cryptography;
using CafeHub.Application.Orders.Contracts;

namespace CafeHub.Infrastructure.Orders
{
    internal sealed class OrderPublicReferenceGenerator : IOrderPublicReferenceGenerator
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
