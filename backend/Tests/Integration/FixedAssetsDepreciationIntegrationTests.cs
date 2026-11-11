using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace KernelIntegrationTests;

public sealed class FixedAssetsDepreciationIntegrationTests
{
    private const string TenantId = "c9fea3c8-249d-f150-a53a-84f5a891c82b";
    private const string LegalEntityId = "aad8a34b-344c-9e5b-8d58-57f182fc6528";
    private const string BookDefinitionId = "73f31634-d275-1554-9c13-ac33d1e040d0";

    [Fact]
    public async Task DepreciationRunPostsLedgerFactAndBalancedJournalEntry()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for Fixed Assets integration verification.");
        var password = Environment.GetEnvironmentVariable("SEED_DEFAULT_PASSWORD")
            ?? throw new InvalidOperationException("Set SEED_DEFAULT_PASSWORD for Fixed Assets integration verification.");

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        Assert.NotNull(csrf);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "admin@atlas.com", password })
        };
        login.Headers.Add("X-CSRF-TOKEN", csrf.Token);
        using var loginResponse = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var idempotencyKey = $"fixed-assets-integration-{Guid.CreateVersion7():N}";
        var request = new
        {
            legalEntityId = Guid.Parse(LegalEntityId),
            bookId = Guid.Parse(BookDefinitionId),
            periodStart = "2026-11-01",
            periodEnd = "2026-11-30",
            idempotencyKey
        };
        var prepareCsrf = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        Assert.NotNull(prepareCsrf);
        using var prepare = new HttpRequestMessage(HttpMethod.Post, "/api/v1/fixed-assets/depreciation-runs/prepare")
        {
            Content = JsonContent.Create(request)
        };
        prepare.Headers.Add("X-CSRF-TOKEN", prepareCsrf.Token);
        using var prepareResponse = await client.SendAsync(prepare);
        var prepareBody = await prepareResponse.Content.ReadAsStringAsync();
        Assert.True(prepareResponse.StatusCode == HttpStatusCode.OK, prepareBody);
        using var preparedJson = JsonDocument.Parse(prepareBody);
        var runId = preparedJson.RootElement.GetProperty("id").GetGuid();
        Assert.True(preparedJson.RootElement.GetProperty("totalAmount").GetDecimal() > 0m);

        var retryCsrf = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        Assert.NotNull(retryCsrf);
        using var retry = new HttpRequestMessage(HttpMethod.Post, "/api/v1/fixed-assets/depreciation-runs/prepare")
        {
            Content = JsonContent.Create(new
            {
                legalEntityId = Guid.Parse(LegalEntityId),
                bookId = Guid.Parse(BookDefinitionId),
                periodStart = "2026-11-01",
                periodEnd = "2026-11-30",
                idempotencyKey = idempotencyKey + "-different"
            })
        };
        retry.Headers.Add("X-CSRF-TOKEN", retryCsrf.Token);
        using var retryResponse = await client.SendAsync(retry);
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        using var retryJson = JsonDocument.Parse(await retryResponse.Content.ReadAsStringAsync());
        Assert.Equal(runId, retryJson.RootElement.GetProperty("id").GetGuid());

        var postCsrf = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        Assert.NotNull(postCsrf);
        using var post = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/fixed-assets/depreciation-runs/{runId:D}/post?legalEntityId={LegalEntityId}");
        post.Headers.Add("X-CSRF-TOKEN", postCsrf.Token);
        using var postResponse = await client.SendAsync(post);
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var runCommand = new NpgsqlCommand("select status,total_amount from business_assets.asset_depreciation_run where tenant_id=$1 and id=$2", connection);
        runCommand.Parameters.AddWithValue(Guid.Parse(TenantId));
        runCommand.Parameters.AddWithValue(runId);
        await using var runReader = await runCommand.ExecuteReaderAsync();
        Assert.True(await runReader.ReadAsync());
        Assert.Equal("Posted", runReader.GetString(0));
        Assert.True(runReader.GetDecimal(1) > 0m);
        await runReader.CloseAsync();

        await using var transactionCommand = new NpgsqlCommand("select ledger_fact_reference from business_assets.asset_book_transaction where tenant_id=$1 and idempotency_key=$2", connection);
        transactionCommand.Parameters.AddWithValue(Guid.Parse(TenantId));
        transactionCommand.Parameters.AddWithValue($"depreciation:{runId:D}:eba3c9e3-db53-e852-8ed1-4d34f2dd5f8b");
        var ledgerFactReference = (string?)await transactionCommand.ExecuteScalarAsync();
        Assert.False(string.IsNullOrWhiteSpace(ledgerFactReference));

        await using var factCommand = new NpgsqlCommand("select id,status,posted_entry_id from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and idempotency_key=$3", connection);
        factCommand.Parameters.AddWithValue(Guid.Parse(TenantId));
        factCommand.Parameters.AddWithValue(Guid.Parse(LegalEntityId));
        factCommand.Parameters.AddWithValue($"fixed-assets:depreciation:{runId:D}:eba3c9e3-db53-e852-8ed1-4d34f2dd5f8b");
        await using var factReader = await factCommand.ExecuteReaderAsync();
        Assert.True(await factReader.ReadAsync());
        var factId = factReader.GetGuid(0);
        Assert.Equal("Posted", factReader.GetString(1));
        var journalEntryId = factReader.GetGuid(2);
        Assert.Equal(factId.ToString("D"), ledgerFactReference);
        await factReader.CloseAsync();

        await using var balanceCommand = new NpgsqlCommand("select coalesce(sum(debit),0),coalesce(sum(credit),0) from finance_ledger.journal_entry_line where tenant_id=$1 and legal_entity_id=$2 and journal_entry_id=$3", connection);
        balanceCommand.Parameters.AddWithValue(Guid.Parse(TenantId));
        balanceCommand.Parameters.AddWithValue(Guid.Parse(LegalEntityId));
        balanceCommand.Parameters.AddWithValue(journalEntryId);
        await using var balanceReader = await balanceCommand.ExecuteReaderAsync();
        Assert.True(await balanceReader.ReadAsync());
        Assert.Equal(balanceReader.GetDecimal(0), balanceReader.GetDecimal(1));
        Assert.True(balanceReader.GetDecimal(0) > 0m);
    }

    private sealed record CsrfResponse(string Token);
}
