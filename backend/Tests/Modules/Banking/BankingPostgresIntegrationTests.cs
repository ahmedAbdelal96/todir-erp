using Modules.Banking.Repositories;
using Shared.Primitives.Identifiers;
using Xunit;
using Npgsql;

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
}
