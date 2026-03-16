namespace KwaWicks.Application.Interfaces;

public interface IS3Service
{
    Task<string> GeneratePresignedUploadUrlAsync(string key, string contentType, CancellationToken ct);
    Task<string> GeneratePresignedViewUrlAsync(string key, CancellationToken ct);
}
