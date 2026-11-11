using System.Data;
using System.Text.Json;
using Core.Transactions;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.FixedAssets;

public sealed class FixedAssetRepository(NpgsqlDataSource dataSource, ITransactionCoordinator coordinator)
{
    public async Task<AssetCategoryView> CreateCategoryAsync(TenantId tenant, Guid actor, CreateAssetCategoryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) throw new FixedAssetException("assets.category_required");
        if (request.DefaultResidualValue < 0m || request.CapitalizationThreshold < 0m || request.DefaultUsefulLifeMonths is <= 0) throw new FixedAssetException("assets.invalid_category_policy");
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
            var id = Guid.CreateVersion7();
            var number = $"FA-{DateTime.UtcNow:yyyyMMdd}-{id.ToString("N")[..8].ToUpperInvariant()}";
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
            return result;
        }, ct);
    }

    public async Task<IReadOnlyList<AssetView>> ListAssetsAsync(TenantId tenant, Guid? legalEntityId, string? lifecycleState, string? search, int limit, int offset, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 200); offset = Math.Max(offset, 0);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var command = new NpgsqlCommand("""
            select id,legal_entity_id,category_id,asset_number,name,description,asset_tag,lifecycle_state,asset_nature,tracking_granularity,product_variant_ref,inventory_serial_ref,acquisition_date,capitalization_date,placed_in_service_date,quantity,created_at,row_version
            from business_assets.fixed_asset where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and ($3::text is null or lifecycle_state=$3) and ($4::text is null or asset_number ilike '%'||$4||'%' or name ilike '%'||$4||'%' or coalesce(asset_tag,'') ilike '%'||$4||'%') order by asset_number,id limit $5 offset $6
            """, connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value); command.Parameters.AddWithValue((object?)lifecycleState ?? DBNull.Value); command.Parameters.AddWithValue((object?)search?.Trim() ?? DBNull.Value); command.Parameters.AddWithValue(limit); command.Parameters.AddWithValue(offset);
        return await ReadAsync(command, reader => new AssetView(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetFieldValue<DateOnly>(12), reader.IsDBNull(13) ? null : reader.GetFieldValue<DateOnly>(13), reader.IsDBNull(14) ? null : reader.GetFieldValue<DateOnly>(14), reader.GetDecimal(15), reader.GetFieldValue<DateTimeOffset>(16), reader.GetInt64(17)), ct);
    }

    public async Task<AssetAssignmentView> AssignAsync(TenantId tenant, Guid actor, Guid assetId, AssignAssetRequest request, CancellationToken ct)
    {
        if (request.EffectiveTo < request.EffectiveFrom) throw new FixedAssetException("assets.assignment_dates_invalid");
        if (request.EmployeeId is null && request.DepartmentId is null && string.IsNullOrWhiteSpace(request.CustodianName)) throw new FixedAssetException("assets.assignee_required");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var id = Guid.CreateVersion7();
            await using var command = new NpgsqlCommand("""
                insert into business_assets.asset_assignment (tenant_id,id,asset_id,assignment_type,employee_id,department_id,custodian_name,location_id,effective_from,effective_to,condition,accessories,acknowledgement_reference,created_by,created_at)
                values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,now()) returning id
                """, context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(assetId); command.Parameters.AddWithValue(request.AssignmentType); command.Parameters.AddWithValue((object?)request.EmployeeId ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.DepartmentId ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.CustodianName ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.LocationId ?? DBNull.Value); command.Parameters.AddWithValue(request.EffectiveFrom); command.Parameters.AddWithValue((object?)request.EffectiveTo ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.Condition ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.Accessories ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.AcknowledgementReference ?? DBNull.Value); command.Parameters.AddWithValue(actor);
            try { await command.ExecuteNonQueryAsync(token); } catch (PostgresException ex) when (ex.SqlState == "23P01") { throw new FixedAssetException("assets.assignment_overlap"); }
            return new AssetAssignmentView(id, assetId, request.AssignmentType, request.EmployeeId, request.DepartmentId, request.CustodianName, request.LocationId, request.EffectiveFrom, request.EffectiveTo, request.Condition, request.Accessories, request.AcknowledgementReference);
        }, ct);
    }

    public async Task<IReadOnlyList<AssetBookView>> ListBooksAsync(TenantId tenant, Guid? legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,code,name,posts_to_ledger,currency_code,default_depreciation_method,default_convention,default_useful_life_months from business_assets.asset_book_definition where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and active order by code,id", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value);
        return await ReadAsync(command, reader => new AssetBookView(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetInt32(8)), ct);
    }

    public async Task<AssetBookView> CreateBookAsync(TenantId tenant, Guid actor, CreateBookRequest request, CancellationToken ct)
    {
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
        if (request.DepreciationMethod is not ("StraightLine" or "NoDepreciation" or "DecliningBalance")) throw new FixedAssetException("assets.depreciation_method_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var bookId = Guid.CreateVersion7();
            await using var command = new NpgsqlCommand("""
                insert into business_assets.asset_book(tenant_id,id,asset_id,book_definition_id,original_cost,residual_value,useful_life_months,depreciation_method,placed_in_service_date,created_at,row_version)
                select $1,$2,a.id,$3,$4,$5,$6,$7,$8,now(),1 from business_assets.fixed_asset a where a.tenant_id=$1 and a.id=$9 and a.legal_entity_id=$10 and a.lifecycle_state in ('Operational','Capitalized')
                """, context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(bookId); command.Parameters.AddWithValue(request.BookDefinitionId); command.Parameters.AddWithValue(request.OriginalCost); command.Parameters.AddWithValue(request.ResidualValue); command.Parameters.AddWithValue(request.UsefulLifeMonths); command.Parameters.AddWithValue(request.DepreciationMethod); command.Parameters.AddWithValue(request.PlacedInServiceDate); command.Parameters.AddWithValue(assetId); command.Parameters.AddWithValue(request.LegalEntityId);
            if (await command.ExecuteNonQueryAsync(token) != 1) throw new FixedAssetException("assets.book_asset_not_found");
            return bookId;
        }, ct);
    }

    public async Task<DepreciationRunView> PrepareDepreciationRunAsync(TenantId tenant, Guid actor, CreateDepreciationRunRequest request, CancellationToken ct)
    {
        if (request.PeriodEnd < request.PeriodStart) throw new FixedAssetException("assets.period_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var runId = Guid.CreateVersion7(); var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? $"depreciation:{request.LegalEntityId:D}:{request.BookId:D}:{request.PeriodStart:yyyyMM}" : request.IdempotencyKey.Trim();
            await using (var create = new NpgsqlCommand("insert into business_assets.asset_depreciation_run(tenant_id,id,legal_entity_id,book_definition_id,period_start,period_end,status,idempotency_key,created_by,created_at) values($1,$2,$3,$4,$5,$6,'Calculated',$7,$8,now()) on conflict(tenant_id,idempotency_key) do nothing", context.Connection, context.Transaction))
            {
                create.Parameters.AddWithValue(tenant.Value); create.Parameters.AddWithValue(runId); create.Parameters.AddWithValue(request.LegalEntityId); create.Parameters.AddWithValue(request.BookId); create.Parameters.AddWithValue(request.PeriodStart); create.Parameters.AddWithValue(request.PeriodEnd); create.Parameters.AddWithValue(key); create.Parameters.AddWithValue(actor);
                if (await create.ExecuteNonQueryAsync(token) == 0)
                {
                    await using var existing = new NpgsqlCommand("select id,legal_entity_id,book_definition_id,period_start,period_end,status,total_amount,(select count(*) from business_assets.asset_depreciation_run_line l where l.tenant_id=r.tenant_id and l.run_id=r.id),created_at from business_assets.asset_depreciation_run r where tenant_id=$1 and idempotency_key=$2", context.Connection, context.Transaction); existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(key); await using var er = await existing.ExecuteReaderAsync(token); if (!await er.ReadAsync(token)) throw new FixedAssetException("assets.idempotency_conflict"); return new DepreciationRunView(er.GetGuid(0), er.GetGuid(1), er.GetGuid(2), er.GetFieldValue<DateOnly>(3), er.GetFieldValue<DateOnly>(4), er.GetString(5), er.GetDecimal(6), er.GetInt32(7), er.GetFieldValue<DateTimeOffset>(8));
                }
            }
            decimal total = 0m; var lines = 0;
            await using (var select = new NpgsqlCommand("select b.id,b.original_cost,b.residual_value,b.useful_life_months,b.depreciation_method,b.placed_in_service_date from business_assets.asset_book b join business_assets.fixed_asset a on a.tenant_id=b.tenant_id and a.id=b.asset_id where b.tenant_id=$1 and b.book_definition_id=$2 and a.legal_entity_id=$3 and a.lifecycle_state in ('Operational','Capitalized') and b.placed_in_service_date is not null and b.placed_in_service_date <= $4 for update", context.Connection, context.Transaction))
            {
                select.Parameters.AddWithValue(tenant.Value); select.Parameters.AddWithValue(request.BookId); select.Parameters.AddWithValue(request.LegalEntityId); select.Parameters.AddWithValue(request.PeriodEnd);
                await using var reader = await select.ExecuteReaderAsync(token); var rows = new List<(Guid, decimal, decimal, int, string, DateOnly)>(); while (await reader.ReadAsync(token)) rows.Add((reader.GetGuid(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetInt32(3), reader.GetString(4), reader.GetFieldValue<DateOnly>(5))); await reader.CloseAsync();
                foreach (var row in rows)
                {
                    var months = Math.Max(1, (request.PeriodEnd.Year - row.Item6.Year) * 12 + request.PeriodEnd.Month - row.Item6.Month + 1);
                    var amount = row.Item5 == "NoDepreciation" ? 0m : DepreciationEngine.PeriodAmount(row.Item2, row.Item3, row.Item4, months);
                    if (amount <= 0m) continue;
                    var lineId = Guid.CreateVersion7(); var basis = JsonSerializer.Serialize(new { row.Item2, row.Item3, row.Item4, months, method = row.Item5 });
                    await using var line = new NpgsqlCommand("insert into business_assets.asset_depreciation_run_line(tenant_id,id,run_id,asset_book_id,amount,calculation_basis) values($1,$2,$3,$4,$5,$6::jsonb)", context.Connection, context.Transaction); line.Parameters.AddWithValue(tenant.Value); line.Parameters.AddWithValue(lineId); line.Parameters.AddWithValue(runId); line.Parameters.AddWithValue(row.Item1); line.Parameters.AddWithValue(amount); line.Parameters.AddWithValue(basis); await line.ExecuteNonQueryAsync(token);
                    await using var transaction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,idempotency_key,created_by,created_at) values($1,$2,$3,'Depreciation',$4,$5,$6,$7,now())", context.Connection, context.Transaction); transaction.Parameters.AddWithValue(tenant.Value); transaction.Parameters.AddWithValue(Guid.CreateVersion7()); transaction.Parameters.AddWithValue(row.Item1); transaction.Parameters.AddWithValue(request.PeriodEnd); transaction.Parameters.AddWithValue(amount); transaction.Parameters.AddWithValue($"{runId:D}:{row.Item1:D}"); transaction.Parameters.AddWithValue(actor); await transaction.ExecuteNonQueryAsync(token);
                    total += amount; lines++;
                }
            }
            await using (var update = new NpgsqlCommand("update business_assets.asset_depreciation_run set total_amount=$1 where tenant_id=$2 and id=$3", context.Connection, context.Transaction)) { update.Parameters.AddWithValue(total); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(runId); await update.ExecuteNonQueryAsync(token); }
            return new DepreciationRunView(runId, request.LegalEntityId, request.BookId, request.PeriodStart, request.PeriodEnd, "Calculated", total, lines, DateTimeOffset.UtcNow);
        }, ct);
    }

    public async Task<DisposalView> DisposeAsync(TenantId tenant, Guid actor, Guid assetId, DisposeAssetRequest request, CancellationToken ct)
    {
        if (request.Proceeds < 0m || request.DirectCosts < 0m || string.IsNullOrWhiteSpace(request.Reason)) throw new FixedAssetException("assets.disposal_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("""
                insert into business_assets.asset_disposal (tenant_id,id,asset_id,legal_entity_id,disposal_type,disposal_date,proceeds,direct_costs,reason,status,created_by,created_at)
                select $1,$2,id,legal_entity_id,$3,$4,$5,$6,$7,'Posted',$8,now() from business_assets.fixed_asset where tenant_id=$1 and id=$9 and lifecycle_state not in ('Disposed','Draft')
                returning asset_id,disposal_type,disposal_date,proceeds,direct_costs,status
                """, context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(Guid.CreateVersion7()); command.Parameters.AddWithValue(request.DisposalType); command.Parameters.AddWithValue(request.DisposalDate); command.Parameters.AddWithValue(request.Proceeds); command.Parameters.AddWithValue(request.DirectCosts); command.Parameters.AddWithValue(request.Reason.Trim()); command.Parameters.AddWithValue(actor); command.Parameters.AddWithValue(assetId);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new FixedAssetException("assets.disposal_not_allowed");
            var result = new DisposalView(reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<DateOnly>(2), 0m, 0m, 0m, reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(3)-reader.GetDecimal(4), reader.GetString(5));
            await reader.CloseAsync();
            await using var update = new NpgsqlCommand("update business_assets.fixed_asset set lifecycle_state='Disposed', row_version=row_version+1, updated_by=$1, updated_at=now() where tenant_id=$2 and id=$3", context.Connection, context.Transaction); update.Parameters.AddWithValue(actor); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(assetId); await update.ExecuteNonQueryAsync(token);
            return result;
        }, ct);
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, CancellationToken ct) { await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct); }
    private static async Task<IReadOnlyList<T>> ReadAsync<T>(NpgsqlCommand command, Func<NpgsqlDataReader,T> map, CancellationToken ct) { var list = new List<T>(); await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct); while (await reader.ReadAsync(ct)) list.Add(map(reader)); return list; }
}

public sealed class FixedAssetException(string code) : Exception(code) { public string Code { get; } = code; }
