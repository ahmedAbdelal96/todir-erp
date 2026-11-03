using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Platform;

namespace Core.Services;

public sealed record IncomingPlatformFile(byte[] Content, string ContentType);

public sealed partial class S3PlatformFileStorage : IDisposable
{
    private readonly AmazonS3Client? client;
    private readonly string? bucket;
    private readonly ILogger<S3PlatformFileStorage> logger;

    public S3PlatformFileStorage(IConfiguration configuration, ILogger<S3PlatformFileStorage> logger)
    {
        this.logger = logger;
        bucket = configuration["TODIR_FILES_BUCKET"];
        if (string.IsNullOrWhiteSpace(bucket)) return;

        var endpoint = configuration["TODIR_FILES_PUBLIC_ENDPOINT"];
        var region = configuration["TODIR_FILES_REGION"] ?? "us-east-1";
        var clientConfiguration = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region),
            ForcePathStyle = !string.IsNullOrWhiteSpace(endpoint)
        };
        if (!string.IsNullOrWhiteSpace(endpoint)) clientConfiguration.ServiceURL = endpoint;

        var accessKey = configuration["TODIR_FILES_ACCESS_KEY"];
        var secretKey = configuration["TODIR_FILES_SECRET_KEY"];
        if (string.IsNullOrWhiteSpace(accessKey) != string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("files.storage_credentials_incomplete");
        client = string.IsNullOrWhiteSpace(accessKey)
            ? new AmazonS3Client(clientConfiguration)
            : new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), clientConfiguration);
    }

    public async Task EnsurePrivateBucketAsync(CancellationToken cancellationToken)
    {
        if (client is null || bucket is null) throw new PlatformFileStorageUnavailableException();
        try
        {
            if (!await AmazonS3Util.DoesS3BucketExistV2Async(client, bucket).WaitAsync(cancellationToken))
                await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket, CannedACL = S3CannedACL.Private }, cancellationToken);
        }
        catch (AmazonS3Exception exception) when (exception.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
            // Another API instance may create the configured private bucket first.
        }
        catch (AmazonS3Exception exception)
        {
            LogS3Failure("private-bucket", exception.ErrorCode, exception.StatusCode);
            throw new PlatformFileStorageUnavailableException();
        }
        catch (AmazonClientException exception)
        {
            LogClientFailure("private-bucket", exception.GetType().Name);
            throw new PlatformFileStorageUnavailableException();
        }
    }

    public async Task<SignedPlatformFileUpload> CreateUploadUrlAsync(Guid fileRef, string objectKey, string contentType, long sizeBytes, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (client is null || bucket is null) throw new PlatformFileStorageUnavailableException();
        var expiresAt = clock.GetUtcNow().AddMinutes(5);
        try
        {
            var post = await client.CreatePresignedPostAsync(new CreatePresignedPostRequest
            {
                BucketName = bucket,
                Key = objectKey,
                Expires = expiresAt.UtcDateTime,
                Fields = new Dictionary<string, string> { ["Content-Type"] = contentType, ["acl"] = "private" },
                Conditions =
                [
                    S3PostCondition.ExactMatch("key", objectKey),
                    S3PostCondition.ExactMatch("Content-Type", contentType),
                    S3PostCondition.ExactMatch("acl", "private"),
                    S3PostCondition.ContentLengthRange(sizeBytes, sizeBytes)
                ]
            }).WaitAsync(cancellationToken);
            return new(fileRef, post.Url, post.Fields, contentType, sizeBytes, expiresAt);
        }
        catch (AmazonS3Exception exception)
        {
            LogS3Failure("presigned-post", exception.ErrorCode, exception.StatusCode);
            throw new PlatformFileStorageUnavailableException();
        }
        catch (AmazonClientException exception)
        {
            LogClientFailure("presigned-post", exception.GetType().Name);
            throw new PlatformFileStorageUnavailableException();
        }
    }

    public async Task<IncomingPlatformFile?> ReadIncomingAsync(string objectKey, long expectedSizeBytes, long maximumSizeBytes, CancellationToken cancellationToken)
    {
        if (client is null || bucket is null) throw new PlatformFileStorageUnavailableException();
        try
        {
            using var response = await client.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = objectKey }, cancellationToken);
            if (response.ContentLength != expectedSizeBytes || response.ContentLength is < 1 || response.ContentLength > maximumSizeBytes)
                throw new PlatformFileContentRejectedException("files.size_invalid");
            await using var buffer = new MemoryStream(checked((int)response.ContentLength));
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length != response.ContentLength || buffer.Length > maximumSizeBytes)
                throw new PlatformFileContentRejectedException("files.size_invalid");
            return new(buffer.ToArray(), response.Headers.ContentType ?? string.Empty);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound || exception.ErrorCode is "NoSuchKey" or "NoSuchBucket")
        {
            return null;
        }
        catch (AmazonS3Exception)
        {
            throw new PlatformFileStorageUnavailableException();
        }
        catch (AmazonClientException)
        {
            throw new PlatformFileStorageUnavailableException();
        }
    }

    public async Task StoreReadyObjectAsync(string objectKey, string contentType, byte[] content, CancellationToken cancellationToken)
    {
        if (client is null || bucket is null) throw new PlatformFileStorageUnavailableException();
        try
        {
            await using var stream = new MemoryStream(content, writable: false);
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = objectKey,
                InputStream = stream,
                ContentType = contentType,
                CannedACL = S3CannedACL.Private,
                AutoCloseStream = false
            }, cancellationToken);
        }
        catch (AmazonS3Exception)
        {
            throw new PlatformFileStorageUnavailableException();
        }
        catch (AmazonClientException)
        {
            throw new PlatformFileStorageUnavailableException();
        }
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        if (client is null || bucket is null) throw new PlatformFileStorageUnavailableException();
        try { await client.DeleteObjectAsync(bucket, objectKey, cancellationToken); }
        catch (AmazonS3Exception) { throw new PlatformFileStorageUnavailableException(); }
        catch (AmazonClientException) { throw new PlatformFileStorageUnavailableException(); }
    }

    public void Dispose() => client?.Dispose();

    [LoggerMessage(7200, LogLevel.Error, "Platform Files operation {Operation} failed with S3 error {ErrorCode} ({StatusCode}).")]
    private partial void LogS3Failure(string operation, string? errorCode, System.Net.HttpStatusCode statusCode);

    [LoggerMessage(7201, LogLevel.Error, "Platform Files operation {Operation} failed with client error type {ErrorType}.")]
    private partial void LogClientFailure(string operation, string errorType);
}

public sealed class PlatformFileContentRejectedException(string reasonCode) : Exception(reasonCode)
{
    public string ReasonCode { get; } = reasonCode;
}
