using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;

var connectionString = Environment.GetEnvironmentVariable("TODIR_MIGRATOR_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("TODIR_MIGRATOR_CONNECTION is required. API and Worker never apply production migrations.");
    return 2;
}

var repositoryRoot = Environment.GetEnvironmentVariable("TODIR_REPOSITORY_ROOT") ?? Directory.GetCurrentDirectory();
var bootstrapDirectory = Path.Combine(repositoryRoot, "ops", "database", "bootstrap");
var foundationalScripts = new List<string> { "runtime-roles-and-schemas.sql", "tenant-boundary.sql", "organization-schema.sql", "identity-membership-schema.sql" }
    .Select(name => Path.Combine(bootstrapDirectory, name))
    .ToArray();
if (foundationalScripts.Any(script => !File.Exists(script)))
    throw new FileNotFoundException("A required foundational migration script is missing.");

var manifests = DiscoverManifests(repositoryRoot);
var orderedModules = TopologicalOrder(manifests);
var scripts = foundationalScripts.Skip(1)
    .Concat(orderedModules.SelectMany(module => ResolveModuleMigrations(bootstrapDirectory, module)))
    .Append(Path.Combine(bootstrapDirectory, "scheduling-schema.sql"))
    .ToArray();

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
await using (var runtimeCommand = new NpgsqlCommand(await File.ReadAllTextAsync(scripts[0]), connection))
{
    Console.WriteLine($"Applying {Path.GetFileName(scripts[0])}");
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

await using (var setOwner = new NpgsqlCommand("set role erp_owner", connection))
    await setOwner.ExecuteNonQueryAsync();

await using (var history = new NpgsqlCommand("create table if not exists platform_migration.applied_script (module_id text not null, script_name text not null, checksum text not null, applied_at timestamptz not null, primary key (module_id, script_name)); alter table platform_migration.applied_script add column if not exists module_id text; update platform_migration.applied_script set module_id = case when script_name like '%runtime-roles-and-schemas.sql' then 'runtime' when script_name like '%tenant-boundary.sql' then 'tenancy' when script_name like '%organization-schema.sql' then 'org' when script_name like '%identity-membership-schema.sql' then 'iam' when script_name like '%scheduling-schema.sql' then 'scheduling' when script_name like '%platform-kernel-schema.sql' then 'platform-kernel' else coalesce(nullif(module_id, ''), 'legacy') end where module_id is null or module_id = 'legacy'; alter table platform_migration.applied_script alter column module_id set not null; do $$ declare old_primary_key text; begin select constraint_name into old_primary_key from information_schema.table_constraints where table_schema = 'platform_migration' and table_name = 'applied_script' and constraint_type = 'PRIMARY KEY'; if old_primary_key is not null then execute format('alter table platform_migration.applied_script drop constraint %I', old_primary_key); end if; end $$; alter table platform_migration.applied_script add primary key (module_id, script_name)", connection))
    await history.ExecuteNonQueryAsync();

var runtimeName = Path.GetRelativePath(repositoryRoot, foundationalScripts[0]).Replace('\\', '/');
var runtimeChecksum = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(foundationalScripts[0]))).ToLowerInvariant();
await using (var runtimeLookup = new NpgsqlCommand("select checksum from platform_migration.applied_script where module_id = $1 and script_name = $2", connection))
{
    runtimeLookup.Parameters.AddWithValue("runtime"); runtimeLookup.Parameters.AddWithValue(runtimeName);
    var appliedChecksum = (string?)await runtimeLookup.ExecuteScalarAsync();
    if (appliedChecksum is not null && !string.Equals(appliedChecksum, runtimeChecksum, StringComparison.Ordinal)) throw new InvalidOperationException($"Migration checksum changed for {runtimeName}; create a forward-fix migration instead.");
    if (appliedChecksum is null)
    {
        await using var runtimeRecord = new NpgsqlCommand("insert into platform_migration.applied_script (module_id, script_name, checksum, applied_at) values ($1, $2, $3, now())", connection);
        runtimeRecord.Parameters.AddWithValue("runtime"); runtimeRecord.Parameters.AddWithValue(runtimeName); runtimeRecord.Parameters.AddWithValue(runtimeChecksum);
        await runtimeRecord.ExecuteNonQueryAsync();
    }
}

foreach (var script in scripts)
{
    var scriptName = Path.GetRelativePath(repositoryRoot, script).Replace('\\', '/');
    var moduleId = ModuleIdForScript(scriptName);
    var checksum = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(script))).ToLowerInvariant();
    await using (var existing = new NpgsqlCommand("select checksum from platform_migration.applied_script where module_id = $1 and script_name = $2", connection))
    {
        existing.Parameters.AddWithValue(moduleId); existing.Parameters.AddWithValue(scriptName);
        var appliedChecksum = (string?)await existing.ExecuteScalarAsync();
        if (appliedChecksum is not null)
        {
            if (!string.Equals(appliedChecksum, checksum, StringComparison.Ordinal)) throw new InvalidOperationException($"Migration checksum changed for {scriptName}; create a forward-fix migration instead.");
            Console.WriteLine($"Already applied {scriptName}");
            continue;
        }
    }

    Console.WriteLine($"Applying {scriptName}");
    await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(script), connection);
    await command.ExecuteNonQueryAsync();
    await using var record = new NpgsqlCommand("insert into platform_migration.applied_script (module_id, script_name, checksum, applied_at) values ($1, $2, $3, now())", connection);
    record.Parameters.AddWithValue(moduleId); record.Parameters.AddWithValue(scriptName); record.Parameters.AddWithValue(checksum);
    await record.ExecuteNonQueryAsync();
}

await using (var resetOwner = new NpgsqlCommand("reset role", connection))
    await resetOwner.ExecuteNonQueryAsync();

await using (var dispatcherOwnership = new NpgsqlCommand("alter function platform_messaging.claim_pending_outbox(integer) owner to erp_dispatcher", connection))
    await dispatcherOwnership.ExecuteNonQueryAsync();
await using (var dispatcherBacklogOwnership = new NpgsqlCommand("alter function platform_messaging.observe_pending_outbox() owner to erp_dispatcher", connection))
    await dispatcherBacklogOwnership.ExecuteNonQueryAsync();
await using (var supportExpiryOwnership = new NpgsqlCommand("alter function platform_iam.expire_support_access_grants() owner to erp_dispatcher", connection))
    await supportExpiryOwnership.ExecuteNonQueryAsync();

Console.WriteLine($"Applied or verified {scripts.Length} ordered platform scripts from {manifests.Count} module manifests.");
return 0;

static string ModuleIdForScript(string scriptName)
{
    var file = Path.GetFileName(scriptName);
    if (file is "tenant-boundary.sql") return "tenancy";
    if (file is "organization-schema.sql") return "org";
    if (file is "identity-membership-schema.sql") return "iam";
    if (file is "scheduling-schema.sql") return "scheduling";
    if (file is "platform-kernel-schema.sql") return "platform-kernel";
    if (file.EndsWith("-schema.sql", StringComparison.Ordinal)) return file[..^"-schema.sql".Length];
    const string forwardFixMarker = "-forward-fix-";
    var marker = file.IndexOf(forwardFixMarker, StringComparison.Ordinal);
    if (marker > 0 && file.EndsWith(".sql", StringComparison.Ordinal)) return file[..marker];
    throw new InvalidOperationException($"No module migration identity is declared for {scriptName}.");
}

static IReadOnlyDictionary<string, ModuleManifest> DiscoverManifests(string repositoryRoot)
{
    var manifestRoot = Path.Combine(repositoryRoot, "src", "backend");
    var manifests = new Dictionary<string, ModuleManifest>(StringComparer.Ordinal);
    foreach (var file in Directory.EnumerateFiles(manifestRoot, "module.manifest.json", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
    {
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
    return [basePath, .. Directory.EnumerateFiles(bootstrapDirectory, $"{module.Id}-forward-fix-*.sql").OrderBy(path => path, StringComparer.Ordinal)];
}

sealed record ModuleManifest(string Id, IReadOnlyList<string> HardDependencies);
