using System.Security.Cryptography;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Media;
using CafeHub.Application.Media.Contracts;

namespace CafeHub.UnitTests.Media
{
    internal sealed class FakeMediaStorage : IMediaStorage
    {
        private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

        public int Count => files.Count;

        public async Task<Result<MediaStorageResult>> SaveAsync(
            MediaKey key,
            Stream content,
            CancellationToken cancellationToken)
        {
            if (files.ContainsKey(key.Value))
            {
                return Result.Failure<MediaStorageResult>(MediaErrors.KeyExists());
            }

            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            var bytes = buffer.ToArray();

            if (bytes.Length == 0)
            {
                return Result.Failure<MediaStorageResult>(MediaErrors.InvalidFile());
            }

            files[key.Value] = bytes;
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));

            return Result.Success(new MediaStorageResult(key, bytes.Length, sha256));
        }

        public Task<Stream?> OpenAsync(MediaKey key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(files.TryGetValue(key.Value, out var bytes)
                ? (Stream?)new MemoryStream(bytes, writable: false)
                : null);
        }

        public Task<bool> ExistsAsync(MediaKey key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(files.ContainsKey(key.Value));
        }

        public Task<bool> DeleteAsync(MediaKey key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(files.Remove(key.Value));
        }

        public byte[] ContentOf(MediaKey key)
        {
            return files[key.Value];
        }
    }
}
