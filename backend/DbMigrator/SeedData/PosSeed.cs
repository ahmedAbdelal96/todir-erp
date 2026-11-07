namespace DbMigrator.SeedData;

public sealed record PosSeedSummary(int Registers, int Terminals, int OpenSessions, int HeldCarts);

public static class PosSeed
{
    public static async Task<PosSeedSummary> SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var register = SeedIds.PosRegister(context.Tenant.Id);
        var terminal = SeedIds.PosTerminal(context.Tenant.Id);
        var session = SeedIds.PosSession(context.Tenant.Id);
        await context.ExecuteAsync("insert into business_pos.pos_register(tenant_id,id,legal_entity_id,branch_id,code,name,currency_code,status,offline_enabled,created_by,created_at,updated_at) values($1,$2,$3,$4,'MAIN','Main Retail Register',$5,'Active',true,$6,$7,$7) on conflict(tenant_id,id) do update set legal_entity_id=excluded.legal_entity_id,branch_id=excluded.branch_id,status='Active',offline_enabled=true,updated_at=excluded.updated_at", cancellationToken, context.Tenant.Id, register, context.EntityId, context.BranchId, context.Tenant.BaseCurrency, context.AdministratorId, context.AsOf);
        await context.ExecuteAsync("insert into business_pos.pos_terminal(tenant_id,id,register_id,device_identity,display_name,status,last_seen_at,created_at,updated_at) values($1,$2,$3,$4,'Main Counter','Ready',$5,$5,$5) on conflict(tenant_id,id) do update set register_id=excluded.register_id,status='Ready',last_seen_at=excluded.last_seen_at,updated_at=excluded.updated_at", cancellationToken, context.Tenant.Id, terminal, register, $"seed-terminal-{context.Tenant.Slug}", context.AsOf);
        await context.ExecuteAsync("insert into business_pos.pos_session(tenant_id,id,register_id,terminal_id,cashier_user_id,business_date,opened_at,status,opening_float,expected_cash,row_version) values($1,$2,$3,$4,$5,$6,$7,'Open',100,100,1) on conflict(tenant_id,id) do update set register_id=excluded.register_id,terminal_id=excluded.terminal_id,status='Open',row_version=business_pos.pos_session.row_version+1", cancellationToken, context.Tenant.Id, session, register, terminal, context.AdministratorId, DateOnly.FromDateTime(context.AsOf.UtcDateTime), context.AsOf);
        return new(1, 1, 1, 0);
    }
}
