using System.Security.Cryptography;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Domain.Tenants;

namespace CafeHub.Infrastructure.Attendance
{
    internal sealed class AttendanceQrTokenGenerator : IAttendanceQrTokenGenerator
    {
        public string Generate()
        {
            var bytes = RandomNumberGenerator.GetBytes(AttendanceQrSession.TokenBytes);

            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
