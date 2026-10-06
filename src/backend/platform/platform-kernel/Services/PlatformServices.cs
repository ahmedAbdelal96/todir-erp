using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Npgsql;
using Todir.SharedKernel.Identifiers;

namespace Todir.Platform.Services;

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
        const string sql = "insert into platform_audit.audit_entry (id, tenant_id, legal_entity_id, branch_id, actor_type, actor_id, membership_id, module, action, entity_type, entity_id, before_json, after_json, reason, source, correlation_id, trace_id, occurred_at, metadata_json) values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12::jsonb, $13::jsonb, $14, $15, $16, $17, $18, $19::jsonb)";
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
public sealed class InMemorySequenceAllocator : ISequenceAllocator
{
    private readonly object gate = new();
    private readonly Dictionary<string, long> values = [];
    public Task<string> AllocateAsync(SequenceDefinition definition, CancellationToken cancellationToken = default) { lock (gate) { var value = values.TryGetValue(definition.Key, out var current) ? current : definition.NextValue; values[definition.Key] = value + 1; return Task.FromResult($"{definition.Prefix}{value.ToString(CultureInfo.InvariantCulture).PadLeft(definition.Padding, '0')}"); } }
}
public sealed class NpgsqlSequenceAllocator(NpgsqlDataSource dataSource) : ISequenceAllocator
{
    public async Task<string> AllocateAsync(SequenceDefinition definition, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var context = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction))
        {
            context.Parameters.AddWithValue(definition.TenantId.Value.ToString("D"));
            await context.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var command = new NpgsqlCommand("update platform_numbering.sequence_definition set next_value = next_value + 1, version = version + 1 where tenant_id = $1 and sequence_key = $2 and legal_entity_id is not distinct from $3 and branch_id is not distinct from $4 and scope_key is not distinct from $5 and period_key is not distinct from $6 returning prefix_template, next_value - 1, padding", connection, transaction);
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
