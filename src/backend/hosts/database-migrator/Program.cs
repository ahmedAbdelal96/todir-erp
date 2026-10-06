using Npgsql;

var connectionString = Environment.GetEnvironmentVariable("TODIR_MIGRATOR_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("TODIR_MIGRATOR_CONNECTION is required. API and Worker never apply production migrations.");
    return 2;
}

var repositoryRoot = Environment.GetEnvironmentVariable("TODIR_REPOSITORY_ROOT") ?? Directory.GetCurrentDirectory();
var bootstrapDirectory = Path.Combine(repositoryRoot, "ops", "database", "bootstrap");
var order = new[] { "runtime-roles-and-schemas.sql", "tenant-boundary.sql", "organization-schema.sql", "identity-membership-schema.sql", "platform-kernel-schema.sql" };
var scripts = order.Select(name => Path.Combine(bootstrapDirectory, name)).Where(File.Exists)
    .ToArray();

await using var dataSource = NpgsqlDataSource.Create(connectionString);
await using var connection = await dataSource.OpenConnectionAsync();
if (scripts.Length == 0)
{
    Console.Error.WriteLine($"No bootstrap scripts found under {bootstrapDirectory}.");
    return 3;
}

await using (var runtimeCommand = new NpgsqlCommand(await File.ReadAllTextAsync(scripts[0]), connection))
{
    Console.WriteLine($"Applying {Path.GetFileName(scripts[0])}");
    await runtimeCommand.ExecuteNonQueryAsync();
}

await using (var setOwner = new NpgsqlCommand("set role erp_owner", connection))
    await setOwner.ExecuteNonQueryAsync();

foreach (var script in scripts.Skip(1))
{
    Console.WriteLine($"Applying {Path.GetFileName(script)}");
    await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(script), connection);
    await command.ExecuteNonQueryAsync();
}

await using (var resetOwner = new NpgsqlCommand("reset role", connection))
    await resetOwner.ExecuteNonQueryAsync();

await using (var dispatcherOwnership = new NpgsqlCommand("alter function platform_messaging.claim_pending_outbox(integer) owner to erp_dispatcher", connection))
    await dispatcherOwnership.ExecuteNonQueryAsync();

Console.WriteLine($"Applied {scripts.Length} ordered platform scripts.");
return 0;
