namespace KwaWicks.Application.Interfaces;

public interface IS3Service
{
    Task<string> GeneratePresignedUploadUrlAsync(string key, string contentType, CancellationToken ct);
    Task<string> GeneratePresignedViewUrlAsync(string key, CancellationToken ct);
    Task<string> GeneratePresignedViewUrlAsync(string key, int expiryMinutes, CancellationToken ct);
    Task<string> UploadObjectAsync(string key, byte[] data, string contentType, CancellationToken ct);
}
