using Modules.Banking.Repositories;
using Modules.Banking.DTOs;
using Shared.Primitives.Identifiers;
using Xunit;
using Npgsql;
using System.Text.Json;

namespace Modules.Banking.Tests;

public sealed class BankingPostgresIntegrationTests
{
    [Fact]
    public async Task SeededTreasuryStatementsAndCashPositionAreReadThroughTheRepository()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_BANKING_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new BankingRepository(dataSource, TimeProvider.System);
        var tenant = new TenantId(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");

        var accounts = await repository.ListAccountsAsync(tenant, entity, null, null, null, 1, 100, default);
        Assert.True(accounts.TotalCount >= 4);
        var statementItems = (await repository.ListStatementsAsync(tenant, entity, accounts.Items[0].Id, 1, 100, default)).Items;
        var statement = statementItems.Count > 0 ? statementItems[0] : null;
        Assert.NotNull(statement);
        var lines = await repository.ListStatementLinesAsync(tenant, entity, statement!.Id, 1, 100, default);
        Assert.NotEmpty(lines.Items);
        var position = await repository.GetCashPositionAsync(tenant, entity, default);
        Assert.NotEmpty(position);
        Assert.All(position, item => Assert.False(string.IsNullOrWhiteSpace(item.BalanceBasis)));
    }

    [Fact]
    public async Task WorkflowCommandsValidateStatementsForecastsAndMakerCheckerChanges()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_BANKING_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new BankingRepository(dataSource, TimeProvider.System);
        var tenant = new TenantId(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
        var entity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");
        var actor = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var account = (await repository.ListAccountsAsync(tenant, entity, null, null, null, 1, 100, default)).Items[0];
        var statement = (await repository.ListStatementsAsync(tenant, entity, account.Id, 1, 100, default)).Items[0];
        var line = (await repository.ListStatementLinesAsync(tenant, entity, statement.Id, 1, 100, default)).Items[0];

        var classified = await repository.ClassifyStatementLineAsync(tenant, actor, entity, line.Id, new("Receipt", "Customer receipt candidate"), default);
        Assert.Equal("Receipt", classified.Classification);
        var validated = await repository.ValidateStatementAsync(tenant, actor, entity, statement.Id, new(), default);
        Assert.Equal("Validated", validated.ProcessingState);

        var forecastDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(Random.Shared.Next(1000, 100000));
        var version = await repository.CreateForecastVersionAsync(tenant, actor, new(entity, forecastDate, 13, "Draft"), default);
        var item = await repository.CreateForecastItemAsync(tenant, actor, new(entity, version.Id, account.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), "Inflow", 125m, "EGP", "Manual", "Acceptance forecast", null), default);
        Assert.Equal(version.Id, (await repository.GetForecastAsync(tenant, entity, version.AsOfDate, version.HorizonDays, default))!.Id);
        Assert.Equal(version.Id, (await repository.GetForecastVersionAsync(tenant, entity, version.Id, default))!.Id);
        Assert.Equal("Estimated", item.Confidence);

        using var payload = JsonDocument.Parse("{\"maskedIdentifier\":\"****1234\"}");
        var change = await repository.CreateAccountChangeRequestAsync(tenant, actor, entity, account.Id, new(payload.RootElement.Clone(), "Update masked bank identifier", account.Version), default);
        var approved = await repository.DecideAccountChangeRequestAsync(tenant, actor, entity, change.Id, new("Approved", change.Version), default);
        Assert.Equal("Approved", approved.Status);

        var connection = await repository.CreateConnectionAsync(tenant, actor, entity, account.Id, new($"acceptance-{Guid.CreateVersion7():N}", "connector://acceptance"), default);
        var disconnected = await repository.ChangeConnectionStateAsync(tenant, actor, entity, connection.Id, "Disconnected", connection.Version, default);
        Assert.Equal("Disconnected", disconnected.Status);
    }
}
