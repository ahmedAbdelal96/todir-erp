using System.Security.Cryptography;
using System.Text.Json;
using DbMigrator.SeedData;
using Npgsql;

var seedDevelopment = args.Length == 2 && string.Equals(args[0], "--seed", StringComparison.OrdinalIgnoreCase) && string.Equals(args[1], "development", StringComparison.OrdinalIgnoreCase);
var rebuildTenantId = Guid.Empty;
var rebuildEntityId = Guid.Empty;
var rebuildSubledger = args.Length == 3 && string.Equals(args[0], "--rebuild-subledger-cache", StringComparison.OrdinalIgnoreCase)
    && Guid.TryParse(args[1], out rebuildTenantId) && Guid.TryParse(args[2], out rebuildEntityId);
if (args.Any(argument => string.Equals(argument, "--seed", StringComparison.OrdinalIgnoreCase)) && !seedDevelopment)
{
    Console.Error.WriteLine("Supported seed mode: --seed development");
    return 2;
}
if (args.Any(argument => string.Equals(argument, "--rebuild-subledger-cache", StringComparison.OrdinalIgnoreCase)) && !rebuildSubledger)
{
    Console.Error.WriteLine("Usage: --rebuild-subledger-cache <tenant-guid> <legal-entity-guid>");
    return 2;
}
if (seedDevelopment)
{
    try { DevelopmentSeedEnvironment.RequireSupported(); }
    catch (InvalidOperationException exception) { Console.Error.WriteLine(exception.Message); return 2; }
}
var connectionString = Environment.GetEnvironmentVariable("TODIR_MIGRATOR_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("TODIR_MIGRATOR_CONNECTION is required. API and Worker never apply production migrations.");
    return 2;
}

var repositoryRoot = Environment.GetEnvironmentVariable("TODIR_REPOSITORY_ROOT") ?? Directory.GetCurrentDirectory();
var bootstrapDirectory = Path.Combine(repositoryRoot, "backend", "DbMigrator", "Database", "bootstrap");
var runtimeScript = Path.Combine(bootstrapDirectory, "runtime-roles-and-schemas.sql");
if (!File.Exists(runtimeScript)) throw new FileNotFoundException("The runtime role migration script is missing.", runtimeScript);

var manifests = DiscoverManifests(repositoryRoot);
var orderedModules = TopologicalOrder(manifests);
var scripts = orderedModules.SelectMany(module => ResolveModuleMigrations(bootstrapDirectory, module))
    .Append(Path.Combine(bootstrapDirectory, "platform-rls-finalization.sql"))
    .ToArray();
if (scripts.Any(script => !File.Exists(script)))
    throw new FileNotFoundException("A required migration script is missing from the canonical DbMigrator database directory.");

await using var dataSource = NpgsqlDataSource.Create(connectionString);
NpgsqlConnection? connection = null;
for (var attempt = 1; attempt <= 60; attempt++)
{
    try
    {
        connection = await dataSource.OpenConnectionAsync();
        break;
    }
    catch (NpgsqlException) when (attempt < 60)
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
    }
}

if (connection is null)
{
    Console.Error.WriteLine("Database did not become available within the migration startup window.");
    return 4;
}

await using var ownedConnection = connection;
await using (var runtimeCommand = new NpgsqlCommand(await File.ReadAllTextAsync(runtimeScript), connection))
{
    Console.WriteLine($"Applying {Path.GetFileName(runtimeScript)}");
    await runtimeCommand.ExecuteNonQueryAsync();
}

var runtimePassword = Environment.GetEnvironmentVariable("TODIR_RUNTIME_PASSWORD");
if (!string.IsNullOrWhiteSpace(runtimePassword))
{
    await using var passwordCommand = new NpgsqlCommand("select format('alter role erp_app password %L', $1)", connection);
    passwordCommand.Parameters.AddWithValue(runtimePassword);
    var alterPassword = (string?)await passwordCommand.ExecuteScalarAsync();
    if (string.IsNullOrWhiteSpace(alterPassword)) throw new InvalidOperationException("runtime password command was not generated");
    await using var applyPassword = new NpgsqlCommand(alterPassword, connection);
    await applyPassword.ExecuteNonQueryAsync();
}

await using (var ownerDatabaseGrant = new NpgsqlCommand("select format('grant create on database %I to erp_owner', current_database())", connection))
{
    var grant = (string?)await ownerDatabaseGrant.ExecuteScalarAsync();
    if (string.IsNullOrWhiteSpace(grant)) throw new InvalidOperationException("migration owner database grant command was not generated");
    await using var applyGrant = new NpgsqlCommand(grant, connection);
    await applyGrant.ExecuteNonQueryAsync();
}

// Databases restored from the pre-canonical layout can contain current
// foundation tables owned by the restore administrator. Normalize ownership
// before SET ROLE so the migration owner can perform the guarded consolidation.
    await using (var normalizeCanonicalOwnership = new NpgsqlCommand("""
        do $$
        declare item record;
        begin
            for item in
                select nspname from pg_namespace
                where (nspname in ('platform', 'platform_migration', 'sample')
                    or nspname like 'foundation_%'
                    or nspname like 'business_%'
                    or nspname like 'finance_%')
                  and pg_get_userbyid(nspowner) <> 'erp_owner'
            loop
                execute format('alter schema %I owner to erp_owner', item.nspname);
            end loop;

            for item in
                select relation_namespace.nspname as schema_name, relation.relname as relation_name, relation.relkind
                from pg_class relation
                join pg_namespace relation_namespace on relation_namespace.oid = relation.relnamespace
                where (relation_namespace.nspname in ('platform', 'platform_migration', 'sample')
                    or relation_namespace.nspname like 'foundation_%'
                    or relation_namespace.nspname like 'business_%'
                    or relation_namespace.nspname like 'finance_%')
                  and relation.relkind in ('r', 'S')
                  and pg_get_userbyid(relation.relowner) <> 'erp_owner'
            loop
                execute format('alter %s %I.%I owner to erp_owner', case when item.relkind = 'S' then 'sequence' else 'table' end, item.schema_name, item.relation_name);
            end loop;
        end $$
        """, connection))
    await normalizeCanonicalOwnership.ExecuteNonQueryAsync();

await using (var setOwner = new NpgsqlCommand("set role erp_owner", connection))
    await setOwner.ExecuteNonQueryAsync();

// A restored database may already have its baseline scripts recorded, so the
// session cannot rely on a skipped pg_dump-style baseline to disable RLS while
// applying forward-fix migrations. Migration ownership is explicit here; seed
// transactions reset this session setting before they run as the runtime role.
await using (var disableRowSecurityForMigration = new NpgsqlCommand("set row_security = off", connection))
    await disableRowSecurityForMigration.ExecuteNonQueryAsync();

await using (var history = new NpgsqlCommand("create table if not exists platform_migration.applied_script (module_id text not null, script_name text not null, checksum text not null, applied_at timestamptz not null, primary key (module_id, script_name)); alter table platform_migration.applied_script add column if not exists module_id text; update platform_migration.applied_script set module_id = case when script_name like '%runtime-roles-and-schemas.sql' then 'runtime' when script_name like '%tenant-boundary.sql' then 'tenancy' when script_name like '%organization-schema.sql' then 'org' when script_name like '%identity-membership-schema.sql' then 'iam' when script_name like '%scheduling-schema.sql' then 'scheduling' when script_name like '%platform-kernel-schema.sql' then 'platform-kernel' else coalesce(nullif(module_id, ''), 'legacy') end where module_id is null or module_id = 'legacy'; alter table platform_migration.applied_script alter column module_id set not null; do $$ declare old_primary_key text; begin select constraint_name into old_primary_key from information_schema.table_constraints where table_schema = 'platform_migration' and table_name = 'applied_script' and constraint_type = 'PRIMARY KEY'; if old_primary_key is not null then execute format('alter table platform_migration.applied_script drop constraint %I', old_primary_key); end if; end $$; alter table platform_migration.applied_script add primary key (module_id, script_name)", connection))
    await history.ExecuteNonQueryAsync();

var runtimeName = Path.GetRelativePath(repositoryRoot, runtimeScript).Replace('\\', '/');
var runtimeChecksum = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(runtimeScript))).ToLowerInvariant();
var knownRuntimeRelocationChecksums = new HashSet<string>(StringComparer.Ordinal)
{
    "e5d4937d040040e9dd230d422ade45082d741f762c7ec433ec9b0e6ee26b7442",
    "e02f00d8a78758ece2dc84271d9d5c1cf021f249c3eea83fa068e2dbcacf9a2f"
};
await using (var runtimeLookup = new NpgsqlCommand("select checksum from platform_migration.applied_script where module_id = $1 and script_name = $2", connection))
{
    runtimeLookup.Parameters.AddWithValue("runtime"); runtimeLookup.Parameters.AddWithValue(runtimeName);
    var appliedChecksum = (string?)await runtimeLookup.ExecuteScalarAsync();
    if (appliedChecksum is not null && !string.Equals(appliedChecksum, runtimeChecksum, StringComparison.Ordinal))
    {
        if (!knownRuntimeRelocationChecksums.Contains(appliedChecksum))
            throw new InvalidOperationException($"Migration checksum changed for {runtimeName}; create a forward-fix migration instead.");

        await using var relocatedRuntimeRecord = new NpgsqlCommand("update platform_migration.applied_script set checksum = $1, applied_at = now() where module_id = $2 and script_name = $3", connection);
        relocatedRuntimeRecord.Parameters.AddWithValue(runtimeChecksum);
        relocatedRuntimeRecord.Parameters.AddWithValue("runtime");
        relocatedRuntimeRecord.Parameters.AddWithValue(runtimeName);
        await relocatedRuntimeRecord.ExecuteNonQueryAsync();
    }
    if (appliedChecksum is null)
    {
        await using var runtimeRecord = new NpgsqlCommand("insert into platform_migration.applied_script (module_id, script_name, checksum, applied_at) values ($1, $2, $3, now())", connection);
        runtimeRecord.Parameters.AddWithValue("runtime"); runtimeRecord.Parameters.AddWithValue(runtimeName); runtimeRecord.Parameters.AddWithValue(runtimeChecksum);
        await runtimeRecord.ExecuteNonQueryAsync();
    }
}

// The canonical database directory was moved from ops/database to the locked
// backend/DbMigrator/Database location. Reconcile only the known path move for
// already-applied schema baselines; unknown checksum changes still fail below.
foreach (var script in scripts.Where(path => Path.GetFileName(path).EndsWith("-schema.sql", StringComparison.Ordinal)))
{
    var scriptName = Path.GetRelativePath(repositoryRoot, script).Replace('\\', '/');
    var moduleId = ModuleIdForScript(scriptName);
    var checksum = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(script))).ToLowerInvariant();
    var legacyScriptName = $"ops/database/bootstrap/{Path.GetFileName(script)}";
    await using var legacyLookup = new NpgsqlCommand("select script_name from platform_migration.applied_script where module_id = $1 and script_name = $2", connection);
    legacyLookup.Parameters.AddWithValue(moduleId);
    legacyLookup.Parameters.AddWithValue(legacyScriptName);
    var legacyApplied = (string?)await legacyLookup.ExecuteScalarAsync();
    if (legacyApplied is null) continue;

    await using var currentLookup = new NpgsqlCommand("select 1 from platform_migration.applied_script where module_id = $1 and script_name = $2", connection);
    currentLookup.Parameters.AddWithValue(moduleId);
    currentLookup.Parameters.AddWithValue(scriptName);
    if (await currentLookup.ExecuteScalarAsync() is not null) continue;

    await using var relocatedRecord = new NpgsqlCommand("insert into platform_migration.applied_script (module_id, script_name, checksum, applied_at) values ($1, $2, $3, now())", connection);
    relocatedRecord.Parameters.AddWithValue(moduleId);
    relocatedRecord.Parameters.AddWithValue(scriptName);
    relocatedRecord.Parameters.AddWithValue(checksum);
    await relocatedRecord.ExecuteNonQueryAsync();
    Console.WriteLine($"Reconciled relocated migration history {legacyScriptName} -> {scriptName}");
}

foreach (var script in scripts)
{
    var scriptName = Path.GetRelativePath(repositoryRoot, script).Replace('\\', '/');
    var moduleId = ModuleIdForScript(scriptName);
    var checksum = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(script))).ToLowerInvariant();
    string? appliedChecksum;
    await using (var existing = new NpgsqlCommand("select checksum from platform_migration.applied_script where module_id = $1 and script_name = $2", connection))
    {
        existing.Parameters.AddWithValue(moduleId); existing.Parameters.AddWithValue(scriptName);
        appliedChecksum = (string?)await existing.ExecuteScalarAsync();
    }

    var isRlsFinalization = Path.GetFileName(script).Equals("platform-rls-finalization.sql", StringComparison.Ordinal);
    if (appliedChecksum is not null && !string.Equals(appliedChecksum, checksum, StringComparison.Ordinal))
        throw new InvalidOperationException($"Migration checksum changed for {scriptName}; create a forward-fix migration instead.");
    if (appliedChecksum is not null && !isRlsFinalization)
    {
        Console.WriteLine($"Already applied {scriptName}");
        continue;
    }

    Console.WriteLine(appliedChecksum is null ? $"Applying {scriptName}" : $"Reapplying idempotent finalization {scriptName}");
    var maintenanceTables = Path.GetFileName(script) switch
    {
        "catalog-forward-fix-product-template-code.sql" => new[] { "foundation_catalog.product_template" },
        "finbase-forward-fix-canonical-ratio-due-rules.sql" => new[] { "foundation_finbase.payment_term_rule" },
        "finbase-forward-fix-rounding-contexts.sql" => new[] { "foundation_finbase.financial_rounding_policy" },
        "ledger-forward-fix-z-accounting-configuration-mappings.sql" => new[] { "finance_ledger.account_mapping_version", "finance_ledger.accounting_configuration_version", "finance_ledger.ledger_entity_configuration", "finance_ledger.journal_entry", "finance_ledger.journal_entry_line" },
        "ledger-forward-fix-invoicing-debit-notes.sql" => new[] { "finance_ledger.ledger_entity_configuration", "finance_ledger.accounting_configuration_version", "finance_ledger.fact_producer_registration", "finance_ledger.posting_rule", "finance_ledger.posting_rule_version" },
        _ => null
    };
    if (Path.GetFileName(script) is "finbase-forward-fix-provider-history.sql" or "pricing-schema.sql")
    {
        // The SQL function body is validated when created. Compile it with normal
        // RLS semantics; its fixed-parameter dispatcher owner governs runtime access.
        await using var migrationTransaction = await connection.BeginTransactionAsync();
        try
        {
            await using (var rowSecurity = new NpgsqlCommand("set local row_security = on", connection, migrationTransaction))
                await rowSecurity.ExecuteNonQueryAsync();
            await using (var command = new NpgsqlCommand(await File.ReadAllTextAsync(script), connection, migrationTransaction))
            {
                command.CommandTimeout = 0;
                await command.ExecuteNonQueryAsync();
            }
            if (appliedChecksum is null)
            {
                await using var record = new NpgsqlCommand("insert into platform_migration.applied_script (module_id, script_name, checksum, applied_at) values ($1, $2, $3, now())", connection, migrationTransaction);
                record.Parameters.AddWithValue(moduleId); record.Parameters.AddWithValue(scriptName); record.Parameters.AddWithValue(checksum);
                await record.ExecuteNonQueryAsync();
            }
            await migrationTransaction.CommitAsync();
        }
        catch
        {
            await migrationTransaction.RollbackAsync();
            throw;
        }
    }
    else if (maintenanceTables is not null)
    {
        // These data corrections must see rows across tenants. Keep the maintenance
        // override transaction-scoped so FORCE RLS is never observable as disabled.
        await using var migrationTransaction = await connection.BeginTransactionAsync();
        var restoreForcedRls = new List<string>();
        try
        {
            foreach (var maintenanceTable in maintenanceTables)
            {
                await using var forceState = new NpgsqlCommand("select relforcerowsecurity from pg_class where oid = $1::regclass", connection, migrationTransaction);
                forceState.Parameters.AddWithValue(maintenanceTable);
                if ((bool)(await forceState.ExecuteScalarAsync() ?? false)) restoreForcedRls.Add(maintenanceTable);
            }

            foreach (var maintenanceTable in restoreForcedRls)
            {
                await using var relaxForce = new NpgsqlCommand($"alter table {maintenanceTable} no force row level security", connection, migrationTransaction);
                await relaxForce.ExecuteNonQueryAsync();
            }

            await using (var command = new NpgsqlCommand(await File.ReadAllTextAsync(script), connection, migrationTransaction))
            {
                command.CommandTimeout = 0;
                await command.ExecuteNonQueryAsync();
            }

            foreach (var maintenanceTable in restoreForcedRls)
            {
                await using var restoreForce = new NpgsqlCommand($"alter table {maintenanceTable} force row level security", connection, migrationTransaction);
                await restoreForce.ExecuteNonQueryAsync();
            }

            await using (var record = new NpgsqlCommand("insert into platform_migration.applied_script (module_id, script_name, checksum, applied_at) values ($1, $2, $3, now())", connection, migrationTransaction))
            {
                record.Parameters.AddWithValue(moduleId); record.Parameters.AddWithValue(scriptName); record.Parameters.AddWithValue(checksum);
                await record.ExecuteNonQueryAsync();
            }

            await migrationTransaction.CommitAsync();
        }
        catch
        {
            await migrationTransaction.RollbackAsync();
            throw;
        }
    }
    else
    {
        await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(script), connection);
        command.CommandTimeout = 0;
        await command.ExecuteNonQueryAsync();
        if (appliedChecksum is null)
        {
            await using var record = new NpgsqlCommand("insert into platform_migration.applied_script (module_id, script_name, checksum, applied_at) values ($1, $2, $3, now())", connection);
            record.Parameters.AddWithValue(moduleId); record.Parameters.AddWithValue(scriptName); record.Parameters.AddWithValue(checksum);
            await record.ExecuteNonQueryAsync();
        }
    }
}

// Schema baselines are pg_dump-style scripts that set row_security=off. Do not
// carry that migration-only session setting into development seed transactions.
await using (var resetRowSecurity = new NpgsqlCommand("reset row_security", connection))
    await resetRowSecurity.ExecuteNonQueryAsync();

await using (var resetOwner = new NpgsqlCommand("reset role", connection))
    await resetOwner.ExecuteNonQueryAsync();

await using (var identityAccessContextOwnership = new NpgsqlCommand("alter function platform.resolve_login_domain(text) owner to erp_dispatcher", connection))
    await identityAccessContextOwnership.ExecuteNonQueryAsync();
await using (var identityTenantResolverOwnership = new NpgsqlCommand("alter function platform.resolve_identity_tenant(uuid) owner to erp_dispatcher", connection))
    await identityTenantResolverOwnership.ExecuteNonQueryAsync();

await using (var dispatcherOwnership = new NpgsqlCommand("alter function platform.claim_pending_outbox(integer) owner to erp_dispatcher", connection))
    await dispatcherOwnership.ExecuteNonQueryAsync();
await using (var dispatcherBacklogOwnership = new NpgsqlCommand("alter function platform.observe_pending_outbox() owner to erp_dispatcher", connection))
    await dispatcherBacklogOwnership.ExecuteNonQueryAsync();
await using (var financeRateSourceOwnership = new NpgsqlCommand("alter function foundation_finbase.claim_due_exchange_rate_sources(integer) owner to erp_dispatcher", connection))
    await financeRateSourceOwnership.ExecuteNonQueryAsync();
await using (var supportExpiryOwnership = new NpgsqlCommand("alter function platform.expire_support_access_grants() owner to erp_dispatcher", connection))
    await supportExpiryOwnership.ExecuteNonQueryAsync();

if (rebuildSubledger)
{
    try
    {
        var changedRows = await DbMigrator.SubledgerProjectionRebuilder.RunAsync(ownedConnection, rebuildTenantId, rebuildEntityId);
        Console.WriteLine($"Subledger open-item cache rebuild committed; changed rows: {changedRows}.");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Subledger open-item cache rebuild rolled back: {exception.Message}");
        return 5;
    }
}

if (seedDevelopment)
{
    try
    {
        var summary = await SeedRunner.RunAsync(connection);
        summary.Print(Console.Out);
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Development seed failed: {exception}");
        return 3;
    }
}

Console.WriteLine($"Applied or verified {scripts.Length} ordered platform scripts from {manifests.Count} module manifests.");
return 0;

static string ModuleIdForScript(string scriptName)
{
    var file = Path.GetFileName(scriptName);
    if (file is "platform-rls-finalization.sql") return "platform-kernel";
    if (file is "platform-kernel-schema.sql") return "platform-kernel";
    if (file.EndsWith("-schema.sql", StringComparison.Ordinal)) return file[..^"-schema.sql".Length];
    const string forwardFixMarker = "-forward-fix-";
    var marker = file.IndexOf(forwardFixMarker, StringComparison.Ordinal);
    if (marker > 0 && file.EndsWith(".sql", StringComparison.Ordinal)) return file[..marker];
    throw new InvalidOperationException($"No module migration identity is declared for {scriptName}.");
}

static IReadOnlyDictionary<string, ModuleManifest> DiscoverManifests(string repositoryRoot)
{
    var manifestRoot = Path.Combine(repositoryRoot, "backend");
    var manifests = new Dictionary<string, ModuleManifest>(StringComparer.Ordinal);
    foreach (var file in Directory.EnumerateFiles(manifestRoot, "module.manifest.json", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
    {
        var relativePath = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
        if (!relativePath.StartsWith("backend/Core/", StringComparison.OrdinalIgnoreCase) &&
            !relativePath.StartsWith("backend/Modules/", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(file));
        var root = document.RootElement;
        var id = root.GetProperty("id").GetString() ?? throw new InvalidOperationException($"Manifest {file} has no id.");
        var hardDependencies = root.GetProperty("dependencies").GetProperty("hard").EnumerateArray().Select(value => value.GetString() ?? throw new InvalidOperationException($"Manifest {file} contains an empty dependency.")).ToArray();
        if (!manifests.TryAdd(id, new ModuleManifest(id, hardDependencies))) throw new InvalidOperationException($"Duplicate module manifest id: {id}.");
    }

    if (manifests.Count == 0) throw new InvalidOperationException("No backend module manifests were discovered.");
    return manifests;
}

static IReadOnlyList<ModuleManifest> TopologicalOrder(IReadOnlyDictionary<string, ModuleManifest> manifests)
{
    var state = new Dictionary<string, int>(StringComparer.Ordinal);
    var ordered = new List<ModuleManifest>();
    void Visit(string id)
    {
        if (!manifests.TryGetValue(id, out var module)) throw new InvalidOperationException($"Module dependency is not declared: {id}.");
        if (state.TryGetValue(id, out var current))
        {
            if (current == 1) throw new InvalidOperationException($"Module migration dependency cycle includes {id}.");
            if (current == 2) return;
        }
        state[id] = 1;
        foreach (var dependency in module.HardDependencies.OrderBy(value => value, StringComparer.Ordinal)) Visit(dependency);
        state[id] = 2;
        ordered.Add(module);
    }

    foreach (var id in manifests.Keys.OrderBy(value => value, StringComparer.Ordinal)) Visit(id);
    return ordered;
}

static IReadOnlyList<string> ResolveModuleMigrations(string bootstrapDirectory, ModuleManifest module)
{
    var basePath = Path.Combine(bootstrapDirectory, $"{module.Id}-schema.sql");
    if (!File.Exists(basePath)) throw new FileNotFoundException($"No migration script exists for module manifest {module.Id}.", basePath);
    var fixes = Directory.EnumerateFiles(bootstrapDirectory, $"{module.Id}-forward-fix-*.sql")
        .OrderBy(path => MigrationPriority(Path.GetFileName(path)), Comparer<int>.Default)
        .ThenBy(path => path, StringComparer.Ordinal);
    return [basePath, .. fixes];
}

static int MigrationPriority(string fileName) => fileName switch
{
    _ when fileName.EndsWith("-authentication-challenges.sql", StringComparison.Ordinal) => 0,
    _ when fileName.EndsWith("-tenant-bound-identity.sql", StringComparison.Ordinal) => 1,
    _ when fileName.Equals("catalog-forward-fix-import.sql", StringComparison.Ordinal) => 2,
    _ when fileName.Equals("catalog-forward-fix-import-reconciliation.sql", StringComparison.Ordinal) => 3,
    _ when fileName.Equals("catalog-forward-fix-import-worker.sql", StringComparison.Ordinal) => 4,
    _ when fileName.Equals("ledger-forward-fix-invoicing-debit-notes.sql", StringComparison.Ordinal) => 6,
    // This index references write_off, which is introduced by the later-named Subledger pass-2 migration.
    _ when fileName.Equals("subledger-forward-fix-exposure-current-read.sql", StringComparison.Ordinal) => 6,
    _ => 5
};

sealed record ModuleManifest(string Id, IReadOnlyList<string> HardDependencies);
