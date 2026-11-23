using Npgsql;

namespace DbMigrator.SeedData;

public static class PlatformSeed
{
    public static IReadOnlyList<SeedTenant> Tenants { get; } =
    [
        new(SeedIds.AtlasTenant, "atlas-trading", "شركة أطلس للتجارة", "atlas.com", "شركة أطلس للتجارة", "Atlas Trading", "EGP"),
        new(SeedIds.DeltaTenant, "delta-supplies", "شركة دلتا للتوريدات", "delta.com", "شركة دلتا للتوريدات", "Delta Supplies", "EGP")
    ];

    public static async Task EnsureTenantsAsync(NpgsqlConnection connection, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        foreach (var tenant in Tenants)
        {
            await using var context = await SeedContext.OpenAsync(connection, tenant, asOf, cancellationToken);
            try
            {
                await context.ExecuteAsync("insert into platform.tenant (id,slug,display_name,status,login_domain,normalized_login_domain,created_at,version) values ($1,$2,$3,'Active',$4,$4,$5,1) on conflict (id) do update set slug=excluded.slug,display_name=excluded.display_name,status='Active',login_domain=excluded.login_domain,normalized_login_domain=excluded.normalized_login_domain,version=platform.tenant.version+1", cancellationToken, tenant.Id, tenant.Slug, tenant.DisplayName, tenant.LoginDomain, asOf);
                await context.ExecuteAsync("insert into platform.legal_entity (id,tenant_id,code,legal_name,trade_name,country_code,base_currency_code,timezone,locale,status,created_at,version) values ($1,$2,'MAIN',$3,$4,'EG',$5,'Africa/Cairo','ar-EG','Active',$6,1) on conflict (id) do update set legal_name=excluded.legal_name,trade_name=excluded.trade_name,base_currency_code=excluded.base_currency_code,status='Active',version=platform.legal_entity.version+1", cancellationToken, context.EntityId, tenant.Id, tenant.LegalName, tenant.TradeName ?? (object)DBNull.Value, tenant.BaseCurrency, asOf);
                await context.ExecuteAsync("insert into platform.branch (id,tenant_id,legal_entity_id,code,name,timezone_override,status,created_at,version) values ($1,$2,$3,'MAIN',$4,'Africa/Cairo','Active',$5,1) on conflict (id) do update set legal_entity_id=excluded.legal_entity_id,name=excluded.name,status='Active',version=platform.branch.version+1", cancellationToken, context.BranchId, tenant.Id, context.EntityId, tenant.TradeName is null ? tenant.DisplayName : $"{tenant.TradeName} — Main Branch", asOf);
                var secondaryEntityId = SeedIds.TenantSecondaryEntity(tenant.Id);
                var secondaryBranchId = SeedIds.TenantSecondaryBranch(tenant.Id);
                await context.ExecuteAsync("insert into platform.legal_entity (id,tenant_id,code,legal_name,trade_name,country_code,base_currency_code,timezone,locale,status,created_at,version) values ($1,$2,'SECONDARY',$3,$4,'EG',$5,'Africa/Cairo','ar-EG','Active',$6,1) on conflict (id) do update set legal_name=excluded.legal_name,trade_name=excluded.trade_name,base_currency_code=excluded.base_currency_code,status='Active',version=platform.legal_entity.version+1", cancellationToken, secondaryEntityId, tenant.Id, $"{tenant.LegalName} — Secondary", $"{tenant.TradeName ?? tenant.DisplayName} — Secondary", tenant.BaseCurrency, asOf);
                await context.ExecuteAsync("insert into platform.branch (id,tenant_id,legal_entity_id,code,name,timezone_override,status,created_at,version) values ($1,$2,$3,'SECONDARY',$4,'Africa/Cairo','Active',$5,1) on conflict (id) do update set legal_entity_id=excluded.legal_entity_id,name=excluded.name,status='Active',version=platform.branch.version+1", cancellationToken, secondaryBranchId, tenant.Id, secondaryEntityId, $"{tenant.TradeName ?? tenant.DisplayName} — Secondary Branch", asOf);

                const string modulesJson = "[\"manufacturing\",\"accounting\",\"banking\",\"catalog\",\"crm\",\"directory\",\"expenses\",\"finbase\",\"fixed-assets\",\"hr\",\"inventory\",\"invoicing\",\"ledger\",\"payments\",\"payroll\",\"pos\",\"pricing\",\"projects\",\"purchasing\",\"sales\",\"sample\",\"service\",\"subledger\",\"tax\",\"quality\"]";
                await context.ExecuteAsync("insert into platform.entitlement_snapshot (tenant_id, snapshot_version, issued_at, valid_until, key_id, modules, features, limits, scopes, signature) values ($1, 1, $2, $2 + interval '100 years', 'dev-key', $3::jsonb, '[]'::jsonb, '{}'::jsonb, '[]'::jsonb, 'seed-signature') on conflict (tenant_id, snapshot_version) do update set valid_until = excluded.valid_until, modules = excluded.modules", cancellationToken, tenant.Id, asOf, modulesJson);

                var purchasableModules = new[] { "manufacturing", "accounting", "fixed-assets", "hr", "payroll", "pos", "projects", "service", "quality" };
                foreach (var mod in purchasableModules)
                {
                    var instanceId = SeedIds.Stable($"{tenant.Id:D}:platform:module-instance:{mod}");
                    await context.ExecuteAsync("insert into platform.module_instance (id, tenant_id, module_id, scope_type, scope_id, status, manifest_version, version) values ($1, $2, $3, 'tenant', null, 'Active', '1.0.0', 1) on conflict (id) do update set status = 'Active'", cancellationToken, instanceId, tenant.Id, mod);
                }

                await context.CommitAsync(cancellationToken);
            }
            catch
            {
                await context.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }

    public static Task EnsureSequenceAsync(SeedContext context, string key, Guid legalEntityId, string prefix, int padding, long nextValue, CancellationToken cancellationToken) =>
        context.ExecuteAsync("insert into platform.sequence_definition(id,tenant_id,sequence_key,legal_entity_id,branch_id,scope_key,period_key,prefix_template,padding,mode,next_value,version) values($1,$2,$3,$4,null,null,null,$5,$6,'Standard',$7,1) on conflict(tenant_id,sequence_key,legal_entity_id,branch_id,scope_key,period_key) do update set next_value=greatest(platform.sequence_definition.next_value,excluded.next_value),prefix_template=excluded.prefix_template,padding=excluded.padding,version=platform.sequence_definition.version+1", cancellationToken,
            SeedIds.Stable($"{context.Tenant.Id:D}:platform:sequence:{key}:{legalEntityId:D}"), context.Tenant.Id, key, legalEntityId, prefix, padding, nextValue);

}
