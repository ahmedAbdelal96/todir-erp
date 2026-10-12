using System.Collections.Concurrent;
using System.Text.Json;
using Npgsql;
using Core.Transactions;
using Shared.Primitives.Identifiers;
using Shared.BuildingBlocks.Observability;

namespace Core.Services;

public sealed record ImportChunkJob(Guid BatchId, TenantId TenantId, string ImporterId, int StartRow, int RowCount, DateTimeOffset ScheduledAt);

public interface IImportJobScheduler
{
    Task<ImportChunkJob> ScheduleChunkAsync(ImportBatch batch, int startRow, int rowCount, CancellationToken cancellationToken = default);
}

public sealed class InMemoryImportJobScheduler : IImportJobScheduler
{
    private readonly ConcurrentQueue<ImportChunkJob> jobs = new();
    private readonly ConcurrentDictionary<(TenantId TenantId, Guid BatchId, int StartRow), ImportChunkJob> scheduled = new();

    public Task<ImportChunkJob> ScheduleChunkAsync(ImportBatch batch, int startRow, int rowCount, CancellationToken cancellationToken = default)
    {
        Validate(batch, startRow, rowCount, cancellationToken);
        var job = new ImportChunkJob(batch.Id, batch.TenantId, batch.ImporterId, startRow, rowCount, DateTimeOffset.UtcNow);
        if (scheduled.TryAdd((batch.TenantId, batch.Id, startRow), job))
        {
            jobs.Enqueue(job);
            CoreMetrics.ImportChunksScheduled.Add(1, new KeyValuePair<string, object?>("module", batch.ModuleId));
        }
        else job = scheduled[(batch.TenantId, batch.Id, startRow)];
        return Task.FromResult(job);
    }

    public bool TryDequeue(out ImportChunkJob? job) => jobs.TryDequeue(out job);

    private static void Validate(ImportBatch batch, int startRow, int rowCount, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (batch.Status != ImportStatus.Execute) throw new InvalidOperationException("import.schedule_requires_execute_status");
        if (startRow < 0 || rowCount < 1) throw new ArgumentOutOfRangeException(nameof(rowCount), "import.chunk_bounds_invalid");
    }
}

public sealed class NpgsqlImportJobScheduler(ITransactionCoordinator transactionCoordinator) : IImportJobScheduler
{
    public Task<ImportChunkJob> ScheduleChunkAsync(ImportBatch batch, int startRow, int rowCount, CancellationToken cancellationToken = default)
    {
        if (batch.Status != ImportStatus.Execute) throw new InvalidOperationException("import.schedule_requires_execute_status");
        if (startRow < 0 || rowCount < 1) throw new ArgumentOutOfRangeException(nameof(rowCount), "import.chunk_bounds_invalid");
        return transactionCoordinator.ExecuteAsync(batch.TenantId, async (context, token) =>
        {
            var scheduledAt = DateTimeOffset.UtcNow;
            var job = new ImportChunkJob(batch.Id, batch.TenantId, batch.ImporterId, startRow, rowCount, scheduledAt);
            await using (var command = new NpgsqlCommand("insert into platform.import_batch (id, tenant_id, module_id, importer_id, status, file_id, mapping_json, progress, created_by, created_at) values ($1, $2, $3, $4, $5, $6, $7::jsonb, $8, $9, $10) on conflict (tenant_id, id) do nothing", context.Connection, context.Transaction))
            {
                command.Parameters.AddWithValue(batch.Id); command.Parameters.AddWithValue(batch.TenantId.Value); command.Parameters.AddWithValue(batch.ModuleId); command.Parameters.AddWithValue(batch.ImporterId); command.Parameters.AddWithValue(batch.Status.ToString()); command.Parameters.AddWithValue(batch.FileId); command.Parameters.AddWithValue(batch.Mapping.GetRawText()); command.Parameters.AddWithValue(batch.Progress); command.Parameters.AddWithValue(batch.CreatedBy); command.Parameters.AddWithValue(batch.CreatedAt);
                await command.ExecuteNonQueryAsync(token);
            }
            DateTimeOffset? persistedAt;
            var isNew = false;
            await using (var chunk = new NpgsqlCommand("insert into platform.import_chunk_job (tenant_id, batch_id, start_row, row_count, scheduled_at) values ($1, $2, $3, $4, $5) on conflict (tenant_id, batch_id, start_row) do nothing returning scheduled_at", context.Connection, context.Transaction))
            {
                chunk.Parameters.AddWithValue(batch.TenantId.Value); chunk.Parameters.AddWithValue(batch.Id); chunk.Parameters.AddWithValue(startRow); chunk.Parameters.AddWithValue(rowCount); chunk.Parameters.AddWithValue(scheduledAt);
                var insertedAt = await chunk.ExecuteScalarAsync(token);
                if (insertedAt is DateTimeOffset insertedDate) { persistedAt = insertedDate; isNew = true; }
                else if (insertedAt is DateTime insertedDateTime) { persistedAt = new DateTimeOffset(DateTime.SpecifyKind(insertedDateTime, DateTimeKind.Utc)); isNew = true; }
                else persistedAt = null;
            }
            if (persistedAt is null)
            {
                await using var existing = new NpgsqlCommand("select scheduled_at, row_count from platform.import_chunk_job where tenant_id = $1 and batch_id = $2 and start_row = $3", context.Connection, context.Transaction);
                existing.Parameters.AddWithValue(batch.TenantId.Value); existing.Parameters.AddWithValue(batch.Id); existing.Parameters.AddWithValue(startRow);
                await using var reader = await existing.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new InvalidOperationException("import.chunk_schedule_missing");
                persistedAt = reader.GetFieldValue<DateTimeOffset>(0);
                var persistedRowCount = reader.GetInt32(1);
                if (persistedRowCount != rowCount) throw new InvalidOperationException("import.chunk_schedule_conflict");
            }
            job = job with { ScheduledAt = persistedAt.Value };
            if (isNew)
            {
                context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "platform.import.chunk.requested", JsonSerializer.Serialize(new { job.BatchId, job.ImporterId, job.StartRow, job.RowCount }), batch.TenantId, job.ScheduledAt, Guid.CreateVersion7(), batch.Id, "platform-import"));
                CoreMetrics.ImportChunksScheduled.Add(1, new KeyValuePair<string, object?>("module", batch.ModuleId));
            }
            return job;
        }, cancellationToken);
    }
}
