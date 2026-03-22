using Amazon.S3;
using Amazon.S3.Model;
using KwaWicks.Application.Interfaces;

namespace KwaWicks.Infrastructure.S3;

public class S3Service : IS3Service
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;

    public S3Service(IAmazonS3 s3, string bucketName)
    {
        _s3 = s3 ?? throw new ArgumentNullException(nameof(s3));
        _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
    }

    public Task<string> GeneratePresignedUploadUrlAsync(string key, string contentType, CancellationToken ct)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = DateTime.UtcNow.AddMinutes(15)
        };

        var url = _s3.GetPreSignedURL(request);
        return Task.FromResult(url);
    }

    public Task<string> GeneratePresignedViewUrlAsync(string key, CancellationToken ct)
        => GeneratePresignedViewUrlAsync(key, 15, ct);

    public Task<string> GeneratePresignedViewUrlAsync(string key, int expiryMinutes, CancellationToken ct)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(expiryMinutes)
        };

        var url = _s3.GetPreSignedURL(request);
        return Task.FromResult(url);
    }

    public async Task<string> UploadObjectAsync(string key, byte[] data, string contentType, CancellationToken ct)
    {
        using var stream = new MemoryStream(data);
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType
        };

        await _s3.PutObjectAsync(request, ct);
        return key;
    }
}
