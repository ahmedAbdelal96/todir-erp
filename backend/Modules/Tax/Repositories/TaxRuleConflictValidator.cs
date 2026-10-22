using Npgsql;

namespace Modules.Tax.Repositories;

/// <summary>Shared equal-rank contradiction check used by lifecycle validation and readiness coverage.</summary>
public static class TaxRuleConflictValidator
{
    private const string ConflictSql = "select av.treatment,bv.treatment,a.stack_id,b.stack_id from foundation_tax.tax_rule a join foundation_tax.tax_rule b on b.tenant_id=a.tenant_id and b.rule_set_id=a.rule_set_id and b.id>a.id and b.explicit_priority=a.explicit_priority and b.registration_id is not distinct from a.registration_id and b.jurisdiction_code is not distinct from a.jurisdiction_code and b.direction is not distinct from a.direction and b.party_category_id is not distinct from a.party_category_id and b.product_category_id is not distinct from a.product_category_id and b.document_kind is not distinct from a.document_kind and b.transaction_type is not distinct from a.transaction_type and b.place_of_supply_country is not distinct from a.place_of_supply_country and daterange(a.tax_date_from,a.tax_date_to,'[)') && daterange(b.tax_date_from,b.tax_date_to,'[)') join foundation_tax.tax_rule_version av on av.tenant_id=a.tenant_id and av.id=a.rule_version_id join foundation_tax.tax_rule_version bv on bv.tenant_id=b.tenant_id and bv.id=b.rule_version_id where a.tenant_id=$1 and a.rule_set_id=$2 and (av.treatment<>bv.treatment or a.stack_id is distinct from b.stack_id) limit 1";

    public sealed record Candidate(string LeftTreatment, string RightTreatment, Guid? LeftStackId, Guid? RightStackId);

    public static bool HasConflicts(IEnumerable<Candidate> candidates)
        => candidates.Any(candidate => candidate.LeftTreatment != candidate.RightTreatment || candidate.LeftStackId != candidate.RightStackId);

    public static async Task<bool> HasConflictsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid ruleSetId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(ConflictSql, connection, transaction);
        command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(ruleSetId);
        var candidates = new List<Candidate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) candidates.Add(new(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetGuid(3)));
        return HasConflicts(candidates);
    }
}
