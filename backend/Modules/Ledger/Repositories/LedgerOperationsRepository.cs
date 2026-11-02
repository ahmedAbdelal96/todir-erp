using System.Text;
using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Ledger.DTOs;
using Modules.Ledger.Entities;
using Npgsql;
using Shared.Contracts.Accounting;
using Shared.Contracts.Ledger;
using Shared.Primitives.Identifiers;

namespace Modules.Ledger.Repositories;

/// <summary>Ledger-owned application workflows and bounded accountant read models.</summary>
public sealed class LedgerOperationsRepository(
    NpgsqlDataSource dataSource,
    ITransactionCoordinator transactions,
    LedgerRepository posting,
    TimeProvider clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Produces the Accounting composition contract in Ledger, where journal facts
    /// are authoritative. Opening and period movements are aggregated in one set
    /// query so Accounting never reconstructs balances from its own tables.
    /// </summary>
    public async Task<IReadOnlyList<LedgerAccountBalance>> GetAccountingTrialBalanceAsync(TenantId tenant, LegalEntityId entity, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        const string sql = """
            with movements as (
                select l.account_id,
                       coalesce(sum(l.base_debit) filter (where $3::date is not null and e.posting_date < $3::date), 0) as opening_debit,
                       coalesce(sum(l.base_credit) filter (where $3::date is not null and e.posting_date < $3::date), 0) as opening_credit,
                       coalesce(sum(l.base_debit) filter (where ($3::date is null or e.posting_date >= $3::date) and ($4::date is null or e.posting_date <= $4::date)), 0) as period_debit,
                       coalesce(sum(l.base_credit) filter (where ($3::date is null or e.posting_date >= $3::date) and ($4::date is null or e.posting_date <= $4::date)), 0) as period_credit,
                       max(e.base_currency_code) as currency_code
                from finance_ledger.journal_entry e
                join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id
                where e.tenant_id=$1 and e.legal_entity_id=$2 and ($4::date is null or e.posting_date <= $4::date)
                group by l.account_id
            )
            select a.id,a.code,a.name_en,a.account_type,a.subtype,
                   coalesce(m.opening_debit,0),coalesce(m.opening_credit,0),coalesce(m.period_debit,0),coalesce(m.period_credit,0),
                   coalesce(m.currency_code,'')
            from finance_ledger.account a
            left join movements m on m.account_id=a.id
            where a.tenant_id=$1 and a.legal_entity_id=$2
              and (m.account_id is not null or a.status='Active')
            order by a.code
            """;
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value);
        command.Parameters.AddWithValue((object?)from ?? DBNull.Value); command.Parameters.AddWithValue((object?)to ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<LedgerAccountBalance>();
        while (await reader.ReadAsync(ct))
        {
            var openingDebit = reader.GetDecimal(5); var openingCredit = reader.GetDecimal(6);
            var periodDebit = reader.GetDecimal(7); var periodCredit = reader.GetDecimal(8);
            var signed = openingDebit - openingCredit + periodDebit - periodCredit;
            result.Add(new LedgerAccountBalance(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                openingDebit, openingCredit, periodDebit, periodCredit, Math.Max(signed, 0m), Math.Max(-signed, 0m), signed, reader.GetString(9)));
        }
        return result;
    }

    public Task<LedgerPostingResult> ReverseAsync(TenantId tenant, LegalEntityId entity, Guid entryId, ReverseEntryRequest request, Guid actor, Guid correlation, CancellationToken ct) => posting.ReverseAsync(tenant, entity, entryId, request.ReversalDate, request.AutoReversalDate, request.Reason, actor, correlation, ct);

    public Task<GeneralJournalDraftDto> CreateGeneralJournalDraftAsync(TenantId tenant, LegalEntityId entity, CreateGeneralJournalDraftRequest request, Guid actor, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            if (request.Lines.Count == 0) throw new LedgerDomainException("ledger.entry_unbalanced", "A general journal requires at least one line.");
            var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
            await ExecuteAsync(context, "insert into finance_ledger.general_journal_draft (tenant_id,legal_entity_id,id,journal_id,posting_date,description,base_currency_code,status,idempotency_key,created_by,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,'Draft',$8,$9,$10,$10)", token, tenant.Value, entity.Value, id, request.JournalId, request.PostingDate, request.Description.Trim(), request.BaseCurrencyCode.Trim().ToUpperInvariant(), request.IdempotencyKey.Trim(), actor, now);
            var sequence = 1; foreach (var line in request.Lines)
            {
                ValidateDraftLine(line);
                var lineId = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into finance_ledger.general_journal_draft_line (tenant_id,legal_entity_id,id,draft_id,sequence_no,account_id,debit,credit,transaction_currency_code,transaction_debit,transaction_credit,base_debit,base_credit,exchange_rate,description) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)", token, tenant.Value, entity.Value, lineId, id, sequence++, line.AccountId, line.Debit, line.Credit, line.TransactionCurrencyCode.Trim().ToUpperInvariant(), line.TransactionDebit, line.TransactionCredit, line.BaseDebit, line.BaseCredit, line.ExchangeRate, (object?)line.Description ?? DBNull.Value);
                foreach (var valueId in line.DimensionValueIds ?? [])
                {
                    var definitionId = await ScalarGuidAsync(context, "select dimension_definition_id from finance_ledger.dimension_value where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Active'", token, tenant.Value, entity.Value, valueId) ?? throw new LedgerDomainException("ledger.dimension_invalid", "The draft references an invalid dimension value.");
                    await ExecuteAsync(context, "insert into finance_ledger.general_journal_draft_line_dimension (tenant_id,legal_entity_id,draft_line_id,dimension_definition_id,dimension_value_id) values ($1,$2,$3,$4,$5)", token, tenant.Value, entity.Value, lineId, definitionId, valueId);
                }
            }
            await AuditAsync(context, tenant, entity, actor, "general_journal.created", id, request, null, token);
            return new GeneralJournalDraftDto(id, entity.Value, request.JournalId, request.PostingDate, request.Description.Trim(), request.BaseCurrencyCode.Trim().ToUpperInvariant(), GeneralJournalDraftStatus.Draft, request.Lines);
        }, ct);

    public Task<GeneralJournalDraftDto> SubmitGeneralJournalAsync(TenantId tenant, LegalEntityId entity, Guid draftId, Guid actor, Guid correlation, CancellationToken ct) => TransitionDraftAsync(tenant, entity, draftId, actor, "Draft", "Submitted", "general_journal.submitted", ct);
    public Task<GeneralJournalDraftDto> ApproveGeneralJournalAsync(TenantId tenant, LegalEntityId entity, Guid draftId, Guid actor, Guid correlation, CancellationToken ct) => TransitionDraftAsync(tenant, entity, draftId, actor, "Submitted", "Approved", "general_journal.approved", ct);
    public Task<GeneralJournalDraftDto> UpdateGeneralJournalDraftAsync(TenantId tenant, LegalEntityId entity, Guid draftId, CreateGeneralJournalDraftRequest request, Guid actor, CancellationToken ct) => transactions.ExecuteAsync(tenant, async (context, token) => { var draft = await LoadDraftAsync(context, tenant, entity, draftId, token); if (draft is null || draft.Value.Status != "Draft") throw new LedgerDomainException("ledger.general_journal_not_editable", "Only a draft general journal can be edited."); if (request.Lines.Count == 0) throw new LedgerDomainException("ledger.entry_unbalanced", "A general journal requires at least one line."); await ExecuteAsync(context, "update finance_ledger.general_journal_draft set journal_id=$4,posting_date=$5,description=$6,base_currency_code=$7,idempotency_key=$8,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3", token, tenant.Value, entity.Value, draftId, request.JournalId, request.PostingDate, request.Description.Trim(), request.BaseCurrencyCode.Trim().ToUpperInvariant(), request.IdempotencyKey.Trim()); await ExecuteAsync(context, "delete from finance_ledger.general_journal_draft_line_dimension where tenant_id=$1 and legal_entity_id=$2 and draft_line_id in (select id from finance_ledger.general_journal_draft_line where tenant_id=$1 and legal_entity_id=$2 and draft_id=$3)", token, tenant.Value, entity.Value, draftId); await ExecuteAsync(context, "delete from finance_ledger.general_journal_draft_line where tenant_id=$1 and legal_entity_id=$2 and draft_id=$3", token, tenant.Value, entity.Value, draftId); var sequence = 1; foreach (var line in request.Lines) { ValidateDraftLine(line); var lineId = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into finance_ledger.general_journal_draft_line (tenant_id,legal_entity_id,id,draft_id,sequence_no,account_id,debit,credit,transaction_currency_code,transaction_debit,transaction_credit,base_debit,base_credit,exchange_rate,description) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)", token, tenant.Value, entity.Value, lineId, draftId, sequence++, line.AccountId, line.Debit, line.Credit, line.TransactionCurrencyCode.Trim().ToUpperInvariant(), line.TransactionDebit, line.TransactionCredit, line.BaseDebit, line.BaseCredit, line.ExchangeRate, (object?)line.Description ?? DBNull.Value); foreach (var valueId in line.DimensionValueIds ?? []) { var definitionId = await ScalarGuidAsync(context, "select dimension_definition_id from finance_ledger.dimension_value where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Active'", token, tenant.Value, entity.Value, valueId) ?? throw new LedgerDomainException("ledger.dimension_invalid", "The draft references an invalid dimension value."); await ExecuteAsync(context, "insert into finance_ledger.general_journal_draft_line_dimension (tenant_id,legal_entity_id,draft_line_id,dimension_definition_id,dimension_value_id) values ($1,$2,$3,$4,$5)", token, tenant.Value, entity.Value, lineId, definitionId, valueId); } } await AuditAsync(context, tenant, entity, actor, "general_journal.edited", draftId, null, request, token); var updated = await LoadDraftAsync(context, tenant, entity, draftId, token) ?? throw new LedgerDomainException("ledger.general_journal_not_found", "The general journal draft was not found."); var dtoLines = await LoadDraftLineRequestsAsync(context, tenant, entity, draftId, token); return new GeneralJournalDraftDto(draftId, entity.Value, updated.JournalId, updated.PostingDate, updated.Description, updated.BaseCurrency, GeneralJournalDraftStatus.Draft, dtoLines); }, ct);

    public Task<LedgerPostingResult> PostGeneralJournalAsync(TenantId tenant, LegalEntityId entity, Guid draftId, Guid actor, Guid correlation, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var draft = await LoadDraftAsync(context, tenant, entity, draftId, token); if (draft is null) throw new LedgerDomainException("ledger.general_journal_not_found", "The general journal draft was not found."); if (draft.Value.Status is not ("Approved" or "Submitted")) throw new LedgerDomainException("ledger.general_journal_not_postable", "The general journal must be submitted or approved before posting."); if (draft.Value.CreatedBy == actor && draft.Value.Status == "Approved") throw new LedgerDomainException("ledger.sod_violation", "The preparer cannot approve and post the same approved journal.");
            var lines = await LoadDraftLinesAsync(context, tenant, entity, draftId, token); LedgerDomainRules.EnsureBalanced(lines.Sum(x => x.BaseDebit), lines.Sum(x => x.BaseCredit));
            var dimensions = new List<LedgerPostingLinePayload>(); foreach (var line in lines) dimensions.Add(new(line.AccountId, line.Debit, line.Credit, line.Currency, line.TransactionDebit, line.TransactionCredit, line.BaseDebit, line.BaseCredit, line.ExchangeRate, await LoadDimensionsAsync(context, tenant, entity, line.Id, token), line.Description, null));
            var fact = new AccountingFactEnvelope<IAccountingFactPayload>(Guid.CreateVersion7(), tenant, entity, AccountingFactTypes.ManualGeneralJournalPosted, 1, "ledger", $"general-journal:{draftId:N}", draft.Value.IdempotencyKey, draft.Value.PostingDate, draft.Value.PostingDate, new ExplicitLedgerPostingPayload(draft.Value.JournalId, draft.Value.Description, draft.Value.BaseCurrency, dimensions), correlation, actor);
            var result = await posting.PostExplicitInTransactionAsync(context, fact, (ExplicitLedgerPostingPayload)fact.Payload, cancellationToken: token);
            await ExecuteAsync(context, "update finance_ledger.general_journal_draft set status='Posted',posted_by=$4,posted_entry_id=$3,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$5 and status in ('Submitted','Approved')", token, tenant.Value, entity.Value, result.JournalEntryId, actor, draftId);
            await AuditAsync(context, tenant, entity, actor, "general_journal.posted", draftId, null, result, token); return result;
        }, ct);

    public Task<Guid> CreateOpeningBatchAsync(TenantId tenant, LegalEntityId entity, CreateOpeningBalanceBatchRequest request, Guid actor, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            if (request.Lines.Count == 0) throw new LedgerDomainException("ledger.opening_empty", "An opening balance batch cannot be empty."); var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
            await ExecuteAsync(context, "insert into finance_ledger.opening_balance_batch (tenant_id,legal_entity_id,id,batch_identity,file_hash,legacy_system,go_live_date,status,created_by,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,'Draft',$8,$9,$9)", token, tenant.Value, entity.Value, id, request.BatchIdentity.Trim(), request.FileHash.Trim().ToLowerInvariant(), (object?)request.LegacySystem ?? DBNull.Value, request.GoLiveDate, actor, now);
            var sequence = 1; foreach (var line in request.Lines) { if (line.BaseDebit < 0 || line.BaseCredit < 0 || line.BaseDebit > 0 && line.BaseCredit > 0 || line.BaseDebit == 0 && line.BaseCredit == 0) throw new LedgerDomainException("ledger.opening_invalid", "Opening lines must have one positive side."); await ExecuteAsync(context, "insert into finance_ledger.opening_balance_line (tenant_id,legal_entity_id,id,batch_id,sequence_no,account_id,debit,credit,currency_code,base_debit,base_credit,exchange_rate,legacy_account_code,legacy_reference) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14)", token, tenant.Value, entity.Value, Guid.CreateVersion7(), id, sequence++, line.AccountId, line.Debit, line.Credit, line.CurrencyCode.Trim().ToUpperInvariant(), line.BaseDebit, line.BaseCredit, line.ExchangeRate, (object?)line.LegacyAccountCode ?? DBNull.Value, (object?)line.LegacyReference ?? DBNull.Value); }
            await AuditAsync(context, tenant, entity, actor, "opening_batch.created", id, request, null, token); return id;
        }, ct);

    public Task<OpeningBalancePreviewDto> PreviewOpeningBatchAsync(TenantId tenant, LegalEntityId entity, Guid batchId, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var lines = await LoadOpeningLinesAsync(context, tenant, entity, batchId, token); if (lines.Count == 0) throw new LedgerDomainException("ledger.opening_not_found", "The opening batch was not found or has no lines."); var totalDebit = lines.Sum(x => x.BaseDebit); var totalCredit = lines.Sum(x => x.BaseCredit); var errors = new List<string>(); if (totalDebit != totalCredit) errors.Add("ledger.opening_unbalanced"); if (lines.Any(x => x.AccountStatus != "Active" || !x.AllowDirectPosting)) errors.Add("ledger.account_invalid"); return new OpeningBalancePreviewDto(batchId, errors.Count == 0, totalDebit, totalCredit, errors, lines.Select(x => x.Request).ToArray());
        }, ct);

    public Task<LedgerPostingResult> PostOpeningBatchAsync(TenantId tenant, LegalEntityId entity, Guid batchId, Guid actor, Guid correlation, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var batch = await LoadOpeningBatchAsync(context, tenant, entity, batchId, token); if (batch is null) throw new LedgerDomainException("ledger.opening_not_found", "The opening batch was not found."); if (batch.Value.Status is "Posted" or "Locked") throw new LedgerDomainException("ledger.opening_already_posted", "The opening batch is already posted."); var lines = await LoadOpeningLinesAsync(context, tenant, entity, batchId, token); var totalDebit = lines.Sum(x => x.BaseDebit); var totalCredit = lines.Sum(x => x.BaseCredit); LedgerDomainRules.EnsureBalanced(totalDebit, totalCredit); var journalId = await ScalarGuidAsync(context, "select id from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2 and code='OPENING' and status='Active'", token, tenant.Value, entity.Value) ?? throw new LedgerDomainException("ledger.journal_missing", "Opening journal is not configured."); var baseCurrency = await ScalarStringAsync(context, "select base_currency_code from platform.legal_entity where tenant_id=$1 and id=$2", token, tenant.Value, entity.Value) ?? throw new LedgerDomainException("ledger.entity_missing", "Legal entity financial context is unavailable."); var payload = new ExplicitLedgerPostingPayload(journalId, $"Opening balance {batch.Value.BatchIdentity}", baseCurrency, lines.Select(x => new LedgerPostingLinePayload(x.AccountId, x.Debit, x.Credit, x.Currency, x.Debit, x.Credit, x.BaseDebit, x.BaseCredit, x.ExchangeRate, [], x.LegacyReference, x.LegacyReference)).ToArray(), batch.Value.LegacySystem, batch.Value.BatchIdentity, batch.Value.FileHash); var fact = new AccountingFactEnvelope<IAccountingFactPayload>(Guid.CreateVersion7(), tenant, entity, AccountingFactTypes.OpeningBalancePosted, 1, "ledger", $"opening:{batchId:N}", $"opening:{batch.Value.BatchIdentity}", batch.Value.GoLiveDate, batch.Value.GoLiveDate, payload, correlation, actor); var result = await posting.PostExplicitInTransactionAsync(context, fact, payload, cancellationToken: token); await ExecuteAsync(context, "update finance_ledger.opening_balance_batch set status='Posted',posted_entry_id=$3,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$4 and status not in ('Posted','Locked')", token, tenant.Value, entity.Value, result.JournalEntryId, batchId); await AuditAsync(context, tenant, entity, actor, "opening_batch.posted", batchId, null, result, token); return result;
        }, ct);

    public async Task<LedgerOpeningBatchSnapshot?> GetOpeningBatchSnapshotAsync(TenantId tenant, LegalEntityId entity, Guid batchId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,go_live_date,status,posted_entry_id from finance_ledger.opening_balance_batch where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(batchId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new LedgerOpeningBatchSnapshot(reader.GetGuid(0), DateOnly.FromDateTime(reader.GetDateTime(1)), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetGuid(3)) : null;
    }

    public async Task LockOpeningBatchAsync(TenantId tenant, LegalEntityId entity, Guid batchId, Guid actor, Guid correlation, CancellationToken ct)
    {
        await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("update finance_ledger.opening_balance_batch set status='Locked',updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Posted'", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(batchId);
            var changed = await command.ExecuteNonQueryAsync(token);
            if (changed == 0)
            {
                await using var current = new NpgsqlCommand("select status from finance_ledger.opening_balance_batch where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction);
                current.Parameters.AddWithValue(tenant.Value); current.Parameters.AddWithValue(entity.Value); current.Parameters.AddWithValue(batchId);
                if (await current.ExecuteScalarAsync(token) is not "Locked") throw new LedgerDomainException("ledger.opening_not_posted", "Only a posted opening batch can be locked for go-live.");
            }
            await AuditAsync(context, tenant, entity, actor, "opening_batch.locked", batchId, null, new { status = "Locked" }, token);
            return 0;
        }, ct);
    }

    public Task<Guid> CreateDimensionDefinitionAsync(TenantId tenant, LegalEntityId entity, DimensionDefinitionRequest request, Guid actor, CancellationToken ct) => transactions.ExecuteAsync(tenant, async (context, token) => { var id = Guid.CreateVersion7(); var now = clock.GetUtcNow(); await ExecuteAsync(context, "insert into finance_ledger.dimension_definition (tenant_id,legal_entity_id,id,code,name_ar,name_en,value_owner_module,status,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,'Active',$8,$8)", token, tenant.Value, entity.Value, id, request.Code.Trim(), request.NameAr.Trim(), request.NameEn.Trim(), request.ValueOwnerModule.Trim(), now); await AuditAsync(context, tenant, entity, actor, "dimension.created", id, null, request, token); return id; }, ct);
    public Task<Guid> CreateDimensionValueAsync(TenantId tenant, LegalEntityId entity, DimensionValueRequest request, Guid actor, CancellationToken ct) => transactions.ExecuteAsync(tenant, async (context, token) => { var id = Guid.CreateVersion7(); var now = clock.GetUtcNow(); await ExecuteAsync(context, "insert into finance_ledger.dimension_value (tenant_id,legal_entity_id,id,dimension_definition_id,code,name_ar,name_en,external_owner_module,external_reference_id,status,created_at,updated_at) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,'Active',$10,$10)", token, tenant.Value, entity.Value, id, request.DefinitionId, request.Code.Trim(), request.NameAr.Trim(), request.NameEn.Trim(), (object?)request.ExternalOwnerModule ?? DBNull.Value, (object?)request.ExternalReferenceId ?? DBNull.Value, now); await AuditAsync(context, tenant, entity, actor, "dimension_value.created", id, null, request, token); return id; }, ct);
    public Task<Guid> CreateDimensionPolicyAsync(TenantId tenant, LegalEntityId entity, DimensionPolicyRequest request, Guid actor, CancellationToken ct) => transactions.ExecuteAsync(tenant, async (context, token) => { if (request.ScopeType is not ("Account" or "AccountType" or "Journal" or "FactType") || request.Requirement is not ("Required" or "Optional" or "Forbidden")) throw new LedgerDomainException("ledger.dimension_invalid", "The dimension policy is not valid."); var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into finance_ledger.dimension_policy (tenant_id,legal_entity_id,id,dimension_definition_id,scope_type,scope_reference,requirement,created_at) values ($1,$2,$3,$4,$5,$6,$7,now())", token, tenant.Value, entity.Value, id, request.DefinitionId, request.ScopeType, request.ScopeReference.Trim(), request.Requirement); await AuditAsync(context, tenant, entity, actor, "dimension_policy.created", id, null, request, token); return id; }, ct);
    public Task<ProjectionReconciliationResultDto> ReconcileBalancesAsync(TenantId tenant, LegalEntityId entity, Guid? fiscalYearId, Guid? periodId, CancellationToken ct) => transactions.ExecuteAsync(tenant, async (context, token) => await ReconcileInTransactionAsync(context, tenant, entity, fiscalYearId, periodId, token), ct);
    public Task<ProjectionReconciliationResultDto> ReconcileBalancesAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => ReconcileBalancesAsync(tenant, entity, null, null, ct);
    public Task<ProjectionRebuildResult> RebuildBalancesAsync(TenantId tenant, LegalEntityId entity, Guid? fiscalYearId, Guid? periodId, CancellationToken ct) => transactions.ExecuteAsync(tenant, async (context, token) => { await ExecuteCountAsync(context, "delete from finance_ledger.account_period_balance where tenant_id=$1 and legal_entity_id=$2 and ($3::uuid is null or fiscal_year_id=$3) and ($4::uuid is null or accounting_period_id=$4)", token, tenant.Value, entity.Value, (object?)fiscalYearId ?? DBNull.Value, (object?)periodId ?? DBNull.Value); var inserted = await ExecuteCountAsync(context, "insert into finance_ledger.account_period_balance (tenant_id,legal_entity_id,account_id,fiscal_year_id,accounting_period_id,dimension_key,currency_code,opening_debit,opening_credit,period_debit,period_credit,closing_balance,updated_at) select e.tenant_id,e.legal_entity_id,l.account_id,p.fiscal_year_id,e.accounting_period_id,'',e.base_currency_code,coalesce(sum(case when e.fact_type='OpeningBalancePosted' then l.base_debit else 0 end),0),coalesce(sum(case when e.fact_type='OpeningBalancePosted' then l.base_credit else 0 end),0),coalesce(sum(case when e.fact_type<>'OpeningBalancePosted' then l.base_debit else 0 end),0),coalesce(sum(case when e.fact_type<>'OpeningBalancePosted' then l.base_credit else 0 end),0),coalesce(sum(l.base_debit-l.base_credit),0),now() from finance_ledger.journal_entry e join finance_ledger.accounting_period p on p.tenant_id=e.tenant_id and p.legal_entity_id=e.legal_entity_id and p.id=e.accounting_period_id join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id where e.tenant_id=$1 and e.legal_entity_id=$2 and ($3::uuid is null or p.fiscal_year_id=$3) and ($4::uuid is null or e.accounting_period_id=$4) group by e.tenant_id,e.legal_entity_id,l.account_id,p.fiscal_year_id,e.accounting_period_id,e.base_currency_code", token, tenant.Value, entity.Value, (object?)fiscalYearId ?? DBNull.Value, (object?)periodId ?? DBNull.Value); var reconciliation = await ReconcileInTransactionAsync(context, tenant, entity, fiscalYearId, periodId, token); return new ProjectionRebuildResult(inserted, reconciliation.Healthy); }, ct);
    public Task<ProjectionRebuildResult> RebuildBalancesAsync(TenantId tenant, LegalEntityId entity, CancellationToken ct) => RebuildBalancesAsync(tenant, entity, null, null, ct);

    public async Task<EntryExplanationDto> ExplainEntryAsync(TenantId tenant, LegalEntityId entity, Guid entryId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        const string headerSql = "select e.id,e.entry_number,e.journal_id,e.fact_id,e.posting_date,p.code,e.fact_type,e.fact_version,e.posting_rule_version_id,e.accounting_configuration_version_id,e.base_currency_code,e.source_module,e.source_reference from finance_ledger.journal_entry e join finance_ledger.accounting_period p on p.tenant_id=e.tenant_id and p.legal_entity_id=e.legal_entity_id and p.id=e.accounting_period_id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.id=$3";
        await using var header = new NpgsqlCommand(headerSql, connection); header.Parameters.AddWithValue(tenant.Value); header.Parameters.AddWithValue(entity.Value); header.Parameters.AddWithValue(entryId);
        await using var reader = await header.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new LedgerDomainException("ledger.entry_not_found", "The journal entry was not found.");
        var result = new EntryExplanationDto(reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), DateOnly.FromDateTime(reader.GetDateTime(4)), reader.GetString(5), reader.GetString(6), reader.GetInt32(7), reader.GetGuid(8), reader.GetGuid(9), reader.GetString(10), reader.GetString(11), reader.GetString(12), []);
        await reader.CloseAsync();
        const string lineSql = "select l.id,l.sequence_no,coalesce(l.account_role,l.semantic_role),l.account_id,a.code,a.name_en,l.account_mapping_id,l.account_mapping_version_id,m.code,v.version_number,l.debit,l.credit,l.currency_code,coalesce(l.transaction_debit,l.debit),coalesce(l.transaction_credit,l.credit),coalesce(l.base_debit,l.debit),coalesce(l.base_credit,l.credit),l.exchange_rate,coalesce('Mapping '||m.code||' v'||v.version_number||' — '||coalesce(l.determination_context->>'specificity',''), 'Manual or explicit Ledger instruction') from finance_ledger.journal_entry_line l join finance_ledger.account a on a.tenant_id=l.tenant_id and a.legal_entity_id=l.legal_entity_id and a.id=l.account_id left join finance_ledger.account_mapping m on m.tenant_id=l.tenant_id and m.legal_entity_id=l.legal_entity_id and m.id=l.account_mapping_id left join finance_ledger.account_mapping_version v on v.tenant_id=l.tenant_id and v.legal_entity_id=l.legal_entity_id and v.id=l.account_mapping_version_id where l.tenant_id=$1 and l.legal_entity_id=$2 and l.journal_entry_id=$3 order by l.sequence_no";
        await using var lines = new NpgsqlCommand(lineSql, connection); lines.Parameters.AddWithValue(tenant.Value); lines.Parameters.AddWithValue(entity.Value); lines.Parameters.AddWithValue(entryId);
        await using var lineReader = await lines.ExecuteReaderAsync(ct);
        var explanations = new List<EntryExplanationLineDto>();
        while (await lineReader.ReadAsync(ct)) explanations.Add(new(lineReader.GetGuid(0), lineReader.GetInt32(1), lineReader.GetString(2), lineReader.GetGuid(3), lineReader.GetString(4), lineReader.GetString(5), lineReader.IsDBNull(6) ? null : lineReader.GetGuid(6), lineReader.IsDBNull(9) ? null : lineReader.GetInt64(9), lineReader.IsDBNull(7) ? null : lineReader.GetGuid(7), lineReader.GetDecimal(10), lineReader.GetDecimal(11), lineReader.GetString(12), lineReader.GetDecimal(13), lineReader.GetDecimal(14), lineReader.GetDecimal(15), lineReader.GetDecimal(16), lineReader.IsDBNull(17) ? null : lineReader.GetDecimal(17), lineReader.IsDBNull(18) ? "Manual or explicit Ledger instruction" : lineReader.GetString(18), []));
        await lineReader.CloseAsync();
        var enriched = new List<EntryExplanationLineDto>(explanations.Count);
        foreach (var line in explanations)
        {
            await using var dimension = new NpgsqlCommand("select d.code,v.id,v.code from finance_ledger.journal_line_dimension x join finance_ledger.dimension_definition d on d.tenant_id=x.tenant_id and d.legal_entity_id=x.legal_entity_id and d.id=x.dimension_definition_id join finance_ledger.dimension_value v on v.tenant_id=x.tenant_id and v.legal_entity_id=x.legal_entity_id and v.id=x.dimension_value_id where x.tenant_id=$1 and x.legal_entity_id=$2 and x.journal_entry_line_id=$3", connection); dimension.Parameters.AddWithValue(tenant.Value); dimension.Parameters.AddWithValue(entity.Value); dimension.Parameters.AddWithValue(line.LineId); await using var dimensionReader = await dimension.ExecuteReaderAsync(ct); var dimensions = new List<EntryExplanationDimensionDto>(); while (await dimensionReader.ReadAsync(ct)) dimensions.Add(new(dimensionReader.GetString(0), dimensionReader.GetGuid(1), dimensionReader.GetString(2))); enriched.Add(line with { Dimensions = dimensions });
        }
        return result with { Lines = enriched };
    }

    public async Task<IReadOnlyList<TrialBalanceRowDto>> GetTrialBalanceAsync(TenantId tenant, LegalEntityId entity, DateOnly? from, DateOnly? to, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 500); await using var connection = await OpenAsync(tenant, ct); const string sql = "select a.id,a.code,a.name_en,coalesce(sum(l.base_debit),0),coalesce(sum(l.base_credit),0),coalesce(sum(l.base_debit-l.base_credit),0),e.base_currency_code from finance_ledger.journal_entry e join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id join finance_ledger.account a on a.tenant_id=l.tenant_id and a.legal_entity_id=l.legal_entity_id and a.id=l.account_id where e.tenant_id=$1 and e.legal_entity_id=$2 and ($3::date is null or e.posting_date >= $3) and ($4::date is null or e.posting_date <= $4) group by a.id,a.code,a.name_en,e.base_currency_code order by a.code limit $5"; await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue((object?)from ?? DBNull.Value); command.Parameters.AddWithValue((object?)to ?? DBNull.Value); command.Parameters.AddWithValue(limit); await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<TrialBalanceRowDto>(); while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetString(6))); return result;
    }

    public async Task<TrialBalancePageDto> GetTrialBalancePageAsync(TenantId tenant, LegalEntityId entity, DateOnly? from, DateOnly? to,
        string? search, string? cursor, int limit, CancellationToken ct, string? sort = null, int? page = null)
    {
        limit = Math.Clamp(limit, 1, 200);
        var sortKey = sort?.Trim() switch
        {
            "accountNameAsc" => "accountNameAsc",
            "accountCodeAsc" or null or "" => "accountCodeAsc",
            _ => throw new LedgerDomainException("ledger.sort_invalid", "The trial-balance sort is invalid.")
        };
        var sortExpression = sortKey == "accountNameAsc" ? "name_en,id" : "code,id";
        string? cursorCode = null; Guid? cursorId = null;
        if (page is not null)
        {
            var pageNumber = Math.Max(1, page.Value); var offset = (pageNumber - 1) * limit;
            await using var offsetConnection = await OpenAsync(tenant, ct);
            const string groupedOffset = "select a.id,a.code,a.name_en,coalesce(sum(l.base_debit),0) debit,coalesce(sum(l.base_credit),0) credit,coalesce(sum(l.base_debit-l.base_credit),0) balance,max(e.base_currency_code) currency_code from finance_ledger.journal_entry e join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id join finance_ledger.account a on a.tenant_id=l.tenant_id and a.legal_entity_id=l.legal_entity_id and a.id=l.account_id where e.tenant_id=$1 and e.legal_entity_id=$2 and ($3::date is null or e.posting_date >= $3) and ($4::date is null or e.posting_date <= $4) and ($5::text is null or (a.code ilike $5 || '%' or a.name_en ilike $5 || '%')) group by a.id,a.code,a.name_en";
            await using var summary = new NpgsqlCommand($"select count(*)::int,coalesce(sum(debit),0),coalesce(sum(credit),0) from ({groupedOffset}) grouped", offsetConnection);
            Add(summary, tenant.Value, entity.Value, (object?)from ?? DBNull.Value, (object?)to ?? DBNull.Value, (object?)CleanCursorText(search) ?? DBNull.Value);
            await using var summaryReader = await summary.ExecuteReaderAsync(ct);
            await summaryReader.ReadAsync(ct);
            var totalCount = summaryReader.GetInt32(0); var totalDebit = summaryReader.GetDecimal(1); var totalCredit = summaryReader.GetDecimal(2);
            await summaryReader.CloseAsync();
            var offsetSql = $"""
                with grouped as (
                    select a.id,a.code,a.name_en,coalesce(sum(l.base_debit),0) debit,coalesce(sum(l.base_credit),0) credit,coalesce(sum(l.base_debit-l.base_credit),0) balance,max(e.base_currency_code) currency_code
                    from finance_ledger.journal_entry e join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id
                    join finance_ledger.account a on a.tenant_id=l.tenant_id and a.legal_entity_id=l.legal_entity_id and a.id=l.account_id
                    where e.tenant_id=$1 and e.legal_entity_id=$2 and ($3::date is null or e.posting_date >= $3) and ($4::date is null or e.posting_date <= $4) and ($5::text is null or (a.code ilike $5 || '%' or a.name_en ilike $5 || '%'))
                    group by a.id,a.code,a.name_en
                )
                select id,code,name_en,debit,credit,balance,currency_code
                from grouped order by {sortExpression} offset $6 limit $7
                """;
            await using var command = new NpgsqlCommand(offsetSql, offsetConnection); Add(command, tenant.Value, entity.Value, (object?)from ?? DBNull.Value, (object?)to ?? DBNull.Value, (object?)CleanCursorText(search) ?? DBNull.Value, offset, limit);
            await using var reader = await command.ExecuteReaderAsync(ct); var rows = new List<TrialBalanceRowDto>();
            while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetString(6)));
            return new TrialBalancePageDto(rows, null, totalCount, totalDebit, totalCredit);
        }
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            string[] parts;
            try { parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|'); }
            catch (FormatException) { throw new LedgerDomainException("ledger.cursor_invalid", "The trial-balance cursor is invalid."); }
            if (parts.Length == 3 && !string.Equals(parts[0], sortKey, StringComparison.Ordinal))
                throw new LedgerDomainException("ledger.cursor_invalid", "The trial-balance cursor does not match the requested sort.");
            var offset = parts.Length == 3 ? 1 : 0;
            if ((parts.Length != 2 && parts.Length != 3) || !Guid.TryParse(parts[offset + 1], out var parsedId))
                throw new LedgerDomainException("ledger.cursor_invalid", "The trial-balance cursor is invalid.");
            cursorCode = parts[offset]; cursorId = parsedId;
        }

        await using var connection = await OpenAsync(tenant, ct);
        const string grouped = "select a.id,a.code,a.name_en,coalesce(sum(l.base_debit),0) debit,coalesce(sum(l.base_credit),0) credit,coalesce(sum(l.base_debit-l.base_credit),0) balance,max(e.base_currency_code) currency_code from finance_ledger.journal_entry e join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id join finance_ledger.account a on a.tenant_id=l.tenant_id and a.legal_entity_id=l.legal_entity_id and a.id=l.account_id where e.tenant_id=$1 and e.legal_entity_id=$2 and ($3::date is null or e.posting_date >= $3) and ($4::date is null or e.posting_date <= $4) and ($5::text is null or (a.code ilike $5 || '%' or a.name_en ilike $5 || '%')) group by a.id,a.code,a.name_en";
        await using (var summary = new NpgsqlCommand($"select count(*)::int,coalesce(sum(debit),0),coalesce(sum(credit),0) from ({grouped}) grouped", connection))
        {
            Add(summary, tenant.Value, entity.Value, (object?)from ?? DBNull.Value, (object?)to ?? DBNull.Value, (object?)CleanCursorText(search) ?? DBNull.Value);
            await using var summaryReader = await summary.ExecuteReaderAsync(ct);
            await summaryReader.ReadAsync(ct);
            var totalCount = summaryReader.GetInt32(0); var totalDebit = summaryReader.GetDecimal(1); var totalCredit = summaryReader.GetDecimal(2);
            await summaryReader.CloseAsync();
            var pageSql = $"""
                with grouped as (
                    select a.id,a.code,a.name_en,coalesce(sum(l.base_debit),0) debit,coalesce(sum(l.base_credit),0) credit,coalesce(sum(l.base_debit-l.base_credit),0) balance,max(e.base_currency_code) currency_code
                    from finance_ledger.journal_entry e
                    join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id
                    join finance_ledger.account a on a.tenant_id=l.tenant_id and a.legal_entity_id=l.legal_entity_id and a.id=l.account_id
                    where e.tenant_id=$1 and e.legal_entity_id=$2 and ($3::date is null or e.posting_date >= $3) and ($4::date is null or e.posting_date <= $4) and ($5::text is null or (a.code ilike $5 || '%' or a.name_en ilike $5 || '%'))
                    group by a.id,a.code,a.name_en
                )
                select id,code,name_en,debit,credit,balance,currency_code
                from grouped
                where ($6::text is null or ({sortExpression}) > ($6,$7))
                order by {sortExpression}
                limit $8
                """;
            await using var pageCommand = new NpgsqlCommand(pageSql, connection);
            Add(pageCommand, tenant.Value, entity.Value, (object?)from ?? DBNull.Value, (object?)to ?? DBNull.Value, (object?)CleanCursorText(search) ?? DBNull.Value,
                (object?)cursorCode ?? DBNull.Value, (object?)cursorId ?? DBNull.Value, limit + 1);
            await using var reader = await pageCommand.ExecuteReaderAsync(ct); var rows = new List<TrialBalanceRowDto>();
            while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetString(6)));
            var hasMore = rows.Count > limit; if (hasMore) rows.RemoveAt(rows.Count - 1);
            var last = rows.LastOrDefault();
            var next = hasMore && last is not null ? Convert.ToBase64String(Encoding.UTF8.GetBytes($"{sortKey}|{(sortKey == "accountNameAsc" ? last.AccountName : last.AccountCode)}|{last.AccountId}")) : null;
            return new TrialBalancePageDto(rows, next, totalCount, totalDebit, totalCredit);
        }
    }

    public async Task<LedgerGeneralLedgerPage> GetAccountingGeneralLedgerAsync(TenantId tenant, LegalEntityId entity, DateOnly? from, DateOnly? to, Guid? accountId, string? cursor, int pageSize, CancellationToken ct)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        DateOnly? cursorDate = null; string? cursorNumber = null; var cursorSequence = 0; Guid? cursorLineId = null;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|');
            if (parts.Length != 4 || !DateOnly.TryParse(parts[0], out var parsedDate) || !int.TryParse(parts[2], out cursorSequence) || !Guid.TryParse(parts[3], out var parsedId)) throw new LedgerDomainException("ledger.cursor_invalid", "The report cursor is invalid.");
            cursorDate = parsedDate; cursorNumber = parts[1]; cursorLineId = parsedId;
        }
        const string sql = """
            with scoped as (
                select e.id as entry_id,l.id as line_id,e.entry_number,e.posting_date,l.account_id,a.code,a.name_en,l.debit,l.credit,l.base_debit,l.base_credit,l.currency_code,e.source_module,e.source_reference,l.sequence_no,
                       sum(l.base_debit-l.base_credit) over (partition by l.account_id order by e.posting_date,e.entry_number,l.sequence_no,l.id rows unbounded preceding) as running_balance
                from finance_ledger.journal_entry e
                join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id
                join finance_ledger.account a on a.tenant_id=l.tenant_id and a.legal_entity_id=l.legal_entity_id and a.id=l.account_id
                where e.tenant_id=$1 and e.legal_entity_id=$2 and ($3::date is null or e.posting_date <= $3) and ($4::uuid is null or l.account_id=$4)
            )
            select entry_id,line_id,entry_number,posting_date,account_id,code,name_en,debit,credit,base_debit,base_credit,currency_code,source_module,source_reference,sequence_no,running_balance
            from scoped
            where ($5::date is null or posting_date >= $5)
              and ($6::date is null or (posting_date,entry_number,sequence_no,line_id) > ($6,$7,$8,$9))
            order by posting_date,entry_number,sequence_no,line_id
            limit $10
            """;
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue((object?)to ?? DBNull.Value); command.Parameters.AddWithValue((object?)accountId ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)from ?? DBNull.Value); command.Parameters.AddWithValue((object?)cursorDate ?? DBNull.Value); command.Parameters.AddWithValue((object?)cursorNumber ?? DBNull.Value); command.Parameters.AddWithValue(cursorSequence); command.Parameters.AddWithValue((object?)cursorLineId ?? DBNull.Value); command.Parameters.AddWithValue(pageSize + 1);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<LedgerGeneralLedgerLine>();
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), DateOnly.FromDateTime(reader.GetDateTime(3)), reader.GetGuid(4), reader.GetString(5), reader.GetString(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.GetDecimal(10), reader.GetString(11), reader.GetString(12), reader.GetString(13), reader.GetInt32(14), reader.GetDecimal(15)));
        var next = rows.Count > pageSize ? rows[pageSize - 1] : null;
        if (rows.Count > pageSize) rows.RemoveAt(pageSize);
        return new LedgerGeneralLedgerPage(rows, next is null ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes($"{next.PostingDate:yyyy-MM-dd}|{next.EntryNumber}|{next.Sequence}|{next.LineId}")));
    }

    public async Task<GeneralLedgerPageDto> GetGeneralLedgerAsync(TenantId tenant, LegalEntityId entity, DateOnly? from, DateOnly? to, Guid? accountId, string? cursor, int limit, CancellationToken ct, Guid? journalId = null, string? sourceModule = null, string? sourceReference = null, Guid? dimensionDefinitionId = null, Guid? dimensionValueId = null, string? entryNumber = null, int? page = null)
    {
        limit = Math.Clamp(limit, 1, 200); DateOnly? cursorDate = null; string? cursorNumber = null; int cursorSequence = 0; Guid? cursorLineId = null; if (page is null && !string.IsNullOrWhiteSpace(cursor)) { var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|'); if (parts.Length != 4 || !DateOnly.TryParse(parts[0], out var parsed) || !int.TryParse(parts[2], out cursorSequence) || !Guid.TryParse(parts[3], out var parsedLineId)) throw new LedgerDomainException("ledger.cursor_invalid", "The report cursor is invalid."); cursorDate = parsed; cursorNumber = parts[1]; cursorLineId = parsedLineId; }
        await using var connection = await OpenAsync(tenant, ct);
        if (page is not null)
        {
            var pageNumber = Math.Max(1, page.Value); var offset = (pageNumber - 1) * limit;
            const string countSql = "select count(*) from finance_ledger.journal_entry e join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id where e.tenant_id=$1 and e.legal_entity_id=$2 and ($3::date is null or e.posting_date >= $3) and ($4::date is null or e.posting_date <= $4) and ($5::uuid is null or l.account_id=$5) and ($6::uuid is null or e.journal_id=$6) and ($7::text is null or e.source_module=$7) and ($8::text is null or e.source_reference=$8) and ($9::uuid is null or exists (select 1 from finance_ledger.journal_line_dimension xd where xd.tenant_id=l.tenant_id and xd.legal_entity_id=l.legal_entity_id and xd.journal_entry_line_id=l.id and xd.dimension_definition_id=$9)) and ($10::uuid is null or exists (select 1 from finance_ledger.journal_line_dimension xv where xv.tenant_id=l.tenant_id and xv.legal_entity_id=l.legal_entity_id and xv.journal_entry_line_id=l.id and xv.dimension_value_id=$10)) and ($11::text is null or e.entry_number like $11 || '%')";
            await using var countCommand = new NpgsqlCommand(countSql, connection);
            Add(countCommand, tenant.Value, entity.Value, (object?)from ?? DBNull.Value, (object?)to ?? DBNull.Value, (object?)accountId ?? DBNull.Value, (object?)journalId ?? DBNull.Value, (object?)sourceModule ?? DBNull.Value, (object?)sourceReference ?? DBNull.Value, (object?)dimensionDefinitionId ?? DBNull.Value, (object?)dimensionValueId ?? DBNull.Value, (object?)entryNumber ?? DBNull.Value);
            var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
            const string offsetSql = """
                with filtered as (
                    select e.id entry_id,l.id line_id,e.entry_number,e.posting_date,a.id account_id,a.code,a.name_en,l.debit,l.credit,l.base_debit,l.base_credit,l.currency_code,e.source_module,e.source_reference,l.sequence_no
                    from finance_ledger.journal_entry e join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id
                    join finance_ledger.account a on a.tenant_id=l.tenant_id and a.legal_entity_id=l.legal_entity_id and a.id=l.account_id
                    where e.tenant_id=$1 and e.legal_entity_id=$2 and ($3::date is null or e.posting_date >= $3) and ($4::date is null or e.posting_date <= $4)
                      and ($5::uuid is null or l.account_id=$5) and ($6::uuid is null or e.journal_id=$6) and ($7::text is null or e.source_module=$7) and ($8::text is null or e.source_reference=$8)
                      and ($9::uuid is null or exists (select 1 from finance_ledger.journal_line_dimension xd where xd.tenant_id=l.tenant_id and xd.legal_entity_id=l.legal_entity_id and xd.journal_entry_line_id=l.id and xd.dimension_definition_id=$9))
                      and ($10::uuid is null or exists (select 1 from finance_ledger.journal_line_dimension xv where xv.tenant_id=l.tenant_id and xv.legal_entity_id=l.legal_entity_id and xv.journal_entry_line_id=l.id and xv.dimension_value_id=$10))
                      and ($11::text is null or e.entry_number like $11 || '%')
                ), ranked as (
                    select filtered.*, sum(base_debit-base_credit) over (partition by account_id order by posting_date,entry_number,sequence_no,line_id rows unbounded preceding) running_balance
                    from filtered
                )
                select entry_id,line_id,entry_number,posting_date,account_id,code,name_en,debit,credit,base_debit,base_credit,currency_code,source_module,source_reference,sequence_no,running_balance
                from ranked order by posting_date,entry_number,sequence_no,line_id offset $12 limit $13
                """;
            await using var offsetCommand = new NpgsqlCommand(offsetSql, connection);
            Add(offsetCommand, tenant.Value, entity.Value, (object?)from ?? DBNull.Value, (object?)to ?? DBNull.Value, (object?)accountId ?? DBNull.Value, (object?)journalId ?? DBNull.Value, (object?)sourceModule ?? DBNull.Value, (object?)sourceReference ?? DBNull.Value, (object?)dimensionDefinitionId ?? DBNull.Value, (object?)dimensionValueId ?? DBNull.Value, (object?)entryNumber ?? DBNull.Value, offset, limit);
            await using var offsetReader = await offsetCommand.ExecuteReaderAsync(ct); var rows = new List<GeneralLedgerRowDto>();
            while (await offsetReader.ReadAsync(ct)) rows.Add(new(offsetReader.GetGuid(0), offsetReader.GetGuid(1), offsetReader.GetString(2), DateOnly.FromDateTime(offsetReader.GetDateTime(3)), offsetReader.GetGuid(4), offsetReader.GetString(5), offsetReader.GetString(6), offsetReader.GetDecimal(7), offsetReader.GetDecimal(8), offsetReader.GetDecimal(9), offsetReader.GetDecimal(10), offsetReader.GetString(11), offsetReader.GetString(12), offsetReader.GetString(13), offsetReader.GetInt32(14), offsetReader.GetDecimal(15)));
            return new GeneralLedgerPageDto(rows, null, totalCount, pageNumber, limit);
        }
        const string sql = "select e.id,l.id,e.entry_number,e.posting_date,a.id,a.code,a.name_en,l.debit,l.credit,l.base_debit,l.base_credit,l.currency_code,e.source_module,e.source_reference,l.sequence_no,sum(l.base_debit-l.base_credit) over (partition by l.account_id order by e.posting_date,e.entry_number,l.sequence_no,l.id rows unbounded preceding) as running_balance from finance_ledger.journal_entry e join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id join finance_ledger.account a on a.tenant_id=l.tenant_id and a.legal_entity_id=l.legal_entity_id and a.id=l.account_id where e.tenant_id=$1 and e.legal_entity_id=$2 and ($3::date is null or e.posting_date >= $3) and ($4::date is null or e.posting_date <= $4) and ($5::uuid is null or l.account_id=$5) and ($6::date is null or (e.posting_date,e.entry_number,l.sequence_no,l.id) > ($6,$7,$8,$9)) and ($10::uuid is null or e.journal_id=$10) and ($11::text is null or e.source_module=$11) and ($12::text is null or e.source_reference=$12) and ($13::uuid is null or exists (select 1 from finance_ledger.journal_line_dimension xd where xd.tenant_id=l.tenant_id and xd.legal_entity_id=l.legal_entity_id and xd.journal_entry_line_id=l.id and xd.dimension_definition_id=$13)) and ($14::uuid is null or exists (select 1 from finance_ledger.journal_line_dimension xv where xv.tenant_id=l.tenant_id and xv.legal_entity_id=l.legal_entity_id and xv.journal_entry_line_id=l.id and xv.dimension_value_id=$14)) and ($15::text is null or e.entry_number like $15 || '%') order by e.posting_date,e.entry_number,l.sequence_no,l.id limit $16";
        await using var command = new NpgsqlCommand(sql, connection); Add(command, tenant.Value, entity.Value, (object?)from ?? DBNull.Value, (object?)to ?? DBNull.Value, (object?)accountId ?? DBNull.Value, (object?)cursorDate ?? DBNull.Value, (object?)cursorNumber ?? DBNull.Value, cursorSequence, (object?)cursorLineId ?? DBNull.Value, (object?)journalId ?? DBNull.Value, (object?)sourceModule ?? DBNull.Value, (object?)sourceReference ?? DBNull.Value, (object?)dimensionDefinitionId ?? DBNull.Value, (object?)dimensionValueId ?? DBNull.Value, (object?)entryNumber ?? DBNull.Value, limit + 1); await using var reader = await command.ExecuteReaderAsync(ct); var legacyRows = new List<GeneralLedgerRowDto>(); while (await reader.ReadAsync(ct)) legacyRows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), DateOnly.FromDateTime(reader.GetDateTime(3)), reader.GetGuid(4), reader.GetString(5), reader.GetString(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.GetDecimal(10), reader.GetString(11), reader.GetString(12), reader.GetString(13), reader.GetInt32(14), reader.GetDecimal(15))); var next = legacyRows.Count > limit ? legacyRows[limit - 1] : null; if (legacyRows.Count > limit) legacyRows.RemoveAt(limit); return new GeneralLedgerPageDto(legacyRows, next is null ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes($"{next.PostingDate:yyyy-MM-dd}|{next.EntryNumber}|{next.Sequence}|{next.LineId}")));
    }

    public async Task<AccountBalanceDto> GetAccountBalanceAsync(TenantId tenant, LegalEntityId entity, Guid accountId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct); await using var command = new NpgsqlCommand("select coalesce(sum(l.base_debit),0),coalesce(sum(l.base_credit),0),max(e.base_currency_code) from finance_ledger.journal_entry e join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id where e.tenant_id=$1 and e.legal_entity_id=$2 and l.account_id=$3 and ($4::date is null or e.posting_date >= $4) and ($5::date is null or e.posting_date <= $5)", connection); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(accountId); command.Parameters.AddWithValue((object?)from ?? DBNull.Value); command.Parameters.AddWithValue((object?)to ?? DBNull.Value); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new LedgerDomainException("ledger.account_not_found", "The account was not found."); var debit = reader.GetDecimal(0); var credit = reader.GetDecimal(1); return new(accountId, debit, credit, debit - credit, reader.IsDBNull(2) ? "" : reader.GetString(2), from, to);
    }

    private async Task<GeneralJournalDraftDto> TransitionDraftAsync(TenantId tenant, LegalEntityId entity, Guid draftId, Guid actor, string expected, string next, string action, CancellationToken ct) => await transactions.ExecuteAsync(tenant, async (context, token) => { var updated = await ExecuteCountAsync(context, $"update finance_ledger.general_journal_draft set status=$4, {(next == "Submitted" ? "submitted_by" : "approved_by")}=$3,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$5 and status=$6", token, tenant.Value, entity.Value, actor, next, draftId, expected); if (updated != 1) throw new LedgerDomainException("ledger.general_journal_transition_invalid", "The general journal is not in the required lifecycle state."); await AuditAsync(context, tenant, entity, actor, action, draftId, null, new { status = next }, token); var draft = await LoadDraftAsync(context, tenant, entity, draftId, token) ?? throw new LedgerDomainException("ledger.general_journal_not_found", "The general journal draft was not found."); var dtoLines = await LoadDraftLineRequestsAsync(context, tenant, entity, draftId, token); return new GeneralJournalDraftDto(draftId, entity.Value, draft.JournalId, draft.PostingDate, draft.Description, draft.BaseCurrency, Enum.Parse<GeneralJournalDraftStatus>(draft.Status), dtoLines); }, ct);

    private static void ValidateDraftLine(GeneralJournalLineRequest line) { if (line.Debit < 0 || line.Credit < 0 || line.Debit > 0 && line.Credit > 0 || line.Debit == 0 && line.Credit == 0 || line.BaseDebit < 0 || line.BaseCredit < 0 || line.BaseDebit > 0 && line.BaseCredit > 0 || line.BaseDebit == 0 && line.BaseCredit == 0) throw new LedgerDomainException("ledger.fact_invalid", "A journal line must have exactly one positive debit or credit side."); if (line.ExchangeRate <= 0) throw new LedgerDomainException("ledger.currency_mismatch", "Exchange rate must be positive."); }

    private static void Add(NpgsqlCommand command, params object?[] values)
    {
        foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value);
    }
    private static string? CleanCursorText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<NpgsqlConnection> OpenAsync(TenantId tenant, CancellationToken ct) { var connection = await dataSource.OpenConnectionAsync(ct); await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct); return connection; }
    private static async Task ExecuteAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); await command.ExecuteNonQueryAsync(ct); }
    private static async Task<int> ExecuteCountAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); return await command.ExecuteNonQueryAsync(ct); }
    private static async Task<Guid?> ScalarGuidAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); var result = await command.ExecuteScalarAsync(ct); return result is Guid id ? id : null; }
    private static async Task<string?> ScalarStringAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); return (string?)await command.ExecuteScalarAsync(ct); }
    private static async Task<ProjectionReconciliationResultDto> ReconcileInTransactionAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid? fiscalYearId, Guid? periodId, CancellationToken ct)
    {
        const string sql = "with canonical as (select e.tenant_id,e.legal_entity_id,l.account_id,p.fiscal_year_id,e.accounting_period_id,e.base_currency_code,sum(l.base_debit) canonical_debit,sum(l.base_credit) canonical_credit,sum(l.base_debit-l.base_credit) canonical_balance from finance_ledger.journal_entry e join finance_ledger.accounting_period p on p.tenant_id=e.tenant_id and p.legal_entity_id=e.legal_entity_id and p.id=e.accounting_period_id join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id where e.tenant_id=$1 and e.legal_entity_id=$2 and ($3::uuid is null or p.fiscal_year_id=$3) and ($4::uuid is null or e.accounting_period_id=$4) group by e.tenant_id,e.legal_entity_id,l.account_id,p.fiscal_year_id,e.accounting_period_id,e.base_currency_code), projection as (select tenant_id,legal_entity_id,account_id,fiscal_year_id,accounting_period_id,currency_code,sum(opening_debit+period_debit) projection_debit,sum(opening_credit+period_credit) projection_credit,sum(closing_balance) projection_balance from finance_ledger.account_period_balance where tenant_id=$1 and legal_entity_id=$2 and ($3::uuid is null or fiscal_year_id=$3) and ($4::uuid is null or accounting_period_id=$4) group by tenant_id,legal_entity_id,account_id,fiscal_year_id,accounting_period_id,currency_code), compared as (select coalesce(c.account_id,p.account_id) account_id,coalesce(c.accounting_period_id,p.accounting_period_id) period_id,coalesce(c.fiscal_year_id,p.fiscal_year_id) fiscal_year_id,coalesce(c.base_currency_code,p.currency_code) currency_code,coalesce(c.canonical_debit,0) canonical_debit,coalesce(p.projection_debit,0) projection_debit,coalesce(c.canonical_credit,0) canonical_credit,coalesce(p.projection_credit,0) projection_credit,coalesce(c.canonical_balance,0) canonical_balance,coalesce(p.projection_balance,0) projection_balance from canonical c full join projection p on p.tenant_id=c.tenant_id and p.legal_entity_id=c.legal_entity_id and p.account_id=c.account_id and p.accounting_period_id=c.accounting_period_id and p.currency_code=c.base_currency_code) select account_id,period_id,fiscal_year_id,currency_code,canonical_debit,projection_debit,canonical_debit-projection_debit,canonical_credit,projection_credit,canonical_credit-projection_credit,canonical_balance,projection_balance,canonical_balance-projection_balance from compared where abs(canonical_debit-projection_debit) > 0.00000001 or abs(canonical_credit-projection_credit) > 0.00000001 or abs(canonical_balance-projection_balance) > 0.00000001 order by account_id,period_id";
        await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue((object?)fiscalYearId ?? DBNull.Value); command.Parameters.AddWithValue((object?)periodId ?? DBNull.Value); await using var reader = await command.ExecuteReaderAsync(ct); var differences = new List<ProjectionReconciliationDifferenceDto>(); while (await reader.ReadAsync(ct)) differences.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.GetDecimal(10), reader.GetDecimal(11), reader.GetDecimal(12), "Mismatch")); return new(differences.Count == 0, differences);
    }
    private static async Task AuditAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid actor, string action, Guid id, object? before, object? after, CancellationToken ct) => await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, entity.Value, null, "User", actor, null, "ledger", action, "ledger_workflow", id, before is null ? null : JsonSerializer.Serialize(before), after is null ? null : JsonSerializer.Serialize(after), null, "api", Guid.CreateVersion7(), null, DateTimeOffset.UtcNow, null), ct);

    private static async Task<(Guid JournalId, DateOnly PostingDate, string Description, string BaseCurrency, string Status, string IdempotencyKey, Guid CreatedBy)?> LoadDraftAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid id, CancellationToken ct) { await using var command = new NpgsqlCommand("select journal_id,posting_date,description,base_currency_code,status,idempotency_key,created_by from finance_ledger.general_journal_draft where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(id); await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? (reader.GetGuid(0), DateOnly.FromDateTime(reader.GetDateTime(1)), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetGuid(6)) : null; }
    private sealed record DraftLine(Guid Id, Guid AccountId, decimal Debit, decimal Credit, string Currency, decimal TransactionDebit, decimal TransactionCredit, decimal BaseDebit, decimal BaseCredit, decimal ExchangeRate, string? Description);
    private static async Task<IReadOnlyList<DraftLine>> LoadDraftLinesAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid draftId, CancellationToken ct) { await using var command = new NpgsqlCommand("select id,account_id,debit,credit,transaction_currency_code,transaction_debit,transaction_credit,base_debit,base_credit,exchange_rate,description from finance_ledger.general_journal_draft_line where tenant_id=$1 and legal_entity_id=$2 and draft_id=$3 order by sequence_no", context.Connection, context.Transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(draftId); await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<DraftLine>(); while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetString(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.IsDBNull(10) ? null : reader.GetString(10))); return result; }
    private static async Task<IReadOnlyList<GeneralJournalLineRequest>> LoadDraftLineRequestsAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid draftId, CancellationToken ct) { var lines = await LoadDraftLinesAsync(context, tenant, entity, draftId, ct); var result = new List<GeneralJournalLineRequest>(lines.Count); foreach (var line in lines) { var values = await ScalarGuidListAsync(context, "select dimension_value_id from finance_ledger.general_journal_draft_line_dimension where tenant_id=$1 and legal_entity_id=$2 and draft_line_id=$3 order by dimension_definition_id", ct, tenant.Value, entity.Value, line.Id); result.Add(new(line.AccountId, line.Debit, line.Credit, line.Currency, line.TransactionDebit, line.TransactionCredit, line.BaseDebit, line.BaseCredit, line.ExchangeRate, values, line.Description)); } return result; }
    private static async Task<IReadOnlyList<LedgerDimensionAssignment>> LoadDimensionsAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid lineId, CancellationToken ct) { await using var command = new NpgsqlCommand("select d.code,v.id,v.code from finance_ledger.general_journal_draft_line_dimension x join finance_ledger.dimension_definition d on d.tenant_id=x.tenant_id and d.legal_entity_id=x.legal_entity_id and d.id=x.dimension_definition_id join finance_ledger.dimension_value v on v.tenant_id=x.tenant_id and v.legal_entity_id=x.legal_entity_id and v.id=x.dimension_value_id where x.tenant_id=$1 and x.legal_entity_id=$2 and x.draft_line_id=$3", context.Connection, context.Transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(lineId); await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<LedgerDimensionAssignment>(); while (await reader.ReadAsync(ct)) result.Add(new(reader.GetString(0), reader.GetGuid(1), reader.GetString(2))); return result; }
    private static async Task<IReadOnlyList<Guid>> ScalarGuidListAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<Guid>(); while (await reader.ReadAsync(ct)) result.Add(reader.GetGuid(0)); return result; }
    private sealed record OpeningLine(OpeningBalanceLineRequest Request, Guid AccountId, decimal Debit, decimal Credit, string Currency, decimal BaseDebit, decimal BaseCredit, decimal ExchangeRate, string? LegacyReference, string AccountStatus, bool AllowDirectPosting);
    private static async Task<IReadOnlyList<OpeningLine>> LoadOpeningLinesAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid batchId, CancellationToken ct) { await using var command = new NpgsqlCommand("select l.account_id,l.debit,l.credit,l.currency_code,l.base_debit,l.base_credit,l.exchange_rate,l.legacy_account_code,l.legacy_reference,a.status,a.allow_direct_posting from finance_ledger.opening_balance_line l join finance_ledger.account a on a.tenant_id=l.tenant_id and a.legal_entity_id=l.legal_entity_id and a.id=l.account_id where l.tenant_id=$1 and l.legal_entity_id=$2 and l.batch_id=$3 order by l.sequence_no", context.Connection, context.Transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(batchId); await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<OpeningLine>(); while (await reader.ReadAsync(ct)) { var accountId = reader.GetGuid(0); var debit = reader.GetDecimal(1); var credit = reader.GetDecimal(2); var currency = reader.GetString(3); var baseDebit = reader.GetDecimal(4); var baseCredit = reader.GetDecimal(5); var rate = reader.GetDecimal(6); var legacyCode = reader.IsDBNull(7) ? null : reader.GetString(7); var legacyRef = reader.IsDBNull(8) ? null : reader.GetString(8); result.Add(new(new(accountId, debit, credit, currency, baseDebit, baseCredit, rate, legacyCode, legacyRef), accountId, debit, credit, currency, baseDebit, baseCredit, rate, legacyRef, reader.GetString(9), reader.GetBoolean(10))); } return result; }
    private static async Task<(string BatchIdentity, string FileHash, string? LegacySystem, DateOnly GoLiveDate, string Status)?> LoadOpeningBatchAsync(ITransactionContext context, TenantId tenant, LegalEntityId entity, Guid id, CancellationToken ct) { await using var command = new NpgsqlCommand("select batch_identity,file_hash,legacy_system,go_live_date,status from finance_ledger.opening_balance_batch where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity.Value); command.Parameters.AddWithValue(id); await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? (reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), DateOnly.FromDateTime(reader.GetDateTime(3)), reader.GetString(4)) : null; }
}
