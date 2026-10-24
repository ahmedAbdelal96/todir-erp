using Core.Transactions;
using Modules.Ledger.DTOs;
using Modules.Ledger.Entities;
using Npgsql;
using Shared.Contracts.Ledger;

namespace Modules.Ledger.Repositories;

/// <summary>Reads the persisted semantic result inside the preview transaction before it is rolled back.</summary>
public sealed class LedgerPreviewRepository
{
    public static async Task<PostingPreviewDto> ReadAsync(ITransactionContext context, LedgerPostingResult posted, CancellationToken cancellationToken)
    {
        await using var entry = new NpgsqlCommand("select journal_id,posting_date,posting_rule_version_id,accounting_configuration_version_id,base_currency_code from finance_ledger.journal_entry where id=$1", context.Connection, context.Transaction);
        entry.Parameters.AddWithValue(posted.JournalEntryId);
        await using var entryReader = await entry.ExecuteReaderAsync(cancellationToken);
        if (!await entryReader.ReadAsync(cancellationToken)) throw new LedgerDomainException("ledger.preview_unavailable", "The preview posting result could not be read.");
        var journalId = entryReader.GetGuid(0);
        var postingDate = DateOnly.FromDateTime(entryReader.GetDateTime(1));
        var ruleVersionId = entryReader.GetGuid(2);
        var configurationVersionId = entryReader.GetGuid(3);
        var baseCurrency = entryReader.GetString(4);
        await entryReader.CloseAsync();

        await using var lines = new NpgsqlCommand("select l.sequence_no,l.account_id,l.debit,l.credit,l.base_debit,l.base_credit,l.currency_code,l.transaction_debit,l.transaction_credit,l.exchange_rate,l.account_role,l.account_mapping_id,mv.version_number,mv.id,l.description from finance_ledger.journal_entry_line l left join finance_ledger.account_mapping_version mv on mv.tenant_id=l.tenant_id and mv.legal_entity_id=l.legal_entity_id and mv.id=l.account_mapping_version_id where l.journal_entry_id=$1 order by l.sequence_no", context.Connection, context.Transaction);
        lines.Parameters.AddWithValue(posted.JournalEntryId);
        await using var reader = await lines.ExecuteReaderAsync(cancellationToken);
        var result = new List<PostingPreviewLineDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new(
                reader.GetInt32(0), reader.GetGuid(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5),
                reader.GetString(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.IsDBNull(9) ? null : reader.GetDecimal(9), reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetGuid(11), reader.IsDBNull(12) ? null : reader.GetInt64(12), reader.IsDBNull(13) ? null : reader.GetGuid(13),
                reader.IsDBNull(14) ? null : reader.GetString(14), []));
        }
        return new(journalId, postingDate, ruleVersionId, configurationVersionId, baseCurrency, result, result.Sum(line => line.BaseDebit), result.Sum(line => line.BaseCredit), []);
    }
}
