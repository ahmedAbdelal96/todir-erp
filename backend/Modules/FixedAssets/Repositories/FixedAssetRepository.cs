using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Transactions;
using Core.Services;
using Npgsql;
using Shared.Contracts.Ledger;
using Shared.Contracts.FixedAssets;
using Shared.Contracts.Hr;
using Shared.Contracts.Invoicing;
using Shared.Contracts.Inventory;
using Shared.Contracts.Expenses;
using Shared.Contracts.Purchasing;
using Shared.Contracts.Platform;
using Shared.Primitives.Identifiers;

namespace Modules.FixedAssets;

public sealed class FixedAssetRepository(NpgsqlDataSource dataSource, ITransactionCoordinator coordinator, ITransactionalFixedAssetLedgerPostingCapability ledger, ITransactionalLedgerPostingCapability posting, ILedgerControlBalanceRead ledgerBalances, ITransactionalSequenceAllocator sequences, IPlatformFileReferenceCapability? fileReferences = null, IInvoiceComplianceSnapshotCapability? invoiceSnapshots = null, IHumanResourcesPayrollCapability? hr = null, IHumanResourcesFixedAssetCapability? hrCustody = null, IInventoryFixedAssetCapabilities? inventoryAssets = null, IExpenseFixedAssetCapabilities? expenseAssets = null, IPurchasingFixedAssetCapabilities? purchasingAssets = null, IPlatformApprovalCapability? approvals = null, IFixedAssetRevaluationProvider? revaluationProvider = null, IFixedAssetCountryPolicyProvider? countryPolicy = null) : IFixedAssetCustodyCapabilities
{
    public async Task<Guid?> ResolveLegalEntityAsync(TenantId tenant, string resourceType, Guid resourceId, CancellationToken ct)
    {
        var sql = resourceType switch
        {
            "asset" => "select legal_entity_id from business_assets.fixed_asset where tenant_id=$1 and id=$2",
            "book" => "select a.legal_entity_id from business_assets.asset_book b join business_assets.fixed_asset a on a.tenant_id=b.tenant_id and a.id=b.asset_id where b.tenant_id=$1 and b.id=$2",
            "book-definition" => "select legal_entity_id from business_assets.asset_book_definition where tenant_id=$1 and id=$2",
            "run" => "select legal_entity_id from business_assets.asset_depreciation_run where tenant_id=$1 and id=$2",
            "candidate" => "select legal_entity_id from business_assets.asset_capitalization_candidate where tenant_id=$1 and id=$2",
            "opening" => "select legal_entity_id from business_assets.asset_opening_import where tenant_id=$1 and id=$2",
            "campaign" => "select legal_entity_id from business_assets.asset_verification_campaign where tenant_id=$1 and id=$2",
            _ => throw new ArgumentException("Unknown fixed-asset resource type.", nameof(resourceType))
        };
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(resourceId);
        var value = await command.ExecuteScalarAsync(ct);
        await transaction.CommitAsync(ct);
        return value is Guid entityId ? entityId : null;
    }

    public async Task<bool> LegalEntityExistsAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("select exists(select 1 from platform.legal_entity where tenant_id = $1 and id = $2 and status = 'Active')", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(legalEntityId);
        var result = await command.ExecuteScalarAsync(ct);
        await transaction.CommitAsync(ct);
        return result is true;
    }

    public async Task<AssetCategoryView> CreateCategoryAsync(TenantId tenant, Guid actor, CreateAssetCategoryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) throw new FixedAssetException("assets.category_required");
        if (request.DefaultResidualValue < 0m || request.CapitalizationThreshold < 0m || request.DefaultUsefulLifeMonths is <= 0 || request.DefaultDepreciationMethod is not ("StraightLine" or "NoDepreciation")) throw new FixedAssetException("assets.invalid_category_policy");
        var id = Guid.CreateVersion7();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("""
            insert into business_assets.asset_category (tenant_id,id,code,name,asset_nature,tracking_granularity,requires_location,requires_custody,requires_depreciation,capitalization_threshold,default_depreciation_method,default_useful_life_months,default_residual_value,created_by,created_at)
            values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,now()) returning id
            """, connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(request.Code.Trim()); command.Parameters.AddWithValue(request.Name.Trim()); command.Parameters.AddWithValue(request.AssetNature); command.Parameters.AddWithValue(request.TrackingGranularity); command.Parameters.AddWithValue(request.RequiresLocation); command.Parameters.AddWithValue(request.RequiresCustody); command.Parameters.AddWithValue(request.RequiresDepreciation); command.Parameters.AddWithValue((object?)request.CapitalizationThreshold ?? DBNull.Value); command.Parameters.AddWithValue(request.DefaultDepreciationMethod); command.Parameters.AddWithValue((object?)request.DefaultUsefulLifeMonths ?? DBNull.Value); command.Parameters.AddWithValue(request.DefaultResidualValue); command.Parameters.AddWithValue(actor);
        try { await command.ExecuteNonQueryAsync(ct); await transaction.CommitAsync(ct); }
        catch (PostgresException ex) when (ex.SqlState == "23505") { throw new FixedAssetException("assets.category_code_exists"); }
        return new(id, request.Code.Trim(), request.Name.Trim(), request.AssetNature, request.TrackingGranularity, request.RequiresLocation, request.RequiresCustody, request.RequiresDepreciation, request.CapitalizationThreshold, request.DefaultDepreciationMethod, request.DefaultUsefulLifeMonths, request.DefaultResidualValue);
    }

    public async Task<IReadOnlyList<AssetCategoryView>> ListCategoriesAsync(TenantId tenant, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 200);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id,code,name,asset_nature,tracking_granularity,requires_location,requires_custody,requires_depreciation,capitalization_threshold,default_depreciation_method,default_useful_life_months,default_residual_value from business_assets.asset_category where tenant_id=$1 and active order by code,id limit $2", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(limit);
        return await ReadAsync(command, reader => new AssetCategoryView(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetBoolean(5), reader.GetBoolean(6), reader.GetBoolean(7), reader.IsDBNull(8) ? null : reader.GetDecimal(8), reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetInt32(10), reader.GetDecimal(11)), ct);
    }

    public async Task<AssetView> CreateAssetAsync(TenantId tenant, Guid actor, CreateAssetRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty || request.CategoryId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name)) throw new FixedAssetException("assets.required");
        if (request.Quantity is <= 0) throw new FixedAssetException("assets.quantity_invalid");
        if (request.CapitalizationDate < request.AcquisitionDate || request.PlacedInServiceDate < request.CapitalizationDate) throw new FixedAssetException("assets.date_order_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            if (request.ParentAssetId is { } parentAssetId)
            {
                await using var parent = new NpgsqlCommand("select legal_entity_id,lifecycle_state from business_assets.fixed_asset where tenant_id=$1 and id=$2", context.Connection, context.Transaction);
                parent.Parameters.AddWithValue(tenant.Value); parent.Parameters.AddWithValue(parentAssetId);
                await using var parentReader = await parent.ExecuteReaderAsync(token);
                if (!await parentReader.ReadAsync(token)) throw new FixedAssetException("assets.parent_asset_not_found");
                if (parentReader.GetGuid(0) != request.LegalEntityId || parentReader.GetString(1) is "Disposed" or "Archived") throw new FixedAssetException("assets.parent_asset_invalid");
                await parentReader.CloseAsync();
            }
            var id = Guid.CreateVersion7();
            var number = await sequences.AllocateAsync(context, new SequenceDefinition("fixed-assets.asset-number", tenant, request.LegalEntityId, null, null, null, "FA-", 8, 1), token);
            await using var command = new NpgsqlCommand("""
                insert into business_assets.fixed_asset (tenant_id,id,legal_entity_id,category_id,asset_number,name,description,asset_tag,lifecycle_state,asset_nature,tracking_granularity,product_variant_ref,inventory_serial_ref,source_origin_type,acquisition_date,capitalization_date,placed_in_service_date,parent_asset_id,quantity,created_by,created_at,row_version)
                select $1,$2,$3,c.id,$4,$5,$6,$7,'Draft',c.asset_nature,c.tracking_granularity,$8,$9,$10,$11,$12,$13,$14,coalesce($15,1),$16,now(),1
                from business_assets.asset_category c where c.tenant_id=$1 and c.id=$17 and c.active
                returning asset_number,asset_nature,tracking_granularity,created_at,row_version
                """, context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(request.LegalEntityId); command.Parameters.AddWithValue(number); command.Parameters.AddWithValue(request.Name.Trim()); command.Parameters.AddWithValue((object?)request.Description ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.AssetTag ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.ProductVariantRef ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.InventorySerialRef ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.SourceOriginType ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.AcquisitionDate ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.CapitalizationDate ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.PlacedInServiceDate ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.ParentAssetId ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.Quantity ?? DBNull.Value); command.Parameters.AddWithValue(actor); command.Parameters.AddWithValue(request.CategoryId);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new FixedAssetException("assets.category_not_found");
            var result = new AssetView(id, request.LegalEntityId, request.CategoryId, reader.GetString(0), request.Name.Trim(), request.Description, request.AssetTag, "Draft", reader.GetString(1), reader.GetString(2), request.ProductVariantRef, request.InventorySerialRef, request.AcquisitionDate, request.CapitalizationDate, request.PlacedInServiceDate, request.Quantity ?? 1m, reader.GetFieldValue<DateTimeOffset>(3), reader.GetInt64(4));
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "FixedAssetCreatedV1", JsonSerializer.Serialize(new FixedAssetCreatedV1(id, request.LegalEntityId, result.AssetNumber, result.LifecycleState, result.AssetNature, result.CreatedAt)), tenant, result.CreatedAt, id, id, "fixed-assets"));
            return result;
        }, ct);
    }

    public async Task<PagedAssetsView> ListAssetsAsync(TenantId tenant, Guid? legalEntityId, string? lifecycleState, string? search, string? nature, Guid? locationId, int limit, int offset, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 200); offset = Math.Max(offset, 0);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, tenant, ct);

        const string whereClause = """
            from business_assets.fixed_asset a
            where a.tenant_id = $1
              and ($2::uuid is null or a.legal_entity_id = $2)
              and ($3::text is null or a.lifecycle_state = $3)
              and ($4::text is null or a.asset_number ilike '%' || $4 || '%' or a.name ilike '%' || $4 || '%' or coalesce(a.asset_tag, '') ilike '%' || $4 || '%')
              and ($5::text is null or a.asset_nature = $5)
              and ($6::uuid is null or exists(select 1 from business_assets.asset_assignment aa where aa.tenant_id = a.tenant_id and aa.asset_id = a.id and aa.location_id = $6 and aa.effective_from <= current_date and (aa.effective_to is null or aa.effective_to >= current_date)))
            """;

        await using var countCommand = new NpgsqlCommand($"select count(*) {whereClause}", connection);
        countCommand.Parameters.AddWithValue(tenant.Value);
        countCommand.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value);
        countCommand.Parameters.AddWithValue((object?)lifecycleState ?? DBNull.Value);
        countCommand.Parameters.AddWithValue((object?)search?.Trim() ?? DBNull.Value);
        countCommand.Parameters.AddWithValue((object?)nature?.Trim() ?? DBNull.Value);
        countCommand.Parameters.AddWithValue((object?)locationId ?? DBNull.Value);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);

        await using var command = new NpgsqlCommand($"""
            select a.id,a.legal_entity_id,a.category_id,a.asset_number,a.name,a.description,a.asset_tag,a.lifecycle_state,a.asset_nature,a.tracking_granularity,a.product_variant_ref,a.inventory_serial_ref,a.acquisition_date,a.capitalization_date,a.placed_in_service_date,a.quantity,a.created_at,a.row_version
            {whereClause}
            order by a.asset_number, a.id
            limit $7 offset $8
            """, connection);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)lifecycleState ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)search?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)nature?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)locationId ?? DBNull.Value);
        command.Parameters.AddWithValue(limit);
        command.Parameters.AddWithValue(offset);

        var items = await ReadAsync(command, reader => new AssetView(
            reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetFieldValue<DateOnly>(12),
            reader.IsDBNull(13) ? null : reader.GetFieldValue<DateOnly>(13), reader.IsDBNull(14) ? null : reader.GetFieldValue<DateOnly>(14),
            reader.GetDecimal(15), reader.GetFieldValue<DateTimeOffset>(16), reader.GetInt64(17)), ct);

        return new PagedAssetsView(items, totalCount, limit, offset);
    }

    public Task<FixedAssetPolicyDefaults?> GetCountryBookDefaultsAsync(TenantId tenant, Guid legalEntityId, string bookCode, CancellationToken ct) => countryPolicy is null
        ? Task.FromResult<FixedAssetPolicyDefaults?>(null)
        : countryPolicy.GetBookDefaultsAsync(tenant, legalEntityId, bookCode, ct);

    public async Task<MassTransferPreviewView> PreviewMassTransferAsync(TenantId tenant, MassTransferRequest request, CancellationToken ct)
    {
        ValidateMassTransferRequest(request);
        var requested = request.AssetIds.Distinct().ToArray();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("select id,asset_number,lifecycle_state from business_assets.fixed_asset where tenant_id=$1 and legal_entity_id=$2 and id=any($3::uuid[])", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(request.LegalEntityId); command.Parameters.AddWithValue(requested);
        var found = new Dictionary<Guid, (string Number, string State)>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) found[reader.GetGuid(0)] = (reader.GetString(1), reader.GetString(2));
        await reader.CloseAsync();
        var items = requested.Select(id => !found.TryGetValue(id, out var row)
            ? new MassTransferPreviewItemView(id, null, "Rejected", "assets.mass_transfer_asset_not_found")
            : row.State is "Disposed" or "Archived" or "Draft"
                ? new MassTransferPreviewItemView(id, row.Number, "Rejected", "assets.mass_transfer_asset_not_eligible")
                : new MassTransferPreviewItemView(id, row.Number, "Eligible", null)).ToArray();
        await transaction.CommitAsync(ct);
        return new MassTransferPreviewView(request.LegalEntityId, requested.Length, items.Count(x => x.Status == "Eligible"), items.Count(x => x.Status == "Rejected"), items);
    }

    public async Task<MassTransferView> ExecuteMassTransferAsync(TenantId tenant, Guid actor, MassTransferRequest request, CancellationToken ct)
    {
        ValidateMassTransferRequest(request);
        var requested = request.AssetIds.Distinct().OrderBy(id => id).ToArray();
        var key = request.IdempotencyKey.Trim();
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { request.LegalEntityId, AssetIds = requested, request.Assignment, Reason = request.Reason.Trim() })))).ToLowerInvariant();
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var existing = new NpgsqlCommand("select request_hash,status,response from business_assets.asset_mass_transfer where tenant_id=$1 and idempotency_key=$2 for update", context.Connection, context.Transaction);
            existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(key);
            await using var existingReader = await existing.ExecuteReaderAsync(token);
            if (await existingReader.ReadAsync(token))
            {
                var storedHash = existingReader.GetString(0); var status = existingReader.GetString(1); var response = existingReader.IsDBNull(2) ? null : existingReader.GetFieldValue<string>(2); await existingReader.CloseAsync();
                if (!string.Equals(storedHash, requestHash, StringComparison.Ordinal)) throw new FixedAssetException("assets.mass_transfer_idempotency_conflict");
                if (status != "Completed" || response is null) throw new FixedAssetException("assets.mass_transfer_retry_incomplete");
                return (JsonSerializer.Deserialize<MassTransferView>(response) ?? throw new FixedAssetException("assets.mass_transfer_retry_incomplete")) with { AlreadyApplied = true };
            }
            await existingReader.CloseAsync();

            if (request.Assignment.EmployeeId is { } employeeId)
            {
                if (hr is null || !await hr.EmployeeBelongsToLegalEntityAsync(tenant, employeeId, request.LegalEntityId, token)) throw new FixedAssetException("assets.employee_invalid");
                if (hrCustody is not null && await hrCustody.IsOffboardingEffectiveAsync(tenant, employeeId, request.LegalEntityId, request.Assignment.EffectiveFrom, token)) throw new FixedAssetException("assets.employee_offboarding_effective");
            }
            if (request.Assignment.LocationId is { } locationId)
            {
                await using var location = new NpgsqlCommand("select 1 from business_assets.asset_location where tenant_id=$1 and id=$2 and legal_entity_id=$3 and active", context.Connection, context.Transaction);
                location.Parameters.AddWithValue(tenant.Value); location.Parameters.AddWithValue(locationId); location.Parameters.AddWithValue(request.LegalEntityId); if (await location.ExecuteScalarAsync(token) is null) throw new FixedAssetException("assets.location_invalid");
            }

            var operationId = Guid.CreateVersion7();
            await using var insertOperation = new NpgsqlCommand("insert into business_assets.asset_mass_transfer(tenant_id,id,legal_entity_id,idempotency_key,request_hash,status,requested_count,created_by,created_at) values($1,$2,$3,$4,$5,'InProgress',$6,$7,now())", context.Connection, context.Transaction);
            insertOperation.Parameters.AddWithValue(tenant.Value); insertOperation.Parameters.AddWithValue(operationId); insertOperation.Parameters.AddWithValue(request.LegalEntityId); insertOperation.Parameters.AddWithValue(key); insertOperation.Parameters.AddWithValue(requestHash); insertOperation.Parameters.AddWithValue(requested.Length); insertOperation.Parameters.AddWithValue(actor); await insertOperation.ExecuteNonQueryAsync(token);

            await using var assets = new NpgsqlCommand("select id,asset_number,lifecycle_state from business_assets.fixed_asset where tenant_id=$1 and legal_entity_id=$2 and id=any($3::uuid[]) order by id for update", context.Connection, context.Transaction);
            assets.Parameters.AddWithValue(tenant.Value); assets.Parameters.AddWithValue(request.LegalEntityId); assets.Parameters.AddWithValue(requested);
            var rows = new List<(Guid Id, string Number, string State)>(); await using var assetReader = await assets.ExecuteReaderAsync(token); while (await assetReader.ReadAsync(token)) rows.Add((assetReader.GetGuid(0), assetReader.GetString(1), assetReader.GetString(2))); await assetReader.CloseAsync();
            if (rows.Count != requested.Length || rows.Any(row => row.State is "Disposed" or "Archived" or "Draft")) throw new FixedAssetException("assets.mass_transfer_not_eligible");

            var evidence = JsonSerializer.Serialize(new { reason = request.Reason.Trim(), operationId });
            var applied = new List<MassTransferPreviewItemView>(rows.Count);
            foreach (var row in rows)
            {
                await using var current = new NpgsqlCommand("select id,effective_from from business_assets.asset_assignment where tenant_id=$1 and asset_id=$2 and effective_from <= $3 and (effective_to is null or effective_to >= $3) order by effective_from desc,id desc limit 1 for update", context.Connection, context.Transaction);
                current.Parameters.AddWithValue(tenant.Value); current.Parameters.AddWithValue(row.Id); current.Parameters.AddWithValue(request.Assignment.EffectiveFrom);
                await using var currentReader = await current.ExecuteReaderAsync(token); Guid? currentId = null; DateOnly? currentFrom = null; if (await currentReader.ReadAsync(token)) { currentId = currentReader.GetGuid(0); currentFrom = currentReader.GetFieldValue<DateOnly>(1); } await currentReader.CloseAsync();
                if (currentFrom >= request.Assignment.EffectiveFrom) throw new FixedAssetException("assets.transfer_date_invalid");
                if (currentId is { } previousId)
                {
                    await using var close = new NpgsqlCommand("update business_assets.asset_assignment set effective_to=$1 where tenant_id=$2 and id=$3", context.Connection, context.Transaction); close.Parameters.AddWithValue(request.Assignment.EffectiveFrom.AddDays(-1)); close.Parameters.AddWithValue(tenant.Value); close.Parameters.AddWithValue(previousId); await close.ExecuteNonQueryAsync(token);
                }
                var assignmentId = Guid.CreateVersion7();
                await using var insertAssignment = new NpgsqlCommand("insert into business_assets.asset_assignment(tenant_id,id,asset_id,assignment_type,employee_id,department_id,custodian_name,location_id,effective_from,effective_to,condition,accessories,acknowledgement_reference,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,now())", context.Connection, context.Transaction);
                insertAssignment.Parameters.AddWithValue(tenant.Value); insertAssignment.Parameters.AddWithValue(assignmentId); insertAssignment.Parameters.AddWithValue(row.Id); insertAssignment.Parameters.AddWithValue(request.Assignment.AssignmentType); insertAssignment.Parameters.AddWithValue((object?)request.Assignment.EmployeeId ?? DBNull.Value); insertAssignment.Parameters.AddWithValue((object?)request.Assignment.DepartmentId ?? DBNull.Value); insertAssignment.Parameters.AddWithValue((object?)request.Assignment.CustodianName ?? DBNull.Value); insertAssignment.Parameters.AddWithValue((object?)request.Assignment.LocationId ?? DBNull.Value); insertAssignment.Parameters.AddWithValue(request.Assignment.EffectiveFrom); insertAssignment.Parameters.AddWithValue((object?)request.Assignment.EffectiveTo ?? DBNull.Value); insertAssignment.Parameters.AddWithValue((object?)request.Assignment.Condition ?? DBNull.Value); insertAssignment.Parameters.AddWithValue((object?)request.Assignment.Accessories ?? DBNull.Value); insertAssignment.Parameters.AddWithValue((object?)request.Assignment.AcknowledgementReference ?? DBNull.Value); insertAssignment.Parameters.AddWithValue(actor);
                try { await insertAssignment.ExecuteNonQueryAsync(token); } catch (PostgresException ex) when (ex.SqlState == "23P01") { throw new FixedAssetException("assets.assignment_overlap"); }
                await using var handover = new NpgsqlCommand("insert into business_assets.asset_handover(tenant_id,id,asset_id,assignment_id,handover_type,acknowledged_at,condition,evidence,created_by,created_at) values($1,$2,$3,$4,'Transfer',now(),$5,$6::jsonb,$7,now())", context.Connection, context.Transaction);
                handover.Parameters.AddWithValue(tenant.Value); handover.Parameters.AddWithValue(Guid.CreateVersion7()); handover.Parameters.AddWithValue(row.Id); handover.Parameters.AddWithValue(assignmentId); handover.Parameters.AddWithValue((object?)request.Assignment.Condition ?? DBNull.Value); handover.Parameters.AddWithValue(evidence); handover.Parameters.AddWithValue(actor); await handover.ExecuteNonQueryAsync(token);
                context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "FixedAssetAssignedV1", JsonSerializer.Serialize(new FixedAssetAssignedV1(row.Id, assignmentId, request.LegalEntityId, request.Assignment.EmployeeId, request.Assignment.DepartmentId, request.Assignment.LocationId, request.Assignment.EffectiveFrom, request.Assignment.EffectiveTo, DateTimeOffset.UtcNow)), tenant, DateTimeOffset.UtcNow, assignmentId, row.Id, "fixed-assets"));
                await using var insertItem = new NpgsqlCommand("insert into business_assets.asset_mass_transfer_item(tenant_id,id,operation_id,asset_id,assignment_id,status,created_at) values($1,$2,$3,$4,$5,'Applied',now())", context.Connection, context.Transaction); insertItem.Parameters.AddWithValue(tenant.Value); insertItem.Parameters.AddWithValue(Guid.CreateVersion7()); insertItem.Parameters.AddWithValue(operationId); insertItem.Parameters.AddWithValue(row.Id); insertItem.Parameters.AddWithValue(assignmentId); await insertItem.ExecuteNonQueryAsync(token);
                applied.Add(new MassTransferPreviewItemView(row.Id, row.Number, "Applied", null));
            }

            var result = new MassTransferView(operationId, "Completed", requested.Length, applied.Count, applied);
            var responseJson = JsonSerializer.Serialize(result);
            await using var updateOperation = new NpgsqlCommand("update business_assets.asset_mass_transfer set status='Completed',applied_count=$1,response=$2::jsonb,completed_at=now() where tenant_id=$3 and id=$4", context.Connection, context.Transaction); updateOperation.Parameters.AddWithValue(applied.Count); updateOperation.Parameters.AddWithValue(responseJson); updateOperation.Parameters.AddWithValue(tenant.Value); updateOperation.Parameters.AddWithValue(operationId); await updateOperation.ExecuteNonQueryAsync(token);
            return result;
        }, ct);
    }

    private static void ValidateMassTransferRequest(MassTransferRequest request)
    {
        if (request.LegalEntityId == Guid.Empty || request.AssetIds is null || request.AssetIds.Count is < 1 or > 500 || request.AssetIds.Any(id => id == Guid.Empty) || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Trim().Length > 200 || string.IsNullOrWhiteSpace(request.Reason)) throw new FixedAssetException("assets.mass_transfer_invalid");
        if (request.Assignment is null) throw new FixedAssetException("assets.mass_transfer_assignment_invalid");
        var assignment = request.Assignment;
        if (assignment.AssignmentType is not ("Employee" or "Department" or "Pool" or "LocationOnly" or "External" or "Unassigned")) throw new FixedAssetException("assets.mass_transfer_assignment_invalid");
        if (string.IsNullOrWhiteSpace(assignment.AssignmentType) || assignment.EffectiveFrom == default || assignment.EffectiveTo < assignment.EffectiveFrom || assignment.EmployeeId is null && assignment.DepartmentId is null && string.IsNullOrWhiteSpace(assignment.CustodianName) && assignment.LocationId is null) throw new FixedAssetException("assets.mass_transfer_assignment_invalid");
    }

    public async Task<IReadOnlyList<AssetRegisterRowView>> ListAssetRegisterAsync(TenantId tenant, Guid legalEntityId, DateOnly asOfDate, Guid? bookId, string? lifecycleState, string? search, int limit, int offset, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 200); offset = Math.Max(offset, 0);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("""
            select a.id,a.legal_entity_id,a.asset_number,a.name,a.lifecycle_state,a.asset_nature,a.tracking_granularity,a.asset_tag,a.product_variant_ref,a.inventory_serial_ref,a.quantity,
                   b.id,b.code,b.currency_code,coalesce(b.original_cost,0),coalesce(t.accumulated_depreciation,0),coalesce(t.accumulated_impairment,0),coalesce(t.revaluation_effect,0),
                   current_assignment.assignment_type,current_assignment.employee_id,current_assignment.department_id,current_assignment.custodian_name,current_assignment.location_id
            from business_assets.fixed_asset a
            left join lateral (select ab.id,abd.code,abd.currency_code,ab.original_cost from business_assets.asset_book ab join business_assets.asset_book_definition abd on abd.tenant_id=ab.tenant_id and abd.id=ab.book_definition_id where ab.tenant_id=a.tenant_id and ab.asset_id=a.id and ($4::uuid is null or ab.id=$4) order by ab.id limit 1) b on true
            left join lateral (select coalesce(sum(case when bt.transaction_type='Opening' then bt.accumulated_depreciation when bt.transaction_type='Depreciation' then bt.amount when bt.transaction_type='Reversal' then -bt.amount when bt.transaction_type='Disposal' then -bt.accumulated_depreciation when bt.transaction_type='Reinstatement' then bt.accumulated_depreciation else 0 end),0) accumulated_depreciation,coalesce(sum(case when bt.transaction_type='Impairment' then coalesce(nullif(bt.accumulated_impairment,0),bt.amount) when bt.transaction_type='Disposal' then -bt.accumulated_impairment when bt.transaction_type='Reinstatement' then bt.accumulated_impairment else 0 end),0) accumulated_impairment,coalesce(sum(case when bt.transaction_type='Revaluation' then bt.revaluation_effect when bt.transaction_type='Disposal' then -bt.revaluation_effect when bt.transaction_type='Reinstatement' then bt.revaluation_effect else 0 end),0) revaluation_effect from business_assets.asset_book_transaction bt where bt.tenant_id=a.tenant_id and bt.asset_book_id=b.id and bt.transaction_date <= $3) t on true
            left join lateral (select aa.assignment_type,aa.employee_id,aa.department_id,aa.custodian_name,aa.location_id from business_assets.asset_assignment aa where aa.tenant_id=a.tenant_id and aa.asset_id=a.id and aa.effective_from <= $3 and (aa.effective_to is null or aa.effective_to >= $3) order by aa.effective_from desc,aa.id desc limit 1) current_assignment on true
            where a.tenant_id=$1 and a.legal_entity_id=$2 and ($5::text is null or a.lifecycle_state=$5) and ($6::text is null or a.asset_number ilike '%'||$6||'%' or a.name ilike '%'||$6||'%' or coalesce(a.asset_tag,'') ilike '%'||$6||'%' or coalesce(a.inventory_serial_ref,'') ilike '%'||$6||'%')
            order by a.asset_number,b.id
            limit $7 offset $8
            """, connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(asOfDate); command.Parameters.AddWithValue((object?)bookId ?? DBNull.Value); command.Parameters.AddWithValue((object?)lifecycleState ?? DBNull.Value); command.Parameters.AddWithValue((object?)search?.Trim() ?? DBNull.Value); command.Parameters.AddWithValue(limit); command.Parameters.AddWithValue(offset);
        var result = await ReadAsync(command, reader =>
        {
            var assetId = reader.GetGuid(0); var entityId = reader.GetGuid(1); var assetNumber = reader.GetString(2); var name = reader.GetString(3); var lifecycle = reader.GetString(4); var nature = reader.GetString(5); var tracking = reader.GetString(6); var assetTag = reader.IsDBNull(7) ? null : reader.GetString(7); var productRef = reader.IsDBNull(8) ? null : reader.GetString(8); var serialRef = reader.IsDBNull(9) ? null : reader.GetString(9); var quantity = reader.GetDecimal(10); var rowBookId = reader.IsDBNull(11) ? (Guid?)null : reader.GetGuid(11); var bookCode = reader.IsDBNull(12) ? null : reader.GetString(12); var currency = reader.IsDBNull(13) ? null : reader.GetString(13).Trim(); var gross = reader.GetDecimal(14); var depreciation = Math.Max(0m, reader.GetDecimal(15)); var impairment = Math.Max(0m, reader.GetDecimal(16)); var revaluation = reader.GetDecimal(17); var assignmentType = reader.IsDBNull(18) ? null : reader.GetString(18); var employeeId = reader.IsDBNull(19) ? (Guid?)null : reader.GetGuid(19); var departmentId = reader.IsDBNull(20) ? (Guid?)null : reader.GetGuid(20); var custodian = reader.IsDBNull(21) ? null : reader.GetString(21); var locationId = reader.IsDBNull(22) ? (Guid?)null : reader.GetGuid(22);
            return new AssetRegisterRowView(assetId, entityId, assetNumber, name, lifecycle, nature, tracking, assetTag, productRef, serialRef, quantity, rowBookId, bookCode, currency, gross, depreciation, impairment, revaluation, Math.Max(0m, gross - depreciation - impairment + revaluation), assignmentType, employeeId, departmentId, custodian, locationId);
        }, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<IReadOnlyList<AssetSourceDrilldownView>> ListAssetSourceDrilldownAsync(TenantId tenant, Guid assetId, Guid? bookId, int limit, CancellationToken ct)
    {
        var bounded = Math.Clamp(limit, 1, 500);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("select b.asset_id,t.asset_book_id,bd.code,t.transaction_type,t.transaction_date,t.amount,t.accumulated_depreciation,t.accumulated_impairment,t.revaluation_effect,t.source_reference,t.idempotency_key,t.ledger_fact_reference from business_assets.asset_book_transaction t join business_assets.asset_book b on b.tenant_id=t.tenant_id and b.id=t.asset_book_id join business_assets.asset_book_definition bd on bd.tenant_id=b.tenant_id and bd.id=b.book_definition_id where t.tenant_id=$1 and b.asset_id=$2 and ($3::uuid is null or t.asset_book_id=$3) order by t.transaction_date desc,t.id desc limit $4", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(assetId); command.Parameters.AddWithValue((object?)bookId ?? DBNull.Value); command.Parameters.AddWithValue(bounded);
        var rows = new List<AssetSourceDrilldownView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetFieldValue<DateOnly>(4), reader.GetDecimal(5), reader.IsDBNull(6) ? 0m : reader.GetDecimal(6), reader.IsDBNull(7) ? 0m : reader.GetDecimal(7), reader.IsDBNull(8) ? 0m : reader.GetDecimal(8), reader.IsDBNull(9) ? null : reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetString(11)));
        await reader.CloseAsync(); await transaction.CommitAsync(ct); return rows;
    }

    public async Task<IReadOnlyList<AssetTimelineEventView>> GetAssetTimelineAsync(TenantId tenant, Guid assetId, int limit, CancellationToken ct)
    {
        var bounded = Math.Clamp(limit, 1, 200);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        const string sql = """
            select id, event_type, effective_date, occurred_at, payload
            from (
                select a.id, 'AssetCreated'::text event_type, a.acquisition_date effective_date, a.created_at occurred_at,
                       jsonb_build_object('assetNumber', a.asset_number, 'name', a.name, 'lifecycleState', a.lifecycle_state) payload
                from business_assets.fixed_asset a where a.tenant_id=$1 and a.id=$2
                union all
                select aa.id, 'AssignmentRecorded'::text, aa.effective_from, aa.created_at,
                       jsonb_build_object('assignmentType', aa.assignment_type, 'employeeId', aa.employee_id, 'departmentId', aa.department_id, 'locationId', aa.location_id, 'condition', aa.condition)
                from business_assets.asset_assignment aa where aa.tenant_id=$1 and aa.asset_id=$2
                union all
                select h.id, 'HandoverRecorded'::text, h.created_at::date, h.created_at,
                       jsonb_build_object('assignmentId', h.assignment_id, 'handoverType', h.handover_type, 'acknowledgedAt', h.acknowledged_at, 'condition', h.condition)
                from business_assets.asset_handover h where h.tenant_id=$1 and h.asset_id=$2
                union all
                select bt.id, bt.transaction_type, bt.transaction_date, bt.created_at,
                       jsonb_build_object('sourceReference', bt.source_reference, 'ledgerFactReference', bt.ledger_fact_reference)
                from business_assets.asset_book_transaction bt
                join business_assets.asset_book b on b.tenant_id=bt.tenant_id and b.id=bt.asset_book_id
                where bt.tenant_id=$1 and b.asset_id=$2
                union all
                select d.id, 'DisposalRecorded'::text, d.disposal_date, d.created_at,
                       jsonb_build_object('disposalType', d.disposal_type, 'status', d.status, 'reason', d.reason)
                from business_assets.asset_disposal d where d.tenant_id=$1 and d.asset_id=$2
                union all
                select e.id, e.action, e.occurred_at::date, e.occurred_at,
                       jsonb_build_object('entityType', e.entity_type, 'actorId', e.actor_id)
                from business_assets.asset_audit_event e where e.tenant_id=$1 and e.entity_id=$2
            ) events
            order by occurred_at desc, id desc
            limit $3
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(assetId); command.Parameters.AddWithValue(bounded);
        var rows = new List<AssetTimelineEventView>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            using var payload = await reader.GetFieldValueAsync<JsonDocument>(4, ct);
            rows.Add(new(reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetFieldValue<DateOnly>(2), reader.GetFieldValue<DateTimeOffset>(3), payload.RootElement.Clone()));
        }
        await reader.CloseAsync();
        await transaction.CommitAsync(ct);
        return rows;
    }

    public async Task<AssetLocationView> CreateLocationAsync(TenantId tenant, Guid actor, CreateAssetLocationRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name) || request.LocationType is not ("Site" or "Building" or "Floor" or "Room" or "External" or "Other")) throw new FixedAssetException("assets.location_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            if (request.ParentLocationId is { } parent)
            {
                await using var parentCommand = new NpgsqlCommand("select 1 from business_assets.asset_location where tenant_id=$1 and id=$2 and legal_entity_id=$3 and active", context.Connection, context.Transaction);
                parentCommand.Parameters.AddWithValue(tenant.Value); parentCommand.Parameters.AddWithValue(parent); parentCommand.Parameters.AddWithValue(request.LegalEntityId);
                if (await parentCommand.ExecuteScalarAsync(token) is null) throw new FixedAssetException("assets.parent_location_not_found");
            }
            var id = Guid.CreateVersion7();
            await using var command = new NpgsqlCommand("insert into business_assets.asset_location(tenant_id,id,legal_entity_id,parent_location_id,code,name,location_type,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,now())", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(request.LegalEntityId); command.Parameters.AddWithValue((object?)request.ParentLocationId ?? DBNull.Value); command.Parameters.AddWithValue(request.Code.Trim()); command.Parameters.AddWithValue(request.Name.Trim()); command.Parameters.AddWithValue(request.LocationType); command.Parameters.AddWithValue(actor);
            try { await command.ExecuteNonQueryAsync(token); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new FixedAssetException("assets.location_code_exists"); }
            return new AssetLocationView(id, request.LegalEntityId, request.ParentLocationId, request.Code.Trim(), request.Name.Trim(), request.LocationType, true);
        }, ct);
    }

    public async Task<IReadOnlyList<AssetLocationView>> ListLocationsAsync(TenantId tenant, Guid? legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,parent_location_id,code,name,location_type,active from business_assets.asset_location where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) order by code,id", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value);
        return await ReadAsync(command, reader => new AssetLocationView(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetBoolean(6)), ct);
    }

    public Task<AssetAssignmentView> AssignAsync(TenantId tenant, Guid actor, Guid assetId, AssignAssetRequest request, CancellationToken ct) => AssignInternalAsync(tenant, actor, assetId, request, false, ct);

    public Task<AssetAssignmentView> TransferAsync(TenantId tenant, Guid actor, Guid assetId, AssignAssetRequest request, CancellationToken ct) => AssignInternalAsync(tenant, actor, assetId, request, true, ct);

    private async Task<AssetAssignmentView> AssignInternalAsync(TenantId tenant, Guid actor, Guid assetId, AssignAssetRequest request, bool closeCurrent, CancellationToken ct)
    {
        if (request.EffectiveTo < request.EffectiveFrom) throw new FixedAssetException("assets.assignment_dates_invalid");
        if (request.EmployeeId is null && request.DepartmentId is null && string.IsNullOrWhiteSpace(request.CustodianName)) throw new FixedAssetException("assets.assignee_required");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var assetEntityId = await ResolveAssetEntityAsync(context, tenant, assetId, token);
            if (closeCurrent)
            {
                await using var current = new NpgsqlCommand("select id,effective_from from business_assets.asset_assignment where tenant_id=$1 and asset_id=$2 and effective_from <= $3 and (effective_to is null or effective_to >= $3) order by effective_from desc,id desc limit 1 for update", context.Connection, context.Transaction);
                current.Parameters.AddWithValue(tenant.Value); current.Parameters.AddWithValue(assetId); current.Parameters.AddWithValue(request.EffectiveFrom);
                await using var currentReader = await current.ExecuteReaderAsync(token);
                if (await currentReader.ReadAsync(token))
                {
                    var currentId = currentReader.GetGuid(0); var currentFrom = currentReader.GetFieldValue<DateOnly>(1); await currentReader.CloseAsync();
                    if (currentFrom >= request.EffectiveFrom) throw new FixedAssetException("assets.transfer_date_invalid");
                    await using var close = new NpgsqlCommand("update business_assets.asset_assignment set effective_to=$1 where tenant_id=$2 and id=$3", context.Connection, context.Transaction);
                    close.Parameters.AddWithValue(request.EffectiveFrom.AddDays(-1)); close.Parameters.AddWithValue(tenant.Value); close.Parameters.AddWithValue(currentId); await close.ExecuteNonQueryAsync(token);
                }
                else await currentReader.CloseAsync();
            }
            if (request.EmployeeId is { } employeeId)
            {
                if (hr is null || !await hr.EmployeeBelongsToLegalEntityAsync(tenant, employeeId, assetEntityId, token)) throw new FixedAssetException("assets.employee_invalid");
                if (hrCustody is not null && await hrCustody.IsOffboardingEffectiveAsync(tenant, employeeId, assetEntityId, request.EffectiveFrom, token)) throw new FixedAssetException("assets.employee_offboarding_effective");
            }
            if (request.LocationId is { } locationId)
            {
                await using var location = new NpgsqlCommand("select 1 from business_assets.asset_location where tenant_id=$1 and id=$2 and legal_entity_id=$3 and active", context.Connection, context.Transaction);
                location.Parameters.AddWithValue(tenant.Value); location.Parameters.AddWithValue(locationId); location.Parameters.AddWithValue(assetEntityId);
                if (await location.ExecuteScalarAsync(token) is null) throw new FixedAssetException("assets.location_invalid");
            }
            var id = Guid.CreateVersion7();
            await using var command = new NpgsqlCommand("""
                insert into business_assets.asset_assignment (tenant_id,id,asset_id,assignment_type,employee_id,department_id,custodian_name,location_id,effective_from,effective_to,condition,accessories,acknowledgement_reference,created_by,created_at)
                values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,now()) returning id
                """, context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(assetId); command.Parameters.AddWithValue(request.AssignmentType); command.Parameters.AddWithValue((object?)request.EmployeeId ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.DepartmentId ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.CustodianName ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.LocationId ?? DBNull.Value); command.Parameters.AddWithValue(request.EffectiveFrom); command.Parameters.AddWithValue((object?)request.EffectiveTo ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.Condition ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.Accessories ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.AcknowledgementReference ?? DBNull.Value); command.Parameters.AddWithValue(actor);
            try { await command.ExecuteNonQueryAsync(token); } catch (PostgresException ex) when (ex.SqlState == "23P01") { throw new FixedAssetException("assets.assignment_overlap"); }
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "FixedAssetAssignedV1", JsonSerializer.Serialize(new FixedAssetAssignedV1(assetId, id, assetEntityId, request.EmployeeId, request.DepartmentId, request.LocationId, request.EffectiveFrom, request.EffectiveTo, DateTimeOffset.UtcNow)), tenant, DateTimeOffset.UtcNow, id, assetId, "fixed-assets"));
            return new AssetAssignmentView(id, assetId, request.AssignmentType, request.EmployeeId, request.DepartmentId, request.CustodianName, request.LocationId, request.EffectiveFrom, request.EffectiveTo, request.Condition, request.Accessories, request.AcknowledgementReference);
        }, ct);
    }

    public async Task<AssetAssignmentView> ReturnAsync(TenantId tenant, Guid actor, Guid assetId, ReturnAssetRequest request, CancellationToken ct)
    {
        if (request.EffectiveFrom == default || request.Evidence.ValueKind is not JsonValueKind.Object) throw new FixedAssetException("assets.return_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var assetEntityId = await ResolveAssetEntityAsync(context, tenant, assetId, token);
            await using var current = new NpgsqlCommand("select id,assignment_type,employee_id,department_id,custodian_name,location_id,effective_from,effective_to,condition,accessories,acknowledgement_reference from business_assets.asset_assignment where tenant_id=$1 and asset_id=$2 and effective_from <= $3 and (effective_to is null or effective_to >= $3) order by effective_from desc,id desc limit 1 for update", context.Connection, context.Transaction);
            current.Parameters.AddWithValue(tenant.Value); current.Parameters.AddWithValue(assetId); current.Parameters.AddWithValue(request.EffectiveFrom);
            await using var currentReader = await current.ExecuteReaderAsync(token);
            if (!await currentReader.ReadAsync(token)) throw new FixedAssetException("assets.return_assignment_not_found");
            var assignmentId = currentReader.GetGuid(0); var assignmentType = currentReader.GetString(1); var employeeId = currentReader.IsDBNull(2) ? (Guid?)null : currentReader.GetGuid(2); var departmentId = currentReader.IsDBNull(3) ? (Guid?)null : currentReader.GetGuid(3); var custodian = currentReader.IsDBNull(4) ? null : currentReader.GetString(4); var existingLocation = currentReader.IsDBNull(5) ? (Guid?)null : currentReader.GetGuid(5); var effectiveFrom = currentReader.GetFieldValue<DateOnly>(6); var effectiveTo = currentReader.IsDBNull(7) ? (DateOnly?)null : currentReader.GetFieldValue<DateOnly>(7); var existingCondition = currentReader.IsDBNull(8) ? null : currentReader.GetString(8); var existingAccessories = currentReader.IsDBNull(9) ? null : currentReader.GetString(9); var existingAcknowledgement = currentReader.IsDBNull(10) ? null : currentReader.GetString(10); await currentReader.CloseAsync();
            if (assignmentType == "Unassigned") throw new FixedAssetException("assets.return_not_allowed");
            if (effectiveFrom >= request.EffectiveFrom) throw new FixedAssetException("assets.return_date_invalid");
            var locationId = request.LocationId ?? existingLocation;
            if (locationId is { } requestedLocation)
            {
                await using var location = new NpgsqlCommand("select 1 from business_assets.asset_location where tenant_id=$1 and id=$2 and legal_entity_id=$3 and active", context.Connection, context.Transaction);
                location.Parameters.AddWithValue(tenant.Value); location.Parameters.AddWithValue(requestedLocation); location.Parameters.AddWithValue(assetEntityId);
                if (await location.ExecuteScalarAsync(token) is null) throw new FixedAssetException("assets.location_invalid");
            }
            await using var close = new NpgsqlCommand("update business_assets.asset_assignment set effective_to=$1 where tenant_id=$2 and id=$3", context.Connection, context.Transaction);
            close.Parameters.AddWithValue(request.EffectiveFrom.AddDays(-1)); close.Parameters.AddWithValue(tenant.Value); close.Parameters.AddWithValue(assignmentId); await close.ExecuteNonQueryAsync(token);
            var returnedAssignmentId = Guid.CreateVersion7();
            await using var returned = new NpgsqlCommand("insert into business_assets.asset_assignment(tenant_id,id,asset_id,assignment_type,location_id,effective_from,condition,accessories,acknowledgement_reference,created_by,created_at) values($1,$2,$3,'Unassigned',$4,$5,$6,$7,$8,$9,now())", context.Connection, context.Transaction);
            returned.Parameters.AddWithValue(tenant.Value); returned.Parameters.AddWithValue(returnedAssignmentId); returned.Parameters.AddWithValue(assetId); returned.Parameters.AddWithValue((object?)locationId ?? DBNull.Value); returned.Parameters.AddWithValue(request.EffectiveFrom); returned.Parameters.AddWithValue((object?)request.Condition ?? (object?)existingCondition ?? DBNull.Value); returned.Parameters.AddWithValue((object?)request.Accessories ?? (object?)existingAccessories ?? DBNull.Value); returned.Parameters.AddWithValue((object?)request.AcknowledgementReference ?? (object?)existingAcknowledgement ?? DBNull.Value); returned.Parameters.AddWithValue(actor); await returned.ExecuteNonQueryAsync(token);
            var handoverId = Guid.CreateVersion7();
            await using var handover = new NpgsqlCommand("insert into business_assets.asset_handover(tenant_id,id,asset_id,assignment_id,handover_type,acknowledged_at,condition,evidence,created_by,created_at) values($1,$2,$3,$4,'Return',now(),$5,$6::jsonb,$7,now())", context.Connection, context.Transaction);
            handover.Parameters.AddWithValue(tenant.Value); handover.Parameters.AddWithValue(handoverId); handover.Parameters.AddWithValue(assetId); handover.Parameters.AddWithValue(assignmentId); handover.Parameters.AddWithValue((object?)request.Condition ?? DBNull.Value); handover.Parameters.AddWithValue(request.Evidence.GetRawText()); handover.Parameters.AddWithValue(actor); await handover.ExecuteNonQueryAsync(token);
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "FixedAssetAssignedV1", JsonSerializer.Serialize(new FixedAssetAssignedV1(assetId, returnedAssignmentId, assetEntityId, null, null, locationId, request.EffectiveFrom, null, DateTimeOffset.UtcNow)), tenant, DateTimeOffset.UtcNow, returnedAssignmentId, assetId, "fixed-assets"));
            return new AssetAssignmentView(returnedAssignmentId, assetId, "Unassigned", null, null, null, locationId, request.EffectiveFrom, null, request.Condition ?? existingCondition, request.Accessories ?? existingAccessories, request.AcknowledgementReference ?? existingAcknowledgement);
        }, ct);
    }

    public async Task<IReadOnlyList<AssetAssignmentView>> ListAssignmentsAsync(TenantId tenant, Guid assetId, DateOnly? asOfDate, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        const string sql = "select id,asset_id,assignment_type,employee_id,department_id,custodian_name,location_id,effective_from,effective_to,condition,accessories,acknowledgement_reference from business_assets.asset_assignment where tenant_id=$1 and asset_id=$2 and ($3::date is null or effective_from <= $3 and (effective_to is null or effective_to >= $3)) order by effective_from desc,id desc";
        await using var command = new NpgsqlCommand(sql, connection, transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(assetId); command.Parameters.AddWithValue((object?)asOfDate ?? DBNull.Value);
        var rows = new List<AssetAssignmentView>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.GetFieldValue<DateOnly>(7), reader.IsDBNull(8) ? null : reader.GetFieldValue<DateOnly>(8), reader.IsDBNull(9) ? null : reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetString(11)));
        await reader.CloseAsync(); await transaction.CommitAsync(ct); return rows;
    }

    public async Task<IReadOnlyList<FixedAssetCustodyItem>> ListEmployeeAssetsAsync(TenantId tenantId, Guid employeeId, Guid legalEntityId, DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await SetTenantAsync(connection, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("select a.id,a.asset_number,a.name,a.legal_entity_id,aa.id,aa.effective_from,aa.effective_to,aa.location_id,aa.condition from business_assets.asset_assignment aa join business_assets.fixed_asset a on a.tenant_id=aa.tenant_id and a.id=aa.asset_id where aa.tenant_id=$1 and aa.employee_id=$2 and a.legal_entity_id=$3 and aa.effective_from <= $4 and (aa.effective_to is null or aa.effective_to >= $4) and a.lifecycle_state <> 'Archived' order by a.asset_number,a.id,aa.effective_from desc,aa.id desc", connection);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(asOfDate);
        return await ReadAsync(command, reader => new FixedAssetCustodyItem(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetGuid(3), reader.GetGuid(4), reader.GetFieldValue<DateOnly>(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6), reader.IsDBNull(7) ? null : reader.GetGuid(7), reader.IsDBNull(8) ? null : reader.GetString(8)), cancellationToken);
    }

    public async Task<IReadOnlyList<FixedAssetCustodyItem>> ListMyAssetsAsync(TenantId tenantId, Guid membershipId, Guid legalEntityId, DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        if (hr is null) throw new FixedAssetException("assets.hr_source_unavailable");
        var employeeId = await hr.GetSelfEmployeeAsync(tenantId, membershipId, legalEntityId, cancellationToken);
        return employeeId is null ? Array.Empty<FixedAssetCustodyItem>() : await ListEmployeeAssetsAsync(tenantId, employeeId.Value, legalEntityId, asOfDate, cancellationToken);
    }

    public async Task<AssetScanView?> GetAssetScanAsync(TenantId tenant, Guid? legalEntityId, Guid? assetId, string? assetTag, CancellationToken ct)
    {
        if (assetId is null && string.IsNullOrWhiteSpace(assetTag)) return null;
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id,asset_number,name,asset_tag,lifecycle_state from business_assets.fixed_asset where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and ($3::uuid is null or id=$3) and ($4::text is null or asset_tag=$4) limit 1", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value); command.Parameters.AddWithValue((object?)assetId ?? DBNull.Value); command.Parameters.AddWithValue((object?)assetTag?.Trim() ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var id = reader.GetGuid(0); var number = reader.GetString(1); var name = reader.GetString(2); var tag = reader.IsDBNull(3) ? null : reader.GetString(3); var state = reader.GetString(4);
        return new AssetScanView(id, number, name, tag, state, $"fixed-assets://asset/{id:D}");
    }

    public async Task<IReadOnlyList<AssetDocumentView>> ListAssetDocumentsAsync(TenantId tenant, Guid assetId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id,asset_id,file_ref,role,title,sha256,content_type,size_bytes,classification,created_at from business_assets.asset_document_reference where tenant_id=$1 and asset_id=$2 order by created_at desc,id desc", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(assetId);
        return await ReadAsync(command, reader => new AssetDocumentView(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetInt64(7), reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9)), ct);
    }

    public async Task<AssetDocumentView> AddAssetDocumentAsync(TenantId tenant, Guid actor, Guid assetId, AddAssetDocumentRequest request, CancellationToken ct)
    {
        if (request.FileRef == Guid.Empty || request.Role is not ("AcquisitionEvidence" or "Ownership" or "Warranty" or "Insurance" or "Maintenance" or "Verification" or "Other")) throw new FixedAssetException("assets.document_invalid");
        if (fileReferences is null) throw new FixedAssetException("assets.documents_unavailable");
        var verified = await fileReferences.VerifyReadyReferenceAsync(tenant, request.FileRef, "fixed-assets", "asset", assetId, ct) ?? throw new FixedAssetException("assets.document_not_ready");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var asset = new NpgsqlCommand("select 1 from business_assets.fixed_asset where tenant_id=$1 and id=$2", context.Connection, context.Transaction); asset.Parameters.AddWithValue(tenant.Value); asset.Parameters.AddWithValue(assetId); if (await asset.ExecuteScalarAsync(token) is null) throw new FixedAssetException("assets.asset_not_found");
            var id = Guid.CreateVersion7();
            await using var insert = new NpgsqlCommand("insert into business_assets.asset_document_reference(tenant_id,id,asset_id,file_ref,role,title,sha256,content_type,size_bytes,classification,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,now()) returning created_at", context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(assetId); insert.Parameters.AddWithValue(verified.FileRef); insert.Parameters.AddWithValue(request.Role); insert.Parameters.AddWithValue((object?)request.Title?.Trim() ?? DBNull.Value); insert.Parameters.AddWithValue(verified.Sha256.ToLowerInvariant()); insert.Parameters.AddWithValue(verified.ContentType); insert.Parameters.AddWithValue(verified.SizeBytes); insert.Parameters.AddWithValue(verified.Classification); insert.Parameters.AddWithValue(actor);
            try
            {
                var createdAt = (DateTimeOffset)(await insert.ExecuteScalarAsync(token) ?? throw new FixedAssetException("assets.document_create_failed"));
                return new AssetDocumentView(id, assetId, verified.FileRef, request.Role, string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(), verified.Sha256.ToLowerInvariant(), verified.ContentType, verified.SizeBytes, verified.Classification, createdAt);
            }
            catch (PostgresException exception) when (exception.SqlState == "23505") { throw new FixedAssetException("assets.document_exists"); }
        }, ct);
    }

    public async Task<AssetHandoverView> CreateHandoverAsync(TenantId tenant, Guid actor, Guid assetId, CreateAssetHandoverRequest request, CancellationToken ct)
    {
        if (request.AssignmentId == Guid.Empty || request.HandoverType is not ("Issue" or "Return" or "Transfer" or "Acknowledgement") || request.Evidence.ValueKind is not JsonValueKind.Object) throw new FixedAssetException("assets.handover_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var assignment = new NpgsqlCommand("select 1 from business_assets.asset_assignment where tenant_id=$1 and id=$2 and asset_id=$3", context.Connection, context.Transaction);
            assignment.Parameters.AddWithValue(tenant.Value); assignment.Parameters.AddWithValue(request.AssignmentId); assignment.Parameters.AddWithValue(assetId);
            if (await assignment.ExecuteScalarAsync(token) is null) throw new FixedAssetException("assets.assignment_not_found");
            var id = Guid.CreateVersion7(); var createdAt = DateTimeOffset.UtcNow;
            await using var command = new NpgsqlCommand("insert into business_assets.asset_handover(tenant_id,id,asset_id,assignment_id,handover_type,acknowledged_at,condition,evidence,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8::jsonb,$9,$10)", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(assetId); command.Parameters.AddWithValue(request.AssignmentId); command.Parameters.AddWithValue(request.HandoverType); command.Parameters.AddWithValue((object?)request.AcknowledgedAt ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.Condition ?? DBNull.Value); command.Parameters.AddWithValue(request.Evidence.GetRawText()); command.Parameters.AddWithValue(actor); command.Parameters.AddWithValue(createdAt); await command.ExecuteNonQueryAsync(token);
            return new AssetHandoverView(id, assetId, request.AssignmentId, request.HandoverType, request.AcknowledgedAt, request.Condition, request.Evidence.Clone(), createdAt);
        }, ct);
    }

    public async Task<VerificationCampaignView> CreateVerificationCampaignAsync(TenantId tenant, Guid actor, CreateVerificationCampaignRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name)) throw new FixedAssetException("assets.verification_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var id = Guid.CreateVersion7(); var snapshotAt = DateTimeOffset.UtcNow; var createdAt = snapshotAt;
            await using var campaign = new NpgsqlCommand("insert into business_assets.asset_verification_campaign(tenant_id,id,legal_entity_id,name,snapshot_at,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7)", context.Connection, context.Transaction);
            campaign.Parameters.AddWithValue(tenant.Value); campaign.Parameters.AddWithValue(id); campaign.Parameters.AddWithValue(request.LegalEntityId); campaign.Parameters.AddWithValue(request.Name.Trim()); campaign.Parameters.AddWithValue(snapshotAt); campaign.Parameters.AddWithValue(actor); campaign.Parameters.AddWithValue(createdAt); await campaign.ExecuteNonQueryAsync(token);
            await using var lines = new NpgsqlCommand("insert into business_assets.asset_verification_line(tenant_id,id,campaign_id,asset_id,expected_location_id) select $1,gen_random_uuid(),$2,a.id,current_assignment.location_id from business_assets.fixed_asset a left join lateral (select aa.location_id from business_assets.asset_assignment aa where aa.tenant_id=a.tenant_id and aa.asset_id=a.id and aa.effective_from <= $3::date and (aa.effective_to is null or aa.effective_to >= $3::date) order by aa.effective_from desc,aa.id desc limit 1) current_assignment on true where a.tenant_id=$1 and a.legal_entity_id=$4 and a.lifecycle_state <> 'Archived' on conflict (tenant_id,campaign_id,asset_id) do nothing", context.Connection, context.Transaction);
            lines.Parameters.AddWithValue(tenant.Value); lines.Parameters.AddWithValue(id); lines.Parameters.AddWithValue(snapshotAt.Date); lines.Parameters.AddWithValue(request.LegalEntityId); await lines.ExecuteNonQueryAsync(token);
            return await LoadVerificationCampaignAsync(context, tenant, id, token) ?? throw new FixedAssetException("assets.verification_not_found");
        }, ct);
    }

    public async Task<IReadOnlyList<VerificationCampaignView>> ListVerificationCampaignsAsync(TenantId tenant, Guid? legalEntityId, string? status, int limit, CancellationToken ct)
    {
        var bounded = Math.Clamp(limit, 1, 200);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        const string sql = """
            select c.id,c.legal_entity_id,c.name,c.snapshot_at,c.status,
                   count(l.id)::int as line_count,
                   count(l.id) filter (where l.result='Pending')::int as pending_count,
                   c.created_at
            from business_assets.asset_verification_campaign c
            left join business_assets.asset_verification_line l on l.tenant_id=c.tenant_id and l.campaign_id=c.id
            where c.tenant_id=$1 and ($2::uuid is null or c.legal_entity_id=$2) and ($3::text is null or c.status=$3)
            group by c.id,c.legal_entity_id,c.name,c.snapshot_at,c.status,c.created_at
            order by c.created_at desc,c.id desc limit $4
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value); command.Parameters.AddWithValue((object?)status ?? DBNull.Value); command.Parameters.AddWithValue(bounded);
        var rows = new List<VerificationCampaignView>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3), reader.GetString(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetFieldValue<DateTimeOffset>(7)));
        await reader.CloseAsync();
        await transaction.CommitAsync(ct);
        return rows;
    }

    public async Task<VerificationCampaignView> VerifyAssetAsync(TenantId tenant, Guid actor, Guid campaignId, Guid assetId, VerifyAssetRequest request, CancellationToken ct)
    {
        if (request.Result is not ("Found" or "Missing" or "WrongLocation" or "WrongCustodian" or "Damaged" or "Unexpected") || request.Result is ("Found" or "WrongLocation") && request.ObservedLocationId is null) throw new FixedAssetException("assets.verification_result_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            if (request.ObservedLocationId is { } locationId)
            {
                await using var location = new NpgsqlCommand("select 1 from business_assets.asset_location l join business_assets.asset_verification_campaign c on c.tenant_id=l.tenant_id and c.legal_entity_id=l.legal_entity_id where l.tenant_id=$1 and l.id=$2 and c.id=$3 and l.active", context.Connection, context.Transaction);
                location.Parameters.AddWithValue(tenant.Value); location.Parameters.AddWithValue(locationId); location.Parameters.AddWithValue(campaignId); if (await location.ExecuteScalarAsync(token) is null) throw new FixedAssetException("assets.verification_location_invalid");
            }
            if (request.Evidence is { } evidence && evidence.ValueKind is not JsonValueKind.Object and not JsonValueKind.Array) throw new FixedAssetException("assets.verification_evidence_invalid");
            await using var update = new NpgsqlCommand("update business_assets.asset_verification_line set observed_location_id=$1,result=$2,notes=$3,evidence=$4::jsonb,resolved_by=$5,resolved_at=now() where tenant_id=$6 and campaign_id=$7 and asset_id=$8 and exists(select 1 from business_assets.asset_verification_campaign c where c.tenant_id=$6 and c.id=$7 and c.status in ('Open','Review'))", context.Connection, context.Transaction);
            update.Parameters.AddWithValue((object?)request.ObservedLocationId ?? DBNull.Value); update.Parameters.AddWithValue(request.Result); update.Parameters.AddWithValue((object?)request.Notes?.Trim() ?? DBNull.Value); update.Parameters.AddWithValue((object?)request.Evidence?.GetRawText() ?? DBNull.Value); update.Parameters.AddWithValue(actor); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(campaignId); update.Parameters.AddWithValue(assetId); if (await update.ExecuteNonQueryAsync(token) != 1) throw new FixedAssetException("assets.verification_line_not_found");
            await using var status = new NpgsqlCommand("update business_assets.asset_verification_campaign set status='Review' where tenant_id=$1 and id=$2 and status='Open'", context.Connection, context.Transaction); status.Parameters.AddWithValue(tenant.Value); status.Parameters.AddWithValue(campaignId); await status.ExecuteNonQueryAsync(token);
            return await LoadVerificationCampaignAsync(context, tenant, campaignId, token) ?? throw new FixedAssetException("assets.verification_not_found");
        }, ct);
    }

    public async Task<VerificationCampaignView> CloseVerificationCampaignAsync(TenantId tenant, Guid actor, Guid campaignId, CancellationToken ct)
    {
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var pending = new NpgsqlCommand("select count(*) from business_assets.asset_verification_line where tenant_id=$1 and campaign_id=$2 and result='Pending'", context.Connection, context.Transaction); pending.Parameters.AddWithValue(tenant.Value); pending.Parameters.AddWithValue(campaignId); if (Convert.ToInt32(await pending.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture) != 0) throw new FixedAssetException("assets.verification_pending_lines");
            await using var update = new NpgsqlCommand("update business_assets.asset_verification_campaign set status='Completed' where tenant_id=$1 and id=$2 and status in ('Open','Review')", context.Connection, context.Transaction); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(campaignId); if (await update.ExecuteNonQueryAsync(token) != 1) throw new FixedAssetException("assets.verification_not_closeable");
            return await LoadVerificationCampaignAsync(context, tenant, campaignId, token) ?? throw new FixedAssetException("assets.verification_not_found");
        }, ct);
    }

    public async Task<IReadOnlyList<AssetBookView>> ListBooksAsync(TenantId tenant, Guid? legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,code,name,posts_to_ledger,currency_code,default_depreciation_method,default_convention,default_useful_life_months from business_assets.asset_book_definition where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and active order by code,id", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value);
        return await ReadAsync(command, reader => new AssetBookView(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetInt32(8)), ct);
    }

    public async Task<AssetBookView> CreateBookAsync(TenantId tenant, Guid actor, CreateBookRequest request, CancellationToken ct)
    {
        if (request.DefaultDepreciationMethod is not ("StraightLine" or "NoDepreciation")) throw new FixedAssetException("assets.depreciation_method_not_supported");
        var id = Guid.CreateVersion7();
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("insert into business_assets.asset_book_definition (tenant_id,id,legal_entity_id,code,name,posts_to_ledger,currency_code,default_depreciation_method,default_convention,default_useful_life_months,created_by,created_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,now())", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(request.LegalEntityId); command.Parameters.AddWithValue(request.Code.Trim()); command.Parameters.AddWithValue(request.Name.Trim()); command.Parameters.AddWithValue(request.PostsToLedger); command.Parameters.AddWithValue(request.CurrencyCode.Trim().ToUpperInvariant()); command.Parameters.AddWithValue(request.DefaultDepreciationMethod); command.Parameters.AddWithValue(request.DefaultConvention); command.Parameters.AddWithValue((object?)request.DefaultUsefulLifeMonths ?? DBNull.Value); command.Parameters.AddWithValue(actor);
        try { await command.ExecuteNonQueryAsync(ct); await transaction.CommitAsync(ct); } catch (PostgresException ex) when (ex.SqlState == "23505") { throw new FixedAssetException("assets.book_code_exists"); }
        return new(id, request.LegalEntityId, request.Code.Trim(), request.Name.Trim(), request.PostsToLedger, request.CurrencyCode.Trim().ToUpperInvariant(), request.DefaultDepreciationMethod, request.DefaultConvention, request.DefaultUsefulLifeMonths);
    }

    public async Task<Guid> AddAssetBookAsync(TenantId tenant, Guid actor, Guid assetId, CreateAssetBookRequest request, CancellationToken ct)
    {
        if (request.OriginalCost < 0m || request.ResidualValue < 0m || request.ResidualValue > request.OriginalCost || request.UsefulLifeMonths <= 0) throw new FixedAssetException("assets.book_values_invalid");
        if (request.DepreciationMethod is not ("StraightLine" or "NoDepreciation")) throw new FixedAssetException("assets.depreciation_method_not_supported");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var bookId = Guid.CreateVersion7();
            await using var command = new NpgsqlCommand("""
                insert into business_assets.asset_book(tenant_id,id,asset_id,book_definition_id,original_cost,residual_value,useful_life_months,depreciation_method,placed_in_service_date,created_at,row_version)
                select $1,$2,a.id,$3,$4,$5,$6,$7,$8,now(),1 from business_assets.fixed_asset a where a.tenant_id=$1 and a.id=$9 and a.legal_entity_id=$10 and a.lifecycle_state in ('Operational','Capitalized','Draft')
                """, context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(bookId); command.Parameters.AddWithValue(request.BookDefinitionId); command.Parameters.AddWithValue(request.OriginalCost); command.Parameters.AddWithValue(request.ResidualValue); command.Parameters.AddWithValue(request.UsefulLifeMonths); command.Parameters.AddWithValue(request.DepreciationMethod); command.Parameters.AddWithValue(request.PlacedInServiceDate); command.Parameters.AddWithValue(assetId); command.Parameters.AddWithValue(request.LegalEntityId);
            if (await command.ExecuteNonQueryAsync(token) != 1) throw new FixedAssetException("assets.book_asset_not_found");
            await using var updateState = new NpgsqlCommand("update business_assets.fixed_asset set lifecycle_state='Operational',row_version=row_version+1 where tenant_id=$1 and id=$2 and lifecycle_state='Draft'", context.Connection, context.Transaction);
            updateState.Parameters.AddWithValue(tenant.Value); updateState.Parameters.AddWithValue(assetId);
            await updateState.ExecuteNonQueryAsync(token);
            return bookId;
        }, ct);
    }

    public async Task<DepreciationRunView> PrepareDepreciationRunAsync(TenantId tenant, Guid actor, CreateDepreciationRunRequest request, CancellationToken ct)
    {
        if (request.PeriodEnd < request.PeriodStart) throw new FixedAssetException("assets.period_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var runId = Guid.CreateVersion7(); var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? $"depreciation:{request.LegalEntityId:D}:{request.BookId:D}:{request.PeriodStart:yyyyMM}" : request.IdempotencyKey.Trim();
            await using (var create = new NpgsqlCommand("insert into business_assets.asset_depreciation_run(tenant_id,id,legal_entity_id,book_definition_id,period_start,period_end,status,idempotency_key,created_by,created_at) values($1,$2,$3,$4,$5,$6,'Calculated',$7,$8,now()) on conflict do nothing", context.Connection, context.Transaction))
            {
                create.Parameters.AddWithValue(tenant.Value); create.Parameters.AddWithValue(runId); create.Parameters.AddWithValue(request.LegalEntityId); create.Parameters.AddWithValue(request.BookId); create.Parameters.AddWithValue(request.PeriodStart); create.Parameters.AddWithValue(request.PeriodEnd); create.Parameters.AddWithValue(key); create.Parameters.AddWithValue(actor);
                if (await create.ExecuteNonQueryAsync(token) == 0)
                {
                    await using var existing = new NpgsqlCommand("select id,legal_entity_id,book_definition_id,period_start,period_end,status,total_amount,(select count(*) from business_assets.asset_depreciation_run_line l where l.tenant_id=r.tenant_id and l.run_id=r.id),created_at from business_assets.asset_depreciation_run r where tenant_id=$1 and (idempotency_key=$2 or (legal_entity_id=$3 and book_definition_id=$4 and period_start=$5 and period_end=$6)) order by case when idempotency_key=$2 then 0 else 1 end limit 1", context.Connection, context.Transaction); existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(key); existing.Parameters.AddWithValue(request.LegalEntityId); existing.Parameters.AddWithValue(request.BookId); existing.Parameters.AddWithValue(request.PeriodStart); existing.Parameters.AddWithValue(request.PeriodEnd); await using var er = await existing.ExecuteReaderAsync(token); if (!await er.ReadAsync(token)) throw new FixedAssetException("assets.idempotency_conflict"); return new DepreciationRunView(er.GetGuid(0), er.GetGuid(1), er.GetGuid(2), er.GetFieldValue<DateOnly>(3), er.GetFieldValue<DateOnly>(4), er.GetString(5), er.GetDecimal(6), er.GetInt32(7), er.GetFieldValue<DateTimeOffset>(8));
                }
            }
            decimal total = 0m; var lines = 0;
            await using (var select = new NpgsqlCommand("select b.id,b.original_cost,b.residual_value,b.useful_life_months,b.depreciation_method,b.placed_in_service_date,coalesce((select jsonb_agg(jsonb_build_object('effectiveFrom',e.effective_from,'usefulLifeMonths',e.useful_life_months,'residualValue',e.residual_value) order by e.effective_from,e.id) from business_assets.asset_book_estimate_change e where e.tenant_id=b.tenant_id and e.asset_book_id=b.id and e.effective_from <= $4),'[]'::jsonb)::text,coalesce((select sum(case when t.transaction_type='Depreciation' then t.amount when t.transaction_type='Reversal' then -t.amount when t.transaction_type='Disposal' then -t.accumulated_depreciation else 0 end) from business_assets.asset_book_transaction t where t.tenant_id=b.tenant_id and t.asset_book_id=b.id and t.transaction_date < $5),0),coalesce((select sum(case when t.transaction_type='Impairment' then coalesce(nullif(t.accumulated_impairment,0),t.amount) when t.transaction_type='Disposal' then -t.accumulated_impairment else 0 end) from business_assets.asset_book_transaction t where t.tenant_id=b.tenant_id and t.asset_book_id=b.id and t.transaction_date <= $4),0),coalesce((select sum(case when t.transaction_type='Depreciation' then t.amount when t.transaction_type='Reversal' then -t.amount else 0 end) from business_assets.asset_book_transaction t where t.tenant_id=b.tenant_id and t.asset_book_id=b.id and t.transaction_date between $5 and $4),0) from business_assets.asset_book b join business_assets.fixed_asset a on a.tenant_id=b.tenant_id and a.id=b.asset_id where b.tenant_id=$1 and b.book_definition_id=$2 and a.legal_entity_id=$3 and a.lifecycle_state in ('Operational','Capitalized') and b.placed_in_service_date is not null and b.placed_in_service_date <= $4 for update", context.Connection, context.Transaction))
            {
                select.Parameters.AddWithValue(tenant.Value); select.Parameters.AddWithValue(request.BookId); select.Parameters.AddWithValue(request.LegalEntityId); select.Parameters.AddWithValue(request.PeriodEnd); select.Parameters.AddWithValue(request.PeriodStart);
                await using var reader = await select.ExecuteReaderAsync(token); var rows = new List<(Guid, decimal, decimal, int, string, DateOnly, string, decimal, decimal, decimal)>(); while (await reader.ReadAsync(token)) rows.Add((reader.GetGuid(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetInt32(3), reader.GetString(4), reader.GetFieldValue<DateOnly>(5), reader.GetString(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9))); await reader.CloseAsync();
                foreach (var row in rows)
                {
                    var months = Math.Max(1, (request.PeriodEnd.Year - row.Item6.Year) * 12 + request.PeriodEnd.Month - row.Item6.Month + 1);
                    var amount = row.Item5 == "NoDepreciation" ? 0m : CalculateProspectivePeriodAmount(row.Item2, row.Item3, row.Item4, row.Item6, row.Item7, request.PeriodStart, request.PeriodEnd);
                    if (row.Item9 > 0m)
                    {
                        var elapsedBefore = Math.Max(0, MonthsInclusive(row.Item6, request.PeriodStart.AddDays(-1)));
                        var remainingMonths = Math.Max(1, row.Item4 - elapsedBefore);
                        var impairmentAdjusted = Math.Max(0m, row.Item2 - row.Item8 - row.Item9 - row.Item3) / remainingMonths;
                        amount = Math.Min(amount, impairmentAdjusted);
                    }
                    amount = Math.Max(0m, amount - row.Item10);
                    if (amount <= 0m) continue;
                    using var estimateChanges = JsonDocument.Parse(row.Item7);
                    var lineId = Guid.CreateVersion7(); var basis = JsonSerializer.Serialize(new { originalCost = row.Item2, residualValue = row.Item3, usefulLifeMonths = row.Item4, months, method = row.Item5, estimateChanges = estimateChanges.RootElement });
                    await using var line = new NpgsqlCommand("insert into business_assets.asset_depreciation_run_line(tenant_id,id,run_id,asset_book_id,amount,calculation_basis) values($1,$2,$3,$4,$5,$6::jsonb)", context.Connection, context.Transaction); line.Parameters.AddWithValue(tenant.Value); line.Parameters.AddWithValue(lineId); line.Parameters.AddWithValue(runId); line.Parameters.AddWithValue(row.Item1); line.Parameters.AddWithValue(amount); line.Parameters.AddWithValue(basis); await line.ExecuteNonQueryAsync(token);
                    total += amount; lines++;
                }
            }
            await using (var update = new NpgsqlCommand("update business_assets.asset_depreciation_run set total_amount=$1 where tenant_id=$2 and id=$3", context.Connection, context.Transaction)) { update.Parameters.AddWithValue(total); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(runId); await update.ExecuteNonQueryAsync(token); }
            return new DepreciationRunView(runId, request.LegalEntityId, request.BookId, request.PeriodStart, request.PeriodEnd, "Calculated", total, lines, DateTimeOffset.UtcNow);
        }, ct);
    }

    public async Task<DepreciationRunView> PostDepreciationRunAsync(TenantId tenant, Guid actor, Guid runId, CancellationToken ct)
    {
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var run = new NpgsqlCommand("select legal_entity_id,book_definition_id,period_start,period_end,status,total_amount,created_at from business_assets.asset_depreciation_run where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction); run.Parameters.AddWithValue(tenant.Value); run.Parameters.AddWithValue(runId);
            await using var runReader = await run.ExecuteReaderAsync(token); if (!await runReader.ReadAsync(token)) throw new FixedAssetException("assets.depreciation_run_not_found"); var entity = runReader.GetGuid(0); var bookDefinition = runReader.GetGuid(1); var periodStart = runReader.GetFieldValue<DateOnly>(2); var periodEnd = runReader.GetFieldValue<DateOnly>(3); var status = runReader.GetString(4); var total = runReader.GetDecimal(5); var createdAt = runReader.GetFieldValue<DateTimeOffset>(6); await runReader.CloseAsync();
            if (status == "Posted")
            {
                await using var count = new NpgsqlCommand("select count(*) from business_assets.asset_depreciation_run_line where tenant_id=$1 and run_id=$2", context.Connection, context.Transaction); count.Parameters.AddWithValue(tenant.Value); count.Parameters.AddWithValue(runId); var existingCount = Convert.ToInt32(await count.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture); return new DepreciationRunView(runId, entity, bookDefinition, periodStart, periodEnd, status, total, existingCount, createdAt);
            }
            if (status != "Calculated") throw new FixedAssetException("assets.depreciation_run_not_postable");
            await using var closedPeriod = new NpgsqlCommand("select exists (select 1 from finance_ledger.accounting_period where tenant_id=$1 and legal_entity_id=$2 and status in ('Closed','Locked') and start_date <= $3 and end_date >= $4)", context.Connection, context.Transaction); closedPeriod.Parameters.AddWithValue(tenant.Value); closedPeriod.Parameters.AddWithValue(entity); closedPeriod.Parameters.AddWithValue(periodEnd); closedPeriod.Parameters.AddWithValue(periodStart);
            if (Convert.ToBoolean(await closedPeriod.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture)) throw new FixedAssetException("assets.accounting_period_closed");
            await using var lines = new NpgsqlCommand("""
                select l.id,l.asset_book_id,l.amount,b.book_definition_id,bd.posts_to_ledger,bd.currency_code,a.asset_number,a.legal_entity_id
                from business_assets.asset_depreciation_run_line l
                join business_assets.asset_book b on b.tenant_id=l.tenant_id and b.id=l.asset_book_id
                join business_assets.asset_book_definition bd on bd.tenant_id=b.tenant_id and bd.id=b.book_definition_id
                join business_assets.fixed_asset a on a.tenant_id=b.tenant_id and a.id=b.asset_id
                where l.tenant_id=$1 and l.run_id=$2 order by l.asset_book_id,l.id
                """, context.Connection, context.Transaction); lines.Parameters.AddWithValue(tenant.Value); lines.Parameters.AddWithValue(runId);
            await using var lineReader = await lines.ExecuteReaderAsync(token); var rows = new List<(Guid LineId, Guid BookId, decimal Amount, Guid BookDefinitionId, bool PostsToLedger, string Currency, string AssetNumber, Guid LegalEntityId)>(); while (await lineReader.ReadAsync(token)) rows.Add((lineReader.GetGuid(0), lineReader.GetGuid(1), lineReader.GetDecimal(2), lineReader.GetGuid(3), lineReader.GetBoolean(4), lineReader.GetString(5), lineReader.GetString(6), lineReader.GetGuid(7))); await lineReader.CloseAsync();
            foreach (var row in rows)
            {
                Guid? ledgerFact = null;
                if (row.PostsToLedger)
                {
                    var factId = DeterministicGuid(runId, row.BookId); var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new(row.LegalEntityId), AccountingFactTypes.FixedAssetDepreciationPosted, 1, "fixed-assets", $"depreciation-run:{runId:D}:book:{row.BookId:D}", $"fixed-assets:depreciation:{runId:D}:{row.BookId:D}", periodEnd, periodEnd, new FixedAssetDepreciationPostedPayload(row.Amount, row.Currency, $"{row.AssetNumber}/{row.BookId:D}", "FixedAssetDepreciationExpense"), Guid.CreateVersion7(), actor);
                    var result = await ledger.PostFixedAssetDepreciationAsync(context, fact, (FixedAssetDepreciationPostedPayload)fact.Payload, token); ledgerFact = result.FactId;
                }
                await using var transaction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,idempotency_key,ledger_fact_reference,created_by,created_at) values($1,$2,$3,'Depreciation',$4,$5,$6,$7,$8,now()) on conflict(tenant_id,asset_book_id,idempotency_key) do nothing", context.Connection, context.Transaction); transaction.Parameters.AddWithValue(tenant.Value); transaction.Parameters.AddWithValue(DeterministicGuid(runId, row.BookId)); transaction.Parameters.AddWithValue(row.BookId); transaction.Parameters.AddWithValue(periodEnd); transaction.Parameters.AddWithValue(row.Amount); transaction.Parameters.AddWithValue($"depreciation:{runId:D}:{row.BookId:D}"); transaction.Parameters.AddWithValue((object?)ledgerFact?.ToString("D") ?? DBNull.Value); transaction.Parameters.AddWithValue(actor); await transaction.ExecuteNonQueryAsync(token);
                await using var lineUpdate = new NpgsqlCommand("update business_assets.asset_depreciation_run_line set ledger_fact_reference=$1 where tenant_id=$2 and id=$3", context.Connection, context.Transaction); lineUpdate.Parameters.AddWithValue((object?)ledgerFact?.ToString("D") ?? DBNull.Value); lineUpdate.Parameters.AddWithValue(tenant.Value); lineUpdate.Parameters.AddWithValue(row.LineId); await lineUpdate.ExecuteNonQueryAsync(token);
            }
            await using var update = new NpgsqlCommand("update business_assets.asset_depreciation_run set status='Posted',posted_at=now() where tenant_id=$1 and id=$2", context.Connection, context.Transaction); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(runId); await update.ExecuteNonQueryAsync(token);
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "FixedAssetDepreciatedV1", JsonSerializer.Serialize(new FixedAssetDepreciatedV1(runId, entity, bookDefinition, periodStart, periodEnd, total, rows.Count, DateTimeOffset.UtcNow)), tenant, DateTimeOffset.UtcNow, runId, runId, "fixed-assets"));
            return new DepreciationRunView(runId, entity, bookDefinition, periodStart, periodEnd, "Posted", total, rows.Count, createdAt);
        }, ct);
    }

    public async Task<DepreciationRunView> ReverseDepreciationRunAsync(TenantId tenant, Guid actor, Guid runId, ReverseDepreciationRunRequest request, CancellationToken ct)
    {
        if (request.ReversalDate == default || string.IsNullOrWhiteSpace(request.Reason)) throw new FixedAssetException("assets.depreciation_reversal_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var run = new NpgsqlCommand("select legal_entity_id,book_definition_id,period_start,period_end,status,total_amount,created_at from business_assets.asset_depreciation_run where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction); run.Parameters.AddWithValue(tenant.Value); run.Parameters.AddWithValue(runId);
            await using var runReader = await run.ExecuteReaderAsync(token); if (!await runReader.ReadAsync(token)) throw new FixedAssetException("assets.depreciation_run_not_found"); var entity = runReader.GetGuid(0); var bookDefinition = runReader.GetGuid(1); var periodStart = runReader.GetFieldValue<DateOnly>(2); var periodEnd = runReader.GetFieldValue<DateOnly>(3); var status = runReader.GetString(4); var total = runReader.GetDecimal(5); var createdAt = runReader.GetFieldValue<DateTimeOffset>(6); await runReader.CloseAsync();
            if (status == "Reversed")
            {
                await using var count = new NpgsqlCommand("select count(*) from business_assets.asset_depreciation_run_line where tenant_id=$1 and run_id=$2", context.Connection, context.Transaction); count.Parameters.AddWithValue(tenant.Value); count.Parameters.AddWithValue(runId); return new DepreciationRunView(runId, entity, bookDefinition, periodStart, periodEnd, status, total, Convert.ToInt32(await count.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture), createdAt);
            }
            if (status != "Posted") throw new FixedAssetException("assets.depreciation_run_not_reversible");
            await using var closedPeriod = new NpgsqlCommand("select exists (select 1 from finance_ledger.accounting_period where tenant_id=$1 and legal_entity_id=$2 and status in ('Closed','Locked') and start_date <= $3 and end_date >= $3)", context.Connection, context.Transaction); closedPeriod.Parameters.AddWithValue(tenant.Value); closedPeriod.Parameters.AddWithValue(entity); closedPeriod.Parameters.AddWithValue(request.ReversalDate);
            if (Convert.ToBoolean(await closedPeriod.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture)) throw new FixedAssetException("assets.accounting_period_closed");
            await using var lines = new NpgsqlCommand("select l.asset_book_id,l.amount,l.ledger_fact_reference,bd.posts_to_ledger,bd.currency_code,a.asset_number from business_assets.asset_depreciation_run_line l join business_assets.asset_book b on b.tenant_id=l.tenant_id and b.id=l.asset_book_id join business_assets.asset_book_definition bd on bd.tenant_id=b.tenant_id and bd.id=b.book_definition_id join business_assets.fixed_asset a on a.tenant_id=b.tenant_id and a.id=b.asset_id where l.tenant_id=$1 and l.run_id=$2 order by l.asset_book_id,l.id", context.Connection, context.Transaction); lines.Parameters.AddWithValue(tenant.Value); lines.Parameters.AddWithValue(runId);
            await using var lineReader = await lines.ExecuteReaderAsync(token); var rows = new List<(Guid BookId, decimal Amount, string? LedgerFactReference, bool PostsToLedger, string Currency, string AssetNumber)>(); while (await lineReader.ReadAsync(token)) rows.Add((lineReader.GetGuid(0), lineReader.GetDecimal(1), lineReader.IsDBNull(2) ? null : lineReader.GetString(2), lineReader.GetBoolean(3), lineReader.GetString(4), lineReader.GetString(5))); await lineReader.CloseAsync();
            foreach (var row in rows)
            {
                Guid? ledgerFact = null;
                if (row.PostsToLedger && row.LedgerFactReference is not null)
                {
                    var factId = DeterministicGuid(runId, StableGuid($"reversal:{row.BookId:D}")); var payload = new FixedAssetDepreciationReversedPayload(row.Amount, row.Currency, $"{row.AssetNumber}/{row.BookId:D}", "FixedAssetDepreciationExpense"); var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new(entity), AccountingFactTypes.FixedAssetDepreciationReversed, 1, "fixed-assets", $"depreciation-reversal:{runId:D}:book:{row.BookId:D}", $"fixed-assets:depreciation-reversal:{runId:D}:{row.BookId:D}", request.ReversalDate, request.ReversalDate, payload, Guid.CreateVersion7(), actor); ledgerFact = (await ledger.PostFixedAssetDepreciationReversalAsync(context, fact, payload, token)).FactId;
                }
                await using var transaction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,source_reference,idempotency_key,ledger_fact_reference,created_by,created_at) values($1,$2,$3,'Reversal',$4,$5,$6,$7,$8,$9,now()) on conflict(tenant_id,asset_book_id,idempotency_key) do nothing", context.Connection, context.Transaction); transaction.Parameters.AddWithValue(tenant.Value); transaction.Parameters.AddWithValue(DeterministicGuid(runId, StableGuid($"transaction-reversal:{row.BookId:D}"))); transaction.Parameters.AddWithValue(row.BookId); transaction.Parameters.AddWithValue(request.ReversalDate); transaction.Parameters.AddWithValue(row.Amount); transaction.Parameters.AddWithValue($"depreciation-reversal:{runId:D}:reason:{request.Reason.Trim()}"); transaction.Parameters.AddWithValue($"depreciation-reversal:{runId:D}:book:{row.BookId:D}"); transaction.Parameters.AddWithValue((object?)ledgerFact?.ToString("D") ?? DBNull.Value); transaction.Parameters.AddWithValue(actor); await transaction.ExecuteNonQueryAsync(token);
            }
            await using var update = new NpgsqlCommand("update business_assets.asset_depreciation_run set status='Reversed' where tenant_id=$1 and id=$2", context.Connection, context.Transaction); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(runId); await update.ExecuteNonQueryAsync(token);
            return new DepreciationRunView(runId, entity, bookDefinition, periodStart, periodEnd, "Reversed", total, rows.Count, createdAt);
        }, ct);
    }

    public async Task<IReadOnlyList<DepreciationRunView>> ListDepreciationRunsAsync(TenantId tenant, Guid? legalEntityId, int limit, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select r.id,r.legal_entity_id,r.book_definition_id,r.period_start,r.period_end,r.status,r.total_amount,(select count(*) from business_assets.asset_depreciation_run_line l where l.tenant_id=r.tenant_id and l.run_id=r.id),r.created_at from business_assets.asset_depreciation_run r where r.tenant_id=$1 and ($2::uuid is null or r.legal_entity_id=$2) order by r.created_at desc limit $3", connection);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value);
        command.Parameters.AddWithValue(Math.Clamp(limit, 1, 500));
        return await ReadAsync(command, reader => new DepreciationRunView(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetFieldValue<DateOnly>(3), reader.GetFieldValue<DateOnly>(4), reader.GetString(5), reader.GetDecimal(6), reader.GetInt32(7), reader.GetFieldValue<DateTimeOffset>(8)), ct);
    }

    public async Task<IReadOnlyList<CapitalizationCandidateView>> ListCapitalizationCandidatesAsync(TenantId tenant, Guid? legalEntityId, string? status, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 200);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,source_type,source_reference,source_snapshot,eligible_amount,allocated_amount,status,decision,created_at from business_assets.asset_capitalization_candidate where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and ($3::text is null or status=$3) order by created_at desc,id desc limit $4", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value); command.Parameters.AddWithValue((object?)status?.Trim() ?? DBNull.Value); command.Parameters.AddWithValue(limit);
        return await ReadAsync(command, reader => new CapitalizationCandidateView(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), ReadJsonElement(reader, 4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9)), ct);
    }

    public async Task<CapitalizationCandidateView> CreateCapitalizationCandidateAsync(TenantId tenant, Guid actor, CreateCapitalizationCandidateRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(request.SourceType) || string.IsNullOrWhiteSpace(request.SourceReference) || request.EligibleAmount <= 0m || request.SourceSnapshot.ValueKind != JsonValueKind.Object)
            throw new FixedAssetException("assets.capitalization_candidate_invalid");
        var economicSourceKey = ResolveEconomicSourceKey(request);
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var id = Guid.CreateVersion7();
            await using var command = new NpgsqlCommand("insert into business_assets.asset_capitalization_candidate(tenant_id,id,legal_entity_id,source_type,source_reference,economic_source_key,source_snapshot,eligible_amount,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7::jsonb,$8,$9,now()) returning created_at", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(request.LegalEntityId); command.Parameters.AddWithValue(request.SourceType.Trim()); command.Parameters.AddWithValue(request.SourceReference.Trim()); command.Parameters.AddWithValue(JsonSerializer.Serialize(request.SourceSnapshot)); command.Parameters.AddWithValue(request.EligibleAmount); command.Parameters.AddWithValue(actor);
            command.Parameters.Insert(5, new NpgsqlParameter { Value = economicSourceKey });
            try
            {
                await using var createdReader = await command.ExecuteReaderAsync(token);
                if (!await createdReader.ReadAsync(token)) throw new FixedAssetException("assets.capitalization_candidate_create_failed");
                var createdAt = createdReader.GetFieldValue<DateTimeOffset>(0);
                return new CapitalizationCandidateView(id, request.LegalEntityId, request.SourceType.Trim(), request.SourceReference.Trim(), request.SourceSnapshot.Clone(), request.EligibleAmount, 0m, "PendingReview", null, createdAt);
            }
            catch (PostgresException exception) when (exception.SqlState == "23505")
            {
                throw new FixedAssetException("assets.capitalization_candidate_exists");
            }
        }, ct);
    }

    private static string ResolveEconomicSourceKey(CreateCapitalizationCandidateRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.EconomicSourceKey)) return request.EconomicSourceKey.Trim().ToLowerInvariant();
        var sourceType = request.SourceType.Trim();
        var reference = request.SourceReference.Trim().ToLowerInvariant();
        return sourceType is "SupplierInvoice" or "FinalizedPurchasingInvoice" ? $"invoice:{reference}" : $"{sourceType.ToLowerInvariant()}:{reference}";
    }

    public async Task<CapitalizationCandidateView> CreateCapitalizationCandidateFromInvoiceAsync(TenantId tenant, Guid actor, CreateCapitalizationCandidateFromInvoiceRequest request, CancellationToken ct)
    {
        if (invoiceSnapshots is null) throw new FixedAssetException("assets.invoice_source_unavailable");
        if (request.LegalEntityId == Guid.Empty || request.InvoiceId == Guid.Empty) throw new FixedAssetException("assets.invoice_source_invalid");
        var snapshot = await invoiceSnapshots.GetInvoiceComplianceSnapshotAsync(tenant, request.LegalEntityId, request.InvoiceId, ct) ?? throw new FixedAssetException("assets.invoice_not_found");
        if (snapshot.LegalEntityId != request.LegalEntityId || !string.Equals(snapshot.Direction, "Payable", StringComparison.OrdinalIgnoreCase)) throw new FixedAssetException("assets.invoice_source_invalid");
        if (!decimal.TryParse(snapshot.NetAmount, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var eligibleAmount) || eligibleAmount <= 0m) throw new FixedAssetException("assets.invoice_amount_invalid");
        var source = JsonSerializer.SerializeToElement(snapshot);
        return await CreateCapitalizationCandidateAsync(tenant, actor, new CreateCapitalizationCandidateRequest(request.LegalEntityId, "SupplierInvoice", request.InvoiceId.ToString("D"), eligibleAmount, source), ct);
    }

    public async Task<CapitalizationCandidateView> CreateCapitalizationCandidateFromInventoryAsync(TenantId tenant, Guid actor, CreateCapitalizationCandidateFromInventoryRequest request, CancellationToken ct)
    {
        if (inventoryAssets is null) throw new FixedAssetException("assets.inventory_source_unavailable");
        if (request.LegalEntityId == Guid.Empty || request.ProductVariantId == Guid.Empty) throw new FixedAssetException("assets.inventory_source_invalid");
        var snapshot = await inventoryAssets.GetInternalUseCapitalizationSourceAsync(tenant, request.LegalEntityId, request.ProductVariantId, request.SerialId, request.AsOfDate, ct)
            ?? throw new FixedAssetException("assets.inventory_source_not_eligible");
        var sourceReference = $"{snapshot.ProductVariantId:D}:{snapshot.SerialId?.ToString("D") ?? "unserialized"}:{snapshot.AsOfDate:yyyyMMdd}";
        return await CreateCapitalizationCandidateAsync(tenant, actor,
            new CreateCapitalizationCandidateRequest(request.LegalEntityId, "InventoryInternalUse", sourceReference,
                snapshot.CarryingValue, JsonSerializer.SerializeToElement(snapshot)), ct);
    }

    public async Task<CapitalizationCandidateView> CreateCapitalizationCandidateFromExpenseAsync(TenantId tenant, Guid actor, CreateCapitalizationCandidateFromExpenseRequest request, CancellationToken ct)
    {
        if (expenseAssets is null) throw new FixedAssetException("assets.expense_source_unavailable");
        if (request.LegalEntityId == Guid.Empty || request.ExpenseId == Guid.Empty) throw new FixedAssetException("assets.expense_source_invalid");
        var snapshot = await expenseAssets.GetFinalizedCapitalSourceAsync(tenant, request.LegalEntityId, request.ExpenseId, ct) ?? throw new FixedAssetException("assets.expense_source_not_eligible");
        return await CreateCapitalizationCandidateAsync(tenant, actor, new CreateCapitalizationCandidateRequest(request.LegalEntityId, "FinalizedExpense", request.ExpenseId.ToString("D"), snapshot.Amount, JsonSerializer.SerializeToElement(snapshot)), ct);
    }

    public async Task<CapitalizationCandidateView> CreateCapitalizationCandidateFromPurchasingAsync(TenantId tenant, Guid actor, CreateCapitalizationCandidateFromPurchasingRequest request, CancellationToken ct)
    {
        if (purchasingAssets is null) throw new FixedAssetException("assets.purchasing_source_unavailable");
        if (request.LegalEntityId == Guid.Empty || request.PurchaseOrderId == Guid.Empty) throw new FixedAssetException("assets.purchasing_source_invalid");
        var source = await purchasingAssets.GetFinalizedCapitalizationSourceAsync(tenant, request.LegalEntityId, request.PurchaseOrderId, ct) ?? throw new FixedAssetException("assets.purchasing_source_not_finalized");
        if (source.LegalEntityId != request.LegalEntityId || source.Lines.Count == 0 || source.EligibleAmount <= 0m) throw new FixedAssetException("assets.purchasing_source_invalid");
        return await CreateCapitalizationCandidateAsync(tenant, actor, new CreateCapitalizationCandidateRequest(request.LegalEntityId, "FinalizedPurchasingInvoice", source.SupplierInvoiceId.ToString("D"), source.EligibleAmount, JsonSerializer.SerializeToElement(source)), ct);
    }

    public async Task<CapitalizationCandidateView> ReviewCapitalizationCandidateAsync(TenantId tenant, Guid actor, Guid candidateId, ReviewCapitalizationCandidateRequest request, CancellationToken ct)
    {
        var decision = request.Decision.Trim();
        if (decision is not ("Approve" or "Reject") || decision == "Reject" && string.IsNullOrWhiteSpace(request.Reason)) throw new FixedAssetException("assets.capitalization_decision_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("update business_assets.asset_capitalization_candidate set status=$1,decision=$2 where tenant_id=$3 and id=$4 and status='PendingReview' returning legal_entity_id,source_type,source_reference,source_snapshot,eligible_amount,allocated_amount,status,decision,created_at", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(decision == "Approve" ? "Approved" : "Rejected"); command.Parameters.AddWithValue((object?)(string.IsNullOrWhiteSpace(request.Reason) ? decision : request.Reason.Trim()) ?? DBNull.Value); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(candidateId);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new FixedAssetException("assets.capitalization_candidate_not_reviewable");
            return new CapitalizationCandidateView(candidateId, reader.GetGuid(0), reader.GetString(1), reader.GetString(2), ReadJsonElement(reader, 3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetFieldValue<DateTimeOffset>(8));
        }, ct);
    }

    public async Task<SourceAllocationView> AllocateCapitalizationCandidateAsync(TenantId tenant, Guid actor, Guid candidateId, AllocateCapitalizationCandidateRequest request, CancellationToken ct)
    {
        if (request.AssetId == Guid.Empty || request.Amount <= 0m || request.AllocationType is not ("Acquisition" or "CIPAddition" or "CapitalAddition" or "CostReduction")) throw new FixedAssetException("assets.source_allocation_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var candidate = new NpgsqlCommand("select legal_entity_id,eligible_amount,allocated_amount,status from business_assets.asset_capitalization_candidate where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction); candidate.Parameters.AddWithValue(tenant.Value); candidate.Parameters.AddWithValue(candidateId);
            await using var candidateReader = await candidate.ExecuteReaderAsync(token);
            if (!await candidateReader.ReadAsync(token)) throw new FixedAssetException("assets.capitalization_candidate_not_found");
            var legalEntityId = candidateReader.GetGuid(0); var eligibleAmount = candidateReader.GetDecimal(1); var allocatedAmount = candidateReader.GetDecimal(2); var status = candidateReader.GetString(3); await candidateReader.CloseAsync();
            await using var existingAllocation = new NpgsqlCommand("select id,amount,allocation_type,created_at from business_assets.asset_source_allocation where tenant_id=$1 and candidate_id=$2 and asset_id=$3", context.Connection, context.Transaction); existingAllocation.Parameters.AddWithValue(tenant.Value); existingAllocation.Parameters.AddWithValue(candidateId); existingAllocation.Parameters.AddWithValue(request.AssetId);
            await using var existingAllocationReader = await existingAllocation.ExecuteReaderAsync(token);
            if (await existingAllocationReader.ReadAsync(token))
            {
                var existingId = existingAllocationReader.GetGuid(0); var existingAmount = existingAllocationReader.GetDecimal(1); var existingType = existingAllocationReader.GetString(2); var existingCreatedAt = existingAllocationReader.GetFieldValue<DateTimeOffset>(3); await existingAllocationReader.CloseAsync();
                if (existingAmount != request.Amount || existingType != request.AllocationType) throw new FixedAssetException("assets.source_allocation_conflict");
                return new SourceAllocationView(existingId, candidateId, request.AssetId, existingAmount, existingType, existingCreatedAt, true);
            }
            await existingAllocationReader.CloseAsync();
            if (status != "Approved") throw new FixedAssetException("assets.capitalization_candidate_not_approved");
            await using var asset = new NpgsqlCommand("select a.legal_entity_id,a.lifecycle_state,a.asset_number,c.code from business_assets.fixed_asset a join business_assets.asset_category c on c.tenant_id=a.tenant_id and c.id=a.category_id where a.tenant_id=$1 and a.id=$2 for update of a", context.Connection, context.Transaction); asset.Parameters.AddWithValue(tenant.Value); asset.Parameters.AddWithValue(request.AssetId);
            await using var assetReader = await asset.ExecuteReaderAsync(token);
            if (!await assetReader.ReadAsync(token)) throw new FixedAssetException("assets.asset_not_found");
            var assetNumber = assetReader.GetString(2); var assetCategory = assetReader.GetString(3);
            if (assetReader.GetGuid(0) != legalEntityId || assetReader.GetString(1) is "Disposed" or "Archived") throw new FixedAssetException("assets.source_allocation_asset_invalid");
            await assetReader.CloseAsync();
            var newAllocatedAmount = allocatedAmount + request.Amount;
            if (newAllocatedAmount > eligibleAmount) throw new FixedAssetException("assets.source_allocation_exceeds_eligible");
            var allocationId = Guid.CreateVersion7();
            await using var insert = new NpgsqlCommand("insert into business_assets.asset_source_allocation(tenant_id,id,candidate_id,asset_id,amount,allocation_type,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,now()) returning created_at", context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(allocationId); insert.Parameters.AddWithValue(candidateId); insert.Parameters.AddWithValue(request.AssetId); insert.Parameters.AddWithValue(request.Amount); insert.Parameters.AddWithValue(request.AllocationType); insert.Parameters.AddWithValue(actor);
            DateTimeOffset createdAt;
            try
            {
                await using var createdReader = await insert.ExecuteReaderAsync(token);
                if (!await createdReader.ReadAsync(token)) throw new FixedAssetException("assets.source_allocation_create_failed");
                createdAt = createdReader.GetFieldValue<DateTimeOffset>(0);
            }
            catch (PostgresException exception) when (exception.SqlState == "23505") { throw new FixedAssetException("assets.source_allocation_exists"); }
            await using var books = new NpgsqlCommand("select b.id,b.original_cost,bd.posts_to_ledger,bd.currency_code from business_assets.asset_book b join business_assets.asset_book_definition bd on bd.tenant_id=b.tenant_id and bd.id=b.book_definition_id where b.tenant_id=$1 and b.asset_id=$2 for update of b", context.Connection, context.Transaction); books.Parameters.AddWithValue(tenant.Value); books.Parameters.AddWithValue(request.AssetId);
            await using var bookReader = await books.ExecuteReaderAsync(token); var bookRows = new List<(Guid Id, decimal OriginalCost, bool PostsToLedger, string Currency)>(); while (await bookReader.ReadAsync(token)) bookRows.Add((bookReader.GetGuid(0), bookReader.GetDecimal(1), bookReader.GetBoolean(2), bookReader.GetString(3).Trim())); await bookReader.CloseAsync();
            var transactionDate = request.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            foreach (var bookRow in bookRows)
            {
                var delta = request.AllocationType == "CostReduction" ? -request.Amount : request.Amount;
                var newCost = bookRow.OriginalCost + delta;
                if (newCost < 0m) throw new FixedAssetException("assets.source_allocation_cost_invalid");
                if (delta < 0m)
                {
                    await using var accumulated = new NpgsqlCommand("select coalesce(sum(case when transaction_type='Depreciation' then amount when transaction_type='Reversal' then -amount when transaction_type='Disposal' then -accumulated_depreciation when transaction_type='Reinstatement' then accumulated_depreciation else 0 end),0) from business_assets.asset_book_transaction where tenant_id=$1 and asset_book_id=$2", context.Connection, context.Transaction); accumulated.Parameters.AddWithValue(tenant.Value); accumulated.Parameters.AddWithValue(bookRow.Id);
                    if (newCost < (decimal)(await accumulated.ExecuteScalarAsync(token) ?? 0m)) throw new FixedAssetException("assets.source_allocation_cost_invalid");
                }
                Guid? ledgerFact = null;
                if (bookRow.PostsToLedger)
                {
                    var factId = DeterministicGuid(allocationId, bookRow.Id);
                    var factDate = transactionDate;
                    if (request.AllocationType == "Acquisition")
                    {
                        var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new(legalEntityId), AccountingFactTypes.FixedAssetAcquisitionPosted, 1, "fixed-assets", $"source-allocation:{allocationId:D}:book:{bookRow.Id:D}", $"fixed-assets:source-allocation:{allocationId:D}:book:{bookRow.Id:D}", factDate, factDate, new FixedAssetAcquisitionPostedPayload(request.Amount, bookRow.Currency, assetNumber, assetCategory), Guid.CreateVersion7(), actor);
                        ledgerFact = (await ledger.PostFixedAssetAcquisitionAsync(context, fact, (FixedAssetAcquisitionPostedPayload)fact.Payload, token)).FactId;
                    }
                    else
                    {
                        var factType = request.AllocationType == "CostReduction" ? AccountingFactTypes.FixedAssetCostReduced : AccountingFactTypes.FixedAssetCostAdded;
                        var payload = new FixedAssetCostAdjustmentPostedPayload(request.Amount, bookRow.Currency, $"{assetNumber}/{bookRow.Id:D}", assetNumber, $"Source allocation {candidateId:D}");
                        var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new(legalEntityId), factType, 1, "fixed-assets", $"source-allocation:{allocationId:D}:book:{bookRow.Id:D}", $"fixed-assets:source-allocation:{allocationId:D}:book:{bookRow.Id:D}", factDate, factDate, payload, Guid.CreateVersion7(), actor);
                        ledgerFact = (request.AllocationType == "CostReduction" ? await ledger.PostFixedAssetCostReducedAsync(context, fact, payload, token) : await ledger.PostFixedAssetCostAddedAsync(context, fact, payload, token)).FactId;
                    }
                }
                await using var updateBook = new NpgsqlCommand("update business_assets.asset_book set original_cost=$1,row_version=row_version+1 where tenant_id=$2 and id=$3", context.Connection, context.Transaction); updateBook.Parameters.AddWithValue(newCost); updateBook.Parameters.AddWithValue(tenant.Value); updateBook.Parameters.AddWithValue(bookRow.Id); await updateBook.ExecuteNonQueryAsync(token);
                await using var bookTransaction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,source_reference,idempotency_key,ledger_fact_reference,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,now())", context.Connection, context.Transaction); bookTransaction.Parameters.AddWithValue(tenant.Value); bookTransaction.Parameters.AddWithValue(Guid.CreateVersion7()); bookTransaction.Parameters.AddWithValue(bookRow.Id); bookTransaction.Parameters.AddWithValue(request.AllocationType == "Acquisition" ? "Acquisition" : request.AllocationType == "CostReduction" ? "CostReduction" : "CapitalAddition"); bookTransaction.Parameters.AddWithValue(transactionDate); bookTransaction.Parameters.AddWithValue(request.Amount); bookTransaction.Parameters.AddWithValue($"candidate:{candidateId:D}:allocation:{allocationId:D}"); bookTransaction.Parameters.AddWithValue($"source-allocation:{allocationId:D}:book:{bookRow.Id:D}"); bookTransaction.Parameters.AddWithValue((object?)ledgerFact?.ToString("D") ?? DBNull.Value); bookTransaction.Parameters.AddWithValue(actor); await bookTransaction.ExecuteNonQueryAsync(token);
            }
            await using var update = new NpgsqlCommand("update business_assets.asset_capitalization_candidate set allocated_amount=$1,status=case when $1=eligible_amount then 'Consumed' else status end where tenant_id=$2 and id=$3", context.Connection, context.Transaction); update.Parameters.AddWithValue(newAllocatedAmount); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(candidateId); await update.ExecuteNonQueryAsync(token);
            return new SourceAllocationView(allocationId, candidateId, request.AssetId, request.Amount, request.AllocationType, createdAt);
        }, ct);
    }

    public async Task<SourceAllocationCorrectionView> CorrectSourceAllocationAsync(TenantId tenant, Guid actor, Guid candidateId, CorrectSourceAllocationRequest request, CancellationToken ct)
    {
        if (request.AllocationId == Guid.Empty || request.Amount <= 0m || string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new FixedAssetException("assets.source_allocation_correction_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var key = request.IdempotencyKey.Trim();
            await using var existing = new NpgsqlCommand("select id,allocation_id,candidate_id,asset_id,amount,reason,created_at from business_assets.asset_source_allocation_correction where tenant_id=$1 and idempotency_key=$2", context.Connection, context.Transaction); existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(key);
            await using var existingReader = await existing.ExecuteReaderAsync(token);
            if (await existingReader.ReadAsync(token))
            {
                var existingId = existingReader.GetGuid(0); var existingAllocationId = existingReader.GetGuid(1); var existingCandidateId = existingReader.GetGuid(2); var existingAssetId = existingReader.GetGuid(3); var existingAmount = existingReader.GetDecimal(4); var existingReason = existingReader.GetString(5); var existingCreatedAt = existingReader.GetFieldValue<DateTimeOffset>(6); await existingReader.CloseAsync();
                if (existingAllocationId != request.AllocationId || existingCandidateId != candidateId || existingAmount != request.Amount || existingReason != request.Reason.Trim()) throw new FixedAssetException("assets.source_allocation_correction_conflict");
                await using var existingTotal = new NpgsqlCommand("select allocated_amount from business_assets.asset_capitalization_candidate where tenant_id=$1 and id=$2", context.Connection, context.Transaction); existingTotal.Parameters.AddWithValue(tenant.Value); existingTotal.Parameters.AddWithValue(candidateId); var candidateAllocated = (decimal?)(await existingTotal.ExecuteScalarAsync(token)) ?? throw new FixedAssetException("assets.capitalization_candidate_not_found");
                await using var existingRemaining = new NpgsqlCommand("select a.amount-coalesce((select sum(c.amount) from business_assets.asset_source_allocation_correction c where c.tenant_id=a.tenant_id and c.allocation_id=a.id),0) from business_assets.asset_source_allocation a where a.tenant_id=$1 and a.id=$2", context.Connection, context.Transaction); existingRemaining.Parameters.AddWithValue(tenant.Value); existingRemaining.Parameters.AddWithValue(request.AllocationId); var remaining = (decimal?)(await existingRemaining.ExecuteScalarAsync(token)) ?? 0m;
                return new SourceAllocationCorrectionView(existingId, existingAllocationId, existingCandidateId, existingAssetId, existingAmount, remaining, candidateAllocated, existingReason, existingCreatedAt, true);
            }
            await existingReader.CloseAsync();

            await using var candidate = new NpgsqlCommand("select allocated_amount,status,legal_entity_id from business_assets.asset_capitalization_candidate where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction); candidate.Parameters.AddWithValue(tenant.Value); candidate.Parameters.AddWithValue(candidateId);
            await using var candidateReader = await candidate.ExecuteReaderAsync(token);
            if (!await candidateReader.ReadAsync(token)) throw new FixedAssetException("assets.capitalization_candidate_not_found");
            var candidateAllocatedAmount = candidateReader.GetDecimal(0); var candidateStatus = candidateReader.GetString(1); var legalEntityId = candidateReader.GetGuid(2); await candidateReader.CloseAsync();
            if (candidateStatus is "Rejected" or "PendingReview") throw new FixedAssetException("assets.source_allocation_correction_not_allowed");

            await using var allocation = new NpgsqlCommand("select asset_id,amount from business_assets.asset_source_allocation where tenant_id=$1 and id=$2 and candidate_id=$3 for update", context.Connection, context.Transaction); allocation.Parameters.AddWithValue(tenant.Value); allocation.Parameters.AddWithValue(request.AllocationId); allocation.Parameters.AddWithValue(candidateId);
            await using var allocationReader = await allocation.ExecuteReaderAsync(token);
            if (!await allocationReader.ReadAsync(token)) throw new FixedAssetException("assets.source_allocation_not_found");
            var assetId = allocationReader.GetGuid(0); var originalAmount = allocationReader.GetDecimal(1); await allocationReader.CloseAsync();
            await using var asset = new NpgsqlCommand("select asset_number from business_assets.fixed_asset where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction); asset.Parameters.AddWithValue(tenant.Value); asset.Parameters.AddWithValue(assetId); var assetNumber = (string?)await asset.ExecuteScalarAsync(token) ?? throw new FixedAssetException("assets.asset_not_found");
            await using var totalCorrections = new NpgsqlCommand("select coalesce(sum(amount),0) from business_assets.asset_source_allocation_correction where tenant_id=$1 and allocation_id=$2", context.Connection, context.Transaction); totalCorrections.Parameters.AddWithValue(tenant.Value); totalCorrections.Parameters.AddWithValue(request.AllocationId); var correctedAmount = (decimal)(await totalCorrections.ExecuteScalarAsync(token) ?? 0m);
            if (correctedAmount + request.Amount > originalAmount || request.Amount > candidateAllocatedAmount) throw new FixedAssetException("assets.source_allocation_correction_exceeds_amount");
            var correctionId = Guid.CreateVersion7();
            await using var insert = new NpgsqlCommand("insert into business_assets.asset_source_allocation_correction(tenant_id,id,allocation_id,candidate_id,asset_id,amount,reason,idempotency_key,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,now())", context.Connection, context.Transaction); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(correctionId); insert.Parameters.AddWithValue(request.AllocationId); insert.Parameters.AddWithValue(candidateId); insert.Parameters.AddWithValue(assetId); insert.Parameters.AddWithValue(request.Amount); insert.Parameters.AddWithValue(request.Reason.Trim()); insert.Parameters.AddWithValue(key); insert.Parameters.AddWithValue(actor); await insert.ExecuteNonQueryAsync(token);
            await using var books = new NpgsqlCommand("select b.id,b.original_cost,bd.posts_to_ledger,bd.currency_code from business_assets.asset_book b join business_assets.asset_book_definition bd on bd.tenant_id=b.tenant_id and bd.id=b.book_definition_id where b.tenant_id=$1 and b.asset_id=$2 for update of b", context.Connection, context.Transaction); books.Parameters.AddWithValue(tenant.Value); books.Parameters.AddWithValue(assetId);
            await using var bookReader = await books.ExecuteReaderAsync(token); var bookRows = new List<(Guid Id, decimal OriginalCost, bool PostsToLedger, string Currency)>(); while (await bookReader.ReadAsync(token)) bookRows.Add((bookReader.GetGuid(0), bookReader.GetDecimal(1), bookReader.GetBoolean(2), bookReader.GetString(3).Trim())); await bookReader.CloseAsync();
            foreach (var bookRow in bookRows)
            {
                var newCost = bookRow.OriginalCost - request.Amount;
                if (newCost < 0m) throw new FixedAssetException("assets.source_allocation_cost_invalid");
                await using var accumulated = new NpgsqlCommand("select coalesce(sum(case when transaction_type='Depreciation' then amount when transaction_type='Reversal' then -amount when transaction_type='Disposal' then -accumulated_depreciation when transaction_type='Reinstatement' then accumulated_depreciation else 0 end),0) from business_assets.asset_book_transaction where tenant_id=$1 and asset_book_id=$2", context.Connection, context.Transaction); accumulated.Parameters.AddWithValue(tenant.Value); accumulated.Parameters.AddWithValue(bookRow.Id);
                if (newCost < (decimal)(await accumulated.ExecuteScalarAsync(token) ?? 0m)) throw new FixedAssetException("assets.source_allocation_cost_invalid");
                Guid? ledgerFact = null;
                if (bookRow.PostsToLedger)
                {
                    var factId = DeterministicGuid(correctionId, bookRow.Id); var payload = new FixedAssetCostAdjustmentPostedPayload(request.Amount, bookRow.Currency, $"{assetNumber}/{bookRow.Id:D}", assetNumber, request.Reason.Trim());
                    var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new(legalEntityId), AccountingFactTypes.FixedAssetCostReduced, 1, "fixed-assets", $"source-allocation-correction:{correctionId:D}:book:{bookRow.Id:D}", $"fixed-assets:source-allocation-correction:{correctionId:D}:book:{bookRow.Id:D}", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), payload, Guid.CreateVersion7(), actor);
                    ledgerFact = (await ledger.PostFixedAssetCostReducedAsync(context, fact, payload, token)).FactId;
                }
                await using var updateBook = new NpgsqlCommand("update business_assets.asset_book set original_cost=$1,row_version=row_version+1 where tenant_id=$2 and id=$3", context.Connection, context.Transaction); updateBook.Parameters.AddWithValue(newCost); updateBook.Parameters.AddWithValue(tenant.Value); updateBook.Parameters.AddWithValue(bookRow.Id); await updateBook.ExecuteNonQueryAsync(token);
                await using var bookTransaction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,source_reference,idempotency_key,ledger_fact_reference,created_by,created_at) values($1,$2,$3,'CostReduction',current_date,$4,$5,$6,$7,$8,now())", context.Connection, context.Transaction); bookTransaction.Parameters.AddWithValue(tenant.Value); bookTransaction.Parameters.AddWithValue(Guid.CreateVersion7()); bookTransaction.Parameters.AddWithValue(bookRow.Id); bookTransaction.Parameters.AddWithValue(request.Amount); bookTransaction.Parameters.AddWithValue($"source-allocation-correction:{correctionId:D}"); bookTransaction.Parameters.AddWithValue($"source-allocation-correction:{correctionId:D}:book:{bookRow.Id:D}"); bookTransaction.Parameters.AddWithValue((object?)ledgerFact?.ToString("D") ?? DBNull.Value); bookTransaction.Parameters.AddWithValue(actor); await bookTransaction.ExecuteNonQueryAsync(token);
            }
            var candidateNewAmount = candidateAllocatedAmount - request.Amount;
            await using var update = new NpgsqlCommand("update business_assets.asset_capitalization_candidate set allocated_amount=$1,status=case when status='Consumed' then 'Approved' else status end where tenant_id=$2 and id=$3", context.Connection, context.Transaction); update.Parameters.AddWithValue(candidateNewAmount); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(candidateId); await update.ExecuteNonQueryAsync(token);
            return new SourceAllocationCorrectionView(correctionId, request.AllocationId, candidateId, assetId, request.Amount, originalAmount - correctedAmount - request.Amount, candidateNewAmount, request.Reason.Trim(), DateTimeOffset.UtcNow, false);
        }, ct);
    }

    public async Task<CipAssetView> CreateCipAssetAsync(TenantId tenant, Guid actor, CreateCipAssetRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty || request.CategoryId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.SourceType) || string.IsNullOrWhiteSpace(request.SourceReference) || request.Amount <= 0m)
            throw new FixedAssetException("assets.cip_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var category = new NpgsqlCommand("select asset_nature,tracking_granularity from business_assets.asset_category where tenant_id=$1 and id=$2 and active", context.Connection, context.Transaction); category.Parameters.AddWithValue(tenant.Value); category.Parameters.AddWithValue(request.CategoryId);
            await using var categoryReader = await category.ExecuteReaderAsync(token);
            if (!await categoryReader.ReadAsync(token)) throw new FixedAssetException("assets.category_not_found");
            if (categoryReader.GetString(0) != "CIP") throw new FixedAssetException("assets.cip_category_required");
            var trackingGranularity = categoryReader.GetString(1); await categoryReader.CloseAsync();
            var assetId = Guid.CreateVersion7(); var assetNumber = await sequences.AllocateAsync(context, new SequenceDefinition("fixed-assets.asset-number", tenant, request.LegalEntityId, null, null, null, "FA-", 8, 1), token);
            await using var asset = new NpgsqlCommand("insert into business_assets.fixed_asset(tenant_id,id,legal_entity_id,category_id,asset_number,name,description,lifecycle_state,asset_nature,tracking_granularity,source_origin_type,acquisition_date,quantity,created_by,created_at,row_version) values($1,$2,$3,$4,$5,$6,$7,'UnderConstruction','CIP',$8,'CIP',$9,1,$10,now(),1)", context.Connection, context.Transaction);
            asset.Parameters.AddWithValue(tenant.Value); asset.Parameters.AddWithValue(assetId); asset.Parameters.AddWithValue(request.LegalEntityId); asset.Parameters.AddWithValue(request.CategoryId); asset.Parameters.AddWithValue(assetNumber); asset.Parameters.AddWithValue(request.Name.Trim()); asset.Parameters.AddWithValue((object?)request.Description ?? DBNull.Value); asset.Parameters.AddWithValue(trackingGranularity); asset.Parameters.AddWithValue(request.AcquisitionDate); asset.Parameters.AddWithValue(actor); await asset.ExecuteNonQueryAsync(token);
            await using var source = new NpgsqlCommand("insert into business_assets.asset_cip_cost_source(tenant_id,id,asset_id,source_type,source_reference,amount,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,now())", context.Connection, context.Transaction); source.Parameters.AddWithValue(tenant.Value); source.Parameters.AddWithValue(Guid.CreateVersion7()); source.Parameters.AddWithValue(assetId); source.Parameters.AddWithValue(request.SourceType.Trim()); source.Parameters.AddWithValue(request.SourceReference.Trim()); source.Parameters.AddWithValue(request.Amount); source.Parameters.AddWithValue(actor);
            try { await source.ExecuteNonQueryAsync(token); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new FixedAssetException("assets.cip_source_exists"); }
            await using var books = new NpgsqlCommand("select id from business_assets.asset_book_definition where tenant_id=$1 and legal_entity_id=$2 and active order by id", context.Connection, context.Transaction); books.Parameters.AddWithValue(tenant.Value); books.Parameters.AddWithValue(request.LegalEntityId);
            await using var bookReader = await books.ExecuteReaderAsync(token); var bookDefinitions = new List<Guid>(); while (await bookReader.ReadAsync(token)) bookDefinitions.Add(bookReader.GetGuid(0)); await bookReader.CloseAsync();
            if (bookDefinitions.Count == 0) throw new FixedAssetException("assets.book_not_found");
            foreach (var bookDefinitionId in bookDefinitions)
            {
                var bookId = Guid.CreateVersion7();
                await using var book = new NpgsqlCommand("insert into business_assets.asset_book(tenant_id,id,asset_id,book_definition_id,original_cost,residual_value,useful_life_months,depreciation_method,created_at,row_version) values($1,$2,$3,$4,$5,0,null,'NoDepreciation',now(),1)", context.Connection, context.Transaction); book.Parameters.AddWithValue(tenant.Value); book.Parameters.AddWithValue(bookId); book.Parameters.AddWithValue(assetId); book.Parameters.AddWithValue(bookDefinitionId); book.Parameters.AddWithValue(request.Amount); await book.ExecuteNonQueryAsync(token);
                await using var transaction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,source_reference,idempotency_key,created_by,created_at) values($1,$2,$3,'CapitalAddition',$4,$5,$6,$7,$8,now())", context.Connection, context.Transaction); transaction.Parameters.AddWithValue(tenant.Value); transaction.Parameters.AddWithValue(Guid.CreateVersion7()); transaction.Parameters.AddWithValue(bookId); transaction.Parameters.AddWithValue(request.AcquisitionDate); transaction.Parameters.AddWithValue(request.Amount); transaction.Parameters.AddWithValue($"{request.SourceType.Trim()}:{request.SourceReference.Trim()}"); transaction.Parameters.AddWithValue($"cip:{assetId:D}:{bookDefinitionId:D}:{request.SourceType.Trim()}:{request.SourceReference.Trim()}"); transaction.Parameters.AddWithValue(actor); await transaction.ExecuteNonQueryAsync(token);
            }
            return new CipAssetView(assetId, assetNumber, request.Name.Trim(), "UnderConstruction", request.Amount, 1, null, null);
        }, ct);
    }

    public async Task<CipAssetView> AddCipCostSourceAsync(TenantId tenant, Guid actor, Guid assetId, AddCipCostSourceRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SourceType) || string.IsNullOrWhiteSpace(request.SourceReference) || request.Amount == 0m || request.Amount < 0m && request.ReversalOfId is null)
            throw new FixedAssetException("assets.cip_source_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var asset = new NpgsqlCommand("select lifecycle_state from business_assets.fixed_asset where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction); asset.Parameters.AddWithValue(tenant.Value); asset.Parameters.AddWithValue(assetId);
            var lifecycle = (string?)await asset.ExecuteScalarAsync(token) ?? throw new FixedAssetException("assets.asset_not_found");
            if (lifecycle != "UnderConstruction") throw new FixedAssetException("assets.cip_not_open");
            if (request.ReversalOfId is { } reversalId)
            {
                await using var reversal = new NpgsqlCommand("select amount from business_assets.asset_cip_cost_source where tenant_id=$1 and id=$2 and asset_id=$3", context.Connection, context.Transaction); reversal.Parameters.AddWithValue(tenant.Value); reversal.Parameters.AddWithValue(reversalId); reversal.Parameters.AddWithValue(assetId); var original = (decimal?)await reversal.ExecuteScalarAsync(token); if (original is null || original <= 0m || request.Amount != -original.Value) throw new FixedAssetException("assets.cip_reversal_invalid");
            }
            var sourceId = Guid.CreateVersion7();
            await using var source = new NpgsqlCommand("insert into business_assets.asset_cip_cost_source(tenant_id,id,asset_id,source_type,source_reference,amount,reversal_of_id,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,now())", context.Connection, context.Transaction); source.Parameters.AddWithValue(tenant.Value); source.Parameters.AddWithValue(sourceId); source.Parameters.AddWithValue(assetId); source.Parameters.AddWithValue(request.SourceType.Trim()); source.Parameters.AddWithValue(request.SourceReference.Trim()); source.Parameters.AddWithValue(request.Amount); source.Parameters.AddWithValue((object?)request.ReversalOfId ?? DBNull.Value); source.Parameters.AddWithValue(actor);
            try { await source.ExecuteNonQueryAsync(token); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new FixedAssetException("assets.cip_source_exists"); }
            await using var books = new NpgsqlCommand("select id,original_cost from business_assets.asset_book where tenant_id=$1 and asset_id=$2 for update", context.Connection, context.Transaction); books.Parameters.AddWithValue(tenant.Value); books.Parameters.AddWithValue(assetId); await using var reader = await books.ExecuteReaderAsync(token); var bookRows = new List<(Guid Id, decimal Cost)>(); while (await reader.ReadAsync(token)) bookRows.Add((reader.GetGuid(0), reader.GetDecimal(1))); await reader.CloseAsync();
            foreach (var bookRow in bookRows)
            {
                if (bookRow.Cost + request.Amount < 0m) throw new FixedAssetException("assets.cip_cost_negative");
                await using var update = new NpgsqlCommand("update business_assets.asset_book set original_cost=$1,row_version=row_version+1 where tenant_id=$2 and id=$3", context.Connection, context.Transaction); update.Parameters.AddWithValue(bookRow.Cost + request.Amount); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(bookRow.Id); await update.ExecuteNonQueryAsync(token);
                await using var transaction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,source_reference,idempotency_key,created_by,created_at) values($1,$2,$3,$4,current_date,$5,$6,$7,$8,now())", context.Connection, context.Transaction); transaction.Parameters.AddWithValue(tenant.Value); transaction.Parameters.AddWithValue(Guid.CreateVersion7()); transaction.Parameters.AddWithValue(bookRow.Id); transaction.Parameters.AddWithValue(request.Amount > 0m ? "CapitalAddition" : "CostReduction"); transaction.Parameters.AddWithValue(Math.Abs(request.Amount)); transaction.Parameters.AddWithValue($"{request.SourceType.Trim()}:{request.SourceReference.Trim()}"); transaction.Parameters.AddWithValue($"cip:{assetId:D}:{bookRow.Id:D}:{request.SourceType.Trim()}:{request.SourceReference.Trim()}"); transaction.Parameters.AddWithValue(actor); await transaction.ExecuteNonQueryAsync(token);
            }
            await using var view = new NpgsqlCommand("select a.asset_number,a.name,a.lifecycle_state,coalesce(sum(s.amount),0),count(s.id),a.capitalization_date,a.placed_in_service_date from business_assets.fixed_asset a left join business_assets.asset_cip_cost_source s on s.tenant_id=a.tenant_id and s.asset_id=a.id where a.tenant_id=$1 and a.id=$2 group by a.asset_number,a.name,a.lifecycle_state,a.capitalization_date,a.placed_in_service_date", context.Connection, context.Transaction); view.Parameters.AddWithValue(tenant.Value); view.Parameters.AddWithValue(assetId); await using var viewReader = await view.ExecuteReaderAsync(token); if (!await viewReader.ReadAsync(token)) throw new FixedAssetException("assets.asset_not_found"); return new CipAssetView(assetId, viewReader.GetString(0), viewReader.GetString(1), viewReader.GetString(2), viewReader.GetDecimal(3), viewReader.GetInt32(4), viewReader.IsDBNull(5) ? null : viewReader.GetFieldValue<DateOnly>(5), viewReader.IsDBNull(6) ? null : viewReader.GetFieldValue<DateOnly>(6));
        }, ct);
    }

    public async Task<CipSplitView> SplitCipAsync(TenantId tenant, Guid actor, Guid assetId, SplitCipRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.Targets.Count == 0 || request.Targets.Count > 200 || request.Targets.Any(target => target.TargetCategoryId == Guid.Empty || string.IsNullOrWhiteSpace(target.Name) || target.Amount <= 0m || target.Quantity <= 0m || target.CapitalizationDate == default || target.PlacedInServiceDate < target.CapitalizationDate))
            throw new FixedAssetException("assets.cip_split_invalid");
        var splitKey = request.IdempotencyKey.Trim();
        var operation = $"cip-split:{assetId:D}";
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request)))).ToLowerInvariant();
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var existing = new NpgsqlCommand("select request_hash,response::text from business_assets.asset_idempotency_key where tenant_id=$1 and operation=$2 and idempotency_key=$3 for update", context.Connection, context.Transaction);
            existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(operation); existing.Parameters.AddWithValue(splitKey);
            await using var existingReader = await existing.ExecuteReaderAsync(token);
            if (await existingReader.ReadAsync(token))
            {
                var existingHash = existingReader.GetString(0); var response = existingReader.IsDBNull(1) ? null : existingReader.GetString(1); await existingReader.CloseAsync();
                if (!string.Equals(existingHash, requestHash, StringComparison.Ordinal)) throw new FixedAssetException("assets.cip_split_conflict");
                if (string.IsNullOrWhiteSpace(response)) throw new FixedAssetException("assets.cip_split_retry_incomplete");
                var replay = JsonSerializer.Deserialize<CipSplitView>(response) ?? throw new FixedAssetException("assets.cip_split_retry_incomplete");
                return replay with { AlreadyApplied = true };
            }
            await existingReader.CloseAsync();

            await using var cip = new NpgsqlCommand("select legal_entity_id,asset_number,lifecycle_state from business_assets.fixed_asset where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction);
            cip.Parameters.AddWithValue(tenant.Value); cip.Parameters.AddWithValue(assetId);
            await using var cipReader = await cip.ExecuteReaderAsync(token);
            if (!await cipReader.ReadAsync(token)) throw new FixedAssetException("assets.asset_not_found");
            var legalEntityId = cipReader.GetGuid(0); var cipNumber = cipReader.GetString(1); var lifecycle = cipReader.GetString(2); await cipReader.CloseAsync();
            if (lifecycle != "UnderConstruction") throw new FixedAssetException("assets.cip_not_open");

            await using var cipBooks = new NpgsqlCommand("select b.id,b.book_definition_id,b.original_cost,bd.posts_to_ledger,bd.currency_code from business_assets.asset_book b join business_assets.asset_book_definition bd on bd.tenant_id=b.tenant_id and bd.id=b.book_definition_id where b.tenant_id=$1 and b.asset_id=$2 order by b.id for update", context.Connection, context.Transaction);
            cipBooks.Parameters.AddWithValue(tenant.Value); cipBooks.Parameters.AddWithValue(assetId);
            await using var cipBookReader = await cipBooks.ExecuteReaderAsync(token);
            var bookRows = new List<(Guid Id, Guid DefinitionId, decimal Cost, bool PostsToLedger, string Currency)>(); while (await cipBookReader.ReadAsync(token)) bookRows.Add((cipBookReader.GetGuid(0), cipBookReader.GetGuid(1), cipBookReader.GetDecimal(2), cipBookReader.GetBoolean(3), cipBookReader.GetString(4).Trim())); await cipBookReader.CloseAsync();
            if (bookRows.Count == 0) throw new FixedAssetException("assets.book_not_found");
            var cipOriginalCost = bookRows[0].Cost;

            await using var sources = new NpgsqlCommand("select s.id,s.amount,coalesce((select sum(r.amount) from business_assets.asset_cip_cost_source r where r.tenant_id=s.tenant_id and r.reversal_of_id=s.id),0),coalesce((select sum(a.amount) from business_assets.asset_cip_split_allocation a where a.tenant_id=s.tenant_id and a.cost_source_id=s.id),0) from business_assets.asset_cip_cost_source s where s.tenant_id=$1 and s.asset_id=$2 and s.amount > 0 order by s.created_at,s.id", context.Connection, context.Transaction);
            sources.Parameters.AddWithValue(tenant.Value); sources.Parameters.AddWithValue(assetId);
            await using var sourceReader = await sources.ExecuteReaderAsync(token);
            var sourceRows = new List<(Guid Id, decimal Available)>(); while (await sourceReader.ReadAsync(token)) { var available = sourceReader.GetDecimal(1) + sourceReader.GetDecimal(2) - sourceReader.GetDecimal(3); if (available > 0m) sourceRows.Add((sourceReader.GetGuid(0), available)); } await sourceReader.CloseAsync();
            var eligibleCost = sourceRows.Sum(row => row.Available);
            var requestedCost = request.Targets.Sum(target => target.Amount);
            if (eligibleCost <= 0m || requestedCost > eligibleCost || requestedCost > cipOriginalCost) throw new FixedAssetException("assets.cip_split_amount_invalid");

            var targetViews = new List<CipSplitTargetView>(); var sourceIndex = 0; var sourceRemaining = sourceRows.Count == 0 ? 0m : sourceRows[0].Available;
            foreach (var target in request.Targets)
            {
                await using var category = new NpgsqlCommand("select code,asset_nature,tracking_granularity,default_depreciation_method,default_useful_life_months,default_residual_value from business_assets.asset_category where tenant_id=$1 and id=$2 and active", context.Connection, context.Transaction);
                category.Parameters.AddWithValue(tenant.Value); category.Parameters.AddWithValue(target.TargetCategoryId);
                await using var categoryReader = await category.ExecuteReaderAsync(token);
                if (!await categoryReader.ReadAsync(token)) throw new FixedAssetException("assets.category_not_found");
                var categoryCode = categoryReader.GetString(0); var nature = categoryReader.GetString(1); var tracking = categoryReader.GetString(2); var method = categoryReader.GetString(3); var usefulLife = categoryReader.IsDBNull(4) ? (int?)null : categoryReader.GetInt32(4); var residual = Math.Min(target.Amount, categoryReader.GetDecimal(5)); await categoryReader.CloseAsync();
                if (nature == "CIP" || method is not ("StraightLine" or "NoDepreciation")) throw new FixedAssetException("assets.cip_split_target_policy_invalid");
                var targetId = Guid.CreateVersion7(); var targetNumber = await sequences.AllocateAsync(context, new SequenceDefinition("fixed-assets.asset-number", tenant, legalEntityId, null, null, null, "FA-", 8, 1), token);
                await using var targetAsset = new NpgsqlCommand("insert into business_assets.fixed_asset(tenant_id,id,legal_entity_id,category_id,asset_number,name,lifecycle_state,asset_nature,tracking_granularity,source_origin_type,acquisition_date,capitalization_date,placed_in_service_date,parent_asset_id,quantity,created_by,created_at,row_version) values($1,$2,$3,$4,$5,$6,'Capitalized',$7,$8,'CIPSplit',$9,$10,$11,$12,$13,$14,now(),1)", context.Connection, context.Transaction);
                targetAsset.Parameters.AddWithValue(tenant.Value); targetAsset.Parameters.AddWithValue(targetId); targetAsset.Parameters.AddWithValue(legalEntityId); targetAsset.Parameters.AddWithValue(target.TargetCategoryId); targetAsset.Parameters.AddWithValue(targetNumber); targetAsset.Parameters.AddWithValue(target.Name.Trim()); targetAsset.Parameters.AddWithValue(nature); targetAsset.Parameters.AddWithValue(tracking); targetAsset.Parameters.AddWithValue(target.CapitalizationDate); targetAsset.Parameters.AddWithValue(target.CapitalizationDate); targetAsset.Parameters.AddWithValue((object?)target.PlacedInServiceDate ?? DBNull.Value); targetAsset.Parameters.AddWithValue(assetId); targetAsset.Parameters.AddWithValue(target.Quantity); targetAsset.Parameters.AddWithValue(actor); await targetAsset.ExecuteNonQueryAsync(token);
                foreach (var bookRow in bookRows)
                {
                    var bookId = Guid.CreateVersion7();
                    await using var book = new NpgsqlCommand("insert into business_assets.asset_book(tenant_id,id,asset_id,book_definition_id,original_cost,residual_value,useful_life_months,depreciation_method,placed_in_service_date,created_at,row_version) select $1,$2,$3,bd.id,$4,$5,$6,$7,$8,now(),1 from business_assets.asset_book_definition bd where bd.tenant_id=$1 and bd.id=$9", context.Connection, context.Transaction);
                    book.Parameters.AddWithValue(tenant.Value); book.Parameters.AddWithValue(bookId); book.Parameters.AddWithValue(targetId); book.Parameters.AddWithValue(target.Amount); book.Parameters.AddWithValue(residual); book.Parameters.AddWithValue((object?)usefulLife ?? DBNull.Value); book.Parameters.AddWithValue(method); book.Parameters.AddWithValue((object?)target.PlacedInServiceDate ?? DBNull.Value); book.Parameters.AddWithValue(bookRow.DefinitionId); await book.ExecuteNonQueryAsync(token);
                    Guid? ledgerFact = null;
                    if (bookRow.PostsToLedger)
                    {
                        var factId = StableGuid($"cip-split:{splitKey}:target:{targetId:D}:book:{bookRow.Id:D}"); var payload = new FixedAssetAcquisitionPostedPayload(target.Amount, bookRow.Currency, $"{targetNumber}/{bookRow.Id:D}", categoryCode); var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new(legalEntityId), AccountingFactTypes.FixedAssetAcquisitionPosted, 1, "fixed-assets", $"cip-split:{splitKey}:target:{targetId:D}:book:{bookRow.Id:D}", $"fixed-assets:cip-split:{splitKey}:target:{targetId:D}:book:{bookRow.Id:D}", target.CapitalizationDate, target.CapitalizationDate, payload, Guid.CreateVersion7(), actor); ledgerFact = (await ledger.PostFixedAssetAcquisitionAsync(context, fact, payload, token)).FactId;
                    }
                    await using var transaction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,source_reference,idempotency_key,ledger_fact_reference,created_by,created_at) values($1,$2,$3,'CapitalAddition',$4,$5,$6,$7,$8,$9,now())", context.Connection, context.Transaction);
                    transaction.Parameters.AddWithValue(tenant.Value); transaction.Parameters.AddWithValue(Guid.CreateVersion7()); transaction.Parameters.AddWithValue(bookId); transaction.Parameters.AddWithValue(target.CapitalizationDate); transaction.Parameters.AddWithValue(target.Amount); transaction.Parameters.AddWithValue($"cip-split:{splitKey}:cip:{assetId:D}"); transaction.Parameters.AddWithValue($"cip-split:{splitKey}:target:{targetId:D}:book:{bookId:D}"); transaction.Parameters.AddWithValue((object?)ledgerFact?.ToString("D") ?? DBNull.Value); transaction.Parameters.AddWithValue(actor); await transaction.ExecuteNonQueryAsync(token);
                }
                var targetRemaining = target.Amount;
                while (targetRemaining > 0m && sourceIndex < sourceRows.Count)
                {
                    var take = Math.Min(targetRemaining, sourceRemaining);
                    await using var allocation = new NpgsqlCommand("insert into business_assets.asset_cip_split_allocation(tenant_id,id,cip_asset_id,target_asset_id,cost_source_id,split_key,amount,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,now())", context.Connection, context.Transaction);
                    allocation.Parameters.AddWithValue(tenant.Value); allocation.Parameters.AddWithValue(Guid.CreateVersion7()); allocation.Parameters.AddWithValue(assetId); allocation.Parameters.AddWithValue(targetId); allocation.Parameters.AddWithValue(sourceRows[sourceIndex].Id); allocation.Parameters.AddWithValue(splitKey); allocation.Parameters.AddWithValue(take); allocation.Parameters.AddWithValue(actor); await allocation.ExecuteNonQueryAsync(token);
                    targetRemaining -= take; sourceRemaining -= take;
                    if (sourceRemaining <= 0.000001m) { sourceIndex++; sourceRemaining = sourceIndex < sourceRows.Count ? sourceRows[sourceIndex].Available : 0m; }
                }
                if (targetRemaining > 0.000001m) throw new FixedAssetException("assets.cip_split_source_balance_invalid");
                foreach (var bookRow in bookRows)
                {
                    var newCost = bookRow.Cost - target.Amount; if (newCost < 0m) throw new FixedAssetException("assets.cip_cost_negative");
                    Guid? ledgerFact = null;
                    if (bookRow.PostsToLedger)
                    {
                        var factId = StableGuid($"cip-split:{splitKey}:cip:{assetId:D}:book:{bookRow.Id:D}:target:{targetId:D}"); var payload = new FixedAssetCostAdjustmentPostedPayload(target.Amount, bookRow.Currency, $"{cipNumber}/{bookRow.Id:D}", cipNumber, $"CIP split to {targetNumber}"); var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new(legalEntityId), AccountingFactTypes.FixedAssetCostReduced, 1, "fixed-assets", $"cip-split:{splitKey}:cip:{assetId:D}:book:{bookRow.Id:D}:target:{targetId:D}", $"fixed-assets:cip-split:{splitKey}:cip:{assetId:D}:book:{bookRow.Id:D}:target:{targetId:D}", target.CapitalizationDate, target.CapitalizationDate, payload, Guid.CreateVersion7(), actor); ledgerFact = (await ledger.PostFixedAssetCostReducedAsync(context, fact, payload, token)).FactId;
                    }
                    await using var updateBook = new NpgsqlCommand("update business_assets.asset_book set original_cost=$1,row_version=row_version+1 where tenant_id=$2 and id=$3", context.Connection, context.Transaction); updateBook.Parameters.AddWithValue(newCost); updateBook.Parameters.AddWithValue(tenant.Value); updateBook.Parameters.AddWithValue(bookRow.Id); await updateBook.ExecuteNonQueryAsync(token);
                    await using var reduction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,source_reference,idempotency_key,ledger_fact_reference,created_by,created_at) values($1,$2,$3,'CostReduction',$4,$5,$6,$7,$8,$9,now())", context.Connection, context.Transaction); reduction.Parameters.AddWithValue(tenant.Value); reduction.Parameters.AddWithValue(Guid.CreateVersion7()); reduction.Parameters.AddWithValue(bookRow.Id); reduction.Parameters.AddWithValue(target.CapitalizationDate); reduction.Parameters.AddWithValue(target.Amount); reduction.Parameters.AddWithValue($"cip-split:{splitKey}:target:{targetId:D}"); reduction.Parameters.AddWithValue($"cip-split:{splitKey}:cip:{assetId:D}:book:{bookRow.Id:D}:target:{targetId:D}:reduction"); reduction.Parameters.AddWithValue((object?)ledgerFact?.ToString("D") ?? DBNull.Value); reduction.Parameters.AddWithValue(actor); await reduction.ExecuteNonQueryAsync(token);
                }
                targetViews.Add(new CipSplitTargetView(targetId, targetNumber, target.Name.Trim(), target.Amount, target.Quantity, "Capitalized"));
                context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "FixedAssetCapitalizedV1", JsonSerializer.Serialize(new FixedAssetCapitalizedV1(targetId, legalEntityId, target.CapitalizationDate, target.PlacedInServiceDate, target.Amount, DateTimeOffset.UtcNow)), tenant, DateTimeOffset.UtcNow, targetId, targetId, "fixed-assets"));
            }
            var remainingCost = cipOriginalCost - requestedCost;
            await using var lifecycleUpdate = new NpgsqlCommand("update business_assets.fixed_asset set lifecycle_state=case when $1 <= 0.000001 then 'Capitalized' else 'UnderConstruction' end,row_version=row_version+1 where tenant_id=$2 and id=$3", context.Connection, context.Transaction); lifecycleUpdate.Parameters.AddWithValue(remainingCost); lifecycleUpdate.Parameters.AddWithValue(tenant.Value); lifecycleUpdate.Parameters.AddWithValue(assetId); await lifecycleUpdate.ExecuteNonQueryAsync(token);
            var result = new CipSplitView(assetId, remainingCost <= 0.000001m ? "Capitalized" : "UnderConstruction", cipOriginalCost, requestedCost, remainingCost, targetViews);
            await using var idempotency = new NpgsqlCommand("insert into business_assets.asset_idempotency_key(tenant_id,operation,idempotency_key,request_hash,response,created_at) values($1,$2,$3,$4,$5::jsonb,now())", context.Connection, context.Transaction); idempotency.Parameters.AddWithValue(tenant.Value); idempotency.Parameters.AddWithValue(operation); idempotency.Parameters.AddWithValue(splitKey); idempotency.Parameters.AddWithValue(requestHash); idempotency.Parameters.AddWithValue(JsonSerializer.Serialize(result)); await idempotency.ExecuteNonQueryAsync(token);
            return result;
        }, ct);
    }

    public async Task<CipAssetView> CapitalizeCipAsync(TenantId tenant, Guid actor, Guid assetId, CapitalizeCipRequest request, CancellationToken ct)
    {
        if (request.TargetCategoryId == Guid.Empty || request.CapitalizationDate == default || request.PlacedInServiceDate < request.CapitalizationDate) throw new FixedAssetException("assets.cip_capitalization_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var category = new NpgsqlCommand("select asset_nature,tracking_granularity,default_depreciation_method,default_useful_life_months from business_assets.asset_category where tenant_id=$1 and id=$2 and active", context.Connection, context.Transaction); category.Parameters.AddWithValue(tenant.Value); category.Parameters.AddWithValue(request.TargetCategoryId); await using var categoryReader = await category.ExecuteReaderAsync(token); if (!await categoryReader.ReadAsync(token)) throw new FixedAssetException("assets.category_not_found"); var nature = categoryReader.GetString(0); var tracking = categoryReader.GetString(1); var method = categoryReader.GetString(2); var usefulLife = categoryReader.IsDBNull(3) ? (int?)null : categoryReader.GetInt32(3); await categoryReader.CloseAsync(); if (nature == "CIP") throw new FixedAssetException("assets.cip_target_category_invalid");
            await using var asset = new NpgsqlCommand("select lifecycle_state from business_assets.fixed_asset where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction); asset.Parameters.AddWithValue(tenant.Value); asset.Parameters.AddWithValue(assetId); if ((string?)await asset.ExecuteScalarAsync(token) != "UnderConstruction") throw new FixedAssetException("assets.cip_not_open");
            await using var cost = new NpgsqlCommand("select coalesce(sum(amount),0) from business_assets.asset_cip_cost_source where tenant_id=$1 and asset_id=$2", context.Connection, context.Transaction); cost.Parameters.AddWithValue(tenant.Value); cost.Parameters.AddWithValue(assetId); var total = (decimal)(await cost.ExecuteScalarAsync(token) ?? 0m); if (total <= 0m) throw new FixedAssetException("assets.cip_without_cost");
            await using var update = new NpgsqlCommand("update business_assets.fixed_asset set category_id=$1,asset_nature=$2,tracking_granularity=$3,lifecycle_state='Capitalized',name=coalesce($4,name),capitalization_date=$5,placed_in_service_date=$6,updated_by=$7,updated_at=now(),row_version=row_version+1 where tenant_id=$8 and id=$9", context.Connection, context.Transaction); update.Parameters.AddWithValue(request.TargetCategoryId); update.Parameters.AddWithValue(nature); update.Parameters.AddWithValue(tracking); update.Parameters.AddWithValue((object?)request.Name?.Trim() ?? DBNull.Value); update.Parameters.AddWithValue(request.CapitalizationDate); update.Parameters.AddWithValue((object?)request.PlacedInServiceDate ?? DBNull.Value); update.Parameters.AddWithValue(actor); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(assetId); await update.ExecuteNonQueryAsync(token);
            await using var books = new NpgsqlCommand("update business_assets.asset_book set depreciation_method=$1,useful_life_months=$2,placed_in_service_date=$3,row_version=row_version+1 where tenant_id=$4 and asset_id=$5", context.Connection, context.Transaction); books.Parameters.AddWithValue(method); books.Parameters.AddWithValue((object?)usefulLife ?? DBNull.Value); books.Parameters.AddWithValue((object?)request.PlacedInServiceDate ?? DBNull.Value); books.Parameters.AddWithValue(tenant.Value); books.Parameters.AddWithValue(assetId); await books.ExecuteNonQueryAsync(token);
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "FixedAssetCapitalizedV1", JsonSerializer.Serialize(new FixedAssetCapitalizedV1(assetId, await ResolveAssetEntityAsync(context, tenant, assetId, token), request.CapitalizationDate, request.PlacedInServiceDate, total, DateTimeOffset.UtcNow)), tenant, DateTimeOffset.UtcNow, assetId, assetId, "fixed-assets"));
            await using var view = new NpgsqlCommand("select a.asset_number,a.name,a.lifecycle_state,coalesce(sum(s.amount),0),count(s.id),a.capitalization_date,a.placed_in_service_date from business_assets.fixed_asset a left join business_assets.asset_cip_cost_source s on s.tenant_id=a.tenant_id and s.asset_id=a.id where a.tenant_id=$1 and a.id=$2 group by a.asset_number,a.name,a.lifecycle_state,a.capitalization_date,a.placed_in_service_date", context.Connection, context.Transaction); view.Parameters.AddWithValue(tenant.Value); view.Parameters.AddWithValue(assetId); await using var viewReader = await view.ExecuteReaderAsync(token); if (!await viewReader.ReadAsync(token)) throw new FixedAssetException("assets.asset_not_found"); return new CipAssetView(assetId, viewReader.GetString(0), viewReader.GetString(1), viewReader.GetString(2), viewReader.GetDecimal(3), viewReader.GetInt32(4), viewReader.GetFieldValue<DateOnly>(5), viewReader.IsDBNull(6) ? null : viewReader.GetFieldValue<DateOnly>(6));
        }, ct);
    }

    public async Task<GroupedAssetSplitView> SplitGroupedAssetAsync(TenantId tenant, Guid actor, Guid assetId, SplitGroupedAssetRequest request, CancellationToken ct)
    {
        if (assetId == Guid.Empty || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.Quantity <= 0m || string.IsNullOrWhiteSpace(request.Name) || request.EffectiveDate == default)
            throw new FixedAssetException("assets.grouped_split_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            const string operation = "grouped-asset-split";
            var key = request.IdempotencyKey.Trim();
            var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
            await using (var existing = new NpgsqlCommand("select request_hash,response::text from business_assets.asset_idempotency_key where tenant_id=$1 and operation=$2 and idempotency_key=$3 for update", context.Connection, context.Transaction))
            {
                existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(operation); existing.Parameters.AddWithValue(key);
                await using var reader = await existing.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    var hash = reader.GetString(0); var response = reader.IsDBNull(1) ? null : reader.GetString(1); await reader.CloseAsync();
                    if (!string.Equals(hash, requestHash, StringComparison.Ordinal)) throw new FixedAssetException("assets.grouped_split_conflict");
                    if (string.IsNullOrWhiteSpace(response)) throw new FixedAssetException("assets.grouped_split_retry_incomplete");
                    return (JsonSerializer.Deserialize<GroupedAssetSplitView>(response) ?? throw new FixedAssetException("assets.grouped_split_retry_incomplete")) with { AlreadyApplied = true };
                }
            }

            await using var parent = new NpgsqlCommand("select legal_entity_id,category_id,asset_number,name,lifecycle_state,asset_nature,tracking_granularity,description,asset_tag,product_variant_ref,inventory_serial_ref,source_origin_type,acquisition_date,capitalization_date,placed_in_service_date,quantity from business_assets.fixed_asset where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction);
            parent.Parameters.AddWithValue(tenant.Value); parent.Parameters.AddWithValue(assetId);
            await using var parentReader = await parent.ExecuteReaderAsync(token);
            if (!await parentReader.ReadAsync(token)) throw new FixedAssetException("assets.asset_not_found");
            var legalEntityId = parentReader.GetGuid(0); var categoryId = parentReader.GetGuid(1); var parentNumber = parentReader.GetString(2); var lifecycle = parentReader.GetString(4); var nature = parentReader.GetString(5); var tracking = parentReader.GetString(6);
            var description = parentReader.IsDBNull(7) ? null : parentReader.GetString(7); var tag = parentReader.IsDBNull(8) ? null : parentReader.GetString(8); var product = parentReader.IsDBNull(9) ? null : parentReader.GetString(9); var serial = parentReader.IsDBNull(10) ? null : parentReader.GetString(10); var source = parentReader.IsDBNull(11) ? null : parentReader.GetString(11); var acquisition = parentReader.IsDBNull(12) ? (DateOnly?)null : parentReader.GetFieldValue<DateOnly>(12); var capitalization = parentReader.IsDBNull(13) ? (DateOnly?)null : parentReader.GetFieldValue<DateOnly>(13); var serviceDate = parentReader.IsDBNull(14) ? (DateOnly?)null : parentReader.GetFieldValue<DateOnly>(14); var parentQuantity = parentReader.GetDecimal(15); await parentReader.CloseAsync();
            if (nature == "CIP" || lifecycle is "Disposed" or "Archived" || parentQuantity <= request.Quantity || tracking is "Serial" or "Unique") throw new FixedAssetException("assets.grouped_split_not_allowed");

            await using var history = new NpgsqlCommand("select exists(select 1 from business_assets.asset_book_transaction t join business_assets.asset_book b on b.tenant_id=t.tenant_id and b.id=t.asset_book_id where t.tenant_id=$1 and b.asset_id=$2 and t.transaction_type in ('Depreciation','Reversal','Impairment','Revaluation','Disposal'))", context.Connection, context.Transaction);
            history.Parameters.AddWithValue(tenant.Value); history.Parameters.AddWithValue(assetId);
            if ((bool)(await history.ExecuteScalarAsync(token) ?? false)) throw new FixedAssetException("assets.grouped_split_requires_unposted_history");

            await using var books = new NpgsqlCommand("select b.id,b.book_definition_id,b.original_cost,b.residual_value,b.useful_life_months,b.depreciation_method,b.placed_in_service_date,bd.posts_to_ledger,bd.currency_code,c.code from business_assets.asset_book b join business_assets.asset_book_definition bd on bd.tenant_id=b.tenant_id and bd.id=b.book_definition_id join business_assets.asset_category c on c.tenant_id=b.tenant_id and c.id=$3 where b.tenant_id=$1 and b.asset_id=$2 order by b.id for update", context.Connection, context.Transaction);
            books.Parameters.AddWithValue(tenant.Value); books.Parameters.AddWithValue(assetId); books.Parameters.AddWithValue(categoryId);
            await using var bookReader = await books.ExecuteReaderAsync(token);
            var bookRows = new List<(Guid Id, Guid DefinitionId, decimal Cost, decimal Residual, int? Life, string Method, DateOnly? ServiceDate, bool Posts, string Currency, string CategoryCode)>();
            while (await bookReader.ReadAsync(token)) bookRows.Add((bookReader.GetGuid(0), bookReader.GetGuid(1), bookReader.GetDecimal(2), bookReader.GetDecimal(3), bookReader.IsDBNull(4) ? null : bookReader.GetInt32(4), bookReader.GetString(5), bookReader.IsDBNull(6) ? null : bookReader.GetFieldValue<DateOnly>(6), bookReader.GetBoolean(7), bookReader.GetString(8).Trim(), bookReader.GetString(9)));
            await bookReader.CloseAsync();
            if (bookRows.Count == 0) throw new FixedAssetException("assets.book_not_found");

            var ratio = request.Quantity / parentQuantity;
            var childId = Guid.CreateVersion7();
            var childNumber = await sequences.AllocateAsync(context, new SequenceDefinition("fixed-assets.asset-number", tenant, legalEntityId, null, null, null, "FA-", 8, 1), token);
            await using (var insert = new NpgsqlCommand("insert into business_assets.fixed_asset(tenant_id,id,legal_entity_id,category_id,asset_number,name,description,asset_tag,lifecycle_state,asset_nature,tracking_granularity,product_variant_ref,inventory_serial_ref,source_origin_type,acquisition_date,capitalization_date,placed_in_service_date,parent_asset_id,quantity,created_by,created_at,row_version) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,'GroupedSplit',$14,$15,$16,$17,$18,$19,now(),1)", context.Connection, context.Transaction))
            {
                insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(childId); insert.Parameters.AddWithValue(legalEntityId); insert.Parameters.AddWithValue(categoryId); insert.Parameters.AddWithValue(childNumber); insert.Parameters.AddWithValue(request.Name.Trim()); insert.Parameters.AddWithValue((object?)description ?? DBNull.Value); insert.Parameters.AddWithValue((object?)tag ?? DBNull.Value); insert.Parameters.AddWithValue(lifecycle); insert.Parameters.AddWithValue(nature); insert.Parameters.AddWithValue(tracking); insert.Parameters.AddWithValue((object?)product ?? DBNull.Value); insert.Parameters.AddWithValue((object?)serial ?? DBNull.Value); insert.Parameters.AddWithValue((object?)acquisition ?? DBNull.Value); insert.Parameters.AddWithValue((object?)capitalization ?? DBNull.Value); insert.Parameters.AddWithValue((object?)serviceDate ?? DBNull.Value); insert.Parameters.AddWithValue(assetId); insert.Parameters.AddWithValue(request.Quantity); insert.Parameters.AddWithValue(actor); await insert.ExecuteNonQueryAsync(token);
            }
            var allocatedCost = 0m;
            foreach (var row in bookRows)
            {
                var childCost = Math.Round(row.Cost * ratio, 2, MidpointRounding.AwayFromZero); var childResidual = Math.Min(childCost, Math.Round(row.Residual * ratio, 2, MidpointRounding.AwayFromZero)); allocatedCost += childCost;
                var childBookId = Guid.CreateVersion7();
                await using (var childBook = new NpgsqlCommand("insert into business_assets.asset_book(tenant_id,id,asset_id,book_definition_id,original_cost,residual_value,useful_life_months,depreciation_method,placed_in_service_date,created_at,row_version) values($1,$2,$3,$4,$5,$6,$7,$8,$9,now(),1)", context.Connection, context.Transaction))
                {
                    childBook.Parameters.AddWithValue(tenant.Value); childBook.Parameters.AddWithValue(childBookId); childBook.Parameters.AddWithValue(childId); childBook.Parameters.AddWithValue(row.DefinitionId); childBook.Parameters.AddWithValue(childCost); childBook.Parameters.AddWithValue(childResidual); childBook.Parameters.AddWithValue((object?)row.Life ?? DBNull.Value); childBook.Parameters.AddWithValue(row.Method); childBook.Parameters.AddWithValue((object?)row.ServiceDate ?? DBNull.Value); await childBook.ExecuteNonQueryAsync(token);
                }
                Guid? childFact = null; Guid? parentFact = null;
                if (row.Posts)
                {
                    var childPayload = new FixedAssetCostAdjustmentPostedPayload(childCost, row.Currency, $"{childNumber}/{childBookId:D}", childNumber, $"Grouped asset split from {parentNumber}");
                    var childEnvelope = new AccountingFactEnvelope<IAccountingFactPayload>(StableGuid($"grouped-split:{key}:child:{childBookId:D}"), tenant, new(legalEntityId), AccountingFactTypes.FixedAssetCostAdded, 1, "fixed-assets", $"grouped-split:{key}:child:{childBookId:D}", $"fixed-assets:grouped-split:{key}:child:{childBookId:D}", request.EffectiveDate, request.EffectiveDate, childPayload, Guid.CreateVersion7(), actor);
                    childFact = (await ledger.PostFixedAssetCostAddedAsync(context, childEnvelope, childPayload, token)).FactId;
                    var parentPayload = new FixedAssetCostAdjustmentPostedPayload(childCost, row.Currency, $"{parentNumber}/{row.Id:D}", parentNumber, $"Grouped asset split to {childNumber}");
                    var parentEnvelope = new AccountingFactEnvelope<IAccountingFactPayload>(StableGuid($"grouped-split:{key}:parent:{row.Id:D}"), tenant, new(legalEntityId), AccountingFactTypes.FixedAssetCostReduced, 1, "fixed-assets", $"grouped-split:{key}:parent:{row.Id:D}", $"fixed-assets:grouped-split:{key}:parent:{row.Id:D}", request.EffectiveDate, request.EffectiveDate, parentPayload, Guid.CreateVersion7(), actor);
                    parentFact = (await ledger.PostFixedAssetCostReducedAsync(context, parentEnvelope, parentPayload, token)).FactId;
                }
                await using (var childTransaction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,source_reference,idempotency_key,ledger_fact_reference,created_by,created_at) values($1,$2,$3,'CapitalAddition',$4,$5,$6,$7,$8,$9,now())", context.Connection, context.Transaction))
                {
                    childTransaction.Parameters.AddWithValue(tenant.Value); childTransaction.Parameters.AddWithValue(Guid.CreateVersion7()); childTransaction.Parameters.AddWithValue(childBookId); childTransaction.Parameters.AddWithValue(request.EffectiveDate); childTransaction.Parameters.AddWithValue(childCost); childTransaction.Parameters.AddWithValue($"grouped-split:{key}:parent:{assetId:D}"); childTransaction.Parameters.AddWithValue($"grouped-split:{key}:child:{childBookId:D}"); childTransaction.Parameters.AddWithValue((object?)childFact?.ToString("D") ?? DBNull.Value); childTransaction.Parameters.AddWithValue(actor); await childTransaction.ExecuteNonQueryAsync(token);
                }
                await using (var parentTransaction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,source_reference,idempotency_key,ledger_fact_reference,created_by,created_at) values($1,$2,$3,'CostReduction',$4,$5,$6,$7,$8,$9,now())", context.Connection, context.Transaction))
                {
                    parentTransaction.Parameters.AddWithValue(tenant.Value); parentTransaction.Parameters.AddWithValue(Guid.CreateVersion7()); parentTransaction.Parameters.AddWithValue(row.Id); parentTransaction.Parameters.AddWithValue(request.EffectiveDate); parentTransaction.Parameters.AddWithValue(childCost); parentTransaction.Parameters.AddWithValue($"grouped-split:{key}:child:{childId:D}"); parentTransaction.Parameters.AddWithValue($"grouped-split:{key}:parent:{row.Id:D}"); parentTransaction.Parameters.AddWithValue((object?)parentFact?.ToString("D") ?? DBNull.Value); parentTransaction.Parameters.AddWithValue(actor); await parentTransaction.ExecuteNonQueryAsync(token);
                }
                await using var updateBook = new NpgsqlCommand("update business_assets.asset_book set original_cost=original_cost-$1,residual_value=greatest(0,residual_value-$2),row_version=row_version+1 where tenant_id=$3 and id=$4", context.Connection, context.Transaction); updateBook.Parameters.AddWithValue(childCost); updateBook.Parameters.AddWithValue(childResidual); updateBook.Parameters.AddWithValue(tenant.Value); updateBook.Parameters.AddWithValue(row.Id); await updateBook.ExecuteNonQueryAsync(token);
            }
            var parentAfter = parentQuantity - request.Quantity;
            await using (var updateParent = new NpgsqlCommand("update business_assets.fixed_asset set quantity=$1,row_version=row_version+1,updated_by=$2,updated_at=now() where tenant_id=$3 and id=$4", context.Connection, context.Transaction)) { updateParent.Parameters.AddWithValue(parentAfter); updateParent.Parameters.AddWithValue(actor); updateParent.Parameters.AddWithValue(tenant.Value); updateParent.Parameters.AddWithValue(assetId); await updateParent.ExecuteNonQueryAsync(token); }
            var result = new GroupedAssetSplitView(assetId, childId, childNumber, parentQuantity, parentAfter, request.Quantity, allocatedCost, "Completed");
            await using var idempotency = new NpgsqlCommand("insert into business_assets.asset_idempotency_key(tenant_id,operation,idempotency_key,request_hash,response,created_at) values($1,$2,$3,$4,$5::jsonb,now())", context.Connection, context.Transaction); idempotency.Parameters.AddWithValue(tenant.Value); idempotency.Parameters.AddWithValue(operation); idempotency.Parameters.AddWithValue(key); idempotency.Parameters.AddWithValue(requestHash); idempotency.Parameters.AddWithValue(JsonSerializer.Serialize(result)); await idempotency.ExecuteNonQueryAsync(token);
            return result;
        }, ct);
    }

    public async Task<AssetBookCostChangeView> ChangeAssetBookCostAsync(TenantId tenant, Guid actor, Guid assetId, ChangeAssetBookCostRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty || request.BookId == Guid.Empty || request.Amount == 0m || string.IsNullOrWhiteSpace(request.Reason)) throw new FixedAssetException("assets.book_cost_change_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? $"cost:{assetId:D}:{request.BookId:D}:{request.EffectiveDate:yyyyMMdd}:{request.Amount}:{request.Reason.Trim()}" : request.IdempotencyKey.Trim();
            await using var existing = new NpgsqlCommand("select b.original_cost,t.amount,t.transaction_type,t.ledger_fact_reference from business_assets.asset_book_transaction t join business_assets.asset_book b on b.tenant_id=t.tenant_id and b.id=t.asset_book_id where t.tenant_id=$1 and t.asset_book_id=$2 and t.idempotency_key=$3", context.Connection, context.Transaction); existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(request.BookId); existing.Parameters.AddWithValue(key);
            await using var existingReader = await existing.ExecuteReaderAsync(token);
            if (await existingReader.ReadAsync(token))
            {
                var currentCost = existingReader.GetDecimal(0); var appliedAmount = existingReader.GetDecimal(1); var appliedType = existingReader.GetString(2); var expectedType = request.Amount > 0m ? "CapitalAddition" : "CostReduction";
                if (appliedAmount != Math.Abs(request.Amount) || appliedType != expectedType) throw new FixedAssetException("assets.book_cost_change_conflict");
                var previousCost = expectedType == "CapitalAddition" ? currentCost - appliedAmount : currentCost + appliedAmount;
                var factReference = existingReader.IsDBNull(3) ? null : existingReader.GetString(3); await existingReader.CloseAsync(); return new AssetBookCostChangeView(assetId, request.BookId, request.Amount, appliedType, previousCost, currentCost, factReference, true);
            }
            await existingReader.CloseAsync();
            await using var book = new NpgsqlCommand("select b.original_cost,bd.posts_to_ledger,bd.currency_code,a.legal_entity_id,a.asset_number,a.lifecycle_state from business_assets.asset_book b join business_assets.asset_book_definition bd on bd.tenant_id=b.tenant_id and bd.id=b.book_definition_id join business_assets.fixed_asset a on a.tenant_id=b.tenant_id and a.id=b.asset_id where b.tenant_id=$1 and b.id=$2 and b.asset_id=$3 and a.legal_entity_id=$4 for update of b", context.Connection, context.Transaction); book.Parameters.AddWithValue(tenant.Value); book.Parameters.AddWithValue(request.BookId); book.Parameters.AddWithValue(assetId); book.Parameters.AddWithValue(request.LegalEntityId);
            await using var bookReader = await book.ExecuteReaderAsync(token);
            if (!await bookReader.ReadAsync(token)) throw new FixedAssetException("assets.book_not_found");
            var previousCostValue = bookReader.GetDecimal(0); var postsToLedger = bookReader.GetBoolean(1); var currency = bookReader.GetString(2).Trim(); var legalEntityId = bookReader.GetGuid(3); var assetNumber = bookReader.GetString(4); var lifecycle = bookReader.GetString(5); await bookReader.CloseAsync();
            if (lifecycle is "Disposed" or "Draft") throw new FixedAssetException("assets.book_cost_change_not_allowed");
            await using var accumulatedCommand = new NpgsqlCommand("select coalesce(sum(case when transaction_type='Depreciation' then amount when transaction_type='Reversal' then -amount when transaction_type='Disposal' then -accumulated_depreciation when transaction_type='Reinstatement' then accumulated_depreciation else 0 end),0),coalesce(sum(case when transaction_type='Impairment' then coalesce(nullif(accumulated_impairment,0),amount) when transaction_type='Disposal' then -accumulated_impairment when transaction_type='Reinstatement' then accumulated_impairment else 0 end),0) from business_assets.asset_book_transaction where tenant_id=$1 and asset_book_id=$2", context.Connection, context.Transaction); accumulatedCommand.Parameters.AddWithValue(tenant.Value); accumulatedCommand.Parameters.AddWithValue(request.BookId);
            await using var accumulatedReader = await accumulatedCommand.ExecuteReaderAsync(token);
            if (!await accumulatedReader.ReadAsync(token)) throw new FixedAssetException("assets.book_not_found");
            var accumulated = accumulatedReader.GetDecimal(0); var impairment = accumulatedReader.GetDecimal(1); await accumulatedReader.CloseAsync();
            var newCost = previousCostValue + request.Amount;
            if (newCost < 0m || newCost < accumulated + impairment) throw new FixedAssetException("assets.book_cost_change_below_carrying_value");
            Guid? ledgerFact = null;
            if (postsToLedger)
            {
                var factId = DeterministicGuid(request.BookId, StableGuid(key)); var factType = request.Amount > 0m ? AccountingFactTypes.FixedAssetCostAdded : AccountingFactTypes.FixedAssetCostReduced; var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new(legalEntityId), factType, 1, "fixed-assets", $"cost-adjustment:{assetId:D}:book:{request.BookId:D}:{key}", $"fixed-assets:cost:{request.BookId:D}:{key}", request.EffectiveDate, request.EffectiveDate, new FixedAssetCostAdjustmentPostedPayload(Math.Abs(request.Amount), currency, $"{assetNumber}/{request.BookId:D}", assetNumber, request.Reason.Trim()), Guid.CreateVersion7(), actor);
                var posting = request.Amount > 0m ? await ledger.PostFixedAssetCostAddedAsync(context, fact, (FixedAssetCostAdjustmentPostedPayload)fact.Payload, token) : await ledger.PostFixedAssetCostReducedAsync(context, fact, (FixedAssetCostAdjustmentPostedPayload)fact.Payload, token); ledgerFact = posting.FactId;
            }
            await using var update = new NpgsqlCommand("update business_assets.asset_book set original_cost=$1,row_version=row_version+1 where tenant_id=$2 and id=$3", context.Connection, context.Transaction); update.Parameters.AddWithValue(newCost); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(request.BookId); await update.ExecuteNonQueryAsync(token);
            await using var transaction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,source_reference,idempotency_key,ledger_fact_reference,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,now())", context.Connection, context.Transaction); transaction.Parameters.AddWithValue(tenant.Value); transaction.Parameters.AddWithValue(Guid.CreateVersion7()); transaction.Parameters.AddWithValue(request.BookId); transaction.Parameters.AddWithValue(request.Amount > 0m ? "CapitalAddition" : "CostReduction"); transaction.Parameters.AddWithValue(request.EffectiveDate); transaction.Parameters.AddWithValue(Math.Abs(request.Amount)); transaction.Parameters.AddWithValue($"asset:{assetId:D}:book:{request.BookId:D}"); transaction.Parameters.AddWithValue(key); transaction.Parameters.AddWithValue((object?)ledgerFact?.ToString("D") ?? DBNull.Value); transaction.Parameters.AddWithValue(actor); await transaction.ExecuteNonQueryAsync(token);
            return new AssetBookCostChangeView(assetId, request.BookId, request.Amount, request.Amount > 0m ? "CapitalAddition" : "CostReduction", previousCostValue, newCost, ledgerFact?.ToString("D"), false);
        }, ct);
    }

    public async Task<CombineCipView> CombineCipAsync(TenantId tenant, Guid actor, CombineCipRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty || request.TargetCategoryId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name) || request.CapitalizationDate == default || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.Sources is null or { Count: < 2 or > 50 } || request.Sources.Any(source => source.AssetId == Guid.Empty || source.Amount <= 0m) || request.Sources.Select(source => source.AssetId).Distinct().Count() != request.Sources.Count)
            throw new FixedAssetException("assets.cip_combine_invalid");
        if (request.PlacedInServiceDate is { } serviceDate && serviceDate < request.CapitalizationDate) throw new FixedAssetException("assets.cip_capitalization_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            const string operation = "cip-combine";
            var key = request.IdempotencyKey.Trim();
            var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
            await using (var existing = new NpgsqlCommand("select request_hash,response::text from business_assets.asset_idempotency_key where tenant_id=$1 and operation=$2 and idempotency_key=$3 for update", context.Connection, context.Transaction))
            {
                existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(operation); existing.Parameters.AddWithValue(key);
                await using var reader = await existing.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    var hash = reader.GetString(0); var response = reader.IsDBNull(1) ? null : reader.GetString(1); await reader.CloseAsync();
                    if (!string.Equals(hash, requestHash, StringComparison.Ordinal)) throw new FixedAssetException("assets.cip_combine_conflict");
                    if (string.IsNullOrWhiteSpace(response)) throw new FixedAssetException("assets.cip_combine_retry_incomplete");
                    return (JsonSerializer.Deserialize<CombineCipView>(response) ?? throw new FixedAssetException("assets.cip_combine_retry_incomplete")) with { AlreadyApplied = true };
                }
            }

            await using var category = new NpgsqlCommand("select code,asset_nature,tracking_granularity,default_depreciation_method,default_useful_life_months,default_residual_value from business_assets.asset_category where tenant_id=$1 and id=$2 and active", context.Connection, context.Transaction);
            category.Parameters.AddWithValue(tenant.Value); category.Parameters.AddWithValue(request.TargetCategoryId);
            await using var categoryReader = await category.ExecuteReaderAsync(token);
            if (!await categoryReader.ReadAsync(token)) throw new FixedAssetException("assets.category_not_found");
            var categoryCode = categoryReader.GetString(0); var nature = categoryReader.GetString(1); var tracking = categoryReader.GetString(2); var method = categoryReader.GetString(3); var usefulLife = categoryReader.IsDBNull(4) ? (int?)null : categoryReader.GetInt32(4); var residualDefault = categoryReader.GetDecimal(5); await categoryReader.CloseAsync();
            if (nature == "CIP" || method is not ("StraightLine" or "NoDepreciation")) throw new FixedAssetException("assets.cip_target_category_policy_invalid");

            var sourceRows = new List<(Guid Id, string Number, decimal Total, List<(Guid BookId, Guid DefinitionId, decimal Cost, decimal Residual, bool Posts, string Currency)> Books)>();
            foreach (var sourceRequest in request.Sources.OrderBy(source => source.AssetId))
            {
                await using var source = new NpgsqlCommand("select asset_number,legal_entity_id,lifecycle_state,asset_nature from business_assets.fixed_asset where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction); source.Parameters.AddWithValue(tenant.Value); source.Parameters.AddWithValue(sourceRequest.AssetId);
                await using var sourceReader = await source.ExecuteReaderAsync(token); if (!await sourceReader.ReadAsync(token)) throw new FixedAssetException("assets.asset_not_found"); var sourceNumber = sourceReader.GetString(0); var entity = sourceReader.GetGuid(1); var lifecycle = sourceReader.GetString(2); var sourceNature = sourceReader.GetString(3); await sourceReader.CloseAsync();
                if (entity != request.LegalEntityId || lifecycle != "UnderConstruction" || sourceNature != "CIP") throw new FixedAssetException("assets.cip_combine_source_invalid");
                await using var totalCommand = new NpgsqlCommand("select coalesce(sum(amount),0) from business_assets.asset_cip_cost_source where tenant_id=$1 and asset_id=$2", context.Connection, context.Transaction); totalCommand.Parameters.AddWithValue(tenant.Value); totalCommand.Parameters.AddWithValue(sourceRequest.AssetId); var total = (decimal)(await totalCommand.ExecuteScalarAsync(token) ?? 0m); if (total <= 0m || sourceRequest.Amount > total) throw new FixedAssetException("assets.cip_combine_amount_invalid");
                await using var books = new NpgsqlCommand("select b.id,b.book_definition_id,b.original_cost,b.residual_value,bd.posts_to_ledger,bd.currency_code from business_assets.asset_book b join business_assets.asset_book_definition bd on bd.tenant_id=b.tenant_id and bd.id=b.book_definition_id where b.tenant_id=$1 and b.asset_id=$2 order by b.book_definition_id for update", context.Connection, context.Transaction); books.Parameters.AddWithValue(tenant.Value); books.Parameters.AddWithValue(sourceRequest.AssetId);
                await using var bookReader = await books.ExecuteReaderAsync(token); var bookRows = new List<(Guid BookId, Guid DefinitionId, decimal Cost, decimal Residual, bool Posts, string Currency)>(); while (await bookReader.ReadAsync(token)) bookRows.Add((bookReader.GetGuid(0), bookReader.GetGuid(1), bookReader.GetDecimal(2), bookReader.GetDecimal(3), bookReader.GetBoolean(4), bookReader.GetString(5).Trim())); await bookReader.CloseAsync(); if (bookRows.Count == 0) throw new FixedAssetException("assets.book_not_found");
                if (bookRows.Any(row => sourceRequest.Amount > row.Cost)) throw new FixedAssetException("assets.cip_combine_book_amount_invalid");
                sourceRows.Add((sourceRequest.AssetId, sourceNumber, total, bookRows));
            }
            var totalAmount = request.Sources.Sum(source => source.Amount);
            var targetId = Guid.CreateVersion7(); var targetNumber = await sequences.AllocateAsync(context, new SequenceDefinition("fixed-assets.asset-number", tenant, request.LegalEntityId, null, null, null, "FA-", 8, 1), token);
            await using (var target = new NpgsqlCommand("insert into business_assets.fixed_asset(tenant_id,id,legal_entity_id,category_id,asset_number,name,lifecycle_state,asset_nature,tracking_granularity,source_origin_type,capitalization_date,placed_in_service_date,quantity,created_by,created_at,row_version) values($1,$2,$3,$4,$5,$6,'Capitalized',$7,$8,'CIPCombine',$9,$10,1,$11,now(),1)", context.Connection, context.Transaction)) { target.Parameters.AddWithValue(tenant.Value); target.Parameters.AddWithValue(targetId); target.Parameters.AddWithValue(request.LegalEntityId); target.Parameters.AddWithValue(request.TargetCategoryId); target.Parameters.AddWithValue(targetNumber); target.Parameters.AddWithValue(request.Name.Trim()); target.Parameters.AddWithValue(nature); target.Parameters.AddWithValue(tracking); target.Parameters.AddWithValue(request.CapitalizationDate); target.Parameters.AddWithValue((object?)request.PlacedInServiceDate ?? DBNull.Value); target.Parameters.AddWithValue(actor); await target.ExecuteNonQueryAsync(token); }

            var definitionIds = sourceRows.SelectMany(source => source.Books.Select(book => book.DefinitionId)).Distinct().ToArray();
            foreach (var definitionId in definitionIds)
            {
                var targetAmount = 0m; var posts = false; var currency = string.Empty;
                foreach (var source in sourceRows) { var book = source.Books.FirstOrDefault(row => row.DefinitionId == definitionId); if (book.BookId == Guid.Empty) continue; var amount = request.Sources.Single(item => item.AssetId == source.Id).Amount; targetAmount += amount; posts |= book.Posts; currency = book.Currency; }
                var targetBookId = Guid.CreateVersion7(); var targetResidual = Math.Min(targetAmount, residualDefault);
                await using (var targetBook = new NpgsqlCommand("insert into business_assets.asset_book(tenant_id,id,asset_id,book_definition_id,original_cost,residual_value,useful_life_months,depreciation_method,placed_in_service_date,created_at,row_version) values($1,$2,$3,$4,$5,$6,$7,$8,$9,now(),1)", context.Connection, context.Transaction)) { targetBook.Parameters.AddWithValue(tenant.Value); targetBook.Parameters.AddWithValue(targetBookId); targetBook.Parameters.AddWithValue(targetId); targetBook.Parameters.AddWithValue(definitionId); targetBook.Parameters.AddWithValue(targetAmount); targetBook.Parameters.AddWithValue(targetResidual); targetBook.Parameters.AddWithValue((object?)usefulLife ?? DBNull.Value); targetBook.Parameters.AddWithValue(method); targetBook.Parameters.AddWithValue((object?)request.PlacedInServiceDate ?? DBNull.Value); await targetBook.ExecuteNonQueryAsync(token); }
                Guid? targetFact = null; if (posts) { var payload = new FixedAssetAcquisitionPostedPayload(targetAmount, currency, $"{targetNumber}/{targetBookId:D}", categoryCode); var envelope = new AccountingFactEnvelope<IAccountingFactPayload>(StableGuid($"cip-combine:{key}:target:{targetBookId:D}"), tenant, new(request.LegalEntityId), AccountingFactTypes.FixedAssetAcquisitionPosted, 1, "fixed-assets", $"cip-combine:{key}:target:{targetBookId:D}", $"fixed-assets:cip-combine:{key}:target:{targetBookId:D}", request.CapitalizationDate, request.CapitalizationDate, payload, Guid.CreateVersion7(), actor); targetFact = (await ledger.PostFixedAssetAcquisitionAsync(context, envelope, payload, token)).FactId; }
                await using var targetTransaction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,source_reference,idempotency_key,ledger_fact_reference,created_by,created_at) values($1,$2,$3,'CapitalAddition',$4,$5,$6,$7,$8,$9,now())", context.Connection, context.Transaction); targetTransaction.Parameters.AddWithValue(tenant.Value); targetTransaction.Parameters.AddWithValue(Guid.CreateVersion7()); targetTransaction.Parameters.AddWithValue(targetBookId); targetTransaction.Parameters.AddWithValue(request.CapitalizationDate); targetTransaction.Parameters.AddWithValue(targetAmount); targetTransaction.Parameters.AddWithValue($"cip-combine:{key}"); targetTransaction.Parameters.AddWithValue($"cip-combine:{key}:target:{targetBookId:D}"); targetTransaction.Parameters.AddWithValue((object?)targetFact?.ToString("D") ?? DBNull.Value); targetTransaction.Parameters.AddWithValue(actor); await targetTransaction.ExecuteNonQueryAsync(token);
                foreach (var source in sourceRows)
                {
                    var book = source.Books.FirstOrDefault(row => row.DefinitionId == definitionId); if (book.BookId == Guid.Empty) continue; var amount = request.Sources.Single(item => item.AssetId == source.Id).Amount; Guid? sourceFact = null;
                    if (book.Posts) { var payload = new FixedAssetCostAdjustmentPostedPayload(amount, book.Currency, $"{source.Number}/{book.BookId:D}", source.Number, $"CIP combined into {targetNumber}"); var envelope = new AccountingFactEnvelope<IAccountingFactPayload>(StableGuid($"cip-combine:{key}:source:{book.BookId:D}"), tenant, new(request.LegalEntityId), AccountingFactTypes.FixedAssetCostReduced, 1, "fixed-assets", $"cip-combine:{key}:source:{book.BookId:D}", $"fixed-assets:cip-combine:{key}:source:{book.BookId:D}", request.CapitalizationDate, request.CapitalizationDate, payload, Guid.CreateVersion7(), actor); sourceFact = (await ledger.PostFixedAssetCostReducedAsync(context, envelope, payload, token)).FactId; }
                    var residual = Math.Max(0m, book.Residual - Math.Min(book.Residual, book.Cost == 0m ? 0m : amount * book.Residual / book.Cost));
                    await using var update = new NpgsqlCommand("update business_assets.asset_book set original_cost=original_cost-$1,residual_value=$2,row_version=row_version+1 where tenant_id=$3 and id=$4", context.Connection, context.Transaction); update.Parameters.AddWithValue(amount); update.Parameters.AddWithValue(residual); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(book.BookId); await update.ExecuteNonQueryAsync(token);
                    await using var transaction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,source_reference,idempotency_key,ledger_fact_reference,created_by,created_at) values($1,$2,$3,'CostReduction',$4,$5,$6,$7,$8,$9,now())", context.Connection, context.Transaction); transaction.Parameters.AddWithValue(tenant.Value); transaction.Parameters.AddWithValue(Guid.CreateVersion7()); transaction.Parameters.AddWithValue(book.BookId); transaction.Parameters.AddWithValue(request.CapitalizationDate); transaction.Parameters.AddWithValue(amount); transaction.Parameters.AddWithValue($"cip-combine:{key}:target:{targetId:D}"); transaction.Parameters.AddWithValue($"cip-combine:{key}:source:{book.BookId:D}"); transaction.Parameters.AddWithValue((object?)sourceFact?.ToString("D") ?? DBNull.Value); transaction.Parameters.AddWithValue(actor); await transaction.ExecuteNonQueryAsync(token);
                }
            }
            foreach (var source in sourceRows) { var amount = request.Sources.Single(item => item.AssetId == source.Id).Amount; var remaining = source.Total - amount; await using var lifecycle = new NpgsqlCommand("update business_assets.fixed_asset set lifecycle_state=case when $1 <= 0.000001 then 'Capitalized' else 'UnderConstruction' end,row_version=row_version+1,updated_by=$2,updated_at=now() where tenant_id=$3 and id=$4", context.Connection, context.Transaction); lifecycle.Parameters.AddWithValue(remaining); lifecycle.Parameters.AddWithValue(actor); lifecycle.Parameters.AddWithValue(tenant.Value); lifecycle.Parameters.AddWithValue(source.Id); await lifecycle.ExecuteNonQueryAsync(token); }
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "FixedAssetCapitalizedV1", JsonSerializer.Serialize(new FixedAssetCapitalizedV1(targetId, request.LegalEntityId, request.CapitalizationDate, request.PlacedInServiceDate, totalAmount, DateTimeOffset.UtcNow)), tenant, DateTimeOffset.UtcNow, targetId, targetId, "fixed-assets"));
            var result = new CombineCipView(targetId, targetNumber, totalAmount, sourceRows.Select(source => source.Id).ToArray(), "Completed"); await using var idempotency = new NpgsqlCommand("insert into business_assets.asset_idempotency_key(tenant_id,operation,idempotency_key,request_hash,response,created_at) values($1,$2,$3,$4,$5::jsonb,now())", context.Connection, context.Transaction); idempotency.Parameters.AddWithValue(tenant.Value); idempotency.Parameters.AddWithValue(operation); idempotency.Parameters.AddWithValue(key); idempotency.Parameters.AddWithValue(requestHash); idempotency.Parameters.AddWithValue(JsonSerializer.Serialize(result)); await idempotency.ExecuteNonQueryAsync(token); return result;
        }, ct);
    }

    public async Task<OpeningImportView> CreateOpeningImportAsync(TenantId tenant, Guid actor, CreateOpeningImportRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(request.SourceSystem) || string.IsNullOrWhiteSpace(request.BatchReference) || request.OpeningDate == default || request.Lines.Count == 0)
            throw new FixedAssetException("assets.opening_import_required");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var importId = Guid.CreateVersion7();
            await using var import = new NpgsqlCommand("insert into business_assets.asset_opening_import(tenant_id,id,legal_entity_id,source_system,batch_reference,opening_date,status,created_by,created_at) values($1,$2,$3,$4,$5,$6,'Draft',$7,now())", context.Connection, context.Transaction);
            import.Parameters.AddWithValue(tenant.Value); import.Parameters.AddWithValue(importId); import.Parameters.AddWithValue(request.LegalEntityId); import.Parameters.AddWithValue(request.SourceSystem.Trim()); import.Parameters.AddWithValue(request.BatchReference.Trim()); import.Parameters.AddWithValue(request.OpeningDate); import.Parameters.AddWithValue(actor);
            var createdAt = DateTimeOffset.UtcNow;
            try { await import.ExecuteNonQueryAsync(token); }
            catch (PostgresException ex) when (ex.SqlState == "23505") { throw new FixedAssetException("assets.opening_import_exists"); }
            var validCount = 0;
            foreach (var line in request.Lines)
            {
                var lineId = Guid.CreateVersion7();
                var error = ValidateOpeningLine(line);
                if (error is null) validCount++;
                await using var insert = new NpgsqlCommand("insert into business_assets.asset_opening_import_line(tenant_id,id,import_id,legacy_asset_number,category_id,asset_name,book_definition_id,acquisition_date,placed_in_service_date,original_cost,accumulated_depreciation,net_book_value,residual_value,useful_life_months,depreciation_method,outcome,error_code) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17)", context.Connection, context.Transaction);
                insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(lineId); insert.Parameters.AddWithValue(importId); insert.Parameters.AddWithValue(line.LegacyAssetNumber.Trim()); insert.Parameters.AddWithValue(line.CategoryId); insert.Parameters.AddWithValue(line.Name.Trim()); insert.Parameters.AddWithValue(line.BookDefinitionId); insert.Parameters.AddWithValue((object?)line.AcquisitionDate ?? DBNull.Value); insert.Parameters.AddWithValue(line.PlacedInServiceDate); insert.Parameters.AddWithValue(line.OriginalCost); insert.Parameters.AddWithValue(line.AccumulatedDepreciation); insert.Parameters.AddWithValue(line.NetBookValue); insert.Parameters.AddWithValue(line.ResidualValue); insert.Parameters.AddWithValue((object?)line.UsefulLifeMonths ?? DBNull.Value); insert.Parameters.AddWithValue(line.DepreciationMethod.Trim()); insert.Parameters.AddWithValue(error is null ? "Valid" : "Rejected"); insert.Parameters.AddWithValue((object?)error ?? DBNull.Value); await insert.ExecuteNonQueryAsync(token);
            }
            var status = validCount == request.Lines.Count ? "Validated" : "Draft";
            await using var update = new NpgsqlCommand("update business_assets.asset_opening_import set status=$1 where tenant_id=$2 and id=$3", context.Connection, context.Transaction); update.Parameters.AddWithValue(status); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(importId); await update.ExecuteNonQueryAsync(token);
            return new OpeningImportView(importId, request.LegalEntityId, request.SourceSystem.Trim(), request.BatchReference.Trim(), request.OpeningDate, status, request.Lines.Count, validCount, createdAt);
        }, ct);
    }

    public async Task<OpeningImportView> PostOpeningImportAsync(TenantId tenant, Guid actor, Guid importId, CancellationToken ct)
    {
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var import = new NpgsqlCommand("select legal_entity_id,source_system,batch_reference,opening_date,status,created_at from business_assets.asset_opening_import where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction); import.Parameters.AddWithValue(tenant.Value); import.Parameters.AddWithValue(importId);
            await using var importReader = await import.ExecuteReaderAsync(token);
            if (!await importReader.ReadAsync(token)) throw new FixedAssetException("assets.opening_import_not_found");
            var legalEntityId = importReader.GetGuid(0); var sourceSystem = importReader.GetString(1); var batchReference = importReader.GetString(2); var openingDate = importReader.GetFieldValue<DateOnly>(3); var status = importReader.GetString(4); var createdAt = importReader.GetFieldValue<DateTimeOffset>(5); await importReader.CloseAsync();
            if (status == "Posted") return new OpeningImportView(importId, legalEntityId, sourceSystem, batchReference, openingDate, status, await OpeningLineCountAsync(context, tenant, importId, token), await OpeningValidLineCountAsync(context, tenant, importId, token), createdAt);
            if (status != "Validated") throw new FixedAssetException("assets.opening_import_not_validated");
            await using var lines = new NpgsqlCommand("select l.id,l.legacy_asset_number,l.category_id,l.asset_name,l.book_definition_id,l.acquisition_date,l.placed_in_service_date,l.original_cost,l.accumulated_depreciation,l.residual_value,l.useful_life_months,l.depreciation_method,c.asset_nature,c.tracking_granularity,bd.legal_entity_id,bd.posts_to_ledger,bd.currency_code from business_assets.asset_opening_import_line l join business_assets.asset_category c on c.tenant_id=l.tenant_id and c.id=l.category_id join business_assets.asset_book_definition bd on bd.tenant_id=l.tenant_id and bd.id=l.book_definition_id where l.tenant_id=$1 and l.import_id=$2 and l.outcome='Valid' order by l.id", context.Connection, context.Transaction); lines.Parameters.AddWithValue(tenant.Value); lines.Parameters.AddWithValue(importId);
            await using var lineReader = await lines.ExecuteReaderAsync(token); var rows = new List<(Guid Id, string Legacy, Guid CategoryId, string Name, Guid BookDefinitionId, DateOnly? AcquisitionDate, DateOnly PlacedInServiceDate, decimal Cost, decimal Accumulated, decimal Residual, int? UsefulLife, string Method, string Nature, string Tracking, Guid BookEntity, bool PostsToLedger, string Currency)>(); while (await lineReader.ReadAsync(token)) rows.Add((lineReader.GetGuid(0), lineReader.GetString(1), lineReader.GetGuid(2), lineReader.GetString(3), lineReader.GetGuid(4), lineReader.IsDBNull(5) ? null : lineReader.GetFieldValue<DateOnly>(5), lineReader.GetFieldValue<DateOnly>(6), lineReader.GetDecimal(7), lineReader.GetDecimal(8), lineReader.GetDecimal(9), lineReader.IsDBNull(10) ? null : lineReader.GetInt32(10), lineReader.GetString(11), lineReader.GetString(12), lineReader.GetString(13), lineReader.GetGuid(14), lineReader.GetBoolean(15), lineReader.GetString(16))); await lineReader.CloseAsync();
            if (rows.Count != await OpeningPendingValidLineCountAsync(context, tenant, importId, token)) throw new FixedAssetException("assets.opening_import_reference_invalid");
            foreach (var row in rows)
            {
                if (row.BookEntity != legalEntityId) throw new FixedAssetException("assets.opening_import_book_entity_mismatch");
                var assetId = Guid.CreateVersion7(); var assetNumber = await sequences.AllocateAsync(context, new SequenceDefinition("fixed-assets.asset-number", tenant, legalEntityId, null, null, null, "FA-", 8, 1), token);
                await using var asset = new NpgsqlCommand("insert into business_assets.fixed_asset(tenant_id,id,legal_entity_id,category_id,asset_number,name,asset_tag,lifecycle_state,asset_nature,tracking_granularity,source_origin_type,acquisition_date,capitalization_date,placed_in_service_date,quantity,created_by,created_at,row_version) values($1,$2,$3,$4,$5,$6,$7,'Operational',$8,$9,'OpeningMigration',$10,$10,$11,1,$12,now(),1)", context.Connection, context.Transaction); asset.Parameters.AddWithValue(tenant.Value); asset.Parameters.AddWithValue(assetId); asset.Parameters.AddWithValue(legalEntityId); asset.Parameters.AddWithValue(row.CategoryId); asset.Parameters.AddWithValue(assetNumber); asset.Parameters.AddWithValue(row.Name.Trim()); asset.Parameters.AddWithValue(row.Legacy.Trim()); asset.Parameters.AddWithValue(row.Nature); asset.Parameters.AddWithValue(row.Tracking); asset.Parameters.AddWithValue(row.AcquisitionDate ?? openingDate); asset.Parameters.AddWithValue(row.PlacedInServiceDate); asset.Parameters.AddWithValue(actor); await asset.ExecuteNonQueryAsync(token);
                var bookId = Guid.CreateVersion7(); await using var book = new NpgsqlCommand("insert into business_assets.asset_book(tenant_id,id,asset_id,book_definition_id,original_cost,residual_value,useful_life_months,depreciation_method,placed_in_service_date,created_at,row_version) values($1,$2,$3,$4,$5,$6,$7,$8,$9,now(),1)", context.Connection, context.Transaction); book.Parameters.AddWithValue(tenant.Value); book.Parameters.AddWithValue(bookId); book.Parameters.AddWithValue(assetId); book.Parameters.AddWithValue(row.BookDefinitionId); book.Parameters.AddWithValue(row.Cost); book.Parameters.AddWithValue(row.Residual); book.Parameters.AddWithValue((object?)row.UsefulLife ?? DBNull.Value); book.Parameters.AddWithValue(row.Method); book.Parameters.AddWithValue(row.PlacedInServiceDate); await book.ExecuteNonQueryAsync(token);
                Guid? ledgerFact = null;
                if (row.PostsToLedger)
                {
                    if (row.Cost < row.Accumulated) throw new FixedAssetException("assets.opening_book_value_invalid");
                    var costAccount = await ledgerBalances.GetActiveControlBalanceAsync(tenant, new LegalEntityId(legalEntityId), "FixedAssetCost", openingDate, token);
                    var accumulatedAccount = await ledgerBalances.GetActiveControlBalanceAsync(tenant, new LegalEntityId(legalEntityId), "FixedAssetAccumulatedDepreciation", openingDate, token);
                    var offsetAccount = await ledgerBalances.GetActiveControlBalanceAsync(tenant, new LegalEntityId(legalEntityId), "FixedAssetCapitalizationOffset", openingDate, token);
                    var baseCurrency = costAccount.BaseCurrencyCode.Trim().ToUpperInvariant();
                    var currency = row.Currency.Trim().ToUpperInvariant();
                    if (currency != baseCurrency) throw new FixedAssetException("assets.opening_currency_mismatch");
                    var linesForLedger = new List<LedgerPostingLinePayload>();
                    if (row.Cost > 0m) linesForLedger.Add(new(costAccount.AccountId, row.Cost, 0m, currency, row.Cost, 0m, row.Cost, 0m, 1m, [], $"Opening fixed asset {row.Legacy}", row.Legacy));
                    if (row.Accumulated > 0m) linesForLedger.Add(new(accumulatedAccount.AccountId, 0m, row.Accumulated, currency, 0m, row.Accumulated, 0m, row.Accumulated, 1m, [], $"Opening accumulated depreciation {row.Legacy}", row.Legacy));
                    var net = row.Cost - row.Accumulated;
                    if (net > 0m) linesForLedger.Add(new(offsetAccount.AccountId, 0m, net, currency, 0m, net, 0m, net, 1m, [], $"Opening fixed asset offset {row.Legacy}", row.Legacy));
                    var factId = DeterministicGuid(row.Id, bookId);
                    var payload = new ExplicitLedgerPostingPayload(await OpeningJournalAsync(context, tenant, legalEntityId, token), $"Opening fixed asset {row.Legacy}", baseCurrency, linesForLedger, row.Legacy, $"{importId:D}:{row.Id:D}");
                    var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new(legalEntityId), AccountingFactTypes.OpeningBalancePosted, 1, "fixed-assets", $"opening:{importId:D}:line:{row.Id:D}:book:{bookId:D}", $"opening:{importId:D}:line:{row.Id:D}:book:{bookId:D}", openingDate, openingDate, payload, Guid.CreateVersion7(), actor);
                    ledgerFact = (await posting.PostExplicitAsync(context, fact, payload, token)).FactId;
                }
                await using var transaction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,accumulated_depreciation,source_reference,idempotency_key,ledger_fact_reference,created_by,created_at) values($1,$2,$3,'Opening',$4,$5,$6,$7,$8,$9,$10,now())", context.Connection, context.Transaction); transaction.Parameters.AddWithValue(tenant.Value); transaction.Parameters.AddWithValue(Guid.CreateVersion7()); transaction.Parameters.AddWithValue(bookId); transaction.Parameters.AddWithValue(openingDate); transaction.Parameters.AddWithValue(row.Cost); transaction.Parameters.AddWithValue(row.Accumulated); transaction.Parameters.AddWithValue($"opening:{importId:D}:line:{row.Id:D}"); transaction.Parameters.AddWithValue($"opening:{importId:D}:line:{row.Id:D}:book:{bookId:D}"); transaction.Parameters.AddWithValue((object?)ledgerFact?.ToString("D") ?? DBNull.Value); transaction.Parameters.AddWithValue(actor); await transaction.ExecuteNonQueryAsync(token);
                await using var updateLine = new NpgsqlCommand("update business_assets.asset_opening_import_line set asset_id=$1,outcome='Created',error_code=null where tenant_id=$2 and id=$3", context.Connection, context.Transaction); updateLine.Parameters.AddWithValue(assetId); updateLine.Parameters.AddWithValue(tenant.Value); updateLine.Parameters.AddWithValue(row.Id); await updateLine.ExecuteNonQueryAsync(token);
            }
            await using var updateImport = new NpgsqlCommand("update business_assets.asset_opening_import set status='Posted' where tenant_id=$1 and id=$2", context.Connection, context.Transaction); updateImport.Parameters.AddWithValue(tenant.Value); updateImport.Parameters.AddWithValue(importId); await updateImport.ExecuteNonQueryAsync(token);
            return new OpeningImportView(importId, legalEntityId, sourceSystem, batchReference, openingDate, "Posted", rows.Count, rows.Count, createdAt);
        }, ct);
    }

    public async Task<AssetBookEstimateChangeView> CreateAssetBookEstimateChangeAsync(TenantId tenant, Guid actor, Guid assetId, Guid bookId, CreateAssetBookEstimateChangeRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty || request.EffectiveFrom == default || request.UsefulLifeMonths is null && request.ResidualValue is null || request.UsefulLifeMonths is <= 0 || request.ResidualValue is < 0m || string.IsNullOrWhiteSpace(request.Reason))
            throw new FixedAssetException("assets.estimate_change_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? $"estimate:{bookId:D}:{request.EffectiveFrom:yyyyMMdd}" : request.IdempotencyKey.Trim();
            await using var existing = new NpgsqlCommand("select b.asset_id,e.effective_from,e.useful_life_months,e.residual_value,e.reason,e.idempotency_key,e.created_at from business_assets.asset_book_estimate_change e join business_assets.asset_book b on b.tenant_id=e.tenant_id and b.id=e.asset_book_id where e.tenant_id=$1 and e.idempotency_key=$2", context.Connection, context.Transaction);
            existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(key);
            await using var existingReader = await existing.ExecuteReaderAsync(token);
            if (await existingReader.ReadAsync(token))
            {
                var existingAssetId = existingReader.GetGuid(0); var existingDate = existingReader.GetFieldValue<DateOnly>(1); int? existingLife = existingReader.IsDBNull(2) ? null : existingReader.GetInt32(2); decimal? existingResidual = existingReader.IsDBNull(3) ? null : existingReader.GetDecimal(3); var existingReason = existingReader.GetString(4); var existingKey = existingReader.GetString(5); var existingCreated = existingReader.GetFieldValue<DateTimeOffset>(6);
                await existingReader.CloseAsync();
                if (existingAssetId != assetId || existingDate != request.EffectiveFrom || existingLife != request.UsefulLifeMonths || existingResidual != request.ResidualValue || existingReason != request.Reason.Trim()) throw new FixedAssetException("assets.estimate_change_idempotency_conflict");
                return new AssetBookEstimateChangeView(existingAssetId, bookId, existingDate, existingLife, existingResidual, existingReason, existingKey, existingCreated, true);
            }
            await existingReader.CloseAsync();
            await using var book = new NpgsqlCommand("select b.original_cost,b.residual_value,b.useful_life_months,b.depreciation_method,b.placed_in_service_date,a.legal_entity_id from business_assets.asset_book b join business_assets.fixed_asset a on a.tenant_id=b.tenant_id and a.id=b.asset_id where b.tenant_id=$1 and b.id=$2 and b.asset_id=$3 for update of b", context.Connection, context.Transaction);
            book.Parameters.AddWithValue(tenant.Value); book.Parameters.AddWithValue(bookId); book.Parameters.AddWithValue(assetId);
            await using var bookReader = await book.ExecuteReaderAsync(token);
            if (!await bookReader.ReadAsync(token)) throw new FixedAssetException("assets.book_not_found");
            var originalCost = bookReader.GetDecimal(0); var currentResidual = bookReader.GetDecimal(1); var currentLife = bookReader.GetInt32(2); var method = bookReader.GetString(3); var placed = bookReader.GetFieldValue<DateOnly>(4); var entity = bookReader.GetGuid(5); await bookReader.CloseAsync();
            if (entity != request.LegalEntityId || method is not "StraightLine") throw new FixedAssetException("assets.estimate_change_not_allowed");
            if (request.EffectiveFrom < placed || request.ResidualValue is { } residual && residual > originalCost) throw new FixedAssetException("assets.estimate_change_invalid");
            await using var posted = new NpgsqlCommand("select max(transaction_date) from business_assets.asset_book_transaction where tenant_id=$1 and asset_book_id=$2 and transaction_type in ('Depreciation','Reversal')", context.Connection, context.Transaction); posted.Parameters.AddWithValue(tenant.Value); posted.Parameters.AddWithValue(bookId);
            var lastPosted = await posted.ExecuteScalarAsync(token) as DateOnly?;
            if (lastPosted is { } date && request.EffectiveFrom <= date) throw new FixedAssetException("assets.estimate_change_historical");
            var id = Guid.CreateVersion7(); var createdAt = DateTimeOffset.UtcNow;
            await using var insert = new NpgsqlCommand("insert into business_assets.asset_book_estimate_change(tenant_id,id,asset_book_id,effective_from,useful_life_months,residual_value,reason,idempotency_key,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(bookId); insert.Parameters.AddWithValue(request.EffectiveFrom); insert.Parameters.AddWithValue((object?)request.UsefulLifeMonths ?? DBNull.Value); insert.Parameters.AddWithValue((object?)request.ResidualValue ?? DBNull.Value); insert.Parameters.AddWithValue(request.Reason.Trim()); insert.Parameters.AddWithValue(key); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(createdAt);
            try { await insert.ExecuteNonQueryAsync(token); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new FixedAssetException("assets.estimate_change_exists"); }
            return new AssetBookEstimateChangeView(assetId, bookId, request.EffectiveFrom, request.UsefulLifeMonths, request.ResidualValue, request.Reason.Trim(), key, createdAt, false);
        }, ct);
    }

    public async Task<AssetImpairmentView> CreateAssetImpairmentAsync(TenantId tenant, Guid actor, Guid assetId, CreateAssetImpairmentRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty || request.BookId == Guid.Empty || request.Amount <= 0m || request.EffectiveDate == default || request.ApprovalRequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason))
            throw new FixedAssetException("assets.impairment_invalid");
        await ValidateApprovalBindingAsync(tenant, actor, assetId, request.LegalEntityId, request.BookId, request.ApprovalRequestId,
            "asset-impairment", JsonSerializer.SerializeToElement(new { Action = "Impairment", AssetId = assetId, BookId = request.BookId, Amount = request.Amount, EffectiveDate = request.EffectiveDate, Reason = request.Reason.Trim() }), ct);
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? $"impairment:{assetId:D}:{request.BookId:D}:{request.EffectiveDate:yyyyMMdd}" : request.IdempotencyKey.Trim();
            await using var existing = new NpgsqlCommand("select asset_id,amount,effective_date,reason,approval_request_id,status,ledger_fact_reference,created_at from business_assets.asset_impairment where tenant_id=$1 and asset_book_id=$2 and idempotency_key=$3", context.Connection, context.Transaction);
            existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(request.BookId); existing.Parameters.AddWithValue(key);
            await using var existingReader = await existing.ExecuteReaderAsync(token);
            if (await existingReader.ReadAsync(token))
            {
                var existingAsset = existingReader.GetGuid(0); var existingAmount = existingReader.GetDecimal(1); var existingDate = existingReader.GetFieldValue<DateOnly>(2); var existingReason = existingReader.GetString(3); var existingApproval = existingReader.GetGuid(4); var existingStatus = existingReader.GetString(5); var existingFact = existingReader.IsDBNull(6) ? null : existingReader.GetString(6); var existingCreated = existingReader.GetFieldValue<DateTimeOffset>(7);
                if (existingAsset != assetId || existingAmount != request.Amount || existingDate != request.EffectiveDate || existingReason != request.Reason.Trim() || existingApproval != request.ApprovalRequestId) throw new FixedAssetException("assets.impairment_idempotency_conflict");
                return new AssetImpairmentView(existingAsset, request.BookId, existingAmount, existingDate, existingReason, existingApproval, existingStatus, existingFact, existingCreated, true);
            }
            await existingReader.CloseAsync();
            await using var lockBook = new NpgsqlCommand("select 1 from business_assets.asset_book b join business_assets.fixed_asset a on a.tenant_id=b.tenant_id and a.id=b.asset_id where b.tenant_id=$1 and b.id=$2 and b.asset_id=$3 and a.legal_entity_id=$4 for update of b", context.Connection, context.Transaction);
            lockBook.Parameters.AddWithValue(tenant.Value); lockBook.Parameters.AddWithValue(request.BookId); lockBook.Parameters.AddWithValue(assetId); lockBook.Parameters.AddWithValue(request.LegalEntityId);
            if (await lockBook.ExecuteScalarAsync(token) is null) throw new FixedAssetException("assets.book_not_found");
            await using var book = new NpgsqlCommand("select b.original_cost,bd.posts_to_ledger,bd.currency_code,a.legal_entity_id,a.asset_number,a.lifecycle_state,coalesce(sum(case when t.transaction_type='Opening' then t.accumulated_depreciation when t.transaction_type='Depreciation' then t.amount when t.transaction_type='Reversal' then -t.amount when t.transaction_type='Disposal' then -t.accumulated_depreciation when t.transaction_type='Reinstatement' then t.accumulated_depreciation else 0 end),0),coalesce(sum(case when t.transaction_type='Impairment' then coalesce(nullif(t.accumulated_impairment,0),t.amount) when t.transaction_type='Disposal' then -t.accumulated_impairment when t.transaction_type='Reinstatement' then t.accumulated_impairment else 0 end),0),coalesce(sum(case when t.transaction_type='Revaluation' then t.revaluation_effect when t.transaction_type='Disposal' then -t.revaluation_effect when t.transaction_type='Reinstatement' then t.revaluation_effect else 0 end),0) from business_assets.asset_book b join business_assets.asset_book_definition bd on bd.tenant_id=b.tenant_id and bd.id=b.book_definition_id join business_assets.fixed_asset a on a.tenant_id=b.tenant_id and a.id=b.asset_id left join business_assets.asset_book_transaction t on t.tenant_id=b.tenant_id and t.asset_book_id=b.id where b.tenant_id=$1 and b.id=$2 and b.asset_id=$3 and a.legal_entity_id=$4 group by b.original_cost,bd.posts_to_ledger,bd.currency_code,a.legal_entity_id,a.asset_number,a.lifecycle_state", context.Connection, context.Transaction);
            book.Parameters.AddWithValue(tenant.Value); book.Parameters.AddWithValue(request.BookId); book.Parameters.AddWithValue(assetId); book.Parameters.AddWithValue(request.LegalEntityId);
            await using var bookReader = await book.ExecuteReaderAsync(token);
            if (!await bookReader.ReadAsync(token)) throw new FixedAssetException("assets.book_not_found");
            var gross = bookReader.GetDecimal(0); var postsToLedger = bookReader.GetBoolean(1); var currency = bookReader.GetString(2).Trim(); var entity = bookReader.GetGuid(3); var assetNumber = bookReader.GetString(4); var lifecycle = bookReader.GetString(5); var accumulated = bookReader.GetDecimal(6); var impairment = bookReader.GetDecimal(7); var revaluation = bookReader.GetDecimal(8); await bookReader.CloseAsync();
            if (entity != request.LegalEntityId || lifecycle is "Disposed" or "Draft" || request.Amount > Math.Max(0m, gross - accumulated - impairment + revaluation)) throw new FixedAssetException("assets.impairment_exceeds_carrying_value");
            Guid? ledgerFact = null;
            if (postsToLedger)
            {
                var factId = DeterministicGuid(request.BookId, StableGuid($"impairment:{assetId:D}:{key}"));
                var payload = new FixedAssetImpairmentPostedPayload(request.Amount, currency, $"{assetNumber}/{request.BookId:D}", assetNumber, request.Reason.Trim());
                var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new(entity), AccountingFactTypes.FixedAssetImpairmentPosted, 1, "fixed-assets", $"impairment:{assetId:D}:book:{request.BookId:D}:{key}", $"fixed-assets:impairment:{assetId:D}:{request.BookId:D}:{key}", request.EffectiveDate, request.EffectiveDate, payload, Guid.CreateVersion7(), actor);
                ledgerFact = (await ledger.PostFixedAssetImpairmentAsync(context, fact, payload, token)).FactId;
            }
            var id = Guid.CreateVersion7();
            await using var insert = new NpgsqlCommand("insert into business_assets.asset_impairment(tenant_id,id,asset_id,asset_book_id,legal_entity_id,amount,effective_date,reason,approval_request_id,evidence_file_ref,status,idempotency_key,ledger_fact_reference,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,'Posted',$11,$12,$13,now())", context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(assetId); insert.Parameters.AddWithValue(request.BookId); insert.Parameters.AddWithValue(request.LegalEntityId); insert.Parameters.AddWithValue(request.Amount); insert.Parameters.AddWithValue(request.EffectiveDate); insert.Parameters.AddWithValue(request.Reason.Trim()); insert.Parameters.AddWithValue(request.ApprovalRequestId); insert.Parameters.AddWithValue((object?)request.EvidenceFileRef ?? DBNull.Value); insert.Parameters.AddWithValue(key); insert.Parameters.AddWithValue((object?)ledgerFact?.ToString("D") ?? DBNull.Value); insert.Parameters.AddWithValue(actor);
            try { await insert.ExecuteNonQueryAsync(token); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new FixedAssetException("assets.impairment_exists"); }
            await using var transaction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,accumulated_impairment,source_reference,idempotency_key,ledger_fact_reference,created_by,created_at) values($1,$2,$3,'Impairment',$4,$5,$5,$6,$7,$8,$9,now())", context.Connection, context.Transaction);
            transaction.Parameters.AddWithValue(tenant.Value); transaction.Parameters.AddWithValue(Guid.CreateVersion7()); transaction.Parameters.AddWithValue(request.BookId); transaction.Parameters.AddWithValue(request.EffectiveDate); transaction.Parameters.AddWithValue(request.Amount); transaction.Parameters.AddWithValue($"impairment:{id:D}"); transaction.Parameters.AddWithValue($"{key}:book-transaction"); transaction.Parameters.AddWithValue((object?)ledgerFact?.ToString("D") ?? DBNull.Value); transaction.Parameters.AddWithValue(actor); await transaction.ExecuteNonQueryAsync(token);
            return new AssetImpairmentView(assetId, request.BookId, request.Amount, request.EffectiveDate, request.Reason.Trim(), request.ApprovalRequestId, "Posted", ledgerFact?.ToString("D"), DateTimeOffset.UtcNow, false);
        }, ct);
    }

    public async Task<PlatformApprovalRequestResult> RequestAssetImpairmentApprovalAsync(TenantId tenant, Guid actor, Guid membershipId, Guid assetId, RequestAssetImpairmentApprovalRequest request, CancellationToken ct)
    {
        if (approvals is null) throw new FixedAssetException("assets.approval_unavailable");
        if (request.LegalEntityId == Guid.Empty || request.BookId == Guid.Empty || request.Amount <= 0m || request.EffectiveDate == default || string.IsNullOrWhiteSpace(request.Reason)) throw new FixedAssetException("assets.impairment_invalid");
        var scope = await ReadApprovalScopeAsync(tenant, assetId, request.BookId, request.LegalEntityId, ct);
        var evidence = JsonSerializer.Serialize(new { Action = "Impairment", AssetId = assetId, BookId = request.BookId, Amount = request.Amount, EffectiveDate = request.EffectiveDate, Reason = request.Reason.Trim() });
        return await approvals.RequestAsync(tenant, actor, membershipId, request.LegalEntityId, "fixed-assets", "asset-impairment", assetId, scope.RowVersion, request.PolicyVersionId, evidence, requesterPermission: "assets.impair", cancellationToken: ct);
    }

    public async Task<PlatformApprovalRequestResult> RequestAssetRevaluationApprovalAsync(TenantId tenant, Guid actor, Guid membershipId, Guid assetId, RequestAssetRevaluationApprovalRequest request, CancellationToken ct)
    {
        if (approvals is null) throw new FixedAssetException("assets.approval_unavailable");
        if (request.LegalEntityId == Guid.Empty || request.BookId == Guid.Empty || request.ProposedGrossCost < 0m || request.ProposedResidualValue < 0m || request.ProposedResidualValue > request.ProposedGrossCost || request.EffectiveDate == default || string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.ProviderCode) || string.IsNullOrWhiteSpace(request.ProviderVersion)) throw new FixedAssetException("assets.revaluation_invalid");
        var scope = await ReadApprovalScopeAsync(tenant, assetId, request.BookId, request.LegalEntityId, ct);
        var evidence = JsonSerializer.Serialize(new { Action = "Revaluation", AssetId = assetId, BookId = request.BookId, ProposedGrossCost = request.ProposedGrossCost, ProposedResidualValue = request.ProposedResidualValue, EffectiveDate = request.EffectiveDate, Reason = request.Reason.Trim(), ProviderCode = request.ProviderCode.Trim(), ProviderVersion = request.ProviderVersion.Trim() });
        return await approvals.RequestAsync(tenant, actor, membershipId, request.LegalEntityId, "fixed-assets", "asset-revaluation", assetId, scope.RowVersion, request.PolicyVersionId, evidence, requesterPermission: "assets.revalue", cancellationToken: ct);
    }

    public async Task<AssetRevaluationView> ProposeAssetRevaluationAsync(TenantId tenant, Guid actor, Guid assetId, CreateAssetRevaluationRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty || request.BookId == Guid.Empty || request.ProposedGrossCost < 0m || request.ProposedResidualValue < 0m || request.ProposedResidualValue > request.ProposedGrossCost || request.ApprovalRequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.ProviderCode) || string.IsNullOrWhiteSpace(request.ProviderVersion))
            throw new FixedAssetException("assets.revaluation_invalid");
        await ValidateApprovalBindingAsync(tenant, actor, assetId, request.LegalEntityId, request.BookId, request.ApprovalRequestId,
            "asset-revaluation", JsonSerializer.SerializeToElement(new { Action = "Revaluation", AssetId = assetId, BookId = request.BookId, ProposedGrossCost = request.ProposedGrossCost, ProposedResidualValue = request.ProposedResidualValue, EffectiveDate = request.EffectiveDate, Reason = request.Reason.Trim(), ProviderCode = request.ProviderCode.Trim(), ProviderVersion = request.ProviderVersion.Trim() }), ct);
        if (revaluationProvider is null || !string.Equals(revaluationProvider.ProviderCode, request.ProviderCode.Trim(), StringComparison.Ordinal) || !string.Equals(revaluationProvider.ProviderVersion, request.ProviderVersion.Trim(), StringComparison.Ordinal)) throw new FixedAssetException("assets.revaluation_provider_unavailable");
        if (!await revaluationProvider.ValidateAsync(tenant, request.LegalEntityId, assetId, request.BookId, request.ProposedGrossCost, request.ProposedResidualValue, request.EffectiveDate, ct)) throw new FixedAssetException("assets.revaluation_rejected");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var id = Guid.CreateVersion7();
            await using var insert = new NpgsqlCommand("insert into business_assets.asset_revaluation(tenant_id,id,asset_id,asset_book_id,legal_entity_id,proposed_gross_cost,proposed_residual_value,effective_date,reason,approval_request_id,provider_code,provider_version,status,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,'Validated',$13,now())", context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(assetId); insert.Parameters.AddWithValue(request.BookId); insert.Parameters.AddWithValue(request.LegalEntityId); insert.Parameters.AddWithValue(request.ProposedGrossCost); insert.Parameters.AddWithValue(request.ProposedResidualValue); insert.Parameters.AddWithValue(request.EffectiveDate); insert.Parameters.AddWithValue(request.Reason.Trim()); insert.Parameters.AddWithValue(request.ApprovalRequestId); insert.Parameters.AddWithValue(request.ProviderCode.Trim()); insert.Parameters.AddWithValue(request.ProviderVersion.Trim()); insert.Parameters.AddWithValue(actor); await insert.ExecuteNonQueryAsync(token);
            return new AssetRevaluationView(assetId, request.BookId, request.ProposedGrossCost, request.ProposedResidualValue, request.EffectiveDate, request.Reason.Trim(), request.ApprovalRequestId, request.ProviderCode.Trim(), request.ProviderVersion.Trim(), "Validated", DateTimeOffset.UtcNow);
        }, ct);
    }

    public async Task<AssetBookBalanceView> GetAssetBookBalanceAsync(TenantId tenant, Guid assetId, Guid bookId, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select a.id,b.id,a.legal_entity_id,b.original_cost,a.lifecycle_state,coalesce(sum(case when t.transaction_type='Opening' then t.accumulated_depreciation when t.transaction_type='Depreciation' then t.amount when t.transaction_type='Reversal' then -t.amount when t.transaction_type='Disposal' then -t.accumulated_depreciation when t.transaction_type='Reinstatement' then t.accumulated_depreciation else 0 end),0),coalesce(sum(case when t.transaction_type='Impairment' then coalesce(nullif(t.accumulated_impairment,0),t.amount) when t.transaction_type='Disposal' then -t.accumulated_impairment when t.transaction_type='Reinstatement' then t.accumulated_impairment else 0 end),0),coalesce(sum(case when t.transaction_type='Revaluation' then t.revaluation_effect when t.transaction_type='Disposal' then -t.revaluation_effect when t.transaction_type='Reinstatement' then t.revaluation_effect else 0 end),0),count(t.id),max(t.transaction_date) from business_assets.fixed_asset a join business_assets.asset_book b on b.tenant_id=a.tenant_id and b.asset_id=a.id left join business_assets.asset_book_transaction t on t.tenant_id=b.tenant_id and t.asset_book_id=b.id where a.tenant_id=$1 and a.id=$2 and b.id=$3 and a.legal_entity_id=$4 group by a.id,b.id,a.legal_entity_id,b.original_cost,a.lifecycle_state", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(assetId); command.Parameters.AddWithValue(bookId); command.Parameters.AddWithValue(legalEntityId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new FixedAssetException("assets.book_not_found");
        var originalCost = reader.GetDecimal(3); var accumulated = Math.Clamp(reader.GetDecimal(5), 0m, originalCost); var impairment = Math.Max(0m, reader.GetDecimal(6)); var revaluation = reader.GetDecimal(7); var lifecycle = reader.GetString(4); var netBookValue = lifecycle == "Disposed" ? 0m : Math.Max(0m, originalCost - accumulated - impairment + revaluation);
        return new AssetBookBalanceView(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), originalCost, accumulated, netBookValue, lifecycle, Convert.ToInt32(reader.GetInt64(8), System.Globalization.CultureInfo.InvariantCulture), reader.IsDBNull(9) ? null : reader.GetFieldValue<DateOnly>(9), impairment, revaluation);
    }

    public async Task<IReadOnlyList<AssetDepreciationScheduleLineView>> GetDepreciationScheduleAsync(TenantId tenant, Guid assetId, Guid bookId, Guid legalEntityId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (from == default || to < from || to.DayNumber - from.DayNumber > 3660) throw new FixedAssetException("assets.schedule_range_invalid");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select b.original_cost,b.residual_value,b.useful_life_months,b.depreciation_method,b.placed_in_service_date,coalesce((select jsonb_agg(jsonb_build_object('effectiveFrom',e.effective_from,'usefulLifeMonths',e.useful_life_months,'residualValue',e.residual_value) order by e.effective_from,e.id) from business_assets.asset_book_estimate_change e where e.tenant_id=b.tenant_id and e.asset_book_id=b.id and e.effective_from <= $5),'[]'::jsonb)::text from business_assets.asset_book b join business_assets.fixed_asset a on a.tenant_id=b.tenant_id and a.id=b.asset_id where b.tenant_id=$1 and b.id=$2 and b.asset_id=$3 and a.legal_entity_id=$4", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(bookId); command.Parameters.AddWithValue(assetId); command.Parameters.AddWithValue(legalEntityId);
        command.Parameters.AddWithValue(to);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new FixedAssetException("assets.book_not_found");
        var cost = reader.GetDecimal(0); var residual = reader.GetDecimal(1); int? usefulLife = reader.IsDBNull(2) ? null : reader.GetInt32(2); var method = reader.GetString(3); DateOnly? placed = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4); var changesJson = reader.GetString(5);
        await reader.CloseAsync();
        if (placed is null || usefulLife is null || method == "NoDepreciation") return Array.Empty<AssetDepreciationScheduleLineView>();
        await using var transactionCommand = new NpgsqlCommand("select transaction_date,transaction_type,amount,accumulated_depreciation,coalesce(nullif(accumulated_impairment,0),0),revaluation_effect from business_assets.asset_book_transaction where tenant_id=$1 and asset_book_id=$2 and transaction_date <= $3 order by transaction_date,id", connection);
        transactionCommand.Parameters.AddWithValue(tenant.Value); transactionCommand.Parameters.AddWithValue(bookId); transactionCommand.Parameters.AddWithValue(to);
        var transactions = new List<(DateOnly Date, string Type, decimal Amount, decimal Depreciation, decimal Impairment, decimal Revaluation)>(); await using (var transactionReader = await transactionCommand.ExecuteReaderAsync(ct)) while (await transactionReader.ReadAsync(ct)) transactions.Add((transactionReader.GetFieldValue<DateOnly>(0), transactionReader.GetString(1), transactionReader.GetDecimal(2), transactionReader.GetDecimal(3), transactionReader.GetDecimal(4), transactionReader.GetDecimal(5)));
        var first = new DateOnly(Math.Max(from.Year, placed.Value.Year), from >= placed.Value ? from.Month : placed.Value.Month, 1);
        if (first < placed.Value) first = new DateOnly(placed.Value.Year, placed.Value.Month, 1);
        var last = new DateOnly(to.Year, to.Month, 1);
        var rows = new List<AssetDepreciationScheduleLineView>();
        var cumulativeDepreciation = transactions.Where(x => x.Date < first && (x.Type is "Opening" or "Depreciation" or "Reversal" or "Disposal" or "Reinstatement")).Sum(x => x.Type == "Opening" ? x.Depreciation : x.Type == "Reversal" ? -x.Amount : x.Type == "Disposal" ? -x.Depreciation : x.Type == "Reinstatement" ? x.Depreciation : x.Amount);
        var cumulativeImpairment = transactions.Where(x => x.Date < first && (x.Type is "Impairment" or "Disposal" or "Reinstatement")).Sum(x => x.Type == "Disposal" ? -x.Impairment : x.Type == "Reinstatement" ? x.Impairment : x.Impairment > 0m ? x.Impairment : x.Amount);
        var cumulativeRevaluation = transactions.Where(x => x.Date < first && (x.Type is "Revaluation" or "Disposal" or "Reinstatement")).Sum(x => x.Type == "Disposal" ? -x.Revaluation : x.Type == "Reinstatement" ? x.Revaluation : x.Revaluation);
        var priorPeriod = first.AddMonths(-1);
        var appliedChanges = new HashSet<string>(StringComparer.Ordinal);
        using var changes = JsonDocument.Parse(changesJson);
        for (var period = first; period <= last && rows.Count < 120; period = period.AddMonths(1))
        {
            var periodEnd = period.AddMonths(1).AddDays(-1);
            foreach (var change in changes.RootElement.EnumerateArray())
            {
                var effective = DateOnly.Parse(change.GetProperty("effectiveFrom").GetString()!, CultureInfo.InvariantCulture);
                if (effective > period) break;
                if (!appliedChanges.Add(change.GetRawText())) continue;
                if (change.GetProperty("residualValue").ValueKind != JsonValueKind.Null) residual = change.GetProperty("residualValue").GetDecimal();
                if (change.GetProperty("usefulLifeMonths").ValueKind != JsonValueKind.Null) usefulLife = change.GetProperty("usefulLifeMonths").GetInt32();
            }
            foreach (var item in transactions.Where(x => x.Date >= period && x.Date <= periodEnd))
            {
                if (item.Type == "Opening") cumulativeDepreciation += item.Depreciation;
                else if (item.Type == "Depreciation") cumulativeDepreciation += item.Amount;
                else if (item.Type == "Reversal") cumulativeDepreciation -= item.Amount;
                else if (item.Type == "Impairment") cumulativeImpairment += item.Impairment > 0m ? item.Impairment : item.Amount;
                else if (item.Type == "Revaluation") cumulativeRevaluation += item.Revaluation;
                else if (item.Type == "Disposal") { cumulativeDepreciation -= item.Depreciation; cumulativeImpairment -= item.Impairment; cumulativeRevaluation -= item.Revaluation; }
                else if (item.Type == "Reinstatement") { cumulativeDepreciation += item.Depreciation; cumulativeImpairment += item.Impairment; cumulativeRevaluation += item.Revaluation; }
            }
            var actual = transactions.Where(x => x.Date >= period && x.Date <= periodEnd && (x.Type is "Depreciation" or "Reversal")).Sum(x => x.Type == "Reversal" ? -x.Amount : x.Amount);
            var elapsedBefore = Math.Max(0, MonthsInclusive(placed.Value, priorPeriod));
            var remainingMonths = Math.Max(1, usefulLife.Value - elapsedBefore);
            var carryingBefore = Math.Max(0m, cost - cumulativeDepreciation - cumulativeImpairment + cumulativeRevaluation);
            var projected = Math.Max(0m, Math.Min(carryingBefore - residual, carryingBefore - residual) / remainingMonths);
            var amount = actual != 0m ? actual : projected;
            if (actual == 0m) cumulativeDepreciation += amount;
            var cumulative = Math.Max(0m, cumulativeDepreciation);
            rows.Add(new(assetId, bookId, period, periodEnd, Math.Max(0m, amount), cumulative, Math.Max(0m, cost - cumulative - cumulativeImpairment + cumulativeRevaluation), method));
            priorPeriod = period;
        }
        return rows;
    }

    public async Task<AssetGlReconciliationView> ReconcileAssetBookAsync(TenantId tenant, Guid bookId, Guid legalEntityId, DateOnly asOfDate, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, tenant, ct);
        await using var command = new NpgsqlCommand("select bd.posts_to_ledger,bd.currency_code,coalesce(sum(case when a.lifecycle_state='Disposed' then 0 else b.original_cost end),0),coalesce(sum(case when a.lifecycle_state='Disposed' then 0 else coalesce((select sum(case when t.transaction_type='Opening' then t.accumulated_depreciation when t.transaction_type='Depreciation' then t.amount when t.transaction_type='Reversal' then -t.amount when t.transaction_type='Disposal' then -t.accumulated_depreciation when t.transaction_type='Reinstatement' then t.accumulated_depreciation else 0 end) from business_assets.asset_book_transaction t where t.tenant_id=b.tenant_id and t.asset_book_id=b.id and t.transaction_date <= $5),0) end),0),coalesce(sum(case when a.lifecycle_state='Disposed' then 0 else coalesce((select sum(case when t.transaction_type='Impairment' then coalesce(nullif(t.accumulated_impairment,0),t.amount) when t.transaction_type='Disposal' then -t.accumulated_impairment when t.transaction_type='Reinstatement' then t.accumulated_impairment else 0 end) from business_assets.asset_book_transaction t where t.tenant_id=b.tenant_id and t.asset_book_id=b.id and t.transaction_date <= $5),0) end),0),coalesce(sum(case when a.lifecycle_state='Disposed' then 0 else coalesce((select sum(case when t.transaction_type='Revaluation' then t.revaluation_effect when t.transaction_type='Disposal' then -t.revaluation_effect when t.transaction_type='Reinstatement' then t.revaluation_effect else 0 end) from business_assets.asset_book_transaction t where t.tenant_id=b.tenant_id and t.asset_book_id=b.id and t.transaction_date <= $5),0) end),0) from business_assets.asset_book b join business_assets.fixed_asset a on a.tenant_id=b.tenant_id and a.id=b.asset_id join business_assets.asset_book_definition bd on bd.tenant_id=b.tenant_id and bd.id=b.book_definition_id where b.tenant_id=$1 and b.book_definition_id=$2 and a.legal_entity_id=$3 group by bd.posts_to_ledger,bd.currency_code", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(bookId); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(asOfDate); command.Parameters.AddWithValue(asOfDate);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new FixedAssetException("assets.book_not_found");
        var postsToLedger = reader.GetBoolean(0); var currency = reader.GetString(1).Trim(); var assetCost = reader.GetDecimal(2); var assetAccumulated = Math.Max(0m, reader.GetDecimal(3)); var assetImpairment = Math.Max(0m, reader.GetDecimal(4)); var revaluation = reader.GetDecimal(5); await reader.CloseAsync();
        var assetNetBookValue = Math.Max(0m, assetCost - assetAccumulated - assetImpairment + revaluation);
        if (!postsToLedger) return new AssetGlReconciliationView(legalEntityId, bookId, asOfDate, assetCost, 0m, 0m, assetAccumulated, 0m, 0m, assetNetBookValue, currency, "NotApplicable", assetImpairment, revaluation);
        var ledgerCost = await ledgerBalances.GetActiveControlBalanceAsync(tenant, new(legalEntityId), "FixedAssetCost", asOfDate, ct);
        var ledgerAccumulated = await ledgerBalances.GetActiveControlBalanceAsync(tenant, new(legalEntityId), "FixedAssetAccumulatedDepreciation", asOfDate, ct);
        var ledgerAccumulatedPositive = -ledgerAccumulated.SignedBaseBalance; var costDifference = assetCost - ledgerCost.SignedBaseBalance; var accumulatedDifference = assetAccumulated - ledgerAccumulatedPositive; var status = assetImpairment > 0m || Math.Abs(costDifference) > 0.000001m || Math.Abs(accumulatedDifference) > 0.000001m ? "Difference" : "Healthy";
        return new AssetGlReconciliationView(legalEntityId, bookId, asOfDate, assetCost, ledgerCost.SignedBaseBalance, costDifference, assetAccumulated, ledgerAccumulatedPositive, accumulatedDifference, assetNetBookValue, ledgerCost.BaseCurrencyCode, status, assetImpairment, revaluation);
    }

    private static string? ValidateOpeningLine(OpeningImportLineRequest line)
    {
        if (string.IsNullOrWhiteSpace(line.LegacyAssetNumber) || string.IsNullOrWhiteSpace(line.Name) || line.CategoryId == Guid.Empty || line.BookDefinitionId == Guid.Empty) return "assets.opening_line_required";
        if (line.OriginalCost < 0m || line.AccumulatedDepreciation < 0m || line.NetBookValue < 0m || line.ResidualValue < 0m || line.ResidualValue > line.OriginalCost) return "assets.opening_line_amount_invalid";
        if (Math.Abs(line.OriginalCost - line.AccumulatedDepreciation - line.NetBookValue) > 0.000001m) return "assets.opening_line_not_balanced";
        if (line.PlacedInServiceDate == default || line.AcquisitionDate > line.PlacedInServiceDate || line.UsefulLifeMonths is <= 0 || line.DepreciationMethod is not ("StraightLine" or "NoDepreciation")) return "assets.opening_line_policy_invalid";
        return null;
    }

    private static async Task<int> OpeningLineCountAsync(ITransactionContext context, TenantId tenant, Guid importId, CancellationToken ct) { await using var command = new NpgsqlCommand("select count(*) from business_assets.asset_opening_import_line where tenant_id=$1 and import_id=$2", context.Connection, context.Transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(importId); return Convert.ToInt32(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture); }
    private static async Task<int> OpeningPendingValidLineCountAsync(ITransactionContext context, TenantId tenant, Guid importId, CancellationToken ct) { await using var command = new NpgsqlCommand("select count(*) from business_assets.asset_opening_import_line where tenant_id=$1 and import_id=$2 and outcome='Valid'", context.Connection, context.Transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(importId); return Convert.ToInt32(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture); }
    private static async Task<int> OpeningValidLineCountAsync(ITransactionContext context, TenantId tenant, Guid importId, CancellationToken ct) { await using var command = new NpgsqlCommand("select count(*) from business_assets.asset_opening_import_line where tenant_id=$1 and import_id=$2 and outcome='Created'", context.Connection, context.Transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(importId); return Convert.ToInt32(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture); }

    public async Task<DisposalView> DisposeAsync(TenantId tenant, Guid actor, Guid assetId, DisposeAssetRequest request, CancellationToken ct)
    {
        if (request.Proceeds < 0m || request.DirectCosts < 0m || string.IsNullOrWhiteSpace(request.Reason)) throw new FixedAssetException("assets.disposal_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var disposalKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();
            var replay = await TryReplayDisposalAsync(context, tenant, assetId, request, disposalKey, token);
            if (replay is not null) return replay;
            await using var asset = new NpgsqlCommand("select legal_entity_id,asset_number,lifecycle_state,quantity from business_assets.fixed_asset where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction); asset.Parameters.AddWithValue(tenant.Value); asset.Parameters.AddWithValue(assetId);
            await using var assetReader = await asset.ExecuteReaderAsync(token);
            if (!await assetReader.ReadAsync(token)) throw new FixedAssetException("assets.asset_not_found");
            var legalEntityId = assetReader.GetGuid(0); var assetNumber = assetReader.GetString(1); var lifecycleState = assetReader.GetString(2); var assetQuantity = assetReader.GetDecimal(3); await assetReader.CloseAsync();
            if (request.LegalEntityId != legalEntityId) throw new FixedAssetException("assets.disposal_entity_mismatch");
            if (lifecycleState is "Disposed" or "Draft")
            {
                replay = await TryReplayDisposalAsync(context, tenant, assetId, request, disposalKey, token);
                if (replay is not null) return replay;
                throw new FixedAssetException("assets.disposal_not_allowed");
            }
            var disposalQuantity = request.Quantity ?? assetQuantity;
            if (disposalQuantity <= 0m || disposalQuantity > assetQuantity || request.Quantity is not null && disposalQuantity == assetQuantity && request.Quantity <= 0m) throw new FixedAssetException("assets.disposal_quantity_invalid");
            var isFullDisposal = disposalQuantity == assetQuantity;
            var disposalId = Guid.CreateVersion7();
            await using var disposal = new NpgsqlCommand("insert into business_assets.asset_disposal (tenant_id,id,asset_id,legal_entity_id,disposal_type,disposal_date,proceeds,direct_costs,reason,quantity,is_full_disposal,sale_reference,idempotency_key,quantity_was_explicit,status,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,'Draft',$15,now()) on conflict (tenant_id,idempotency_key) where idempotency_key is not null do nothing returning id", context.Connection, context.Transaction);
            disposal.Parameters.AddWithValue(tenant.Value); disposal.Parameters.AddWithValue(disposalId); disposal.Parameters.AddWithValue(assetId); disposal.Parameters.AddWithValue(legalEntityId); disposal.Parameters.AddWithValue(request.DisposalType.Trim()); disposal.Parameters.AddWithValue(request.DisposalDate); disposal.Parameters.AddWithValue(request.Proceeds); disposal.Parameters.AddWithValue(request.DirectCosts); disposal.Parameters.AddWithValue(request.Reason.Trim()); disposal.Parameters.AddWithValue(disposalQuantity); disposal.Parameters.AddWithValue(isFullDisposal); disposal.Parameters.AddWithValue((object?)request.SaleReference?.Trim() ?? DBNull.Value); disposal.Parameters.AddWithValue((object?)disposalKey ?? DBNull.Value); disposal.Parameters.AddWithValue(request.Quantity is not null); disposal.Parameters.AddWithValue(actor);
            try
            {
                if (await disposal.ExecuteScalarAsync(token) is null)
                {
                    replay = await TryReplayDisposalAsync(context, tenant, assetId, request, disposalKey, token);
                    if (replay is not null) return replay;
                    throw new FixedAssetException("assets.disposal_already_exists");
                }
            }
            catch (PostgresException exception) when (exception.SqlState == "23505") { throw new FixedAssetException("assets.disposal_already_exists"); }
            await using var books = new NpgsqlCommand("""
                select b.id,b.original_cost,bd.posts_to_ledger,bd.currency_code,
                       coalesce(sum(case when t.transaction_type='Depreciation' then t.amount when t.transaction_type='Reversal' then -t.amount when t.transaction_type='Disposal' then -t.accumulated_depreciation when t.transaction_type='Reinstatement' then t.accumulated_depreciation else 0 end),0),
                       coalesce(sum(case when t.transaction_type='Impairment' then coalesce(nullif(t.accumulated_impairment,0),t.amount) when t.transaction_type='Disposal' then -t.accumulated_impairment when t.transaction_type='Reinstatement' then t.accumulated_impairment else 0 end),0),
                       coalesce(sum(case when t.transaction_type='Revaluation' then t.revaluation_effect when t.transaction_type='Disposal' then -t.revaluation_effect when t.transaction_type='Reinstatement' then t.revaluation_effect else 0 end),0)
                from business_assets.asset_book b
                join business_assets.asset_book_definition bd on bd.tenant_id=b.tenant_id and bd.id=b.book_definition_id
                left join business_assets.asset_book_transaction t on t.tenant_id=b.tenant_id and t.asset_book_id=b.id
                where b.tenant_id=$1 and b.asset_id=$2
                group by b.id,b.original_cost,bd.posts_to_ledger,bd.currency_code
                order by b.id
                """, context.Connection, context.Transaction); books.Parameters.AddWithValue(tenant.Value); books.Parameters.AddWithValue(assetId);
            await using var bookReader = await books.ExecuteReaderAsync(token); var bookRows = new List<(Guid BookId, decimal GrossCost, bool PostsToLedger, string Currency, decimal Accumulated, decimal Impairment, decimal Revaluation)>(); while (await bookReader.ReadAsync(token)) bookRows.Add((bookReader.GetGuid(0), bookReader.GetDecimal(1), bookReader.GetBoolean(2), bookReader.GetString(3), bookReader.GetDecimal(4), bookReader.GetDecimal(5), bookReader.GetDecimal(6))); await bookReader.CloseAsync();
            var allocation = disposalQuantity / assetQuantity; var primary = bookRows.FirstOrDefault(); var primaryGross = isFullDisposal ? primary.GrossCost : primary.GrossCost * allocation; var primaryAccumulated = Math.Min(primary.Accumulated * (isFullDisposal ? 1m : allocation), primaryGross); var primaryImpairment = Math.Max(0m, primary.Impairment * (isFullDisposal ? 1m : allocation)); var primaryRevaluation = primary.Revaluation * (isFullDisposal ? 1m : allocation); var primaryNbv = Math.Max(0m, primaryGross - primaryAccumulated - primaryImpairment + primaryRevaluation); var primaryGainLoss = request.Proceeds - request.DirectCosts - primaryNbv;
            foreach (var book in bookRows)
            {
                var gross = isFullDisposal ? book.GrossCost : book.GrossCost * allocation; var accumulated = Math.Min(book.Accumulated * (isFullDisposal ? 1m : allocation), gross); var impairment = Math.Max(0m, book.Impairment * (isFullDisposal ? 1m : allocation)); var revaluation = book.Revaluation * (isFullDisposal ? 1m : allocation); var netBookValue = Math.Max(0m, gross - accumulated - impairment + revaluation); var gainLoss = request.Proceeds - request.DirectCosts - netBookValue;
                Guid? ledgerFact = null;
                if (book.PostsToLedger)
                {
                    if (gross <= 0m) throw new FixedAssetException("assets.disposal_book_value_invalid");
                    var factId = DeterministicGuid(disposalId, book.BookId); var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new(legalEntityId), AccountingFactTypes.FixedAssetDisposalPosted, 1, "fixed-assets", $"disposal:{disposalId:D}:book:{book.BookId:D}", $"fixed-assets:disposal:{disposalId:D}:{book.BookId:D}", request.DisposalDate, request.DisposalDate, new FixedAssetDisposalPostedPayload(gross, accumulated, netBookValue, request.Proceeds, request.DirectCosts, gainLoss, book.Currency, assetNumber, AccumulatedImpairment: impairment), Guid.CreateVersion7(), actor);
                    var posting = await ledger.PostFixedAssetDisposalAsync(context, fact, (FixedAssetDisposalPostedPayload)fact.Payload, token); ledgerFact = posting.FactId;
                }
                await using var transaction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,accumulated_depreciation,accumulated_impairment,revaluation_effect,idempotency_key,ledger_fact_reference,created_by,created_at) values($1,$2,$3,'Disposal',$4,$5,$6,$7,$8,$9,$10,$11,now())", context.Connection, context.Transaction); transaction.Parameters.AddWithValue(tenant.Value); transaction.Parameters.AddWithValue(Guid.CreateVersion7()); transaction.Parameters.AddWithValue(book.BookId); transaction.Parameters.AddWithValue(request.DisposalDate); transaction.Parameters.AddWithValue(gross); transaction.Parameters.AddWithValue(accumulated); transaction.Parameters.AddWithValue(impairment); transaction.Parameters.AddWithValue(revaluation); transaction.Parameters.AddWithValue($"disposal:{disposalId:D}:{book.BookId:D}"); transaction.Parameters.AddWithValue((object?)ledgerFact?.ToString("D") ?? DBNull.Value); transaction.Parameters.AddWithValue(actor); await transaction.ExecuteNonQueryAsync(token);
                if (!isFullDisposal)
                {
                    await using var reduceBook = new NpgsqlCommand("update business_assets.asset_book set original_cost=original_cost-$1,row_version=row_version+1 where tenant_id=$2 and id=$3", context.Connection, context.Transaction); reduceBook.Parameters.AddWithValue(gross); reduceBook.Parameters.AddWithValue(tenant.Value); reduceBook.Parameters.AddWithValue(book.BookId); await reduceBook.ExecuteNonQueryAsync(token);
                }
            }
            await using var updateDisposal = new NpgsqlCommand("update business_assets.asset_disposal set status='Posted' where tenant_id=$1 and id=$2", context.Connection, context.Transaction); updateDisposal.Parameters.AddWithValue(tenant.Value); updateDisposal.Parameters.AddWithValue(disposalId); await updateDisposal.ExecuteNonQueryAsync(token);
            await using var updateAsset = new NpgsqlCommand("update business_assets.fixed_asset set quantity=case when $1 then quantity else quantity-$2 end,lifecycle_state=case when $1 then 'Disposed' else lifecycle_state end,row_version=row_version+1,updated_by=$3,updated_at=now() where tenant_id=$4 and id=$5", context.Connection, context.Transaction); updateAsset.Parameters.AddWithValue(isFullDisposal); updateAsset.Parameters.AddWithValue(disposalQuantity); updateAsset.Parameters.AddWithValue(actor); updateAsset.Parameters.AddWithValue(tenant.Value); updateAsset.Parameters.AddWithValue(assetId); await updateAsset.ExecuteNonQueryAsync(token);
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "FixedAssetDisposedV1", JsonSerializer.Serialize(new FixedAssetDisposedV1(assetId, legalEntityId, request.DisposalType.Trim(), request.DisposalDate, primaryNbv, primaryGainLoss, DateTimeOffset.UtcNow)), tenant, DateTimeOffset.UtcNow, disposalId, assetId, "fixed-assets"));
            return new DisposalView(assetId, request.DisposalType.Trim(), request.DisposalDate, primaryGross, primaryAccumulated, primaryNbv, request.Proceeds, request.DirectCosts, primaryGainLoss, "Posted", primaryImpairment, primary.Revaluation);
        }, ct);
    }

    public async Task<AssetSaleView> SellAsync(TenantId tenant, Guid actor, Guid assetId, SellAssetRequest request, CancellationToken ct)
    {
        if (invoiceSnapshots is null) throw new FixedAssetException("assets.sale_invoice_source_unavailable");
        if (request.LegalEntityId == Guid.Empty || request.InvoiceId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new FixedAssetException("assets.sale_invalid");
        var snapshot = await invoiceSnapshots.GetInvoiceComplianceSnapshotAsync(tenant, request.LegalEntityId, request.InvoiceId, ct) ?? throw new FixedAssetException("assets.sale_invoice_not_found");
        if (snapshot.LegalEntityId != request.LegalEntityId || !string.Equals(snapshot.Direction, "Receivable", StringComparison.OrdinalIgnoreCase)) throw new FixedAssetException("assets.sale_invoice_invalid");
        if (!decimal.TryParse(snapshot.GrossAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var proceeds) || proceeds < 0m) throw new FixedAssetException("assets.sale_invoice_amount_invalid");
        var disposal = await DisposeAsync(tenant, actor, assetId, new DisposeAssetRequest(request.LegalEntityId, "Sold", request.SaleDate, proceeds, 0m, request.Reason, request.IdempotencyKey, request.Quantity, $"invoice:{request.InvoiceId:D}"), ct);
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var disposalCommand = new NpgsqlCommand("select id from business_assets.asset_disposal where tenant_id=$1 and asset_id=$2 and idempotency_key=$3", context.Connection, context.Transaction);
            disposalCommand.Parameters.AddWithValue(tenant.Value); disposalCommand.Parameters.AddWithValue(assetId); disposalCommand.Parameters.AddWithValue(request.IdempotencyKey.Trim());
            var disposalId = (Guid?)(await disposalCommand.ExecuteScalarAsync(token)) ?? throw new FixedAssetException("assets.sale_disposal_link_missing");
            await using var sale = new NpgsqlCommand("insert into business_assets.asset_sale(tenant_id,id,asset_id,disposal_id,legal_entity_id,invoice_id,invoice_snapshot,payment_reference,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7::jsonb,$8,$9,now()) on conflict (tenant_id,disposal_id) do update set payment_reference=coalesce(business_assets.asset_sale.payment_reference,excluded.payment_reference) returning status", context.Connection, context.Transaction);
            sale.Parameters.AddWithValue(tenant.Value); sale.Parameters.AddWithValue(Guid.CreateVersion7()); sale.Parameters.AddWithValue(assetId); sale.Parameters.AddWithValue(disposalId); sale.Parameters.AddWithValue(request.LegalEntityId); sale.Parameters.AddWithValue(request.InvoiceId); sale.Parameters.AddWithValue(JsonSerializer.Serialize(snapshot)); sale.Parameters.AddWithValue((object?)request.PaymentReference?.Trim() ?? DBNull.Value); sale.Parameters.AddWithValue(actor);
            var status = (string?)await sale.ExecuteScalarAsync(token) ?? "Active";
            return new AssetSaleView(assetId, disposalId, request.InvoiceId, request.PaymentReference?.Trim(), disposal, status);
        }, ct);
    }

    public async Task<ReinstatementView> ReinstateAsync(TenantId tenant, Guid actor, Guid assetId, ReinstateAssetRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty || request.DisposalId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new FixedAssetException("assets.reinstatement_invalid");
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request)))).ToLowerInvariant();
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var existing = new NpgsqlCommand("select request_hash,status,response from business_assets.asset_disposal_reinstatement where tenant_id=$1 and idempotency_key=$2 for update", context.Connection, context.Transaction);
            existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(request.IdempotencyKey.Trim());
            await using var existingReader = await existing.ExecuteReaderAsync(token);
            if (await existingReader.ReadAsync(token))
            {
                var storedHash = existingReader.GetString(0); var status = existingReader.GetString(1); var replayResponse = existingReader.IsDBNull(2) ? null : existingReader.GetFieldValue<string>(2); await existingReader.CloseAsync();
                if (!string.Equals(storedHash, requestHash, StringComparison.Ordinal)) throw new FixedAssetException("assets.reinstatement_idempotency_conflict");
                if (status != "Completed" || string.IsNullOrWhiteSpace(replayResponse)) throw new FixedAssetException("assets.reinstatement_retry_incomplete");
                return (JsonSerializer.Deserialize<ReinstatementView>(replayResponse) ?? throw new FixedAssetException("assets.reinstatement_retry_incomplete")) with { AlreadyApplied = true };
            }
            await existingReader.CloseAsync();

            await using var disposal = new NpgsqlCommand("select d.asset_id,d.legal_entity_id,d.disposal_date,d.proceeds,d.direct_costs,d.quantity,d.is_full_disposal,d.status,a.lifecycle_state,a.quantity from business_assets.asset_disposal d join business_assets.fixed_asset a on a.tenant_id=d.tenant_id and a.id=d.asset_id where d.tenant_id=$1 and d.id=$2 and d.asset_id=$3 for update of d,a", context.Connection, context.Transaction);
            disposal.Parameters.AddWithValue(tenant.Value); disposal.Parameters.AddWithValue(request.DisposalId); disposal.Parameters.AddWithValue(assetId);
            await using var disposalReader = await disposal.ExecuteReaderAsync(token);
            if (!await disposalReader.ReadAsync(token)) throw new FixedAssetException("assets.disposal_not_found");
            var legalEntityId = disposalReader.GetGuid(1); var disposalDate = disposalReader.GetFieldValue<DateOnly>(2); var proceeds = disposalReader.GetDecimal(3); var directCosts = disposalReader.GetDecimal(4); var disposalQuantity = disposalReader.GetDecimal(5); var isFull = disposalReader.GetBoolean(6); var disposalStatus = disposalReader.GetString(7); var assetState = disposalReader.GetString(8); var currentQuantity = disposalReader.GetDecimal(9); await disposalReader.CloseAsync();
            if (legalEntityId != request.LegalEntityId) throw new FixedAssetException("assets.reinstatement_entity_mismatch");
            if (disposalStatus != "Posted" || (!isFull && assetState == "Disposed")) throw new FixedAssetException("assets.reinstatement_not_allowed");
            var reinstatementId = Guid.CreateVersion7();
            await using var insert = new NpgsqlCommand("insert into business_assets.asset_disposal_reinstatement(tenant_id,id,asset_id,disposal_id,legal_entity_id,idempotency_key,request_hash,reinstatement_date,reason,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,now())", context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(reinstatementId); insert.Parameters.AddWithValue(assetId); insert.Parameters.AddWithValue(request.DisposalId); insert.Parameters.AddWithValue(legalEntityId); insert.Parameters.AddWithValue(request.IdempotencyKey.Trim()); insert.Parameters.AddWithValue(requestHash); insert.Parameters.AddWithValue(request.ReinstatementDate); insert.Parameters.AddWithValue(request.Reason.Trim()); insert.Parameters.AddWithValue(actor); await insert.ExecuteNonQueryAsync(token);

            await using var transactions = new NpgsqlCommand("select t.asset_book_id,t.amount,t.accumulated_depreciation,t.accumulated_impairment,t.revaluation_effect,t.ledger_fact_reference,bd.currency_code,a.asset_number,bd.posts_to_ledger from business_assets.asset_book_transaction t join business_assets.asset_book b on b.tenant_id=t.tenant_id and b.id=t.asset_book_id join business_assets.fixed_asset a on a.tenant_id=b.tenant_id and a.id=b.asset_id join business_assets.asset_book_definition bd on bd.tenant_id=b.tenant_id and bd.id=b.book_definition_id where t.tenant_id=$1 and t.idempotency_key like $2 order by t.asset_book_id", context.Connection, context.Transaction);
            transactions.Parameters.AddWithValue(tenant.Value); transactions.Parameters.AddWithValue($"disposal:{request.DisposalId:D}:%");
            var rows = new List<(Guid BookId, decimal Gross, decimal Accumulated, decimal Impairment, decimal Revaluation, Guid? LedgerFact, string Currency, string AssetNumber, bool PostsToLedger)>();
            await using var transactionReader = await transactions.ExecuteReaderAsync(token);
            while (await transactionReader.ReadAsync(token)) rows.Add((transactionReader.GetGuid(0), transactionReader.GetDecimal(1), transactionReader.GetDecimal(2), transactionReader.GetDecimal(3), transactionReader.GetDecimal(4), transactionReader.IsDBNull(5) || !Guid.TryParse(transactionReader.GetString(5), out var factId) ? null : factId, transactionReader.GetString(6), transactionReader.GetString(7), transactionReader.GetBoolean(8)));
            await transactionReader.CloseAsync();
            if (rows.Count == 0) throw new FixedAssetException("assets.reinstatement_transactions_missing");
            foreach (var row in rows)
            {
                Guid? reversalFact = null;
                if (row.PostsToLedger)
                {
                    if (row.LedgerFact is null) throw new FixedAssetException("assets.reinstatement_ledger_link_missing");
                    var nbv = Math.Max(0m, row.Gross - row.Accumulated - row.Impairment + row.Revaluation); var gainLoss = proceeds - directCosts - nbv; var factId = DeterministicGuid(reinstatementId, row.BookId); var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new(legalEntityId), AccountingFactTypes.FixedAssetDisposalReversed, 1, "fixed-assets", $"disposal-reinstatement:{reinstatementId:D}:book:{row.BookId:D}", $"fixed-assets:disposal-reinstatement:{reinstatementId:D}:{row.BookId:D}", request.ReinstatementDate, request.ReinstatementDate, new FixedAssetDisposalReversedPayload(row.Gross, row.Accumulated, nbv, proceeds, directCosts, gainLoss, row.Currency, row.AssetNumber, row.LedgerFact.Value, AccumulatedImpairment: row.Impairment), Guid.CreateVersion7(), actor);
                    reversalFact = (await ledger.PostFixedAssetDisposalReversalAsync(context, fact, (FixedAssetDisposalReversedPayload)fact.Payload, token)).FactId;
                }
                await using var transaction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,accumulated_depreciation,accumulated_impairment,revaluation_effect,source_reference,idempotency_key,ledger_fact_reference,created_by,created_at) values($1,$2,$3,'Reinstatement',$4,$5,$6,$7,$8,$9,$10,$11,$12,now())", context.Connection, context.Transaction);
                transaction.Parameters.AddWithValue(tenant.Value); transaction.Parameters.AddWithValue(Guid.CreateVersion7()); transaction.Parameters.AddWithValue(row.BookId); transaction.Parameters.AddWithValue(request.ReinstatementDate); transaction.Parameters.AddWithValue(row.Gross); transaction.Parameters.AddWithValue(row.Accumulated); transaction.Parameters.AddWithValue(row.Impairment); transaction.Parameters.AddWithValue(row.Revaluation); transaction.Parameters.AddWithValue($"disposal-reinstatement:{reinstatementId:D}"); transaction.Parameters.AddWithValue($"disposal-reinstatement:{reinstatementId:D}:book:{row.BookId:D}"); transaction.Parameters.AddWithValue((object?)reversalFact?.ToString("D") ?? DBNull.Value); transaction.Parameters.AddWithValue(actor); await transaction.ExecuteNonQueryAsync(token);
                if (!isFull) { await using var restoreBook = new NpgsqlCommand("update business_assets.asset_book set original_cost=original_cost+$1,row_version=row_version+1 where tenant_id=$2 and id=$3", context.Connection, context.Transaction); restoreBook.Parameters.AddWithValue(row.Gross); restoreBook.Parameters.AddWithValue(tenant.Value); restoreBook.Parameters.AddWithValue(row.BookId); await restoreBook.ExecuteNonQueryAsync(token); }
            }
            await using var markDisposal = new NpgsqlCommand("update business_assets.asset_disposal set status='Reversed' where tenant_id=$1 and id=$2", context.Connection, context.Transaction); markDisposal.Parameters.AddWithValue(tenant.Value); markDisposal.Parameters.AddWithValue(request.DisposalId); await markDisposal.ExecuteNonQueryAsync(token);
            await using var restoreAsset = new NpgsqlCommand("update business_assets.fixed_asset set quantity=case when $1 then quantity else quantity+$2 end,lifecycle_state=case when $1 then 'Operational' else lifecycle_state end,row_version=row_version+1,updated_by=$3,updated_at=now() where tenant_id=$4 and id=$5", context.Connection, context.Transaction); restoreAsset.Parameters.AddWithValue(isFull); restoreAsset.Parameters.AddWithValue(disposalQuantity); restoreAsset.Parameters.AddWithValue(actor); restoreAsset.Parameters.AddWithValue(tenant.Value); restoreAsset.Parameters.AddWithValue(assetId); await restoreAsset.ExecuteNonQueryAsync(token);
            await using var markSale = new NpgsqlCommand("update business_assets.asset_sale set status='Reinstated',reinstatement_id=$1 where tenant_id=$2 and disposal_id=$3", context.Connection, context.Transaction); markSale.Parameters.AddWithValue(reinstatementId); markSale.Parameters.AddWithValue(tenant.Value); markSale.Parameters.AddWithValue(request.DisposalId); await markSale.ExecuteNonQueryAsync(token);
            var result = new ReinstatementView(assetId, request.DisposalId, request.ReinstatementDate, disposalQuantity, rows.Count, "Completed"); var serializedResponse = JsonSerializer.Serialize(result); await using var complete = new NpgsqlCommand("update business_assets.asset_disposal_reinstatement set status='Completed',response=$1::jsonb,completed_at=now() where tenant_id=$2 and id=$3", context.Connection, context.Transaction); complete.Parameters.AddWithValue(serializedResponse); complete.Parameters.AddWithValue(tenant.Value); complete.Parameters.AddWithValue(reinstatementId); await complete.ExecuteNonQueryAsync(token); return result;
        }, ct);
    }

    private static async Task<DisposalView?> TryReplayDisposalAsync(ITransactionContext context, TenantId tenant, Guid assetId, DisposeAssetRequest request, string? disposalKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(disposalKey)) return null;
        await using var existing = new NpgsqlCommand("select id,asset_id,legal_entity_id,disposal_type,disposal_date,proceeds,direct_costs,reason,quantity,is_full_disposal,sale_reference,quantity_was_explicit,status from business_assets.asset_disposal where tenant_id=$1 and idempotency_key=$2 for update", context.Connection, context.Transaction);
        existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(disposalKey);
        await using var reader = await existing.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var disposalId = reader.GetGuid(0); var existingAssetId = reader.GetGuid(1); var legalEntityId = reader.GetGuid(2); var disposalType = reader.GetString(3); var disposalDate = reader.GetFieldValue<DateOnly>(4); var proceeds = reader.GetDecimal(5); var directCosts = reader.GetDecimal(6); var reason = reader.GetString(7); var quantity = reader.GetDecimal(8); var isFull = reader.GetBoolean(9); var saleReference = reader.IsDBNull(10) ? null : reader.GetString(10); var quantityWasExplicit = reader.GetBoolean(11); var status = reader.GetString(12); await reader.CloseAsync();
        var requestSaleReference = string.IsNullOrWhiteSpace(request.SaleReference) ? null : request.SaleReference.Trim();
        if (existingAssetId != assetId || legalEntityId != request.LegalEntityId || !string.Equals(disposalType, request.DisposalType.Trim(), StringComparison.Ordinal) || disposalDate != request.DisposalDate || proceeds != request.Proceeds || directCosts != request.DirectCosts || !string.Equals(reason, request.Reason.Trim(), StringComparison.Ordinal) || quantityWasExplicit != (request.Quantity is not null) || request.Quantity is not null && quantity != request.Quantity.Value || !string.Equals(saleReference, requestSaleReference, StringComparison.Ordinal)) throw new FixedAssetException("assets.disposal_idempotency_conflict");
        if (status != "Posted") throw new FixedAssetException("assets.disposal_retry_incomplete");
        await using var transaction = new NpgsqlCommand("select amount,accumulated_depreciation,accumulated_impairment,revaluation_effect from business_assets.asset_book_transaction where tenant_id=$1 and idempotency_key like $2 order by id limit 1", context.Connection, context.Transaction);
        transaction.Parameters.AddWithValue(tenant.Value); transaction.Parameters.AddWithValue($"disposal:{disposalId:D}:%");
        await using var transactionReader = await transaction.ExecuteReaderAsync(ct);
        if (!await transactionReader.ReadAsync(ct)) throw new FixedAssetException("assets.disposal_retry_incomplete");
        var gross = transactionReader.GetDecimal(0); var accumulated = transactionReader.GetDecimal(1); var impairment = transactionReader.GetDecimal(2); var revaluation = transactionReader.GetDecimal(3); await transactionReader.CloseAsync();
        var netBookValue = Math.Max(0m, gross - accumulated - impairment + revaluation); var gainLoss = proceeds - directCosts - netBookValue;
        return new DisposalView(assetId, disposalType, disposalDate, gross, accumulated, netBookValue, proceeds, directCosts, gainLoss, status, impairment, revaluation);
    }

    private static decimal CalculateProspectivePeriodAmount(decimal originalCost, decimal originalResidual, int originalLife, DateOnly placedInService, string changesJson, DateOnly periodStart, DateOnly periodEnd)
    {
        var end = CalculateProspectiveCumulative(originalCost, originalResidual, originalLife, placedInService, changesJson, periodEnd);
        var before = periodStart <= placedInService ? 0m : CalculateProspectiveCumulative(originalCost, originalResidual, originalLife, placedInService, changesJson, periodStart.AddDays(-1));
        return Math.Max(0m, end - before);
    }

    private static decimal CalculateProspectiveCumulative(decimal originalCost, decimal originalResidual, int originalLife, DateOnly placedInService, string changesJson, DateOnly asOf)
    {
        if (asOf < placedInService) return 0m;
        var cumulative = 0m; var segmentStart = placedInService; var segmentBasis = originalCost; var segmentResidual = originalResidual; var segmentLife = originalLife;
        using var document = JsonDocument.Parse(changesJson);
        foreach (var change in document.RootElement.EnumerateArray())
        {
            var effective = DateOnly.Parse(change.GetProperty("effectiveFrom").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            if (effective > asOf) break;
            var segmentEnd = effective.AddDays(-1);
            var segmentMonths = MonthsInclusive(segmentStart, segmentEnd);
            if (segmentMonths > 0) cumulative += DepreciationEngine.CalculateStraightLine(segmentBasis, segmentResidual, segmentLife, segmentMonths);
            segmentBasis = originalCost - cumulative;
            segmentResidual = change.GetProperty("residualValue").ValueKind is JsonValueKind.Null ? segmentResidual : change.GetProperty("residualValue").GetDecimal();
            segmentLife = change.GetProperty("usefulLifeMonths").ValueKind is JsonValueKind.Null ? segmentLife : change.GetProperty("usefulLifeMonths").GetInt32();
            if (segmentResidual > segmentBasis) throw new FixedAssetException("assets.estimate_change_invalid");
            segmentStart = effective;
        }
        var finalMonths = MonthsInclusive(segmentStart, asOf);
        if (finalMonths > 0) cumulative += DepreciationEngine.CalculateStraightLine(originalCost - cumulative, segmentResidual, segmentLife, finalMonths);
        return Math.Min(cumulative, originalCost - segmentResidual);
    }

    private static int MonthsInclusive(DateOnly from, DateOnly to) => to < from ? 0 : checked((to.Year - from.Year) * 12 + to.Month - from.Month + 1);
    private static async Task SetTenantAsync(NpgsqlConnection connection, TenantId tenant, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
        command.Parameters.AddWithValue(tenant.Value.ToString("D"));
        await command.ExecuteNonQueryAsync(ct);
    }
    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, CancellationToken ct) { await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct); }
    private async Task<(Guid LegalEntityId, long RowVersion)> ReadApprovalScopeAsync(TenantId tenant, Guid assetId, Guid bookId, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("select a.legal_entity_id,a.row_version from business_assets.fixed_asset a join business_assets.asset_book b on b.tenant_id=a.tenant_id and b.asset_id=a.id and b.id=$3 and b.book_definition_id in (select id from business_assets.asset_book_definition where tenant_id=$1 and legal_entity_id=$2) where a.tenant_id=$1 and a.id=$4 and a.legal_entity_id=$2", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(bookId); command.Parameters.AddWithValue(assetId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new FixedAssetException("assets.book_not_found");
        var result = (reader.GetGuid(0), reader.GetInt64(1)); await reader.CloseAsync(); await transaction.CommitAsync(ct); return result;
    }
    private async Task ValidateApprovalBindingAsync(TenantId tenant, Guid actor, Guid assetId, Guid legalEntityId, Guid bookId, Guid approvalId, string ownerEntityType, JsonElement expectedReason, CancellationToken ct)
    {
        if (approvals is null) throw new FixedAssetException("assets.approval_unavailable");
        var approval = await approvals.GetAsync(tenant, approvalId, ct);
        if (approval is null || approval.State != "Approved" || approval.LegalEntityId != legalEntityId || approval.OwnerModule != "fixed-assets" || approval.OwnerEntityType != ownerEntityType || approval.OwnerEntityId != assetId || approval.RequestedBy is null || approval.RequestedBy == actor || string.IsNullOrWhiteSpace(approval.RequestReason)) throw new FixedAssetException("assets.approval_binding_invalid");
        var scope = await ReadApprovalScopeAsync(tenant, assetId, bookId, legalEntityId, ct);
        if (approval.ResourceVersion != scope.RowVersion) throw new FixedAssetException("assets.approval_stale");
        try
        {
            using var document = JsonDocument.Parse(approval.RequestReason);
            if (!JsonElement.DeepEquals(document.RootElement, expectedReason)) throw new FixedAssetException("assets.approval_binding_invalid");
        }
        catch (JsonException) { throw new FixedAssetException("assets.approval_binding_invalid"); }
    }
    private static async Task<VerificationCampaignView?> LoadVerificationCampaignAsync(ITransactionContext context, TenantId tenant, Guid campaignId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select c.legal_entity_id,c.name,c.snapshot_at,c.status,c.created_at,count(l.id),count(*) filter (where l.result='Pending') from business_assets.asset_verification_campaign c left join business_assets.asset_verification_line l on l.tenant_id=c.tenant_id and l.campaign_id=c.id where c.tenant_id=$1 and c.id=$2 group by c.id,c.legal_entity_id,c.name,c.snapshot_at,c.status,c.created_at", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(campaignId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new VerificationCampaignView(campaignId, reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<DateTimeOffset>(2), reader.GetString(3), Convert.ToInt32(reader.GetInt64(5), System.Globalization.CultureInfo.InvariantCulture), Convert.ToInt32(reader.GetInt64(6), System.Globalization.CultureInfo.InvariantCulture), reader.GetFieldValue<DateTimeOffset>(4));
    }
    private static async Task<Guid> ResolveAssetEntityAsync(ITransactionContext context, TenantId tenant, Guid assetId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select legal_entity_id from business_assets.fixed_asset where tenant_id=$1 and id=$2", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(assetId);
        return (Guid?)(await command.ExecuteScalarAsync(ct)) ?? throw new FixedAssetException("assets.asset_not_found");
    }
    private static async Task<Guid> OpeningJournalAsync(ITransactionContext context, TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='OPENING' and status='Active'", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId);
        return (Guid?)(await command.ExecuteScalarAsync(ct)) ?? throw new FixedAssetException("assets.opening_journal_missing");
    }
    private static Guid DeterministicGuid(Guid first, Guid second) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"fixed-assets:{first:D}:{second:D}"))[..16]);
    private static Guid StableGuid(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"fixed-assets:key:{value}"))[..16]);
    private static JsonElement ReadJsonElement(NpgsqlDataReader reader, int ordinal) => JsonDocument.Parse(reader.GetString(ordinal)).RootElement.Clone();
    private static async Task<IReadOnlyList<T>> ReadAsync<T>(NpgsqlCommand command, Func<NpgsqlDataReader,T> map, CancellationToken ct) { var list = new List<T>(); await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct); while (await reader.ReadAsync(ct)) list.Add(map(reader)); return list; }
}

public sealed class FixedAssetException(string code) : Exception(code) { public string Code { get; } = code; }
