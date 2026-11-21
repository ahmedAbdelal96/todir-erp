using Npgsql;
using Shared.Primitives.Identifiers;

namespace Core.Modules;

public sealed class NpgsqlModuleLifecycleEnforcer(NpgsqlDataSource dataSource, IModuleRegistry? registry = null) : IModuleLifecycleEnforcer
{
    public async Task<ModuleAccessDecision> EvaluateAccessAsync(TenantId tenantId, string moduleId, Guid? legalEntityId = null, bool isMutation = false, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        await using (var setTenant = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, tx))
        {
            setTenant.Parameters.AddWithValue(tenantId.Value.ToString("D"));
            await setTenant.ExecuteNonQueryAsync(cancellationToken);
        }

        // 1. Check purchasable commercial entitlement if registry available
        bool isPurchasable = false;
        try
        {
            if (registry is not null)
            {
                var manifest = registry.Find(moduleId);
                isPurchasable = manifest.Purchasable;
            }
        }
        catch (KeyNotFoundException)
        {
            // Unknown module, fallback to purchasable check
        }

        if (isPurchasable)
        {
            const string entitlementSql = """
                select exists (
                    select 1 from platform.entitlement_snapshot 
                    where tenant_id = $1 and valid_until > now() 
                      and modules::jsonb ? $2
                )
                """;
            await using var entCommand = new NpgsqlCommand(entitlementSql, connection, tx);
            entCommand.Parameters.AddWithValue(tenantId.Value);
            entCommand.Parameters.AddWithValue(moduleId);
            var isEntitled = (bool)(await entCommand.ExecuteScalarAsync(cancellationToken) ?? false);
            if (!isEntitled)
            {
                await tx.CommitAsync(cancellationToken);
                return new ModuleAccessDecision(false, false, "Disabled", "module.unentitled");
            }
        }

        // 2. Check scoped module instance lifecycle status
        const string instanceSql = """
            select status from platform.module_instance 
            where tenant_id = $1 and lower(module_id) = lower($2) 
              and (
                (scope_type = 'legal_entity' and scope_id = $3) 
                or (scope_type = 'tenant')
              )
            order by case when scope_type = 'legal_entity' then 0 else 1 end
            limit 1
            """;
        await using var command = new NpgsqlCommand(instanceSql, connection, tx);
        command.Parameters.AddWithValue(tenantId.Value);
        command.Parameters.AddWithValue(moduleId);
        command.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value);
        var rawStatus = await command.ExecuteScalarAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var status = rawStatus?.ToString() ?? "Active";
        var normStatus = status.ToLowerInvariant();

        if (normStatus is "readonly" or "winddown")
        {
            if (isMutation)
            {
                return new ModuleAccessDecision(false, true, status, "module.lifecycle_readonly");
            }
            return new ModuleAccessDecision(true, true, status, null);
        }

        if (normStatus is "disabled" or "failed" or "deactivated" or "requested" or "validating" or "provisioning" or "awaitinggolive")
        {
            return new ModuleAccessDecision(false, false, status, "module.disabled");
        }

        return new ModuleAccessDecision(true, false, "Active", null);
    }

    public async Task<string> GetStatusAsync(TenantId tenantId, string moduleId, Guid? legalEntityId = null, CancellationToken cancellationToken = default)
    {
        var decision = await EvaluateAccessAsync(tenantId, moduleId, legalEntityId, false, cancellationToken);
        return decision.Status;
    }

    public async Task<bool> CanMutateAsync(TenantId tenantId, string moduleId, Guid? legalEntityId = null, CancellationToken cancellationToken = default)
    {
        var decision = await EvaluateAccessAsync(tenantId, moduleId, legalEntityId, true, cancellationToken);
        return decision.Allowed;
    }
}
