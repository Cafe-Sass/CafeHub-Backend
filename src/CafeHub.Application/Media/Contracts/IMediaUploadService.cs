using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Media.Contracts
{
    public interface IMediaUploadService
    {
        Task<Result<MediaUploadResult>> UploadAsync(
            MediaScope scope,
            MediaPurpose purpose,
            Stream content,
            CancellationToken cancellationToken);
    }
}
