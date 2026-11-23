using System.Buffers;
using System.Security.Cryptography;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Media;
using CafeHub.Application.Media.Contracts;
using Microsoft.Extensions.Logging;

namespace CafeHub.Infrastructure.Media
{
    public sealed class LocalDiskMediaStorage : IMediaStorage
    {
        private const int CopyBufferSize = 81920;

        private readonly MediaOptions options;
        private readonly ILogger<LocalDiskMediaStorage> logger;
        private readonly string rootDirectory;
        private readonly string rootDirectoryPrefix;

        public LocalDiskMediaStorage(MediaOptions options, ILogger<LocalDiskMediaStorage> logger)
        {
            this.options = options;
            this.logger = logger;
            rootDirectory = options.RootDirectory;
            rootDirectoryPrefix = Path.EndsInDirectorySeparator(rootDirectory)
                ? rootDirectory
                : rootDirectory + Path.DirectorySeparatorChar;
        }

        public async Task<Result<MediaStorageResult>> SaveAsync(
            MediaKey key,
            Stream content,
            CancellationToken cancellationToken)
        {
            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            var path = ResolvePath(key);
            if (File.Exists(path))
            {
                return Result.Failure<MediaStorageResult>(MediaErrors.KeyExists());
            }

            var directory = Path.GetDirectoryName(path);
            if (directory is null)
            {
                return Result.Failure<MediaStorageResult>(MediaErrors.StorageFailed());
            }

            Directory.CreateDirectory(directory);
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var size = 0L;

            try
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await using (var destination = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    CopyBufferSize,
                    FileOptions.Asynchronous))
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
                    try
                    {
                        int read;
                        while ((read = await content.ReadAsync(buffer.AsMemory(0, CopyBufferSize), cancellationToken)) > 0)
                        {
                            size += read;
                            if (size > options.MaxBytes)
                            {
                                return Result.Failure<MediaStorageResult>(MediaErrors.TooLarge());
                            }

                            hash.AppendData(buffer, 0, read);
                            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }

                if (size == 0)
                {
                    return Result.Failure<MediaStorageResult>(MediaErrors.InvalidFile());
                }

                File.Move(temporaryPath, path);
                var sha256 = Convert.ToHexStringLower(hash.GetHashAndReset());
                logger.LogInformation("Stored a media file of {Size} bytes.", size);

                return Result.Success(new MediaStorageResult(key, size, sha256));
            }
            catch (IOException)
            {
                var error = File.Exists(path) ? MediaErrors.KeyExists() : MediaErrors.StorageFailed();
                logger.LogWarning("A media file could not be stored; {Reason}.", error.Code);

                return Result.Failure<MediaStorageResult>(error);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        public Task<Stream?> OpenAsync(MediaKey key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = ResolvePath(key);

            if (!File.Exists(path))
            {
                return Task.FromResult<Stream?>(null);
            }

            Stream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            return Task.FromResult<Stream?>(stream);
        }

        public Task<bool> ExistsAsync(MediaKey key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(File.Exists(ResolvePath(key)));
        }

        public Task<bool> DeleteAsync(MediaKey key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = ResolvePath(key);

            if (!File.Exists(path))
            {
                return Task.FromResult(false);
            }

            File.Delete(path);
            logger.LogInformation("Deleted a media file.");

            return Task.FromResult(true);
        }

        private string ResolvePath(MediaKey key)
        {
            var relativePath = Path.Combine(key.Value.Split('/'));
            var path = Path.GetFullPath(Path.Combine(rootDirectory, relativePath));

            if (!path.StartsWith(rootDirectoryPrefix, options.PathComparison))
            {
                throw new InvalidOperationException("The media key resolves outside the configured root path.");
            }

            return path;
        }
    }
}
