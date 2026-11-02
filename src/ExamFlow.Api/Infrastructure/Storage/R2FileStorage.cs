using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace ExamFlow.Api.Infrastructure.Storage;

/// <summary>
/// Cloudflare R2 (S3-compatible) storage for hosts with an ephemeral disk. The bucket stays private: files are only
/// streamed through the API, which checks document and avatar permissions before calling OpenRead.
/// </summary>
public sealed class R2FileStorage : IFileStorage
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;

    public R2FileStorage(IConfiguration configuration)
    {
        string Required(string key) => configuration[$"Storage:R2:{key}"] is { Length: > 0 } value
            ? value : throw new InvalidOperationException($"Storage:R2:{key} is required when Storage:Provider is R2.");
        _bucket = Required("Bucket");
        _client = new AmazonS3Client(new BasicAWSCredentials(Required("AccessKeyId"), Required("SecretAccessKey")), new AmazonS3Config
        {
            ServiceURL = $"https://{Required("AccountId")}.r2.cloudflarestorage.com",
            AuthenticationRegion = "auto",
            ForcePathStyle = true,
            // R2 does not support the newer default CRC checksums of the AWS SDK.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        });
    }

    public async Task SaveAsync(string fileName, Stream content, CancellationToken ct)
    {
        // Uploads are size-limited (Documents:MaxFileSizeBytes), so buffering gives R2 the length it needs.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket, Key = fileName, InputStream = buffer, AutoCloseStream = false, DisablePayloadSigning = true,
        }, ct);
    }

    public Stream OpenRead(string fileName)
    {
        // The interface is synchronous; objects are small (≤ upload limit) so they are read into memory.
        GetObjectResponse response;
        try { response = _client.GetObjectAsync(_bucket, fileName).GetAwaiter().GetResult(); }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { throw new FileNotFoundException("Stored file not found.", fileName); }
        using var _ = response;
        var memory = new MemoryStream();
        response.ResponseStream.CopyTo(memory);
        memory.Position = 0;
        return memory;
    }

    public void Delete(string fileName) => _client.DeleteObjectAsync(_bucket, fileName).GetAwaiter().GetResult();
}
