using System.Security.Cryptography;
using CafeHub.Application.Tables.Contracts;

namespace CafeHub.Infrastructure.Tables
{
    internal sealed class TableQrCodeGenerator : ITableQrCodeGenerator
    {
        private const int CodeBytes = 16;

        public string Generate()
        {
            var bytes = RandomNumberGenerator.GetBytes(CodeBytes);

            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
