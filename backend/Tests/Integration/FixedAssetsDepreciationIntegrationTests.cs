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

        var depreciationAssetId = Guid.CreateVersion7();
        var depreciationAssetBookId = Guid.CreateVersion7();
        var depreciationAssetNumber = $"FA-TEST-DEPR-{depreciationAssetId:N}";
        await using var setupConnection = new NpgsqlConnection(connectionString);
        await setupConnection.OpenAsync();
        await using var setupActor = new NpgsqlCommand("select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", setupConnection); setupActor.Parameters.AddWithValue(Guid.Parse(TenantId));
        var setupActorId = (Guid)(await setupActor.ExecuteScalarAsync() ?? throw new InvalidOperationException("Seed administrator was not found."));
        await using var setupCategory = new NpgsqlCommand("select id from business_assets.asset_category where tenant_id=$1 and code='LAPTOP'", setupConnection); setupCategory.Parameters.AddWithValue(Guid.Parse(TenantId));
        var setupCategoryId = (Guid)(await setupCategory.ExecuteScalarAsync() ?? throw new InvalidOperationException("Seed laptop category was not found."));
        await using var setupAsset = new NpgsqlCommand("insert into business_assets.fixed_asset(tenant_id,id,legal_entity_id,category_id,asset_number,name,lifecycle_state,asset_nature,tracking_granularity,acquisition_date,capitalization_date,placed_in_service_date,quantity,created_by,created_at,row_version) values($1,$2,$3,$4,$5,'Depreciation Integration Asset','Operational','Equipment','Individual','2026-01-01','2026-01-01','2026-01-01',1,$6,now(),1)", setupConnection); setupAsset.Parameters.AddWithValue(Guid.Parse(TenantId)); setupAsset.Parameters.AddWithValue(depreciationAssetId); setupAsset.Parameters.AddWithValue(Guid.Parse(LegalEntityId)); setupAsset.Parameters.AddWithValue(setupCategoryId); setupAsset.Parameters.AddWithValue(depreciationAssetNumber); setupAsset.Parameters.AddWithValue(setupActorId); await setupAsset.ExecuteNonQueryAsync();
        await using var setupBook = new NpgsqlCommand("insert into business_assets.asset_book(tenant_id,id,asset_id,book_definition_id,original_cost,residual_value,useful_life_months,depreciation_method,placed_in_service_date,created_at,row_version) values($1,$2,$3,$4,12000,0,36,'StraightLine','2026-01-01',now(),1)", setupConnection); setupBook.Parameters.AddWithValue(Guid.Parse(TenantId)); setupBook.Parameters.AddWithValue(depreciationAssetBookId); setupBook.Parameters.AddWithValue(depreciationAssetId); setupBook.Parameters.AddWithValue(Guid.Parse(BookDefinitionId)); await setupBook.ExecuteNonQueryAsync();
        await using var periodCommand = new NpgsqlCommand("select (date '2026-08-01' + candidate)::date from generate_series(1,27) candidate where not exists (select 1 from business_assets.asset_depreciation_run where tenant_id=$1 and legal_entity_id=$2 and book_definition_id=$3 and period_start=date '2026-08-01' and period_end=(date '2026-08-01' + candidate)::date) order by candidate limit 1", setupConnection); periodCommand.Parameters.AddWithValue(Guid.Parse(TenantId)); periodCommand.Parameters.AddWithValue(Guid.Parse(LegalEntityId)); periodCommand.Parameters.AddWithValue(Guid.Parse(BookDefinitionId));
        var periodEnd = (DateOnly?)(await periodCommand.ExecuteScalarAsync()) ?? throw new InvalidOperationException("No unused August depreciation period is available for integration verification.");
        var periodEndText = periodEnd.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        var idempotencyKey = $"fixed-assets-integration-{Guid.CreateVersion7():N}";
        var request = new
        {
            legalEntityId = Guid.Parse(LegalEntityId),
            bookId = Guid.Parse(BookDefinitionId),
            periodStart = "2026-08-01",
            periodEnd = periodEndText,
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
                periodStart = "2026-08-01",
                periodEnd = periodEndText,
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
        var postBody = await postResponse.Content.ReadAsStringAsync();
        Assert.True(postResponse.StatusCode == HttpStatusCode.OK, postBody);

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
        transactionCommand.Parameters.AddWithValue($"depreciation:{runId:D}:{depreciationAssetBookId:D}");
        var ledgerFactReference = (string?)await transactionCommand.ExecuteScalarAsync();
        Assert.False(string.IsNullOrWhiteSpace(ledgerFactReference));

        await using var factCommand = new NpgsqlCommand("select id,status,posted_entry_id from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and idempotency_key=$3", connection);
        factCommand.Parameters.AddWithValue(Guid.Parse(TenantId));
        factCommand.Parameters.AddWithValue(Guid.Parse(LegalEntityId));
        factCommand.Parameters.AddWithValue($"fixed-assets:depreciation:{runId:D}:{depreciationAssetBookId:D}");
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
        await balanceReader.CloseAsync();

        var reverseCsrf = await GetCsrfAsync(client);
        using var reverse = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/fixed-assets/depreciation-runs/{runId:D}/reverse?legalEntityId={LegalEntityId}")
        {
            Content = JsonContent.Create(new { reversalDate = periodEnd.AddDays(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), reason = "Integration reversal verification", idempotencyKey = $"reverse-{idempotencyKey}" })
        };
        reverse.Headers.Add("X-CSRF-TOKEN", reverseCsrf.Token);
        using var reverseResponse = await client.SendAsync(reverse);
        var reverseBody = await reverseResponse.Content.ReadAsStringAsync();
        Assert.True(reverseResponse.StatusCode == HttpStatusCode.OK, reverseBody);
        using var reverseJson = JsonDocument.Parse(reverseBody);
        Assert.Equal("Reversed", reverseJson.RootElement.GetProperty("status").GetString());

        await using var reversedTransaction = new NpgsqlCommand("select ledger_fact_reference from business_assets.asset_book_transaction where tenant_id=$1 and asset_book_id=$2 and idempotency_key=$3", connection); reversedTransaction.Parameters.AddWithValue(Guid.Parse(TenantId)); reversedTransaction.Parameters.AddWithValue(depreciationAssetBookId); reversedTransaction.Parameters.AddWithValue($"depreciation-reversal:{runId:D}:book:{depreciationAssetBookId:D}");
        var reversedFactReference = (string?)await reversedTransaction.ExecuteScalarAsync();
        Assert.False(string.IsNullOrWhiteSpace(reversedFactReference));
        await using var reversedFact = new NpgsqlCommand("select status,posted_entry_id from finance_ledger.accounting_fact where tenant_id=$1 and id=$2 and fact_type='FixedAssetDepreciationReversed'", connection); reversedFact.Parameters.AddWithValue(Guid.Parse(TenantId)); reversedFact.Parameters.AddWithValue(Guid.Parse(reversedFactReference!));
        await using var reversedFactReader = await reversedFact.ExecuteReaderAsync();
        Assert.True(await reversedFactReader.ReadAsync());
        Assert.Equal("Posted", reversedFactReader.GetString(0));
        var reversedJournalEntryId = reversedFactReader.GetGuid(1);
        await reversedFactReader.CloseAsync();
        await using var reversedBalance = new NpgsqlCommand("select coalesce(sum(debit),0),coalesce(sum(credit),0) from finance_ledger.journal_entry_line where tenant_id=$1 and journal_entry_id=$2", connection); reversedBalance.Parameters.AddWithValue(Guid.Parse(TenantId)); reversedBalance.Parameters.AddWithValue(reversedJournalEntryId);
        await using var reversedBalanceReader = await reversedBalance.ExecuteReaderAsync();
        Assert.True(await reversedBalanceReader.ReadAsync());
        Assert.Equal(reversedBalanceReader.GetDecimal(0), reversedBalanceReader.GetDecimal(1));
        Assert.True(reversedBalanceReader.GetDecimal(0) > 0m);
    }

    [Fact]
    public async Task OpeningImportCreatesOpeningAssetAndBookHistoryWithoutFakeInvoice()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for Fixed Assets integration verification.");
        var password = Environment.GetEnvironmentVariable("SEED_DEFAULT_PASSWORD")
            ?? throw new InvalidOperationException("Set SEED_DEFAULT_PASSWORD for Fixed Assets integration verification.");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        var loginCsrf = await GetCsrfAsync(client);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email = "admin@atlas.com", password }) };
        login.Headers.Add("X-CSRF-TOKEN", loginCsrf.Token);
        using var loginResponse = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var legacy = $"LEGACY-{Guid.CreateVersion7():N}";
        await using var setup = new NpgsqlConnection(connectionString); await setup.OpenAsync();
        await using var categoryCommand = new NpgsqlCommand("select id from business_assets.asset_category where tenant_id=$1 and code='LAPTOP'", setup); categoryCommand.Parameters.AddWithValue(Guid.Parse(TenantId)); var categoryId = (Guid)(await categoryCommand.ExecuteScalarAsync() ?? throw new InvalidOperationException("Seed laptop category was not found."));
        await using var bookCommand = new NpgsqlCommand("select id from business_assets.asset_book_definition where tenant_id=$1 and code='CORP' and legal_entity_id=$2", setup); bookCommand.Parameters.AddWithValue(Guid.Parse(TenantId)); bookCommand.Parameters.AddWithValue(Guid.Parse(LegalEntityId)); var bookDefinitionId = (Guid)(await bookCommand.ExecuteScalarAsync() ?? throw new InvalidOperationException("Seed corporate book was not found."));
        var createCsrf = await GetCsrfAsync(client);
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/fixed-assets/opening-imports")
        {
            Content = JsonContent.Create(new
            {
                legalEntityId = Guid.Parse(LegalEntityId), sourceSystem = "Legacy ERP", batchReference = $"opening-{legacy}", openingDate = "2026-01-01",
                lines = new[] { new { legacyAssetNumber = legacy, categoryId, name = "Legacy laptop", bookDefinitionId, acquisitionDate = "2025-01-01", placedInServiceDate = "2025-01-01", originalCost = 1000m, accumulatedDepreciation = 400m, netBookValue = 600m, residualValue = 0m, usefulLifeMonths = 36, depreciationMethod = "StraightLine" } }
            })
        };
        create.Headers.Add("X-CSRF-TOKEN", createCsrf.Token);
        using var createResponse = await client.SendAsync(create); var createBody = await createResponse.Content.ReadAsStringAsync(); Assert.True(createResponse.StatusCode == HttpStatusCode.OK, createBody); using var createJson = JsonDocument.Parse(createBody); Assert.Equal("Validated", createJson.RootElement.GetProperty("status").GetString()); var importId = createJson.RootElement.GetProperty("id").GetGuid(); Assert.Equal(1, createJson.RootElement.GetProperty("validLineCount").GetInt32());

        var postCsrf = await GetCsrfAsync(client); using var post = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/fixed-assets/opening-imports/{importId:D}/post"); post.Headers.Add("X-CSRF-TOKEN", postCsrf.Token); using var postResponse = await client.SendAsync(post); var postBody = await postResponse.Content.ReadAsStringAsync(); Assert.True(postResponse.StatusCode == HttpStatusCode.OK, postBody); using var postJson = JsonDocument.Parse(postBody); Assert.Equal("Posted", postJson.RootElement.GetProperty("status").GetString());
        var retryCsrf = await GetCsrfAsync(client); using var retry = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/fixed-assets/opening-imports/{importId:D}/post"); retry.Headers.Add("X-CSRF-TOKEN", retryCsrf.Token); using var retryResponse = await client.SendAsync(retry); Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode); using var retryJson = JsonDocument.Parse(await retryResponse.Content.ReadAsStringAsync()); Assert.Equal("Posted", retryJson.RootElement.GetProperty("status").GetString());

        await using var verify = new NpgsqlConnection(connectionString); await verify.OpenAsync();
        await using var verifyCommand = new NpgsqlCommand("select a.id,b.id,a.source_origin_type,b.original_cost,t.accumulated_depreciation,(b.original_cost-t.accumulated_depreciation),t.ledger_fact_reference,count(*) over() from business_assets.fixed_asset a join business_assets.asset_book b on b.tenant_id=a.tenant_id and b.asset_id=a.id join business_assets.asset_book_transaction t on t.tenant_id=b.tenant_id and t.asset_book_id=b.id where a.tenant_id=$1 and a.asset_tag=$2 and t.transaction_type='Opening'", verify); verifyCommand.Parameters.AddWithValue(Guid.Parse(TenantId)); verifyCommand.Parameters.AddWithValue(legacy); await using var verifyReader = await verifyCommand.ExecuteReaderAsync(); Assert.True(await verifyReader.ReadAsync()); var importedAssetId = verifyReader.GetGuid(0); var importedBookId = verifyReader.GetGuid(1); Assert.Equal("OpeningMigration", verifyReader.GetString(2)); Assert.Equal(1000m, verifyReader.GetDecimal(3)); Assert.Equal(400m, verifyReader.GetDecimal(4)); Assert.Equal(600m, verifyReader.GetDecimal(5)); Assert.False(verifyReader.IsDBNull(6)); var openingFactId = Guid.Parse(verifyReader.GetString(6)); Assert.Equal(1L, verifyReader.GetInt64(7)); await verifyReader.CloseAsync();
        await using var factCommand = new NpgsqlCommand("select fact_type,source_module,status from finance_ledger.accounting_fact where tenant_id=$1 and id=$2", verify); factCommand.Parameters.AddWithValue(Guid.Parse(TenantId)); factCommand.Parameters.AddWithValue(openingFactId); await using var factReader = await factCommand.ExecuteReaderAsync(); Assert.True(await factReader.ReadAsync()); Assert.Equal("OpeningBalancePosted", factReader.GetString(0)); Assert.Equal("fixed-assets", factReader.GetString(1)); Assert.Equal("Posted", factReader.GetString(2));
        using var balanceResponse = await client.GetAsync($"/api/v1/fixed-assets/assets/{importedAssetId:D}/books/{importedBookId:D}/balance?legalEntityId={LegalEntityId}"); var balanceBody = await balanceResponse.Content.ReadAsStringAsync(); Assert.True(balanceResponse.StatusCode == HttpStatusCode.OK, balanceBody); using var balanceJson = JsonDocument.Parse(balanceBody); Assert.Equal(1000m, balanceJson.RootElement.GetProperty("originalCost").GetDecimal()); Assert.Equal(400m, balanceJson.RootElement.GetProperty("accumulatedDepreciation").GetDecimal()); Assert.Equal(600m, balanceJson.RootElement.GetProperty("netBookValue").GetDecimal());
        using var timelineResponse = await client.GetAsync($"/api/v1/fixed-assets/assets/{importedAssetId:D}/timeline?limit=20"); var timelineBody = await timelineResponse.Content.ReadAsStringAsync(); Assert.True(timelineResponse.StatusCode == HttpStatusCode.OK, timelineBody); using var timelineJson = JsonDocument.Parse(timelineBody); Assert.Contains(timelineJson.RootElement.EnumerateArray(), item => item.GetProperty("eventType").GetString() == "AssetCreated"); Assert.Contains(timelineJson.RootElement.EnumerateArray(), item => item.GetProperty("eventType").GetString() == "Opening");
        using var reconciliationResponse = await client.GetAsync($"/api/v1/fixed-assets/books/{BookDefinitionId}/reconciliation?legalEntityId={LegalEntityId}&asOfDate=2026-12-31"); var reconciliationBody = await reconciliationResponse.Content.ReadAsStringAsync(); Assert.True(reconciliationResponse.StatusCode == HttpStatusCode.OK, reconciliationBody); using var reconciliationJson = JsonDocument.Parse(reconciliationBody); Assert.Equal(Guid.Parse(BookDefinitionId), reconciliationJson.RootElement.GetProperty("bookId").GetGuid()); var reconciliationStatus = reconciliationJson.RootElement.GetProperty("status").GetString(); Assert.True(reconciliationStatus is "Healthy" or "Difference");
    }

    [Fact]
    public async Task DepreciationPostingIsBlockedForClosedAccountingPeriod()
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

        var idempotencyKey = $"fixed-assets-closed-period-{Guid.CreateVersion7():N}";
        var prepareCsrf = await GetCsrfAsync(client);
        using var prepare = new HttpRequestMessage(HttpMethod.Post, "/api/v1/fixed-assets/depreciation-runs/prepare")
        {
            Content = JsonContent.Create(new
            {
                legalEntityId = Guid.Parse(LegalEntityId),
                bookId = Guid.Parse(BookDefinitionId),
                periodStart = "2026-12-01",
                periodEnd = "2026-12-31",
                idempotencyKey
            })
        };
        prepare.Headers.Add("X-CSRF-TOKEN", prepareCsrf.Token);
        using var prepareResponse = await client.SendAsync(prepare);
        var prepareBody = await prepareResponse.Content.ReadAsStringAsync();
        Assert.True(prepareResponse.StatusCode == HttpStatusCode.OK, prepareBody);
        using var prepared = JsonDocument.Parse(prepareBody);
        var runId = prepared.RootElement.GetProperty("id").GetGuid();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var closePeriod = new NpgsqlCommand("update finance_ledger.accounting_period set status='Closed' where tenant_id=$1 and legal_entity_id=$2 and start_date='2026-12-01'", connection);
        closePeriod.Parameters.AddWithValue(Guid.Parse(TenantId));
        closePeriod.Parameters.AddWithValue(Guid.Parse(LegalEntityId));
        Assert.Equal(1, await closePeriod.ExecuteNonQueryAsync());
        try
        {
            var postCsrf = await GetCsrfAsync(client);
            using var post = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/fixed-assets/depreciation-runs/{runId:D}/post?legalEntityId={LegalEntityId}");
            post.Headers.Add("X-CSRF-TOKEN", postCsrf.Token);
            using var postResponse = await client.SendAsync(post);
            var postBody = await postResponse.Content.ReadAsStringAsync();
            Assert.True(postResponse.StatusCode == HttpStatusCode.BadRequest, postBody);
            Assert.Contains("assets.accounting_period_closed", postBody, StringComparison.Ordinal);
        }
        finally
        {
            await using var reopenPeriod = new NpgsqlCommand("update finance_ledger.accounting_period set status='Open' where tenant_id=$1 and legal_entity_id=$2 and start_date='2026-12-01'", connection);
            reopenPeriod.Parameters.AddWithValue(Guid.Parse(TenantId));
            reopenPeriod.Parameters.AddWithValue(Guid.Parse(LegalEntityId));
            await reopenPeriod.ExecuteNonQueryAsync();
        }
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

        var allocatedAssetId = Guid.CreateVersion7();
        var allocatedBookId = Guid.CreateVersion7();
        await using var allocationSetupConnection = new NpgsqlConnection(connectionString);
        await allocationSetupConnection.OpenAsync();
        await using var allocationActorCommand = new NpgsqlCommand("select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", allocationSetupConnection); allocationActorCommand.Parameters.AddWithValue(Guid.Parse(TenantId));
        var allocationActorId = (Guid)(await allocationActorCommand.ExecuteScalarAsync() ?? throw new InvalidOperationException("Seed administrator was not found."));
        await using var allocationCategoryCommand = new NpgsqlCommand("select id from business_assets.asset_category where tenant_id=$1 and code='LAPTOP'", allocationSetupConnection); allocationCategoryCommand.Parameters.AddWithValue(Guid.Parse(TenantId));
        var allocationCategoryId = (Guid)(await allocationCategoryCommand.ExecuteScalarAsync() ?? throw new InvalidOperationException("Seed laptop category was not found."));
        await using var allocationAssetSetup = new NpgsqlCommand("insert into business_assets.fixed_asset(tenant_id,id,legal_entity_id,category_id,asset_number,name,lifecycle_state,asset_nature,tracking_granularity,acquisition_date,capitalization_date,placed_in_service_date,quantity,created_by,created_at,row_version) values($1,$2,$3,$4,$5,'Allocation Integration Asset','Operational','Equipment','Individual','2026-10-01','2026-10-01','2026-10-01',1,$6,now(),1)", allocationSetupConnection); allocationAssetSetup.Parameters.AddWithValue(Guid.Parse(TenantId)); allocationAssetSetup.Parameters.AddWithValue(allocatedAssetId); allocationAssetSetup.Parameters.AddWithValue(Guid.Parse(LegalEntityId)); allocationAssetSetup.Parameters.AddWithValue(allocationCategoryId); allocationAssetSetup.Parameters.AddWithValue($"FA-TEST-ALLOC-{allocatedAssetId:N}"); allocationAssetSetup.Parameters.AddWithValue(allocationActorId); await allocationAssetSetup.ExecuteNonQueryAsync();
        await using var allocationBookSetup = new NpgsqlCommand("insert into business_assets.asset_book(tenant_id,id,asset_id,book_definition_id,original_cost,residual_value,useful_life_months,depreciation_method,placed_in_service_date,created_at,row_version) values($1,$2,$3,$4,12000,0,36,'StraightLine','2026-10-01',now(),1)", allocationSetupConnection); allocationBookSetup.Parameters.AddWithValue(Guid.Parse(TenantId)); allocationBookSetup.Parameters.AddWithValue(allocatedBookId); allocationBookSetup.Parameters.AddWithValue(allocatedAssetId); allocationBookSetup.Parameters.AddWithValue(Guid.Parse(BookDefinitionId)); await allocationBookSetup.ExecuteNonQueryAsync();

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
                assetId = allocatedAssetId,
                amount = 5000m,
                allocationType = "Acquisition"
            })
        };
        allocation.Headers.Add("X-CSRF-TOKEN", allocationCsrf.Token);
        using var allocationResponse = await client.SendAsync(allocation);
        Assert.Equal(HttpStatusCode.OK, allocationResponse.StatusCode);
        using var allocationJson = JsonDocument.Parse(await allocationResponse.Content.ReadAsStringAsync());
        Assert.False(allocationJson.RootElement.GetProperty("alreadyApplied").GetBoolean());

        var retryAllocationCsrf = await GetCsrfAsync(client);
        using var retryAllocation = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/fixed-assets/capitalization-candidates/{candidateId:D}/allocations")
        {
            Content = JsonContent.Create(new
            {
                assetId = allocatedAssetId,
                amount = 5000m,
                allocationType = "Acquisition"
            })
        };
        retryAllocation.Headers.Add("X-CSRF-TOKEN", retryAllocationCsrf.Token);
        using var retryAllocationResponse = await client.SendAsync(retryAllocation);
        Assert.Equal(HttpStatusCode.OK, retryAllocationResponse.StatusCode);
        using var retryAllocationJson = JsonDocument.Parse(await retryAllocationResponse.Content.ReadAsStringAsync());
        Assert.True(retryAllocationJson.RootElement.GetProperty("alreadyApplied").GetBoolean());

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

        await using var allocationCommand = new NpgsqlCommand("select id from business_assets.asset_source_allocation where tenant_id=$1 and candidate_id=$2 and asset_id=$3 and amount=$4", connection);
        allocationCommand.Parameters.AddWithValue(Guid.Parse(TenantId));
        allocationCommand.Parameters.AddWithValue(candidateId);
        allocationCommand.Parameters.AddWithValue(allocatedAssetId);
        allocationCommand.Parameters.AddWithValue(5000m);
        var allocationId = (Guid?)(await allocationCommand.ExecuteScalarAsync());
        Assert.NotNull(allocationId);

        var correctionKey = $"integration-allocation-correction-{Guid.CreateVersion7():N}";
        var correctionCsrf = await GetCsrfAsync(client);
        using var correction = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/fixed-assets/capitalization-candidates/{candidateId:D}/allocations/{allocationId.Value:D}/correct")
        {
            Content = JsonContent.Create(new { allocationId, amount = 1000m, reason = "Corrected source allocation", idempotencyKey = correctionKey })
        };
        correction.Headers.Add("X-CSRF-TOKEN", correctionCsrf.Token);
        using var correctionResponse = await client.SendAsync(correction);
        var correctionBody = await correctionResponse.Content.ReadAsStringAsync();
        Assert.True(correctionResponse.StatusCode == HttpStatusCode.OK, correctionBody);
        using var correctionJson = JsonDocument.Parse(correctionBody);
        Assert.Equal(4000m, correctionJson.RootElement.GetProperty("candidateAllocatedAmount").GetDecimal());
        Assert.Equal(4000m, correctionJson.RootElement.GetProperty("remainingAllocationAmount").GetDecimal());
        Assert.False(correctionJson.RootElement.GetProperty("alreadyApplied").GetBoolean());

        var correctionRetryCsrf = await GetCsrfAsync(client);
        using var correctionRetry = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/fixed-assets/capitalization-candidates/{candidateId:D}/allocations/{allocationId.Value:D}/correct")
        {
            Content = JsonContent.Create(new { allocationId, amount = 1000m, reason = "Corrected source allocation", idempotencyKey = correctionKey })
        };
        correctionRetry.Headers.Add("X-CSRF-TOKEN", correctionRetryCsrf.Token);
        using var correctionRetryResponse = await client.SendAsync(correctionRetry);
        Assert.Equal(HttpStatusCode.OK, correctionRetryResponse.StatusCode);
        using var correctionRetryJson = JsonDocument.Parse(await correctionRetryResponse.Content.ReadAsStringAsync());
        Assert.True(correctionRetryJson.RootElement.GetProperty("alreadyApplied").GetBoolean());

        await using var correctedCandidate = new NpgsqlCommand("select allocated_amount,status from business_assets.asset_capitalization_candidate where tenant_id=$1 and id=$2", connection);
        correctedCandidate.Parameters.AddWithValue(Guid.Parse(TenantId));
        correctedCandidate.Parameters.AddWithValue(candidateId);
        await using var correctedCandidateReader = await correctedCandidate.ExecuteReaderAsync();
        Assert.True(await correctedCandidateReader.ReadAsync());
        Assert.Equal(4000m, correctedCandidateReader.GetDecimal(0));
        Assert.Equal("Approved", correctedCandidateReader.GetString(1));
        await correctedCandidateReader.CloseAsync();
        await using var allocatedBookCommand = new NpgsqlCommand("select original_cost from business_assets.asset_book where tenant_id=$1 and id=$2", connection); allocatedBookCommand.Parameters.AddWithValue(Guid.Parse(TenantId)); allocatedBookCommand.Parameters.AddWithValue(allocatedBookId);
        Assert.Equal(16000m, (decimal?)await allocatedBookCommand.ExecuteScalarAsync());
        await using var allocationTransactionCount = new NpgsqlCommand("select count(*) from business_assets.asset_book_transaction where tenant_id=$1 and asset_book_id=$2 and (source_reference like 'candidate:%' or source_reference like 'source-allocation-correction:%')", connection); allocationTransactionCount.Parameters.AddWithValue(Guid.Parse(TenantId)); allocationTransactionCount.Parameters.AddWithValue(allocatedBookId);
        Assert.Equal(2L, (long)(await allocationTransactionCount.ExecuteScalarAsync() ?? 0L));
        await using var allocationFactCount = new NpgsqlCommand("select count(*) from business_assets.asset_book_transaction t join finance_ledger.accounting_fact f on f.tenant_id=t.tenant_id and f.id::text=t.ledger_fact_reference where t.tenant_id=$1 and t.asset_book_id=$2 and f.fact_type in ('FixedAssetAcquisitionPosted','FixedAssetCostReduced') and f.source_module='fixed-assets'", connection); allocationFactCount.Parameters.AddWithValue(Guid.Parse(TenantId)); allocationFactCount.Parameters.AddWithValue(allocatedBookId);
        Assert.Equal(2L, (long)(await allocationFactCount.ExecuteScalarAsync() ?? 0L));
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

    [Fact]
    public async Task PartialDisposalReducesQuantityAndReconstructsRemainingBook()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for Fixed Assets integration verification.");
        var password = Environment.GetEnvironmentVariable("SEED_DEFAULT_PASSWORD")
            ?? throw new InvalidOperationException("Set SEED_DEFAULT_PASSWORD for Fixed Assets integration verification.");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
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
        await using var assetSetup = new NpgsqlCommand("insert into business_assets.fixed_asset(tenant_id,id,legal_entity_id,category_id,asset_number,name,lifecycle_state,asset_nature,tracking_granularity,acquisition_date,capitalization_date,placed_in_service_date,quantity,created_by,created_at,row_version) values($1,$2,$3,$4,$5,'Partial Disposal Integration Asset','Operational','Equipment','Grouped','2026-10-01','2026-10-01','2026-10-01',10,$6,now(),1)", connection);
        assetSetup.Parameters.AddWithValue(Guid.Parse(TenantId)); assetSetup.Parameters.AddWithValue(assetId); assetSetup.Parameters.AddWithValue(Guid.Parse(LegalEntityId)); assetSetup.Parameters.AddWithValue(categoryId); assetSetup.Parameters.AddWithValue($"FA-TEST-PARTIAL-{assetId:N}"); assetSetup.Parameters.AddWithValue(actorId); await assetSetup.ExecuteNonQueryAsync();
        await using var bookSetup = new NpgsqlCommand("insert into business_assets.asset_book(tenant_id,id,asset_id,book_definition_id,original_cost,residual_value,useful_life_months,depreciation_method,placed_in_service_date,created_at,row_version) values($1,$2,$3,$4,12000,0,36,'StraightLine','2026-10-01',now(),1)", connection);
        bookSetup.Parameters.AddWithValue(Guid.Parse(TenantId)); bookSetup.Parameters.AddWithValue(bookId); bookSetup.Parameters.AddWithValue(assetId); bookSetup.Parameters.AddWithValue(Guid.Parse(BookDefinitionId)); await bookSetup.ExecuteNonQueryAsync();

        var idempotencyKey = $"integration-partial-disposal-{Guid.CreateVersion7():N}";
        var disposalPayload = new { legalEntityId = Guid.Parse(LegalEntityId), disposalType = "Sale", disposalDate = "2026-12-15", proceeds = 5000m, directCosts = 0m, reason = "Partial disposal integration verification", idempotencyKey, quantity = 5m, saleReference = "SALE-TEST-001" };
        var csrf = await GetCsrfAsync(client);
        using var disposal = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/fixed-assets/assets/{assetId:D}/disposal")
        {
            Content = JsonContent.Create(disposalPayload)
        };
        disposal.Headers.Add("X-CSRF-TOKEN", csrf.Token);
        using var response = await client.SendAsync(disposal);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Posted", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(6000m, json.RootElement.GetProperty("grossCost").GetDecimal());

        var retryCsrf = await GetCsrfAsync(client);
        using var retry = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/fixed-assets/assets/{assetId:D}/disposal") { Content = JsonContent.Create(disposalPayload) };
        retry.Headers.Add("X-CSRF-TOKEN", retryCsrf.Token);
        using var retryResponse = await client.SendAsync(retry);
        var retryBody = await retryResponse.Content.ReadAsStringAsync();
        Assert.True(retryResponse.StatusCode == HttpStatusCode.OK, retryBody);
        using var retryJson = JsonDocument.Parse(retryBody);
        Assert.Equal(json.RootElement.GetProperty("grossCost").GetDecimal(), retryJson.RootElement.GetProperty("grossCost").GetDecimal());

        var conflictCsrf = await GetCsrfAsync(client);
        using var conflict = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/fixed-assets/assets/{assetId:D}/disposal") { Content = JsonContent.Create(new { disposalPayload.legalEntityId, disposalPayload.disposalType, disposalPayload.disposalDate, proceeds = 5001m, disposalPayload.directCosts, disposalPayload.reason, disposalPayload.idempotencyKey, disposalPayload.quantity, disposalPayload.saleReference }) };
        conflict.Headers.Add("X-CSRF-TOKEN", conflictCsrf.Token);
        using var conflictResponse = await client.SendAsync(conflict);
        Assert.Equal(HttpStatusCode.BadRequest, conflictResponse.StatusCode);
        using var conflictJson = JsonDocument.Parse(await conflictResponse.Content.ReadAsStringAsync());
        Assert.Equal("assets.disposal_idempotency_conflict", conflictJson.RootElement.GetProperty("code").GetString());

        await using var assetState = new NpgsqlCommand("select quantity,lifecycle_state from business_assets.fixed_asset where tenant_id=$1 and id=$2", connection); assetState.Parameters.AddWithValue(Guid.Parse(TenantId)); assetState.Parameters.AddWithValue(assetId);
        await using var assetReader = await assetState.ExecuteReaderAsync(); Assert.True(await assetReader.ReadAsync()); Assert.Equal(5m, assetReader.GetDecimal(0)); Assert.Equal("Operational", assetReader.GetString(1)); await assetReader.CloseAsync();
        await using var bookState = new NpgsqlCommand("select original_cost from business_assets.asset_book where tenant_id=$1 and id=$2", connection); bookState.Parameters.AddWithValue(Guid.Parse(TenantId)); bookState.Parameters.AddWithValue(bookId);
        Assert.Equal(6000m, (decimal)(await bookState.ExecuteScalarAsync() ?? 0m));
        await using var disposalState = new NpgsqlCommand("select quantity,is_full_disposal,sale_reference from business_assets.asset_disposal where tenant_id=$1 and asset_id=$2", connection); disposalState.Parameters.AddWithValue(Guid.Parse(TenantId)); disposalState.Parameters.AddWithValue(assetId);
        await using var disposalReader = await disposalState.ExecuteReaderAsync(); Assert.True(await disposalReader.ReadAsync()); Assert.Equal(5m, disposalReader.GetDecimal(0)); Assert.False(disposalReader.GetBoolean(1)); Assert.Equal("SALE-TEST-001", disposalReader.GetString(2));
    }

    [Fact]
    public async Task AssetBookCostAdditionPostsLedgerFactAndIsIdempotent()
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
        await using var assetSetup = new NpgsqlCommand("insert into business_assets.fixed_asset(tenant_id,id,legal_entity_id,category_id,asset_number,name,lifecycle_state,asset_nature,tracking_granularity,acquisition_date,capitalization_date,placed_in_service_date,quantity,created_by,created_at,row_version) values($1,$2,$3,$4,$5,'Cost Change Integration Asset','Operational','Equipment','Individual','2026-10-01','2026-10-01','2026-10-01',1,$6,now(),1)", connection); assetSetup.Parameters.AddWithValue(Guid.Parse(TenantId)); assetSetup.Parameters.AddWithValue(assetId); assetSetup.Parameters.AddWithValue(Guid.Parse(LegalEntityId)); assetSetup.Parameters.AddWithValue(categoryId); assetSetup.Parameters.AddWithValue($"FA-TEST-COST-{assetId:N}"); assetSetup.Parameters.AddWithValue(actorId); await assetSetup.ExecuteNonQueryAsync();
        await using var bookSetup = new NpgsqlCommand("insert into business_assets.asset_book(tenant_id,id,asset_id,book_definition_id,original_cost,residual_value,useful_life_months,depreciation_method,placed_in_service_date,created_at,row_version) values($1,$2,$3,$4,12000,0,36,'StraightLine','2026-10-01',now(),1)", connection); bookSetup.Parameters.AddWithValue(Guid.Parse(TenantId)); bookSetup.Parameters.AddWithValue(bookId); bookSetup.Parameters.AddWithValue(assetId); bookSetup.Parameters.AddWithValue(Guid.Parse(BookDefinitionId)); await bookSetup.ExecuteNonQueryAsync();
        var key = $"integration-cost-addition-{Guid.CreateVersion7():N}";
        async Task<HttpResponseMessage> PostChangeAsync(decimal amount, string idempotencyKey, string reason)
        {
            var csrf = await GetCsrfAsync(client);
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/fixed-assets/assets/{assetId:D}/books/{bookId:D}/cost-change")
            {
                Content = JsonContent.Create(new { legalEntityId = Guid.Parse(LegalEntityId), bookId, amount, effectiveDate = "2026-12-01", reason, idempotencyKey })
            };
            request.Headers.Add("X-CSRF-TOKEN", csrf.Token);
            return await client.SendAsync(request);
        }
        using var first = await PostChangeAsync(2000m, key, "Integration capital improvement");
        var firstBody = await first.Content.ReadAsStringAsync();
        Assert.True(first.StatusCode == HttpStatusCode.OK, firstBody);
        using var firstJson = JsonDocument.Parse(firstBody);
        Assert.Equal(14000m, firstJson.RootElement.GetProperty("newCost").GetDecimal());
        Assert.False(firstJson.RootElement.GetProperty("alreadyApplied").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(firstJson.RootElement.GetProperty("ledgerFactReference").GetString()));
        using var retry = await PostChangeAsync(2000m, key, "Integration capital improvement");
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        using var retryJson = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
        Assert.True(retryJson.RootElement.GetProperty("alreadyApplied").GetBoolean());
        Assert.Equal(14000m, retryJson.RootElement.GetProperty("newCost").GetDecimal());

        var reductionKey = $"integration-cost-reduction-{Guid.CreateVersion7():N}";
        using var reduction = await PostChangeAsync(-500m, reductionKey, "Integration cost reduction");
        var reductionBody = await reduction.Content.ReadAsStringAsync();
        Assert.True(reduction.StatusCode == HttpStatusCode.OK, reductionBody);
        using var reductionJson = JsonDocument.Parse(reductionBody);
        Assert.Equal(13500m, reductionJson.RootElement.GetProperty("newCost").GetDecimal());
        Assert.Equal("CostReduction", reductionJson.RootElement.GetProperty("transactionType").GetString());
        Assert.False(string.IsNullOrWhiteSpace(reductionJson.RootElement.GetProperty("ledgerFactReference").GetString()));

        await using var bookCommand = new NpgsqlCommand("select original_cost from business_assets.asset_book where tenant_id=$1 and id=$2", connection); bookCommand.Parameters.AddWithValue(Guid.Parse(TenantId)); bookCommand.Parameters.AddWithValue(bookId);
        Assert.Equal(13500m, (decimal?)await bookCommand.ExecuteScalarAsync());
        await using var factCommand = new NpgsqlCommand("select ledger_fact_reference from business_assets.asset_book_transaction where tenant_id=$1 and asset_book_id=$2 and idempotency_key=$3", connection); factCommand.Parameters.AddWithValue(Guid.Parse(TenantId)); factCommand.Parameters.AddWithValue(bookId); factCommand.Parameters.AddWithValue(key);
        var factReference = (string?)await factCommand.ExecuteScalarAsync();
        Assert.False(string.IsNullOrWhiteSpace(factReference));
        await using var factStatus = new NpgsqlCommand("select status,posted_entry_id from finance_ledger.accounting_fact where tenant_id=$1 and id=$2 and fact_type='FixedAssetCostAdded'", connection); factStatus.Parameters.AddWithValue(Guid.Parse(TenantId)); factStatus.Parameters.AddWithValue(Guid.Parse(factReference!));
        await using var factReader = await factStatus.ExecuteReaderAsync();
        Assert.True(await factReader.ReadAsync());
        Assert.Equal("Posted", factReader.GetString(0));
        var journalEntryId = factReader.GetGuid(1);
        await factReader.CloseAsync();
        await using var balanceCommand = new NpgsqlCommand("select coalesce(sum(debit),0),coalesce(sum(credit),0) from finance_ledger.journal_entry_line where tenant_id=$1 and journal_entry_id=$2", connection); balanceCommand.Parameters.AddWithValue(Guid.Parse(TenantId)); balanceCommand.Parameters.AddWithValue(journalEntryId);
        await using var balanceReader = await balanceCommand.ExecuteReaderAsync();
        Assert.True(await balanceReader.ReadAsync());
        Assert.Equal(balanceReader.GetDecimal(0), balanceReader.GetDecimal(1));
        Assert.True(balanceReader.GetDecimal(0) > 0m);
        await balanceReader.CloseAsync();

        await using var reductionFactCommand = new NpgsqlCommand("select ledger_fact_reference from business_assets.asset_book_transaction where tenant_id=$1 and asset_book_id=$2 and idempotency_key=$3", connection); reductionFactCommand.Parameters.AddWithValue(Guid.Parse(TenantId)); reductionFactCommand.Parameters.AddWithValue(bookId); reductionFactCommand.Parameters.AddWithValue(reductionKey);
        var reductionFactReference = (string?)await reductionFactCommand.ExecuteScalarAsync();
        Assert.False(string.IsNullOrWhiteSpace(reductionFactReference));
        await using var reductionFactStatus = new NpgsqlCommand("select status,posted_entry_id from finance_ledger.accounting_fact where tenant_id=$1 and id=$2 and fact_type='FixedAssetCostReduced'", connection); reductionFactStatus.Parameters.AddWithValue(Guid.Parse(TenantId)); reductionFactStatus.Parameters.AddWithValue(Guid.Parse(reductionFactReference!));
        await using var reductionFactReader = await reductionFactStatus.ExecuteReaderAsync();
        Assert.True(await reductionFactReader.ReadAsync());
        Assert.Equal("Posted", reductionFactReader.GetString(0));
        var reductionJournalEntryId = reductionFactReader.GetGuid(1);
        await reductionFactReader.CloseAsync();
        await using var reductionBalanceCommand = new NpgsqlCommand("select coalesce(sum(debit),0),coalesce(sum(credit),0) from finance_ledger.journal_entry_line where tenant_id=$1 and journal_entry_id=$2", connection); reductionBalanceCommand.Parameters.AddWithValue(Guid.Parse(TenantId)); reductionBalanceCommand.Parameters.AddWithValue(reductionJournalEntryId);
        await using var reductionBalanceReader = await reductionBalanceCommand.ExecuteReaderAsync();
        Assert.True(await reductionBalanceReader.ReadAsync());
        Assert.Equal(reductionBalanceReader.GetDecimal(0), reductionBalanceReader.GetDecimal(1));
        Assert.True(reductionBalanceReader.GetDecimal(0) > 0m);
    }

    [Fact]
    public async Task EstimateChangeIsStoredIdempotentlyAndUsedProspectively()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION") ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for Fixed Assets integration verification.");
        var password = Environment.GetEnvironmentVariable("SEED_DEFAULT_PASSWORD") ?? throw new InvalidOperationException("Set SEED_DEFAULT_PASSWORD for Fixed Assets integration verification.");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development").UseSetting("TODIR_DATABASE_CONNECTION", connectionString).UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        var loginCsrf = await GetCsrfAsync(client);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email = "admin@atlas.com", password }) }; login.Headers.Add("X-CSRF-TOKEN", loginCsrf.Token); using var loginResponse = await client.SendAsync(login); Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var assetId = Guid.CreateVersion7(); var bookId = Guid.CreateVersion7(); var assetNumber = $"FA-TEST-EST-{assetId:N}"; var actorId = Guid.Empty; var categoryId = Guid.Empty; var periodStart = new DateOnly(2027, 1, 1); var periodEnd = new DateOnly(2027, 1, 31);
        await using (var setup = new NpgsqlConnection(connectionString))
        {
            await setup.OpenAsync();
            await using var actor = new NpgsqlCommand("select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", setup); actor.Parameters.AddWithValue(Guid.Parse(TenantId)); actorId = (Guid)(await actor.ExecuteScalarAsync() ?? throw new InvalidOperationException("Seed administrator was not found."));
            await using var category = new NpgsqlCommand("select id from business_assets.asset_category where tenant_id=$1 and code='LAPTOP'", setup); category.Parameters.AddWithValue(Guid.Parse(TenantId)); categoryId = (Guid)(await category.ExecuteScalarAsync() ?? throw new InvalidOperationException("Seed laptop category was not found."));
            await using var asset = new NpgsqlCommand("insert into business_assets.fixed_asset(tenant_id,id,legal_entity_id,category_id,asset_number,name,lifecycle_state,asset_nature,tracking_granularity,acquisition_date,capitalization_date,placed_in_service_date,quantity,created_by,created_at,row_version) values($1,$2,$3,$4,$5,'Estimate Integration Asset','Operational','Equipment','Individual','2026-01-01','2026-01-01','2026-01-01',1,$6,now(),1)", setup); asset.Parameters.AddWithValue(Guid.Parse(TenantId)); asset.Parameters.AddWithValue(assetId); asset.Parameters.AddWithValue(Guid.Parse(LegalEntityId)); asset.Parameters.AddWithValue(categoryId); asset.Parameters.AddWithValue(assetNumber); asset.Parameters.AddWithValue(actorId); await asset.ExecuteNonQueryAsync();
            await using var book = new NpgsqlCommand("insert into business_assets.asset_book(tenant_id,id,asset_id,book_definition_id,original_cost,residual_value,useful_life_months,depreciation_method,placed_in_service_date,created_at,row_version) values($1,$2,$3,$4,12000,0,36,'StraightLine','2026-01-01',now(),1)", setup); book.Parameters.AddWithValue(Guid.Parse(TenantId)); book.Parameters.AddWithValue(bookId); book.Parameters.AddWithValue(assetId); book.Parameters.AddWithValue(Guid.Parse(BookDefinitionId)); await book.ExecuteNonQueryAsync();
            await using var period = new NpgsqlCommand("select candidate::date,(candidate + interval '1 month - 1 day')::date from generate_series(date '2027-01-01', date '2035-01-01', interval '1 month') candidate where not exists (select 1 from business_assets.asset_depreciation_run where tenant_id=$1 and legal_entity_id=$2 and book_definition_id=$3 and period_start=candidate::date) order by candidate limit 1", setup); period.Parameters.AddWithValue(Guid.Parse(TenantId)); period.Parameters.AddWithValue(Guid.Parse(LegalEntityId)); period.Parameters.AddWithValue(Guid.Parse(BookDefinitionId)); await using var periodReader = await period.ExecuteReaderAsync(); Assert.True(await periodReader.ReadAsync()); periodStart = periodReader.GetFieldValue<DateOnly>(0); periodEnd = periodReader.GetFieldValue<DateOnly>(1); await periodReader.CloseAsync();
        }

        var estimateKey = $"integration-estimate-{assetId:N}";
        async Task<HttpResponseMessage> PostEstimateAsync()
        {
            var csrf = await GetCsrfAsync(client);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/fixed-assets/assets/{assetId:D}/books/{bookId:D}/estimate-change") { Content = JsonContent.Create(new { legalEntityId = Guid.Parse(LegalEntityId), effectiveFrom = "2026-12-01", usefulLifeMonths = 48, residualValue = 1000m, reason = "Approved useful-life review", idempotencyKey = estimateKey }) };
            request.Headers.Add("X-CSRF-TOKEN", csrf.Token); return await client.SendAsync(request);
        }
        using var estimate = await PostEstimateAsync(); var estimateBody = await estimate.Content.ReadAsStringAsync(); Assert.True(estimate.StatusCode == HttpStatusCode.OK, estimateBody); using var estimateJson = JsonDocument.Parse(estimateBody); Assert.Equal(48, estimateJson.RootElement.GetProperty("usefulLifeMonths").GetInt32()); Assert.Equal(1000m, estimateJson.RootElement.GetProperty("residualValue").GetDecimal());
        using var retry = await PostEstimateAsync(); Assert.Equal(HttpStatusCode.OK, retry.StatusCode); using var retryJson = JsonDocument.Parse(await retry.Content.ReadAsStringAsync()); Assert.True(retryJson.RootElement.GetProperty("alreadyApplied").GetBoolean());

        await using var verify = new NpgsqlConnection(connectionString); await verify.OpenAsync();
        await using var stored = new NpgsqlCommand("select useful_life_months,residual_value from business_assets.asset_book_estimate_change where tenant_id=$1 and asset_book_id=$2 and idempotency_key=$3", verify); stored.Parameters.AddWithValue(Guid.Parse(TenantId)); stored.Parameters.AddWithValue(bookId); stored.Parameters.AddWithValue(estimateKey); await using var storedReader = await stored.ExecuteReaderAsync(); Assert.True(await storedReader.ReadAsync()); Assert.Equal(48, storedReader.GetInt32(0)); Assert.Equal(1000m, storedReader.GetDecimal(1)); await storedReader.CloseAsync();

        var runKey = $"integration-estimate-run-{assetId:N}"; var runCsrf = await GetCsrfAsync(client); using var run = new HttpRequestMessage(HttpMethod.Post, "/api/v1/fixed-assets/depreciation-runs/prepare") { Content = JsonContent.Create(new { legalEntityId = Guid.Parse(LegalEntityId), bookId = Guid.Parse(BookDefinitionId), periodStart = periodStart.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), periodEnd = periodEnd.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), idempotencyKey = runKey }) }; run.Headers.Add("X-CSRF-TOKEN", runCsrf.Token); using var runResponse = await client.SendAsync(run); var runBody = await runResponse.Content.ReadAsStringAsync(); Assert.True(runResponse.StatusCode == HttpStatusCode.OK, runBody); using var runJson = JsonDocument.Parse(runBody); var runId = runJson.RootElement.GetProperty("id").GetGuid();
        await using var basis = new NpgsqlCommand("select amount,calculation_basis from business_assets.asset_depreciation_run_line where tenant_id=$1 and run_id=$2 and asset_book_id=$3", verify); basis.Parameters.AddWithValue(Guid.Parse(TenantId)); basis.Parameters.AddWithValue(runId); basis.Parameters.AddWithValue(bookId); await using var basisReader = await basis.ExecuteReaderAsync(); Assert.True(await basisReader.ReadAsync()); Assert.True(basisReader.GetDecimal(0) > 0m); using var calculation = basisReader.GetFieldValue<JsonDocument>(1); Assert.True(calculation.RootElement.TryGetProperty("estimateChanges", out _));
    }

    private static async Task<CsrfResponse> GetCsrfAsync(HttpClient client) => await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf") ?? throw new InvalidOperationException("CSRF token was not returned.");

    private sealed record CsrfResponse(string Token);
}
