using System.Text.Json;
using Core.Transactions;
using Npgsql;
using Shared.Contracts.Platform;
using Shared.Primitives.Identifiers;

namespace Core.Services;

public sealed class NpgsqlPlatformFileUploadCapability(
    NpgsqlDataSource dataSource,
    S3PlatformFileStorage storage,
    IPlatformFileMalwareScanner malwareScanner,
    TimeProvider clock) : IPlatformFileUploadCapability
{
    public async Task<SignedPlatformFileUpload?> CreateUploadIntentAsync(
        TenantId tenantId,
        Guid actorId,
        Guid membershipId,
        string ownerModule,
        string ownerEntityType,
        Guid ownerEntityId,
        string originalName,
        string contentType,
        long sizeBytes,
        string classification,
        CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty || membershipId == Guid.Empty) throw new PlatformFileContentRejectedException("files.actor_invalid");
        var metadata = PlatformFileUploadPolicy.ValidateRequest(ownerModule, ownerEntityType, ownerEntityId, originalName, contentType, sizeBytes, classification);
        await storage.EnsurePrivateBucketAsync(cancellationToken);

        var fileId = Guid.CreateVersion7();
        var now = clock.GetUtcNow();
        var objectKey = $"tenant/{tenantId.Value:D}/{metadata.OwnerModule}/{metadata.OwnerEntityType}/{ownerEntityId:D}/{fileId:D}";
        var uploadObjectKey = $"incoming/tenant/{tenantId.Value:D}/{metadata.OwnerModule}/{metadata.OwnerEntityType}/{ownerEntityId:D}/{fileId:D}/{Guid.CreateVersion7():N}";
        var file = new UploadRecord(fileId, tenantId, metadata.OwnerModule, metadata.OwnerEntityType, ownerEntityId, objectKey, uploadObjectKey,
            metadata.OriginalName, metadata.ContentType, sizeBytes, new string('0', 64), "Uploading", metadata.Classification, actorId, now);
        var signed = await storage.CreateUploadUrlAsync(fileId, uploadObjectKey, metadata.ContentType, sizeBytes, clock, cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using (var insert = new NpgsqlCommand("insert into platform.file_record(id,tenant_id,owner_module,owner_entity_type,owner_entity_id,object_key,upload_object_key,original_name,content_type,size,sha256,status,classification,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,'Uploading',$12,$13,$14)", connection, transaction))
        {
            insert.Parameters.AddWithValue(file.Id); insert.Parameters.AddWithValue(file.TenantId.Value); insert.Parameters.AddWithValue(file.OwnerModule);
            insert.Parameters.AddWithValue(file.OwnerEntityType); insert.Parameters.AddWithValue(file.OwnerEntityId); insert.Parameters.AddWithValue(file.ObjectKey);
            insert.Parameters.AddWithValue(file.UploadObjectKey); insert.Parameters.AddWithValue(file.OriginalName); insert.Parameters.AddWithValue(file.ContentType);
            insert.Parameters.AddWithValue(file.Size); insert.Parameters.AddWithValue(file.Sha256); insert.Parameters.AddWithValue(file.Classification);
            insert.Parameters.AddWithValue(file.CreatedBy); insert.Parameters.AddWithValue(file.CreatedAt);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await WriteAuditAsync(connection, transaction, file, membershipId, "platform.file.upload_intent.created", null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return signed;
    }

    public async Task<PlatformFileUploadCompletion?> CompleteUploadAsync(
        TenantId tenantId,
        Guid actorId,
        Guid membershipId,
        string ownerModule,
        string ownerEntityType,
        Guid ownerEntityId,
        Guid fileRef,
        CancellationToken cancellationToken = default)
    {
        var file = await LoadAsync(tenantId, ownerModule, ownerEntityType, ownerEntityId, fileRef, cancellationToken);
        if (file is null) return null;
        if (file.CreatedBy != actorId) return null;
        if (file.Status == "Ready") return new(true, false);
        if (file.Status == "Scanned")
        {
            var ready = await TransitionAsync(file, actorId, membershipId, "Ready", null, null, cancellationToken);
            return ready || (await LoadAsync(tenantId, ownerModule, ownerEntityType, ownerEntityId, fileRef, cancellationToken))?.Status == "Ready"
                ? new(true, false)
                : new(false, false, "files.upload_state_changed");
        }
        if (file.Status is not ("Uploading" or "Quarantined")) return new(false, file.Status == "Rejected", "files.upload_state_invalid");

        IncomingPlatformFile? incoming;
        try { incoming = await storage.ReadIncomingAsync(file.UploadObjectKey, file.Size, PlatformFileUploadPolicy.MaximumSizeBytes, cancellationToken); }
        catch (PlatformFileContentRejectedException exception)
        {
            await RejectAsync(file, actorId, membershipId, exception.ReasonCode, cancellationToken);
            return new(false, true, exception.ReasonCode);
        }
        if (incoming is null) return new(false, false, "files.upload_object_missing");
        if (!string.Equals(incoming.ContentType, file.ContentType, StringComparison.OrdinalIgnoreCase)
            || !PlatformFileUploadPolicy.ContentMatches(file.OriginalName, file.ContentType, incoming.Content))
        {
            await RejectAsync(file, actorId, membershipId, "files.content_validation_failed", cancellationToken);
            return new(false, true, "files.content_validation_failed");
        }

        var sha256 = FileObjectKeys.Hash(incoming.Content);
        if (file.Status == "Quarantined" && !string.Equals(file.Sha256, sha256, StringComparison.Ordinal))
        {
            await RejectAsync(file, actorId, membershipId, "files.upload_content_changed", cancellationToken);
            return new(false, true, "files.upload_content_changed");
        }
        if (file.Status == "Uploading")
        {
            if (!await TransitionAsync(file, actorId, membershipId, "Quarantined", sha256, null, cancellationToken))
            {
                var latest = await LoadAsync(tenantId, ownerModule, ownerEntityType, ownerEntityId, fileRef, cancellationToken);
                if (latest is null) return null;
                file = latest;
                if (file.Status == "Ready") return new(true, false);
                if (file.Status != "Quarantined" || !string.Equals(file.Sha256, sha256, StringComparison.Ordinal)) return new(false, false, "files.upload_state_changed");
            }
            else
            {
                file = file with { Status = "Quarantined", Sha256 = sha256 };
            }
        }

        var isClean = await malwareScanner.IsCleanAsync(incoming.Content, cancellationToken);
        if (!isClean)
        {
            await RejectAsync(file, actorId, membershipId, "files.malware_detected", cancellationToken);
            try { await storage.DeleteAsync(file.UploadObjectKey, cancellationToken); } catch (PlatformFileStorageUnavailableException) { }
            return new(false, true, "files.malware_detected");
        }

        await storage.StoreReadyObjectAsync(file.ObjectKey, file.ContentType, incoming.Content, cancellationToken);
        if (!await TransitionAsync(file, actorId, membershipId, "Scanned", null, null, cancellationToken))
        {
            var latest = await LoadAsync(tenantId, ownerModule, ownerEntityType, ownerEntityId, fileRef, cancellationToken);
            if (latest is null) return null;
            file = latest;
            if (file.Status == "Ready") return new(true, false);
            if (file.Status != "Scanned") return new(false, false, "files.upload_state_changed");
        }
        else
        {
            file = file with { Status = "Scanned" };
        }

        var finalTransition = await TransitionAsync(file, actorId, membershipId, "Ready", null, null, cancellationToken);
        if (!finalTransition && (await LoadAsync(tenantId, ownerModule, ownerEntityType, ownerEntityId, fileRef, cancellationToken))?.Status != "Ready")
            return new(false, false, "files.upload_state_changed");
        try { await storage.DeleteAsync(file.UploadObjectKey, cancellationToken); } catch (PlatformFileStorageUnavailableException) { }
        return new(true, false);
    }

    private async Task<UploadRecord?> LoadAsync(TenantId tenantId, string ownerModule, string ownerEntityType, Guid ownerEntityId, Guid fileId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("select object_key,upload_object_key,original_name,content_type,size,sha256,status,classification,created_by,created_at from platform.file_record where tenant_id=$1 and id=$2 and owner_module=$3 and owner_entity_type=$4 and owner_entity_id=$5", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(fileId); command.Parameters.AddWithValue(ownerModule); command.Parameters.AddWithValue(ownerEntityType); command.Parameters.AddWithValue(ownerEntityId);
        UploadRecord? result = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
                result = new(fileId, tenantId, ownerModule, ownerEntityType, ownerEntityId, reader.GetString(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    reader.GetString(2), reader.GetString(3), reader.GetInt64(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetGuid(8), reader.GetFieldValue<DateTimeOffset>(9));
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<bool> TransitionAsync(UploadRecord file, Guid actorId, Guid membershipId, string nextStatus, string? sha256, string? reasonCode, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, file.TenantId, cancellationToken);
        await using var update = new NpgsqlCommand("update platform.file_record set status=$1,sha256=coalesce($2::text,sha256) where tenant_id=$3 and id=$4 and owner_module=$5 and owner_entity_type=$6 and owner_entity_id=$7 and status=$8 returning 1", connection, transaction);
        update.Parameters.AddWithValue(nextStatus); update.Parameters.AddWithValue((object?)sha256 ?? DBNull.Value); update.Parameters.AddWithValue(file.TenantId.Value); update.Parameters.AddWithValue(file.Id);
        update.Parameters.AddWithValue(file.OwnerModule); update.Parameters.AddWithValue(file.OwnerEntityType); update.Parameters.AddWithValue(file.OwnerEntityId); update.Parameters.AddWithValue(file.Status);
        if (await update.ExecuteScalarAsync(cancellationToken) is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
        await WriteAuditAsync(connection, transaction, file with { Status = nextStatus, Sha256 = sha256 ?? file.Sha256 }, membershipId,
            $"platform.file.status.{nextStatus.ToLowerInvariant()}", reasonCode, cancellationToken, actorId);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task RejectAsync(UploadRecord file, Guid actorId, Guid membershipId, string reasonCode, CancellationToken cancellationToken)
    {
        if (file.Status is "Uploading" or "Quarantined" or "Scanned")
            await TransitionAsync(file, actorId, membershipId, "Rejected", null, reasonCode, cancellationToken);
        try { if (!string.IsNullOrWhiteSpace(file.UploadObjectKey)) await storage.DeleteAsync(file.UploadObjectKey, cancellationToken); } catch (PlatformFileStorageUnavailableException) { }
        try { await storage.DeleteAsync(file.ObjectKey, cancellationToken); } catch (PlatformFileStorageUnavailableException) { }
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenantId, CancellationToken cancellationToken)
    {
        await using var scope = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction);
        scope.Parameters.AddWithValue(tenantId.Value.ToString("D"));
        await scope.ExecuteNonQueryAsync(cancellationToken);
    }

    private Task WriteAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, UploadRecord file, Guid membershipId, string action, string? reasonCode, CancellationToken cancellationToken, Guid? actorId = null) =>
        NpgsqlAuditWriter.WriteAsync(connection, transaction, new AuditEntry(Guid.CreateVersion7(), file.TenantId, null, null, "Human", actorId ?? file.CreatedBy, membershipId, "platform", action, "file_record", file.Id, null,
            JsonSerializer.Serialize(new { file.OwnerModule, file.OwnerEntityType, file.OwnerEntityId, file.ContentType, file.Size, file.Sha256, file.Classification, status = action.EndsWith("created", StringComparison.Ordinal) ? "Uploading" : action[(action.LastIndexOf('.') + 1)..], reasonCode }), null,
            "platform-files", file.Id, System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), cancellationToken);

    private sealed record UploadRecord(Guid Id, TenantId TenantId, string OwnerModule, string OwnerEntityType, Guid OwnerEntityId, string ObjectKey, string UploadObjectKey,
        string OriginalName, string ContentType, long Size, string Sha256, string Status, string Classification, Guid CreatedBy, DateTimeOffset CreatedAt);
}
