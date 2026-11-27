using Npgsql;
using System.Globalization;
using Shared.Contracts.Planning;
using Shared.Primitives.Identifiers;

namespace Modules.Ledger.Repositories;

/// <summary>
/// Exposes posted financial actuals to Planning without allowing Planning to read Ledger tables.
/// The posted Ledger fact is authoritative; operational owner rows with the same recognition
/// identity are collapsed by Planning rather than counted again.
/// </summary>
public sealed class LedgerOwnerReconciliationSource(NpgsqlDataSource dataSource) : IPlanningOwnerReconciliationSource
{
    public string SourceModule => "ledger";

    public async Task<IReadOnlyList<PlanningOwnerFinancialFact>> ListFinancialFactsAsync(TenantId tenantId, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            tenant.Parameters.AddWithValue(tenantId.Value.ToString("D"));
            await tenant.ExecuteNonQueryAsync(cancellationToken);
        }

        // Semantic posting roles are intentionally constrained to the fact type.  A debit
        // on an unrelated account is not evidence that the fact is a Planning Actual.
        const string sql = """
            select e.id,e.fact_id,e.fact_version,e.legal_entity_id,e.posting_date,e.base_currency_code,
                   case
                       when e.fact_type='FixedAssetDepreciationReversed' then -sum(l.base_credit)
                       when e.fact_type in ('PayrollPosted','FixedAssetDepreciationPosted') then sum(l.base_debit)
                       when coalesce(sum(l.base_debit),0)>0 then sum(l.base_debit)
                       else -sum(l.base_credit)
                   end,
                   coalesce(e.source_reference,e.id::text),
                   case
                       when e.fact_type='FixedAssetDepreciationPosted' or e.fact_type='FixedAssetDepreciationReversed' then 'DEPRECIATION'
                       else 'OPEX'
                   end,
                   e.fact_type
            from finance_ledger.journal_entry e
            join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id
            where e.tenant_id=$1
              and ($2::uuid is null or e.legal_entity_id=$2)
              and e.status='Posted'
              and (
                  (e.fact_type='ExpenseRecognized' and l.semantic_role='ExpenseRecognized')
                  or (e.fact_type in ('PayrollPosted','FixedAssetDepreciationPosted','FixedAssetDepreciationReversed') and l.semantic_role=e.fact_type)
              )
              and (l.base_debit>0 or l.base_credit>0)
            group by e.id,e.fact_id,e.fact_version,e.legal_entity_id,e.posting_date,e.base_currency_code,e.source_reference,e.fact_type
            order by e.posting_date,e.id
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenantId.Value);
        command.Parameters.AddWithValue(legalEntityId.HasValue ? legalEntityId.Value : DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<PlanningOwnerFinancialFact>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var postingDate = reader.GetFieldValue<DateOnly>(4);
            var sourceReference = reader.GetString(7);
            var factType = reader.GetString(9);
            result.Add(new(
                SourceModule,
                sourceReference,
                reader.GetGuid(0).ToString("D"),
                $"{reader.GetGuid(1):D}:{reader.GetInt64(2).ToString(CultureInfo.InvariantCulture)}",
                reader.GetGuid(3),
                new(postingDate.Year, postingDate.Month, 1),
                reader.GetString(8),
                "{}",
                "Actual",
                reader.GetDecimal(6),
                reader.GetString(5).Trim(),
                sourceReference,
                "Accrual"));
        }

        return result;
    }
}
