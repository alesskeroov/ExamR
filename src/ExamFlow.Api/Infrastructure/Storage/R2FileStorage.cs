using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace ExamFlow.Api.Infrastructure.Storage;

/// <summary>
/// S3-compatible storage for hosts with an ephemeral disk: Cloudflare R2 (Storage:Provider=R2) or any S3 endpoint such
/// as Supabase Storage (Storage:Provider=S3). The bucket stays private: files are only streamed through the API, which
/// checks document and avatar permissions before calling OpenRead.
/// </summary>
public sealed class R2FileStorage : IFileStorage
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private readonly bool _unsignedPayload;

    public R2FileStorage(IConfiguration configuration)
    {
        var r2 = !string.Equals(configuration["Storage:Provider"], "S3", StringComparison.OrdinalIgnoreCase);
        var section = r2 ? "R2" : "S3";
        string Required(string key) => configuration[$"Storage:{section}:{key}"] is { Length: > 0 } value
            ? value : throw new InvalidOperationException($"Storage:{section}:{key} is required when Storage:Provider is {section}.");
        _bucket = Required("Bucket");
        // R2 recommends unsigned payloads; generic S3 endpoints (Supabase) get fully signed requests.
        _unsignedPayload = r2;
        _client = new AmazonS3Client(new BasicAWSCredentials(Required("AccessKeyId"), Required("SecretAccessKey")), new AmazonS3Config
        {
            // Supabase: https://<project-ref>.supabase.co/storage/v1/s3 with the project's region (e.g. eu-central-1).
            ServiceURL = r2 ? $"https://{Required("AccountId")}.r2.cloudflarestorage.com" : Required("ServiceUrl"),
            AuthenticationRegion = r2 ? "auto" : Required("Region"),
            ForcePathStyle = true,
            // R2 and Supabase do not support the newer default CRC checksums of the AWS SDK.
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
            BucketName = _bucket, Key = fileName, InputStream = buffer, AutoCloseStream = false, DisablePayloadSigning = _unsignedPayload,
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
