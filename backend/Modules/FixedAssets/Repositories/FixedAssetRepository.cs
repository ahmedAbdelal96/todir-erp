using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Transactions;
using Npgsql;
using Shared.Contracts.Ledger;
using Shared.Primitives.Identifiers;

namespace Modules.FixedAssets;

public sealed class FixedAssetRepository(NpgsqlDataSource dataSource, ITransactionCoordinator coordinator, ITransactionalFixedAssetLedgerPostingCapability ledger)
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
            await using (var create = new NpgsqlCommand("insert into business_assets.asset_depreciation_run(tenant_id,id,legal_entity_id,book_definition_id,period_start,period_end,status,idempotency_key,created_by,created_at) values($1,$2,$3,$4,$5,$6,'Calculated',$7,$8,now()) on conflict do nothing", context.Connection, context.Transaction))
            {
                create.Parameters.AddWithValue(tenant.Value); create.Parameters.AddWithValue(runId); create.Parameters.AddWithValue(request.LegalEntityId); create.Parameters.AddWithValue(request.BookId); create.Parameters.AddWithValue(request.PeriodStart); create.Parameters.AddWithValue(request.PeriodEnd); create.Parameters.AddWithValue(key); create.Parameters.AddWithValue(actor);
                if (await create.ExecuteNonQueryAsync(token) == 0)
                {
                    await using var existing = new NpgsqlCommand("select id,legal_entity_id,book_definition_id,period_start,period_end,status,total_amount,(select count(*) from business_assets.asset_depreciation_run_line l where l.tenant_id=r.tenant_id and l.run_id=r.id),created_at from business_assets.asset_depreciation_run r where tenant_id=$1 and (idempotency_key=$2 or (legal_entity_id=$3 and book_definition_id=$4 and period_start=$5 and period_end=$6)) order by case when idempotency_key=$2 then 0 else 1 end limit 1", context.Connection, context.Transaction); existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(key); existing.Parameters.AddWithValue(request.LegalEntityId); existing.Parameters.AddWithValue(request.BookId); existing.Parameters.AddWithValue(request.PeriodStart); existing.Parameters.AddWithValue(request.PeriodEnd); await using var er = await existing.ExecuteReaderAsync(token); if (!await er.ReadAsync(token)) throw new FixedAssetException("assets.idempotency_conflict"); return new DepreciationRunView(er.GetGuid(0), er.GetGuid(1), er.GetGuid(2), er.GetFieldValue<DateOnly>(3), er.GetFieldValue<DateOnly>(4), er.GetString(5), er.GetDecimal(6), er.GetInt32(7), er.GetFieldValue<DateTimeOffset>(8));
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
            return new DepreciationRunView(runId, entity, bookDefinition, periodStart, periodEnd, "Posted", total, rows.Count, createdAt);
        }, ct);
    }

    public async Task<IReadOnlyList<CapitalizationCandidateView>> ListCapitalizationCandidatesAsync(TenantId tenant, Guid? legalEntityId, string? status, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 200);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,source_type,source_reference,source_snapshot,eligible_amount,allocated_amount,status,decision,created_at from business_assets.asset_capitalization_candidate where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and ($3::text is null or status=$3) order by created_at desc,id desc limit $4", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value); command.Parameters.AddWithValue((object?)status?.Trim() ?? DBNull.Value); command.Parameters.AddWithValue(limit);
        return await ReadAsync(command, reader => new CapitalizationCandidateView(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), ReadJsonElement(reader, 4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9)), ct);
    }

    public async Task<CapitalizationCandidateView> CreateCapitalizationCandidateAsync(TenantId tenant, Guid actor, CreateCapitalizationCandidateRequest request, CancellationToken ct)
    {
        if (request.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(request.SourceType) || string.IsNullOrWhiteSpace(request.SourceReference) || request.EligibleAmount <= 0m || request.SourceSnapshot.ValueKind != JsonValueKind.Object)
            throw new FixedAssetException("assets.capitalization_candidate_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var id = Guid.CreateVersion7();
            await using var command = new NpgsqlCommand("insert into business_assets.asset_capitalization_candidate(tenant_id,id,legal_entity_id,source_type,source_reference,source_snapshot,eligible_amount,created_by,created_at) values($1,$2,$3,$4,$5,$6::jsonb,$7,$8,now()) returning created_at", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(request.LegalEntityId); command.Parameters.AddWithValue(request.SourceType.Trim()); command.Parameters.AddWithValue(request.SourceReference.Trim()); command.Parameters.AddWithValue(JsonSerializer.Serialize(request.SourceSnapshot)); command.Parameters.AddWithValue(request.EligibleAmount); command.Parameters.AddWithValue(actor);
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
            await using var asset = new NpgsqlCommand("select legal_entity_id,lifecycle_state from business_assets.fixed_asset where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction); asset.Parameters.AddWithValue(tenant.Value); asset.Parameters.AddWithValue(request.AssetId);
            await using var assetReader = await asset.ExecuteReaderAsync(token);
            if (!await assetReader.ReadAsync(token)) throw new FixedAssetException("assets.asset_not_found");
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

            await using var candidate = new NpgsqlCommand("select allocated_amount,status from business_assets.asset_capitalization_candidate where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction); candidate.Parameters.AddWithValue(tenant.Value); candidate.Parameters.AddWithValue(candidateId);
            await using var candidateReader = await candidate.ExecuteReaderAsync(token);
            if (!await candidateReader.ReadAsync(token)) throw new FixedAssetException("assets.capitalization_candidate_not_found");
            var candidateAllocatedAmount = candidateReader.GetDecimal(0); var candidateStatus = candidateReader.GetString(1); await candidateReader.CloseAsync();
            if (candidateStatus is "Rejected" or "PendingReview") throw new FixedAssetException("assets.source_allocation_correction_not_allowed");

            await using var allocation = new NpgsqlCommand("select asset_id,amount from business_assets.asset_source_allocation where tenant_id=$1 and id=$2 and candidate_id=$3 for update", context.Connection, context.Transaction); allocation.Parameters.AddWithValue(tenant.Value); allocation.Parameters.AddWithValue(request.AllocationId); allocation.Parameters.AddWithValue(candidateId);
            await using var allocationReader = await allocation.ExecuteReaderAsync(token);
            if (!await allocationReader.ReadAsync(token)) throw new FixedAssetException("assets.source_allocation_not_found");
            var assetId = allocationReader.GetGuid(0); var originalAmount = allocationReader.GetDecimal(1); await allocationReader.CloseAsync();
            await using var totalCorrections = new NpgsqlCommand("select coalesce(sum(amount),0) from business_assets.asset_source_allocation_correction where tenant_id=$1 and allocation_id=$2", context.Connection, context.Transaction); totalCorrections.Parameters.AddWithValue(tenant.Value); totalCorrections.Parameters.AddWithValue(request.AllocationId); var correctedAmount = (decimal)(await totalCorrections.ExecuteScalarAsync(token) ?? 0m);
            if (correctedAmount + request.Amount > originalAmount || request.Amount > candidateAllocatedAmount) throw new FixedAssetException("assets.source_allocation_correction_exceeds_amount");
            var correctionId = Guid.CreateVersion7();
            await using var insert = new NpgsqlCommand("insert into business_assets.asset_source_allocation_correction(tenant_id,id,allocation_id,candidate_id,asset_id,amount,reason,idempotency_key,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,now())", context.Connection, context.Transaction); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(correctionId); insert.Parameters.AddWithValue(request.AllocationId); insert.Parameters.AddWithValue(candidateId); insert.Parameters.AddWithValue(assetId); insert.Parameters.AddWithValue(request.Amount); insert.Parameters.AddWithValue(request.Reason.Trim()); insert.Parameters.AddWithValue(key); insert.Parameters.AddWithValue(actor); await insert.ExecuteNonQueryAsync(token);
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
            var assetId = Guid.CreateVersion7(); var assetNumber = $"FA-{DateTime.UtcNow:yyyyMMdd}-{assetId.ToString("N")[..8].ToUpperInvariant()}";
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
            await using var view = new NpgsqlCommand("select a.asset_number,a.name,a.lifecycle_state,coalesce(sum(s.amount),0),count(s.id),a.capitalization_date,a.placed_in_service_date from business_assets.fixed_asset a left join business_assets.asset_cip_cost_source s on s.tenant_id=a.tenant_id and s.asset_id=a.id where a.tenant_id=$1 and a.id=$2 group by a.asset_number,a.name,a.lifecycle_state,a.capitalization_date,a.placed_in_service_date", context.Connection, context.Transaction); view.Parameters.AddWithValue(tenant.Value); view.Parameters.AddWithValue(assetId); await using var viewReader = await view.ExecuteReaderAsync(token); if (!await viewReader.ReadAsync(token)) throw new FixedAssetException("assets.asset_not_found"); return new CipAssetView(assetId, viewReader.GetString(0), viewReader.GetString(1), viewReader.GetString(2), viewReader.GetDecimal(3), viewReader.GetInt32(4), viewReader.GetFieldValue<DateOnly>(5), viewReader.IsDBNull(6) ? null : viewReader.GetFieldValue<DateOnly>(6));
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
            await using var accumulatedCommand = new NpgsqlCommand("select coalesce(sum(amount),0) from business_assets.asset_book_transaction where tenant_id=$1 and asset_book_id=$2 and transaction_type='Depreciation'", context.Connection, context.Transaction); accumulatedCommand.Parameters.AddWithValue(tenant.Value); accumulatedCommand.Parameters.AddWithValue(request.BookId);
            var accumulated = (decimal)(await accumulatedCommand.ExecuteScalarAsync(token) ?? 0m);
            var newCost = previousCostValue + request.Amount;
            if (newCost < 0m || newCost < accumulated) throw new FixedAssetException("assets.book_cost_change_below_carrying_value");
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

    public async Task<DisposalView> DisposeAsync(TenantId tenant, Guid actor, Guid assetId, DisposeAssetRequest request, CancellationToken ct)
    {
        if (request.Proceeds < 0m || request.DirectCosts < 0m || string.IsNullOrWhiteSpace(request.Reason)) throw new FixedAssetException("assets.disposal_invalid");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var asset = new NpgsqlCommand("select legal_entity_id,asset_number,lifecycle_state from business_assets.fixed_asset where tenant_id=$1 and id=$2 for update", context.Connection, context.Transaction); asset.Parameters.AddWithValue(tenant.Value); asset.Parameters.AddWithValue(assetId);
            await using var assetReader = await asset.ExecuteReaderAsync(token);
            if (!await assetReader.ReadAsync(token)) throw new FixedAssetException("assets.asset_not_found");
            var legalEntityId = assetReader.GetGuid(0); var assetNumber = assetReader.GetString(1); var lifecycleState = assetReader.GetString(2); await assetReader.CloseAsync();
            if (lifecycleState is "Disposed" or "Draft") throw new FixedAssetException("assets.disposal_not_allowed");
            var disposalId = Guid.CreateVersion7();
            await using var disposal = new NpgsqlCommand("insert into business_assets.asset_disposal (tenant_id,id,asset_id,legal_entity_id,disposal_type,disposal_date,proceeds,direct_costs,reason,status,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Draft',$10,now())", context.Connection, context.Transaction);
            disposal.Parameters.AddWithValue(tenant.Value); disposal.Parameters.AddWithValue(disposalId); disposal.Parameters.AddWithValue(assetId); disposal.Parameters.AddWithValue(legalEntityId); disposal.Parameters.AddWithValue(request.DisposalType.Trim()); disposal.Parameters.AddWithValue(request.DisposalDate); disposal.Parameters.AddWithValue(request.Proceeds); disposal.Parameters.AddWithValue(request.DirectCosts); disposal.Parameters.AddWithValue(request.Reason.Trim()); disposal.Parameters.AddWithValue(actor);
            try { await disposal.ExecuteNonQueryAsync(token); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new FixedAssetException("assets.disposal_already_exists"); }
            await using var books = new NpgsqlCommand("""
                select b.id,b.original_cost,bd.posts_to_ledger,bd.currency_code,
                       coalesce(sum(case when t.transaction_type='Depreciation' then t.amount else 0 end),0)
                from business_assets.asset_book b
                join business_assets.asset_book_definition bd on bd.tenant_id=b.tenant_id and bd.id=b.book_definition_id
                left join business_assets.asset_book_transaction t on t.tenant_id=b.tenant_id and t.asset_book_id=b.id
                where b.tenant_id=$1 and b.asset_id=$2
                group by b.id,b.original_cost,bd.posts_to_ledger,bd.currency_code
                order by b.id
                """, context.Connection, context.Transaction); books.Parameters.AddWithValue(tenant.Value); books.Parameters.AddWithValue(assetId);
            await using var bookReader = await books.ExecuteReaderAsync(token); var bookRows = new List<(Guid BookId, decimal GrossCost, bool PostsToLedger, string Currency, decimal Accumulated)>(); while (await bookReader.ReadAsync(token)) bookRows.Add((bookReader.GetGuid(0), bookReader.GetDecimal(1), bookReader.GetBoolean(2), bookReader.GetString(3), bookReader.GetDecimal(4))); await bookReader.CloseAsync();
            var primary = bookRows.FirstOrDefault(); var primaryGross = primary.GrossCost; var primaryAccumulated = Math.Min(primary.Accumulated, primaryGross); var primaryNbv = primaryGross - primaryAccumulated; var primaryGainLoss = request.Proceeds - request.DirectCosts - primaryNbv;
            foreach (var book in bookRows)
            {
                var accumulated = Math.Min(book.Accumulated, book.GrossCost); var netBookValue = book.GrossCost - accumulated; var gainLoss = request.Proceeds - request.DirectCosts - netBookValue;
                Guid? ledgerFact = null;
                if (book.PostsToLedger)
                {
                    if (book.GrossCost <= 0m) throw new FixedAssetException("assets.disposal_book_value_invalid");
                    var factId = DeterministicGuid(assetId, book.BookId); var fact = new AccountingFactEnvelope<IAccountingFactPayload>(factId, tenant, new(legalEntityId), AccountingFactTypes.FixedAssetDisposalPosted, 1, "fixed-assets", $"disposal:{assetId:D}:book:{book.BookId:D}", $"fixed-assets:disposal:{assetId:D}:{book.BookId:D}", request.DisposalDate, request.DisposalDate, new FixedAssetDisposalPostedPayload(book.GrossCost, accumulated, netBookValue, request.Proceeds, request.DirectCosts, gainLoss, book.Currency, assetNumber), Guid.CreateVersion7(), actor);
                    var posting = await ledger.PostFixedAssetDisposalAsync(context, fact, (FixedAssetDisposalPostedPayload)fact.Payload, token); ledgerFact = posting.FactId;
                }
                await using var transaction = new NpgsqlCommand("insert into business_assets.asset_book_transaction(tenant_id,id,asset_book_id,transaction_type,transaction_date,amount,accumulated_depreciation,idempotency_key,ledger_fact_reference,created_by,created_at) values($1,$2,$3,'Disposal',$4,$5,$6,$7,$8,$9,now())", context.Connection, context.Transaction); transaction.Parameters.AddWithValue(tenant.Value); transaction.Parameters.AddWithValue(Guid.CreateVersion7()); transaction.Parameters.AddWithValue(book.BookId); transaction.Parameters.AddWithValue(request.DisposalDate); transaction.Parameters.AddWithValue(book.GrossCost); transaction.Parameters.AddWithValue(accumulated); transaction.Parameters.AddWithValue($"disposal:{assetId:D}:{book.BookId:D}"); transaction.Parameters.AddWithValue((object?)ledgerFact?.ToString("D") ?? DBNull.Value); transaction.Parameters.AddWithValue(actor); await transaction.ExecuteNonQueryAsync(token);
            }
            await using var updateDisposal = new NpgsqlCommand("update business_assets.asset_disposal set status='Posted' where tenant_id=$1 and id=$2", context.Connection, context.Transaction); updateDisposal.Parameters.AddWithValue(tenant.Value); updateDisposal.Parameters.AddWithValue(disposalId); await updateDisposal.ExecuteNonQueryAsync(token);
            await using var updateAsset = new NpgsqlCommand("update business_assets.fixed_asset set lifecycle_state='Disposed',row_version=row_version+1,updated_by=$1,updated_at=now() where tenant_id=$2 and id=$3", context.Connection, context.Transaction); updateAsset.Parameters.AddWithValue(actor); updateAsset.Parameters.AddWithValue(tenant.Value); updateAsset.Parameters.AddWithValue(assetId); await updateAsset.ExecuteNonQueryAsync(token);
            return new DisposalView(assetId, request.DisposalType.Trim(), request.DisposalDate, primaryGross, primaryAccumulated, primaryNbv, request.Proceeds, request.DirectCosts, primaryGainLoss, "Posted");
        }, ct);
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, CancellationToken ct) { await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct); }
    private static Guid DeterministicGuid(Guid first, Guid second) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"fixed-assets:{first:D}:{second:D}"))[..16]);
    private static Guid StableGuid(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"fixed-assets:key:{value}"))[..16]);
    private static JsonElement ReadJsonElement(NpgsqlDataReader reader, int ordinal) => JsonDocument.Parse(reader.GetString(ordinal)).RootElement.Clone();
    private static async Task<IReadOnlyList<T>> ReadAsync<T>(NpgsqlCommand command, Func<NpgsqlDataReader,T> map, CancellationToken ct) { var list = new List<T>(); await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct); while (await reader.ReadAsync(ct)) list.Add(map(reader)); return list; }
}

public sealed class FixedAssetException(string code) : Exception(code) { public string Code { get; } = code; }
