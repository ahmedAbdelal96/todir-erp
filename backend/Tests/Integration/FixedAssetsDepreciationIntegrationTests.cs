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

    [Fact]
    public async Task CapitalizationCandidateCanBeReviewedAndAllocatedWithoutOverAllocation()
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
        var loginCsrf = await GetCsrfAsync(client);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email = "admin@atlas.com", password }) };
        login.Headers.Add("X-CSRF-TOKEN", loginCsrf.Token);
        using var loginResponse = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var sourceReference = $"integration-source-{Guid.CreateVersion7():N}";
        var candidateCsrf = await GetCsrfAsync(client);
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/fixed-assets/capitalization-candidates")
        {
            Content = JsonContent.Create(new
            {
                legalEntityId = Guid.Parse(LegalEntityId),
                sourceType = "SupplierInvoice",
                sourceReference,
                eligibleAmount = 5000m,
                sourceSnapshot = new { sourceModule = "integration-test", description = "Capitalizable equipment" }
            })
        };
        create.Headers.Add("X-CSRF-TOKEN", candidateCsrf.Token);
        using var createResponse = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        using var candidateJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var candidateId = candidateJson.RootElement.GetProperty("id").GetGuid();

        var reviewCsrf = await GetCsrfAsync(client);
        using var review = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/fixed-assets/capitalization-candidates/{candidateId:D}/review")
        {
            Content = JsonContent.Create(new { decision = "Approve", reason = "Reviewed in integration verification" })
        };
        review.Headers.Add("X-CSRF-TOKEN", reviewCsrf.Token);
        using var reviewResponse = await client.SendAsync(review);
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);

        var allocationCsrf = await GetCsrfAsync(client);
        using var allocation = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/fixed-assets/capitalization-candidates/{candidateId:D}/allocations")
        {
            Content = JsonContent.Create(new
            {
                assetId = Guid.Parse("7e5e47f1-55bc-ca5c-a033-568e9d957125"),
                amount = 5000m,
                allocationType = "Acquisition"
            })
        };
        allocation.Headers.Add("X-CSRF-TOKEN", allocationCsrf.Token);
        using var allocationResponse = await client.SendAsync(allocation);
        Assert.Equal(HttpStatusCode.OK, allocationResponse.StatusCode);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var candidateCommand = new NpgsqlCommand("select allocated_amount,status from business_assets.asset_capitalization_candidate where tenant_id=$1 and id=$2", connection);
        candidateCommand.Parameters.AddWithValue(Guid.Parse(TenantId));
        candidateCommand.Parameters.AddWithValue(candidateId);
        await using var candidateReader = await candidateCommand.ExecuteReaderAsync();
        Assert.True(await candidateReader.ReadAsync());
        Assert.Equal(5000m, candidateReader.GetDecimal(0));
        Assert.Equal("Consumed", candidateReader.GetString(1));
        await candidateReader.CloseAsync();

        await using var allocationCommand = new NpgsqlCommand("select count(*) from business_assets.asset_source_allocation where tenant_id=$1 and candidate_id=$2 and asset_id=$3 and amount=$4", connection);
        allocationCommand.Parameters.AddWithValue(Guid.Parse(TenantId));
        allocationCommand.Parameters.AddWithValue(candidateId);
        allocationCommand.Parameters.AddWithValue(Guid.Parse("7e5e47f1-55bc-ca5c-a033-568e9d957125"));
        allocationCommand.Parameters.AddWithValue(5000m);
        Assert.Equal(1L, (long)(await allocationCommand.ExecuteScalarAsync() ?? 0L));
    }

    [Fact]
    public async Task CipAccumulatesCostAndDoesNotBecomeDepreciableUntilCapitalized()
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
        var loginCsrf = await GetCsrfAsync(client);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email = "admin@atlas.com", password }) };
        login.Headers.Add("X-CSRF-TOKEN", loginCsrf.Token);
        using var loginResponse = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var categoriesResponse = await client.GetAsync("/api/v1/fixed-assets/categories");
        Assert.Equal(HttpStatusCode.OK, categoriesResponse.StatusCode);
        using var categories = JsonDocument.Parse(await categoriesResponse.Content.ReadAsStringAsync());
        var cipCategoryId = categories.RootElement.EnumerateArray().Single(item => item.GetProperty("code").GetString() == "CIP").GetProperty("id").GetGuid();
        var laptopCategoryId = categories.RootElement.EnumerateArray().Single(item => item.GetProperty("code").GetString() == "LAPTOP").GetProperty("id").GetGuid();

        var sourceReference = $"cip-source-{Guid.CreateVersion7():N}";
        var createCsrf = await GetCsrfAsync(client);
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/fixed-assets/cip-assets")
        {
            Content = JsonContent.Create(new
            {
                legalEntityId = Guid.Parse(LegalEntityId),
                categoryId = cipCategoryId,
                name = "Integration Factory Expansion",
                acquisitionDate = "2026-11-01",
                sourceType = "SupplierInvoice",
                sourceReference,
                amount = 10000m,
                description = "CIP integration verification"
            })
        };
        create.Headers.Add("X-CSRF-TOKEN", createCsrf.Token);
        using var createResponse = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        using var cipJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var cipAssetId = cipJson.RootElement.GetProperty("assetId").GetGuid();
        Assert.Equal("UnderConstruction", cipJson.RootElement.GetProperty("lifecycleState").GetString());

        var addCsrf = await GetCsrfAsync(client);
        using var add = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/fixed-assets/cip-assets/{cipAssetId:D}/cost-sources")
        {
            Content = JsonContent.Create(new { sourceType = "ContractorCost", sourceReference = sourceReference + "-installation", amount = 2500m })
        };
        add.Headers.Add("X-CSRF-TOKEN", addCsrf.Token);
        using var addResponse = await client.SendAsync(add);
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);
        using var addedJson = JsonDocument.Parse(await addResponse.Content.ReadAsStringAsync());
        Assert.Equal(12500m, addedJson.RootElement.GetProperty("totalCost").GetDecimal());

        var capitalizeCsrf = await GetCsrfAsync(client);
        using var capitalize = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/fixed-assets/cip-assets/{cipAssetId:D}/capitalize")
        {
            Content = JsonContent.Create(new { targetCategoryId = laptopCategoryId, name = "Capitalized Factory Equipment", capitalizationDate = "2026-12-01" })
        };
        capitalize.Headers.Add("X-CSRF-TOKEN", capitalizeCsrf.Token);
        using var capitalizeResponse = await client.SendAsync(capitalize);
        Assert.Equal(HttpStatusCode.OK, capitalizeResponse.StatusCode);
        using var capitalizedJson = JsonDocument.Parse(await capitalizeResponse.Content.ReadAsStringAsync());
        Assert.Equal("Capitalized", capitalizedJson.RootElement.GetProperty("lifecycleState").GetString());
        Assert.Equal(12500m, capitalizedJson.RootElement.GetProperty("totalCost").GetDecimal());
        Assert.True(capitalizedJson.RootElement.GetProperty("placedInServiceDate").ValueKind is JsonValueKind.Null);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var assetCommand = new NpgsqlCommand("select lifecycle_state,asset_nature,placed_in_service_date from business_assets.fixed_asset where tenant_id=$1 and id=$2", connection);
        assetCommand.Parameters.AddWithValue(Guid.Parse(TenantId));
        assetCommand.Parameters.AddWithValue(cipAssetId);
        await using var assetReader = await assetCommand.ExecuteReaderAsync();
        Assert.True(await assetReader.ReadAsync());
        Assert.Equal("Capitalized", assetReader.GetString(0));
        Assert.Equal("Equipment", assetReader.GetString(1));
        Assert.True(assetReader.IsDBNull(2));
        await assetReader.CloseAsync();
        await using var bookCommand = new NpgsqlCommand("select original_cost,depreciation_method,placed_in_service_date from business_assets.asset_book where tenant_id=$1 and asset_id=$2", connection);
        bookCommand.Parameters.AddWithValue(Guid.Parse(TenantId));
        bookCommand.Parameters.AddWithValue(cipAssetId);
        await using var bookReader = await bookCommand.ExecuteReaderAsync();
        Assert.True(await bookReader.ReadAsync());
        Assert.Equal(12500m, bookReader.GetDecimal(0));
        Assert.Equal("StraightLine", bookReader.GetString(1));
        Assert.True(bookReader.IsDBNull(2));
    }

    [Fact]
    public async Task DisposalReconstructsBookValueAndPostsBalancedLedgerFact()
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
        var loginCsrf = await GetCsrfAsync(client);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email = "admin@atlas.com", password }) };
        login.Headers.Add("X-CSRF-TOKEN", loginCsrf.Token);
        using var loginResponse = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var assetId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var actorCommand = new NpgsqlCommand("select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", connection); actorCommand.Parameters.AddWithValue(Guid.Parse(TenantId));
        var actorId = (Guid)(await actorCommand.ExecuteScalarAsync() ?? throw new InvalidOperationException("Seed administrator was not found."));
        await using var categoryCommand = new NpgsqlCommand("select id from business_assets.asset_category where tenant_id=$1 and code='LAPTOP'", connection); categoryCommand.Parameters.AddWithValue(Guid.Parse(TenantId));
        var categoryId = (Guid)(await categoryCommand.ExecuteScalarAsync() ?? throw new InvalidOperationException("Seed laptop category was not found."));
        var assetNumber = $"FA-TEST-DISPOSAL-{assetId:N}";
        await using var assetSetup = new NpgsqlCommand("insert into business_assets.fixed_asset(tenant_id,id,legal_entity_id,category_id,asset_number,name,lifecycle_state,asset_nature,tracking_granularity,acquisition_date,capitalization_date,placed_in_service_date,quantity,created_by,created_at,row_version) values($1,$2,$3,$4,$5,'Disposal Integration Asset','Operational','Equipment','Individual','2026-10-01','2026-10-01','2026-10-01',1,$6,now(),1)", connection); assetSetup.Parameters.AddWithValue(Guid.Parse(TenantId)); assetSetup.Parameters.AddWithValue(assetId); assetSetup.Parameters.AddWithValue(Guid.Parse(LegalEntityId)); assetSetup.Parameters.AddWithValue(categoryId); assetSetup.Parameters.AddWithValue(assetNumber); assetSetup.Parameters.AddWithValue(actorId); await assetSetup.ExecuteNonQueryAsync();
        await using var bookSetup = new NpgsqlCommand("insert into business_assets.asset_book(tenant_id,id,asset_id,book_definition_id,original_cost,residual_value,useful_life_months,depreciation_method,placed_in_service_date,created_at,row_version) values($1,$2,$3,$4,12000,0,36,'StraightLine','2026-10-01',now(),1)", connection); bookSetup.Parameters.AddWithValue(Guid.Parse(TenantId)); bookSetup.Parameters.AddWithValue(bookId); bookSetup.Parameters.AddWithValue(assetId); bookSetup.Parameters.AddWithValue(Guid.Parse(BookDefinitionId)); await bookSetup.ExecuteNonQueryAsync();

        var disposalCsrf = await GetCsrfAsync(client);
        using var disposal = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/fixed-assets/assets/{assetId:D}/disposal")
        {
            Content = JsonContent.Create(new
            {
                legalEntityId = Guid.Parse(LegalEntityId),
                disposalType = "Sale",
                disposalDate = "2026-12-15",
                proceeds = 10000m,
                directCosts = 100m,
                reason = "Integration disposal verification",
                idempotencyKey = $"integration-disposal-{Guid.CreateVersion7():N}"
            })
        };
        disposal.Headers.Add("X-CSRF-TOKEN", disposalCsrf.Token);
        using var disposalResponse = await client.SendAsync(disposal);
        Assert.Equal(HttpStatusCode.OK, disposalResponse.StatusCode);
        using var disposalJson = JsonDocument.Parse(await disposalResponse.Content.ReadAsStringAsync());
        Assert.Equal("Posted", disposalJson.RootElement.GetProperty("status").GetString());
        Assert.True(disposalJson.RootElement.GetProperty("grossCost").GetDecimal() > 0m);
        Assert.True(disposalJson.RootElement.GetProperty("netBookValue").GetDecimal() >= 0m);

        await using var assetCommand = new NpgsqlCommand("select lifecycle_state from business_assets.fixed_asset where tenant_id=$1 and id=$2", connection); assetCommand.Parameters.AddWithValue(Guid.Parse(TenantId)); assetCommand.Parameters.AddWithValue(assetId);
        Assert.Equal("Disposed", (string?)await assetCommand.ExecuteScalarAsync());
        await using var transactionCommand = new NpgsqlCommand("select ledger_fact_reference from business_assets.asset_book_transaction where tenant_id=$1 and asset_book_id=$2 and transaction_type='Disposal'", connection); transactionCommand.Parameters.AddWithValue(Guid.Parse(TenantId)); transactionCommand.Parameters.AddWithValue(bookId);
        var factReference = (string?)await transactionCommand.ExecuteScalarAsync();
        Assert.False(string.IsNullOrWhiteSpace(factReference));
        await using var factCommand = new NpgsqlCommand("select status,posted_entry_id from finance_ledger.accounting_fact where tenant_id=$1 and id=$2 and fact_type='FixedAssetDisposalPosted'", connection); factCommand.Parameters.AddWithValue(Guid.Parse(TenantId)); factCommand.Parameters.AddWithValue(Guid.Parse(factReference!));
        await using var factReader = await factCommand.ExecuteReaderAsync();
        Assert.True(await factReader.ReadAsync());
        Assert.Equal("Posted", factReader.GetString(0));
        var journalEntryId = factReader.GetGuid(1);
        await factReader.CloseAsync();
        await using var balanceCommand = new NpgsqlCommand("select coalesce(sum(debit),0),coalesce(sum(credit),0) from finance_ledger.journal_entry_line where tenant_id=$1 and journal_entry_id=$2", connection); balanceCommand.Parameters.AddWithValue(Guid.Parse(TenantId)); balanceCommand.Parameters.AddWithValue(journalEntryId);
        await using var balanceReader = await balanceCommand.ExecuteReaderAsync();
        Assert.True(await balanceReader.ReadAsync());
        Assert.Equal(balanceReader.GetDecimal(0), balanceReader.GetDecimal(1));
        Assert.True(balanceReader.GetDecimal(0) > 0m);
    }

    private static async Task<CsrfResponse> GetCsrfAsync(HttpClient client) => await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf") ?? throw new InvalidOperationException("CSRF token was not returned.");

    private sealed record CsrfResponse(string Token);
}
