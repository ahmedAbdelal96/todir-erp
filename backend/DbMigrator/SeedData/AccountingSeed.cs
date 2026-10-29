namespace DbMigrator.SeedData;

/// <summary>
/// Seeds only Accounting-owned setup and report-definition configuration. Ledger and
/// subledger remain the source of financial facts used by the workspace.
/// </summary>
public static class AccountingSeed
{
    public static async Task<SeedAccountingSummary> SeedAsync(SeedContext context, CancellationToken ct)
    {
        await context.ExecuteAsync(
            "insert into business_accounting.accounting_setup(tenant_id,legal_entity_id,mode,go_live_status,updated_by,updated_at) values($1,$2,'Managed','Ready',$3,$4) on conflict(tenant_id,legal_entity_id) do update set mode=excluded.mode,go_live_status=excluded.go_live_status,updated_by=excluded.updated_by,updated_at=excluded.updated_at,row_version=business_accounting.accounting_setup.row_version+1",
            ct, context.Tenant.Id, context.EntityId, context.AdministratorId, context.AsOf);

        foreach (var statement in new[] { ("profit-and-loss", "ProfitAndLoss"), ("balance-sheet", "BalanceSheet") })
        {
            var definitionId = SeedIds.Child(context.Tenant.Id, "accounting-statement", statement.Item1 == "profit-and-loss" ? 1 : 2);
            await context.ExecuteAsync(
                "insert into business_accounting.financial_statement_definition(tenant_id,legal_entity_id,id,code,statement_kind,status,current_version,created_by,created_at,updated_at) values($1,$2,$3,$4,$5,'Active',1,$6,$7,$7) on conflict(tenant_id,legal_entity_id,code) do update set status='Active',updated_at=excluded.updated_at",
                ct, context.Tenant.Id, context.EntityId, definitionId, statement.Item1, statement.Item2, context.AdministratorId, context.AsOf);
            await context.ExecuteAsync(
                "insert into business_accounting.financial_statement_version(tenant_id,definition_id,version,status,effective_from,created_by,created_at) values($1,$2,1,'Active',null,$3,$4) on conflict(tenant_id,definition_id,version) do update set status='Active'",
                ct, context.Tenant.Id, definitionId, context.AdministratorId, context.AsOf);
        }

        return new SeedAccountingSummary("Ready", 2);
    }
}

public sealed record SeedAccountingSummary(string GoLiveStatus, int StatementDefinitions);
