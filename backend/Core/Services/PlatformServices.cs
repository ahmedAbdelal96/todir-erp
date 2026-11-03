using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Core.Transactions;
using Shared.Primitives.Identifiers;
using Shared.Contracts.Platform;

namespace Core.Services;

public sealed class NullPlatformOrganizationCapabilities : IPlatformOrganizationCapabilities
{
    public Task<bool> BranchBelongsToLegalEntityAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult(false);
}

public sealed class NpgsqlPlatformOrganizationCapabilities(NpgsqlDataSource dataSource) : IPlatformOrganizationCapabilities
{
    public async Task<bool> BranchBelongsToLegalEntityAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid branchId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var scope = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            scope.Parameters.AddWithValue(tenantId.Value.ToString("D"));
            await scope.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var command = new NpgsqlCommand("select exists(select 1 from platform.branch where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Active')", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(legalEntityId.Value); command.Parameters.AddWithValue(branchId);
        var result = (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}

public sealed record AuditEntry(Guid Id, TenantId TenantId, Guid? LegalEntityId, Guid? BranchId, string ActorType, Guid? ActorId, Guid? MembershipId, string Module, string Action, string EntityType, Guid EntityId, string? BeforeJson, string? AfterJson, string? Reason, string Source, Guid CorrelationId, string? TraceId, DateTimeOffset OccurredAt, string? MetadataJson);
public interface IAuditWriter { Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default); }
public sealed class InMemoryAuditWriter : IAuditWriter
{
    private readonly List<AuditEntry> entries = [];
    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default) { entries.Add(entry); return Task.CompletedTask; }
    public IReadOnlyList<AuditEntry> Snapshot() => entries.ToArray();
}
public sealed class NpgsqlAuditWriter(NpgsqlDataSource dataSource) : IAuditWriter
{
    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await WriteAsync(connection, transaction, entry, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public static async Task WriteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, AuditEntry entry, CancellationToken cancellationToken = default)
    {
        const string sql = "insert into platform.audit_entry (id, tenant_id, legal_entity_id, branch_id, actor_type, actor_id, membership_id, module, action, entity_type, entity_id, before_json, after_json, reason, source, correlation_id, trace_id, occurred_at, metadata_json) values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12::jsonb, $13::jsonb, $14, $15, $16, $17, $18, $19::jsonb)";
        await using (var context = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction))
        {
            context.Parameters.AddWithValue(entry.TenantId.Value.ToString("D"));
            await context.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(entry.Id); command.Parameters.AddWithValue(entry.TenantId.Value); command.Parameters.AddWithValue((object?)entry.LegalEntityId ?? DBNull.Value); command.Parameters.AddWithValue((object?)entry.BranchId ?? DBNull.Value); command.Parameters.AddWithValue(entry.ActorType); command.Parameters.AddWithValue((object?)entry.ActorId ?? DBNull.Value); command.Parameters.AddWithValue((object?)entry.MembershipId ?? DBNull.Value); command.Parameters.AddWithValue(entry.Module); command.Parameters.AddWithValue(entry.Action); command.Parameters.AddWithValue(entry.EntityType); command.Parameters.AddWithValue(entry.EntityId); command.Parameters.AddWithValue((object?)entry.BeforeJson ?? DBNull.Value); command.Parameters.AddWithValue((object?)entry.AfterJson ?? DBNull.Value); command.Parameters.AddWithValue((object?)entry.Reason ?? DBNull.Value); command.Parameters.AddWithValue(entry.Source); command.Parameters.AddWithValue(entry.CorrelationId); command.Parameters.AddWithValue((object?)entry.TraceId ?? DBNull.Value); command.Parameters.AddWithValue(entry.OccurredAt); command.Parameters.AddWithValue((object?)entry.MetadataJson ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed record SequenceDefinition(string Key, TenantId TenantId, Guid? LegalEntityId, Guid? BranchId, string? ScopeKey, string? PeriodKey, string Prefix, int Padding, long NextValue);
public interface ISequenceAllocator { Task<string> AllocateAsync(SequenceDefinition definition, CancellationToken cancellationToken = default); }
public interface ITransactionalSequenceAllocator { Task<string> AllocateAsync(ITransactionContext context, SequenceDefinition definition, CancellationToken cancellationToken = default); }
public sealed class InMemorySequenceAllocator : ISequenceAllocator, ITransactionalSequenceAllocator
{
    private readonly object gate = new();
    private readonly Dictionary<(TenantId TenantId, string Key, Guid? LegalEntityId, Guid? BranchId, string? ScopeKey, string? PeriodKey), long> values = [];
    public Task<string> AllocateAsync(SequenceDefinition definition, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = (definition.TenantId, definition.Key, definition.LegalEntityId, definition.BranchId, definition.ScopeKey, definition.PeriodKey);
        lock (gate)
        {
            var value = values.TryGetValue(key, out var current) ? current : definition.NextValue;
            values[key] = value + 1;
            return Task.FromResult($"{definition.Prefix}{value.ToString(CultureInfo.InvariantCulture).PadLeft(definition.Padding, '0')}");
        }
    }
    public Task<string> AllocateAsync(ITransactionContext context, SequenceDefinition definition, CancellationToken cancellationToken = default) => AllocateAsync(definition, cancellationToken);
}
public sealed class NpgsqlSequenceAllocator(NpgsqlDataSource dataSource) : ISequenceAllocator, ITransactionalSequenceAllocator
{
    public async Task<string> AllocateAsync(ITransactionContext context, SequenceDefinition definition, CancellationToken cancellationToken = default)
    {
        await using (var ensure = new NpgsqlCommand("insert into platform.sequence_definition(id,tenant_id,sequence_key,legal_entity_id,branch_id,scope_key,period_key,prefix_template,padding,mode,next_value,version) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Standard',$10,1) on conflict do nothing", context.Connection, context.Transaction))
        {
            ensure.Parameters.AddWithValue(Guid.CreateVersion7()); ensure.Parameters.AddWithValue(definition.TenantId.Value); ensure.Parameters.AddWithValue(definition.Key);
            ensure.Parameters.AddWithValue((object?)definition.LegalEntityId ?? DBNull.Value); ensure.Parameters.AddWithValue((object?)definition.BranchId ?? DBNull.Value);
            ensure.Parameters.AddWithValue((object?)definition.ScopeKey ?? DBNull.Value); ensure.Parameters.AddWithValue((object?)definition.PeriodKey ?? DBNull.Value);
            ensure.Parameters.AddWithValue(definition.Prefix); ensure.Parameters.AddWithValue(definition.Padding); ensure.Parameters.AddWithValue(definition.NextValue);
            await ensure.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var command = new NpgsqlCommand("update platform.sequence_definition set next_value = next_value + 1, version = version + 1 where tenant_id = $1 and sequence_key = $2 and legal_entity_id is not distinct from $3 and branch_id is not distinct from $4 and scope_key is not distinct from $5 and period_key is not distinct from $6 returning prefix_template, next_value - 1, padding", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(definition.TenantId.Value);
        command.Parameters.AddWithValue(definition.Key);
        command.Parameters.AddWithValue((object?)definition.LegalEntityId ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)definition.BranchId ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)definition.ScopeKey ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)definition.PeriodKey ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new KeyNotFoundException("sequence.not_found");
        var number = $"{reader.GetString(0)}{reader.GetInt64(1).ToString(CultureInfo.InvariantCulture).PadLeft(reader.GetInt32(2), '0')}";
        return number;
    }

    public async Task<string> AllocateAsync(SequenceDefinition definition, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var context = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction))
        {
            context.Parameters.AddWithValue(definition.TenantId.Value.ToString("D"));
            await context.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var command = new NpgsqlCommand("update platform.sequence_definition set next_value = next_value + 1, version = version + 1 where tenant_id = $1 and sequence_key = $2 and legal_entity_id is not distinct from $3 and branch_id is not distinct from $4 and scope_key is not distinct from $5 and period_key is not distinct from $6 returning prefix_template, next_value - 1, padding", connection, transaction);
        command.Parameters.AddWithValue(definition.TenantId.Value);
        command.Parameters.AddWithValue(definition.Key);
        command.Parameters.AddWithValue((object?)definition.LegalEntityId ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)definition.BranchId ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)definition.ScopeKey ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)definition.PeriodKey ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new KeyNotFoundException("sequence.not_found");
        var number = $"{reader.GetString(0)}{reader.GetInt64(1).ToString(CultureInfo.InvariantCulture).PadLeft(reader.GetInt32(2), '0')}";
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return number;
    }
}

public enum SettingScope { Tenant, LegalEntity, Branch, User }
public sealed record SettingDefinition(string Key, string DataType, SettingScope Scope, JsonElement DefaultValue, bool Sensitive);
public sealed record SettingValue(string Key, SettingScope Scope, string ScopeId, JsonElement Value, long Version);
public interface ISettingsResolver { JsonElement Resolve(string key, IReadOnlyCollection<SettingValue> values, IReadOnlyDictionary<string, SettingDefinition> definitions); }
public sealed class SettingsResolver : ISettingsResolver
{
    public JsonElement Resolve(string key, IReadOnlyCollection<SettingValue> values, IReadOnlyDictionary<string, SettingDefinition> definitions)
    {
        if (!definitions.TryGetValue(key, out var definition)) throw new KeyNotFoundException("setting.not_registered");
        var value = values.Where(v => v.Key == key).OrderByDescending(v => ScopeRank(v.Scope)).FirstOrDefault();
        if (value is null) return definition.DefaultValue;
        if (!IsType(value.Value, definition.DataType)) throw new InvalidOperationException("setting.invalid_type");
        return value.Value;
    }
    private static int ScopeRank(SettingScope scope) => scope switch { SettingScope.User => 4, SettingScope.Branch => 3, SettingScope.LegalEntity => 2, SettingScope.Tenant => 1, _ => 0 };
    private static bool IsType(JsonElement value, string type) => type switch { "string" => value.ValueKind == JsonValueKind.String, "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False, "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _), "number" => value.ValueKind == JsonValueKind.Number, "json" => true, _ => false };
}

public enum FileStatus { Requested, Uploading, Quarantined, Scanned, Ready, Rejected }
public sealed record FileRecord(Guid Id, TenantId TenantId, string OwnerModule, string OwnerEntityType, Guid OwnerEntityId, string ObjectKey, string OriginalName, string ContentType, long Size, string Sha256, FileStatus Status, string Classification, Guid CreatedBy, DateTimeOffset CreatedAt);
public sealed class S3FileDownloadUrlProvider : IDisposable
{
    private readonly AmazonS3Client? client;
    private readonly string? bucket;
    private readonly bool useHttp;

    public S3FileDownloadUrlProvider(IConfiguration configuration)
    {
        bucket = configuration["TODIR_FILES_BUCKET"];
        if (string.IsNullOrWhiteSpace(bucket)) return;

        var endpoint = configuration["TODIR_FILES_PUBLIC_ENDPOINT"];
        useHttp = Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) && endpointUri.Scheme == Uri.UriSchemeHttp;
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

    public async Task<Shared.Contracts.Platform.SignedPlatformFileDownload?> CreateAsync(string objectKey, TimeSpan lifetime, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (client is null || bucket is null) throw new PlatformFileStorageUnavailableException();
        if (string.IsNullOrWhiteSpace(objectKey)) throw new InvalidOperationException("files.object_key_invalid");
        var boundedLifetime = lifetime < TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : lifetime > TimeSpan.FromMinutes(15) ? TimeSpan.FromMinutes(15) : lifetime;
        var expiresAt = clock.GetUtcNow().Add(boundedLifetime);
        var protocol = useHttp
            ? Amazon.S3.Protocol.HTTP
            : Amazon.S3.Protocol.HTTPS;
        var url = await client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime,
            Protocol = protocol
        }).WaitAsync(cancellationToken);
        return new(url, expiresAt);
    }

    public void Dispose() => client?.Dispose();
}

public sealed class NpgsqlPlatformFileReferenceCapability(NpgsqlDataSource dataSource, S3FileDownloadUrlProvider downloadUrls, TimeProvider clock) : Shared.Contracts.Platform.IPlatformFileReferenceCapability
{
    public async Task<Shared.Contracts.Platform.VerifiedPlatformFileReference?> VerifyReadyReferenceAsync(TenantId tenantId, Guid fileRef, string ownerModule, string ownerEntityType, Guid ownerEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var scope = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            scope.Parameters.AddWithValue(tenantId.Value.ToString("D")); await scope.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var command = new NpgsqlCommand("select sha256,content_type,size,classification from platform.file_record where tenant_id=$1 and id=$2 and owner_module=$3 and owner_entity_type=$4 and owner_entity_id=$5 and status='Ready'", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(fileRef); command.Parameters.AddWithValue(ownerModule); command.Parameters.AddWithValue(ownerEntityType); command.Parameters.AddWithValue(ownerEntityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        Shared.Contracts.Platform.VerifiedPlatformFileReference? result = null;
        if (await reader.ReadAsync(cancellationToken)) result = new(tenantId, fileRef, reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetString(3));
        await reader.CloseAsync(); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<Shared.Contracts.Platform.SignedPlatformFileDownload?> CreateReadyDownloadAsync(TenantId tenantId, Guid fileRef, string ownerModule, string ownerEntityType, Guid ownerEntityId, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var scope = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            scope.Parameters.AddWithValue(tenantId.Value.ToString("D")); await scope.ExecuteNonQueryAsync(cancellationToken);
        }
        string? objectKey;
        await using (var command = new NpgsqlCommand("select object_key from platform.file_record where tenant_id=$1 and id=$2 and owner_module=$3 and owner_entity_type=$4 and owner_entity_id=$5 and status='Ready'", connection, transaction))
        {
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(fileRef); command.Parameters.AddWithValue(ownerModule); command.Parameters.AddWithValue(ownerEntityType); command.Parameters.AddWithValue(ownerEntityId);
            objectKey = (string?)await command.ExecuteScalarAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        if (objectKey is null) return null;
        if (!objectKey.StartsWith($"tenant/{tenantId.Value:D}/{ownerModule}/{ownerEntityType}/{ownerEntityId:D}/", StringComparison.Ordinal)) return null;
        return await downloadUrls.CreateAsync(objectKey, lifetime, clock, cancellationToken);
    }
}
public sealed class NullPlatformFileReferenceCapability : Shared.Contracts.Platform.IPlatformFileReferenceCapability
{
    public Task<Shared.Contracts.Platform.VerifiedPlatformFileReference?> VerifyReadyReferenceAsync(TenantId tenantId, Guid fileRef, string ownerModule, string ownerEntityType, Guid ownerEntityId, CancellationToken cancellationToken = default) => Task.FromResult<Shared.Contracts.Platform.VerifiedPlatformFileReference?>(null);
    public Task<Shared.Contracts.Platform.SignedPlatformFileDownload?> CreateReadyDownloadAsync(TenantId tenantId, Guid fileRef, string ownerModule, string ownerEntityType, Guid ownerEntityId, TimeSpan lifetime, CancellationToken cancellationToken = default) => Task.FromResult<Shared.Contracts.Platform.SignedPlatformFileDownload?>(null);
}
public sealed class NullPlatformFileUploadCapability : Shared.Contracts.Platform.IPlatformFileUploadCapability
{
    public Task<Shared.Contracts.Platform.SignedPlatformFileUpload?> CreateUploadIntentAsync(TenantId tenantId, Guid actorId, Guid membershipId, string ownerModule, string ownerEntityType, Guid ownerEntityId, string originalName, string contentType, long sizeBytes, string classification, CancellationToken cancellationToken = default) => Task.FromResult<Shared.Contracts.Platform.SignedPlatformFileUpload?>(null);
    public Task<Shared.Contracts.Platform.PlatformFileUploadCompletion?> CompleteUploadAsync(TenantId tenantId, Guid actorId, Guid membershipId, string ownerModule, string ownerEntityType, Guid ownerEntityId, Guid fileRef, CancellationToken cancellationToken = default) => Task.FromResult<Shared.Contracts.Platform.PlatformFileUploadCompletion?>(null);
}
public static class FileObjectKeys
{
    public static string Create(FileRecord file) => $"tenant/{file.TenantId.Value:D}/{file.OwnerModule}/{file.OwnerEntityType}/{file.OwnerEntityId:D}/{file.Id:D}";
    public static string Hash(ReadOnlySpan<byte> content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}

public sealed record CustomFieldDefinition(Guid Id, TenantId TenantId, string TargetEntityType, string StableKey, string LabelAr, string LabelEn, string FieldType, bool RequiredUi, JsonElement? DefaultValue, JsonElement? Validation, string? Visibility, bool IsActive, long Version);
public interface ICustomFieldRegistry { void Register(CustomFieldDefinition definition); void Rename(TenantId tenantId, string targetEntityType, string stableKey, string labelAr, string labelEn); CustomFieldDefinition Find(TenantId tenantId, string targetEntityType, string stableKey); }
public sealed class CustomFieldRegistry : ICustomFieldRegistry
{
    private readonly Dictionary<(TenantId, string, string), CustomFieldDefinition> values = [];
    public void Register(CustomFieldDefinition definition) { var key = (definition.TenantId, definition.TargetEntityType, definition.StableKey); if (values.ContainsKey(key)) throw new InvalidOperationException("custom_field.duplicate_key"); values[key] = definition; }
    public void Rename(TenantId tenantId, string targetEntityType, string stableKey, string labelAr, string labelEn)
    {
        var key = (tenantId, targetEntityType, stableKey);
        if (!values.TryGetValue(key, out var definition)) throw new KeyNotFoundException("custom_field.not_found");
        values[key] = definition with { LabelAr = labelAr, LabelEn = labelEn, Version = definition.Version + 1 };
    }
    public CustomFieldDefinition Find(TenantId tenantId, string targetEntityType, string stableKey) => values.TryGetValue((tenantId, targetEntityType, stableKey), out var value) ? value : throw new KeyNotFoundException("custom_field.not_found");
}

public enum ImportStatus { Upload, Detect, Map, Validate, Preview, DryRun, Execute, Reconcile, Completed, FailedCorrectable, FailedFatal, Cancelled }
public sealed record ImportBatch(Guid Id, TenantId TenantId, string ModuleId, string ImporterId, ImportStatus Status, Guid FileId, JsonElement Mapping, decimal Progress, Guid CreatedBy, DateTimeOffset CreatedAt);
public interface IImporter { string ImporterId { get; } Task ValidateAsync(ImportBatch batch, CancellationToken cancellationToken = default); Task ExecuteChunkAsync(ImportBatch batch, int startRow, int rowCount, CancellationToken cancellationToken = default); Task ReconcileAsync(ImportBatch batch, CancellationToken cancellationToken = default); }

public enum NotificationChannel { InApp, Email, WhatsApp, Sms }
public enum NotificationStatus { Pending, Sending, Sent, Failed }
public sealed record Notification(Guid Id, TenantId TenantId, string RecipientRef, string TemplateKey, NotificationChannel Channel, JsonElement Payload, NotificationStatus Status, int Attempts, DateTimeOffset CreatedAt, DateTimeOffset? SentAt);
public interface INotificationSender
{
    NotificationChannel Channel { get; }
    Task SendAsync(Notification notification, CancellationToken cancellationToken = default);
}
