using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Accounting.DTOs;
using Modules.Accounting.Services;
using Modules.Ledger.DTOs;
using Modules.Ledger.Services;
using Npgsql;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class AccountingFxRevaluationIntegrationTests : DatabaseIntegrationTestBase
{
    private static readonly TenantId AtlasTenant = new(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
    private static readonly Guid AtlasEntity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");

    [Fact]
    public async Task ForeignCurrencyOpenItemIsPreviewedPostedIdempotentlyAndExplicitlyReversed()
    {
        var runtimeConnection = Environment.GetEnvironmentVariable("TODIR_P04_RUNTIME_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_RUNTIME_CONNECTION for Accounting FX integration tests.");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        var service = factory.Services.GetRequiredService<AccountingService>();
        var actor = Guid.CreateVersion7();
        var preview = await service.PreviewFxRevaluationAsync(AtlasTenant, AtlasEntity,
            new FxRevaluationRequest(new DateOnly(2026, 10, 30), "fx-integration-" + Guid.CreateVersion7().ToString("N")), actor, default);

        Assert.Single(preview.Lines);
        Assert.Equal("Previewed", preview.Status);
        var posted = await service.PostFxRevaluationAsync(AtlasTenant, AtlasEntity, preview.Id, actor, Guid.CreateVersion7(), default);
        Assert.Equal("Posted", posted.Status);
        Assert.All(posted.Lines, line => Assert.NotNull(line.JournalEntryId));

        var retry = await service.PostFxRevaluationAsync(AtlasTenant, AtlasEntity, preview.Id, actor, Guid.CreateVersion7(), default);
        Assert.Equal(posted.Lines.Single().JournalEntryId, retry.Lines.Single().JournalEntryId);

        var reversed = await service.ReverseFxRevaluationAsync(AtlasTenant, AtlasEntity, preview.Id,
            new FxRevaluationReversalRequest(new DateOnly(2026, 10, 31), "next-period reversal"), actor, Guid.CreateVersion7(), default);
        Assert.Equal("Reversed", reversed.Status);
        Assert.All(reversed.Lines, line => Assert.NotNull(line.ReversalEntryId));
    }

    [Fact]
    public async Task PostedOpeningBatchIsReconciledApprovedAndLockedForGoLive()
    {
        var runtimeConnection = Environment.GetEnvironmentVariable("TODIR_P04_RUNTIME_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_RUNTIME_CONNECTION for Accounting go-live integration tests.");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        var ledger = factory.Services.GetRequiredService<LedgerService>();
        var accounting = factory.Services.GetRequiredService<AccountingService>();
        var actor = Guid.CreateVersion7();
        var membership = await LoadAdminMembershipAsync(runtimeConnection);
        var entity = new LegalEntityId(AtlasEntity);
        var goLiveDate = new DateOnly(2026, 1, 10);
        var batchId = await ledger.CreateOpeningBatchAsync(AtlasTenant, entity,
            new CreateOpeningBalanceBatchRequest("go-live-" + Guid.CreateVersion7().ToString("N"), "fixture-sha256-" + Guid.CreateVersion7().ToString("N"), "integration-fixture", goLiveDate,
            [
                new OpeningBalanceLineRequest(Guid.Parse("dd8c5bf5-0f03-e651-9e44-415b5184046f"), 104450m, 0m, "EGP", 104450m, 0m, 1m, "legacy-receivables", "receivables"),
                new OpeningBalanceLineRequest(Guid.Parse("3a77e917-effa-e754-8242-3b327efbe6f0"), 0m, 40000m, "EGP", 0m, 40000m, 1m, "legacy-payables", "payables"),
                new OpeningBalanceLineRequest(Guid.Parse("f389bd3e-b7f3-c556-8905-2095c1a16d6e"), 0m, 64450m, "EGP", 0m, 64450m, 1m, "legacy-equity", "equity")
            ]), actor, default);
        await ledger.PostOpeningBatchAsync(AtlasTenant, entity, batchId, actor, Guid.CreateVersion7(), default);

        var approved = await accounting.ApproveGoLiveAsync(AtlasTenant, membership, AtlasEntity,
            new ApproveGoLiveRequest(batchId, null, goLiveDate, "Opening migration accepted", "go-live-" + Guid.CreateVersion7().ToString("N")), actor, Guid.CreateVersion7(), default);

        Assert.Equal("Approved", approved.Status);
        Assert.Contains(approved.Checks, check => check.Code == "reconciliation.cash_bank" && check.Status == "NotApplicable");
        var locked = await ledger.GetOpeningBatchAsync(AtlasTenant, entity, batchId, default);
        Assert.Equal("Locked", locked?.Status);
    }

    private static async Task<Guid> LoadAdminMembershipAsync(string connectionString)
    {
        await using var connection = await NpgsqlDataSource.Create(connectionString).OpenConnectionAsync();
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            tenant.Parameters.AddWithValue(AtlasTenant.Value.ToString("D"));
            await tenant.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select m.id from platform.membership m join platform.identity i on i.tenant_id=m.tenant_id and i.id=m.identity_id where m.tenant_id=$1 and i.login_prefix='admin'", connection);
        command.Parameters.AddWithValue(AtlasTenant.Value);
        return (Guid)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("The development administrator membership is missing."));
    }
}
