using System.Net;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Modules.Tax.Entities;
using Npgsql;
using NodaTime;
using Shared.Contracts.Subledger;
using Shared.Contracts.Tax;
using Shared.Primitives.Identifiers;
using Xunit;
using Xunit.Abstractions;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class InvoicingApiIntegrationTests : DatabaseIntegrationTestBase
{
    private readonly ITestOutputHelper output;

    public InvoicingApiIntegrationTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public async Task TaxEnabledCustomerInvoiceFreezesCalculatedTaxAndPostsThroughApi()
    {
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        Assert.StartsWith("todir_invoice_", new NpgsqlConnectionStringBuilder(runtimeConnection).Database, StringComparison.Ordinal);
        var password = Required("SEED_DEFAULT_PASSWORD");
        var seed = await ReadInvoiceSeedAsync(adminConnection, "Receivable", requireTaxClassifiedProduct: true);
        seed = seed with { LegalEntityId = await ReadSecondaryEntityIdAsync(adminConnection, seed.TenantId) };
        var suffix = Guid.CreateVersion7().ToString("N");
        var invoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty)
            .ConfigureTestServices(services =>
            {
                services.RemoveAll<ITaxCountryPackRegistry>();
                services.AddSingleton<ITaxCountryPackRegistry, TestOnlyCountryPackRegistry>();
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await LoginAsync(client, "admin@atlas.com", password, seed.TenantId);
        var tax = factory.Services.GetRequiredService<ITaxCapabilities>();
        var engineResult = await tax.CalculateBatchAsync([new(new TenantId(seed.TenantId), new(seed.LegalEntityId), "Sales", "Invoice", "Sale",
            invoiceDate, invoiceDate, null, invoiceDate, seed.PartyId, "EG", "EG", "TEST-REGION", null, seed.ProductVariantId, 2m,
            seed.UnitOfMeasureId, "TaxExclusive", "250", "0", "0", seed.CurrencyCode, seed.CurrencyCode, null, "DEVELOPMENT", "Invoice")]);
        var engineTax = engineResult.Single();
        Assert.True(decimal.Parse(engineTax.TaxTotal, System.Globalization.CultureInfo.InvariantCulture) > 0m,
            $"Expected the development Tax rule to calculate a positive amount; state={engineTax.EvaluationState}, total={engineTax.TaxTotal}, explanation={engineTax.Explanation.Summary}.");

        var body = new
        {
            legalEntityId = seed.LegalEntityId,
            branchId = (Guid?)null,
            partyId = seed.PartyId,
            addressId = (Guid?)null,
            documentDate = invoiceDate,
            postingDate = invoiceDate,
            taxDate = invoiceDate,
            supplyDate = (DateOnly?)null,
            currencyCode = seed.CurrencyCode,
            paymentTermId = (Guid?)null,
            supplierInvoiceNumber = (string?)null,
            lines = new[]
            {
                new
                {
                    productVariantId = seed.ProductVariantId,
                    unitOfMeasureId = seed.UnitOfMeasureId,
                    unitCode = seed.UnitCode,
                    description = "Tax enabled integration invoice " + suffix,
                    quantity = "2",
                    unitPrice = "125.00",
                    discountAmount = "0",
                    priceOverrideReason = "Integration tax contract proof"
                }
            },
            priceMode = "Exclusive"
        };

        var create = await PostAsync(client, "/api/v1/invoicing/customer-invoices", body);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var invoiceId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var preview = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{invoiceId:D}/preview?legalEntityId={seed.LegalEntityId:D}", new { });
        Assert.True(preview.StatusCode == HttpStatusCode.OK, await preview.Content.ReadAsStringAsync());
        var previewBody = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(previewBody.GetProperty("readyToFinalize").GetBoolean(), await preview.Content.ReadAsStringAsync());
        var taxComponents = previewBody.GetProperty("lines")[0].GetProperty("taxComponents");
        Assert.True(taxComponents.GetArrayLength() > 0, await preview.Content.ReadAsStringAsync());
        Assert.True(decimal.Parse(previewBody.GetProperty("totals").GetProperty("taxAmount").GetString()!, System.Globalization.CultureInfo.InvariantCulture) > 0m);

        var finalized = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{invoiceId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
            new { version = 1, idempotencyKey = "invoice-tax-enabled-" + suffix });
        Assert.Equal(HttpStatusCode.OK, finalized.StatusCode);
        var finalizedBody = await finalized.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(decimal.Parse(finalizedBody.GetProperty("totals").GetProperty("taxAmount").GetString()!, System.Globalization.CultureInfo.InvariantCulture) > 0m);

        var originalGross = decimal.Parse(finalizedBody.GetProperty("totals").GetProperty("grossAmount").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        var originalLineId = previewBody.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var originalFingerprint = await ReadIssuedInvoiceFingerprintAsync(adminConnection, seed.TenantId, seed.LegalEntityId, invoiceId);
        var correctionRequest = new
        {
            documentType = "CreditNote",
            reason = "Partial return with original tax basis",
            documentDate = invoiceDate,
            postingDate = invoiceDate,
            taxDate = invoiceDate,
            supplyDate = (DateOnly?)null,
            idempotencyKey = "invoice-tax-credit-" + suffix,
            lines = new[] { new { originalLineId, quantity = "1" } }
        };
        var correction = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{invoiceId:D}/credit-notes?legalEntityId={seed.LegalEntityId:D}", correctionRequest);
        Assert.Equal(HttpStatusCode.Created, correction.StatusCode);
        var correctionId = (await correction.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var correctionReplay = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{invoiceId:D}/credit-notes?legalEntityId={seed.LegalEntityId:D}", correctionRequest);
        Assert.Equal(correctionId, (await correctionReplay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        var correctionPreview = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{correctionId:D}/preview?legalEntityId={seed.LegalEntityId:D}", new { });
        Assert.Equal(HttpStatusCode.OK, correctionPreview.StatusCode);
        var correctionPreviewBody = await correctionPreview.Content.ReadFromJsonAsync<JsonElement>();
        var correctionTax = decimal.Parse(correctionPreviewBody.GetProperty("totals").GetProperty("taxAmount").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(correctionTax > 0m, await correctionPreview.Content.ReadAsStringAsync());
        Assert.True(correctionPreviewBody.GetProperty("lines")[0].GetProperty("taxComponents").GetArrayLength() > 0);
        Assert.Equal(decimal.Round(originalGross / 2m, 2), decimal.Parse(correctionPreviewBody.GetProperty("totals").GetProperty("grossAmount").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
        var correctionFinalizeKey = "invoice-tax-credit-finalize-" + suffix;
        var correctionFinalize = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{correctionId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
            new { version = 1, idempotencyKey = correctionFinalizeKey });
        Assert.Equal(HttpStatusCode.OK, correctionFinalize.StatusCode);
        var correctionFinalizeBody = await correctionFinalize.Content.ReadFromJsonAsync<JsonElement>();
        var correctionFinalizeReplay = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{correctionId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
            new { version = 1, idempotencyKey = correctionFinalizeKey });
        Assert.Equal(correctionFinalizeBody.GetProperty("journalEntryId").GetGuid(),
            (await correctionFinalizeReplay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("journalEntryId").GetGuid());

        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var evidence = new NpgsqlCommand("select count(*) from business_invoicing.invoice_tax_component where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3", connection);
        evidence.Parameters.AddWithValue(seed.TenantId);
        evidence.Parameters.AddWithValue(seed.LegalEntityId);
        evidence.Parameters.AddWithValue(invoiceId);
        Assert.True((long)(await evidence.ExecuteScalarAsync())! > 0L);
        await using (var taxEvidence = new NpgsqlCommand("select (select coalesce(sum(tax_amount),0) from business_invoicing.invoice_tax_component where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3),(select coalesce(sum(tax_amount),0) from business_invoicing.invoice_tax_component where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$4)", connection))
        {
            taxEvidence.Parameters.AddWithValue(seed.TenantId);
            taxEvidence.Parameters.AddWithValue(seed.LegalEntityId);
            taxEvidence.Parameters.AddWithValue(invoiceId);
            taxEvidence.Parameters.AddWithValue(correctionId);
            await using var taxReader = await taxEvidence.ExecuteReaderAsync();
            Assert.True(await taxReader.ReadAsync());
            Assert.True(taxReader.GetDecimal(0) > 0m);
            Assert.Equal(decimal.Round(taxReader.GetDecimal(0) / 2m, 2), taxReader.GetDecimal(1));
            Assert.Equal(correctionTax, taxReader.GetDecimal(1));
        }
        Assert.Equal(originalFingerprint, await ReadIssuedInvoiceFingerprintAsync(adminConnection, seed.TenantId, seed.LegalEntityId, invoiceId));
        await AssertFinalizedInvoiceMutationsRejectedAsync(runtimeConnection, seed.TenantId, seed.LegalEntityId, invoiceId, originalLineId, hasTax: true);
    }

    [Fact]
    public async Task FullCustomerCreditNoteReversesTheIssuedBasisAndPreservesOriginalHistory()
    {
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        Assert.StartsWith("todir_invoice_", new NpgsqlConnectionStringBuilder(runtimeConnection).Database, StringComparison.Ordinal);
        var seed = await ReadInvoiceSeedAsync(adminConnection, "Receivable");
        var suffix = Guid.CreateVersion7().ToString("N");
        var invoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await LoginAsync(client, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), seed.TenantId);

        var create = await PostAsync(client, "/api/v1/invoicing/customer-invoices", new
        {
            legalEntityId = seed.LegalEntityId,
            branchId = (Guid?)null,
            partyId = seed.PartyId,
            addressId = (Guid?)null,
            documentDate = invoiceDate,
            postingDate = invoiceDate,
            taxDate = invoiceDate,
            supplyDate = (DateOnly?)null,
            currencyCode = seed.CurrencyCode,
            paymentTermId = (Guid?)null,
            supplierInvoiceNumber = (string?)null,
            lines = new[] { new { productVariantId = seed.ProductVariantId, unitOfMeasureId = seed.UnitOfMeasureId, unitCode = seed.UnitCode,
                description = "Original for a full customer credit note " + suffix, quantity = "1", unitPrice = "125.00", discountAmount = "0",
                priceOverrideReason = "Full credit integration proof" } },
            priceMode = "Exclusive"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var invoiceId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var preview = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{invoiceId:D}/preview?legalEntityId={seed.LegalEntityId:D}", new { });
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewBody = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(previewBody.GetProperty("readyToFinalize").GetBoolean(), await preview.Content.ReadAsStringAsync());
        var originalLineId = previewBody.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var finalized = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{invoiceId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
            new { version = 1, idempotencyKey = "full-credit-original-" + suffix });
        Assert.Equal(HttpStatusCode.OK, finalized.StatusCode);
        var finalizedBody = await finalized.Content.ReadFromJsonAsync<JsonElement>();
        var originalGross = decimal.Parse(finalizedBody.GetProperty("totals").GetProperty("grossAmount").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        var originalJournalId = finalizedBody.GetProperty("journalEntryId").GetGuid();
        var originalFingerprint = await ReadIssuedInvoiceFingerprintAsync(adminConnection, seed.TenantId, seed.LegalEntityId, invoiceId);

        var request = new
        {
            documentType = "CreditNote",
            reason = "Reverse the full issued customer invoice",
            documentDate = invoiceDate,
            postingDate = invoiceDate,
            taxDate = invoiceDate,
            supplyDate = (DateOnly?)null,
            idempotencyKey = "full-credit-correction-" + suffix,
            lines = new[] { new { originalLineId, quantity = "1" } }
        };
        var credit = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{invoiceId:D}/credit-notes?legalEntityId={seed.LegalEntityId:D}", request);
        Assert.Equal(HttpStatusCode.Created, credit.StatusCode);
        var creditId = (await credit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var creditReplay = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{invoiceId:D}/credit-notes?legalEntityId={seed.LegalEntityId:D}", request);
        Assert.Equal(creditId, (await creditReplay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        var creditPreview = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{creditId:D}/preview?legalEntityId={seed.LegalEntityId:D}", new { });
        Assert.Equal(HttpStatusCode.OK, creditPreview.StatusCode);
        var creditPreviewBody = await creditPreview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(creditPreviewBody.GetProperty("readyToFinalize").GetBoolean(), await creditPreview.Content.ReadAsStringAsync());
        Assert.Equal(originalGross, decimal.Parse(creditPreviewBody.GetProperty("totals").GetProperty("grossAmount").GetString()!, System.Globalization.CultureInfo.InvariantCulture));

        var finalizeKey = "full-credit-finalize-" + suffix;
        var creditFinalize = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{creditId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
            new { version = 1, idempotencyKey = finalizeKey });
        Assert.Equal(HttpStatusCode.OK, creditFinalize.StatusCode);
        var creditFinalizedBody = await creditFinalize.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(finalizedBody.GetProperty("documentNumber").GetString(), creditFinalizedBody.GetProperty("documentNumber").GetString());
        Assert.NotEqual(originalJournalId, creditFinalizedBody.GetProperty("journalEntryId").GetGuid());
        var replay = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{creditId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
            new { version = 1, idempotencyKey = finalizeKey });
        Assert.Equal(creditFinalizedBody.GetProperty("journalEntryId").GetGuid(),
            (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("journalEntryId").GetGuid());

        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using (var relation = new NpgsqlCommand("select r.relationship_type,r.amount,c.gross_total from business_invoicing.invoice_relationship r join business_invoicing.invoice c on c.tenant_id=r.tenant_id and c.legal_entity_id=r.legal_entity_id and c.id=r.invoice_id where r.tenant_id=$1 and r.legal_entity_id=$2 and r.invoice_id=$3 and r.related_invoice_id=$4", admin))
        {
            relation.Parameters.AddWithValue(seed.TenantId);
            relation.Parameters.AddWithValue(seed.LegalEntityId);
            relation.Parameters.AddWithValue(creditId);
            relation.Parameters.AddWithValue(invoiceId);
            await using var reader = await relation.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Credits", reader.GetString(0));
            Assert.Equal(originalGross, reader.GetDecimal(1));
            Assert.Equal(originalGross, reader.GetDecimal(2));
        }
        await using (var ledger = new NpgsqlCommand("select count(*),coalesce(sum(l.debit),0),coalesce(sum(l.credit),0) from finance_ledger.journal_entry e join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.id=$3 and e.source_module='invoicing' and e.source_reference=$4 and e.status='Posted'", admin))
        {
            ledger.Parameters.AddWithValue(seed.TenantId);
            ledger.Parameters.AddWithValue(seed.LegalEntityId);
            ledger.Parameters.AddWithValue(creditFinalizedBody.GetProperty("journalEntryId").GetGuid());
            ledger.Parameters.AddWithValue(creditId.ToString("D"));
            await using var reader = await ledger.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetInt64(0) > 0);
            Assert.Equal(reader.GetDecimal(1), reader.GetDecimal(2));
        }
        await using (var subledger = new NpgsqlCommand("select count(*),count(*) filter (where direction='Receivable' and polarity='Credit') from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and source_reference=$3", admin))
        {
            subledger.Parameters.AddWithValue(seed.TenantId);
            subledger.Parameters.AddWithValue(seed.LegalEntityId);
            subledger.Parameters.AddWithValue(creditId.ToString("D"));
            await using var reader = await subledger.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetInt64(0) > 0);
            Assert.Equal(reader.GetInt64(0), reader.GetInt64(1));
        }
        Assert.Equal(originalFingerprint, await ReadIssuedInvoiceFingerprintAsync(adminConnection, seed.TenantId, seed.LegalEntityId, invoiceId));
        await AssertFinalizedInvoiceMutationsRejectedAsync(runtimeConnection, seed.TenantId, seed.LegalEntityId, invoiceId, originalLineId, hasTax: false);
    }

    [Fact]
    public async Task TaxEnabledSupplierCreditUsesTaxAuthorityAndPreservesTheOriginalTaxEvidence()
    {
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        Assert.StartsWith("todir_invoice_", new NpgsqlConnectionStringBuilder(runtimeConnection).Database, StringComparison.Ordinal);
        var seed = await ReadInvoiceSeedAsync(adminConnection, "Payable", requireTaxClassifiedProduct: true);
        seed = seed with { LegalEntityId = await ReadSecondaryEntityIdAsync(adminConnection, seed.TenantId) };
        var suffix = Guid.CreateVersion7().ToString("N");
        var invoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty)
            .ConfigureTestServices(services =>
            {
                services.RemoveAll<ITaxCountryPackRegistry>();
                services.AddSingleton<ITaxCountryPackRegistry, TestOnlyCountryPackRegistry>();
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await LoginAsync(client, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), seed.TenantId);

        var create = await PostAsync(client, "/api/v1/invoicing/supplier-invoices", new
        {
            legalEntityId = seed.LegalEntityId,
            branchId = (Guid?)null,
            partyId = seed.PartyId,
            addressId = (Guid?)null,
            documentDate = invoiceDate,
            postingDate = invoiceDate,
            taxDate = invoiceDate,
            supplyDate = (DateOnly?)null,
            currencyCode = seed.CurrencyCode,
            paymentTermId = (Guid?)null,
            supplierInvoiceNumber = "TAX-SUP-" + suffix[..12],
            lines = new[] { new { productVariantId = seed.ProductVariantId, unitOfMeasureId = seed.UnitOfMeasureId, unitCode = seed.UnitCode,
                description = "Tax-enabled supplier invoice " + suffix, quantity = "2", unitPrice = "125.00", discountAmount = "0",
                priceOverrideReason = "Supplier tax correction proof" } },
            priceMode = "Exclusive"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var invoiceId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var preview = await PostAsync(client, $"/api/v1/invoicing/supplier-invoices/{invoiceId:D}/preview?legalEntityId={seed.LegalEntityId:D}", new { });
        Assert.True(preview.StatusCode == HttpStatusCode.OK, await preview.Content.ReadAsStringAsync());
        var previewBody = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(previewBody.GetProperty("readyToFinalize").GetBoolean(), await preview.Content.ReadAsStringAsync());
        var originalLineId = previewBody.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var finalized = await PostAsync(client, $"/api/v1/invoicing/supplier-invoices/{invoiceId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
            new { version = 1, idempotencyKey = "supplier-tax-original-" + suffix });
        Assert.Equal(HttpStatusCode.OK, finalized.StatusCode);
        var finalizedBody = await finalized.Content.ReadFromJsonAsync<JsonElement>();
        var originalGross = decimal.Parse(finalizedBody.GetProperty("totals").GetProperty("grossAmount").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        var originalFingerprint = await ReadIssuedInvoiceFingerprintAsync(adminConnection, seed.TenantId, seed.LegalEntityId, invoiceId);

        var correctionRequest = new
        {
            documentType = "CreditNote",
            reason = "Partial supplier credit at the original Tax basis",
            documentDate = invoiceDate,
            postingDate = invoiceDate,
            taxDate = invoiceDate,
            supplyDate = (DateOnly?)null,
            idempotencyKey = "supplier-tax-credit-" + suffix,
            lines = new[] { new { originalLineId, quantity = "1" } }
        };
        var correction = await PostAsync(client, $"/api/v1/invoicing/supplier-invoices/{invoiceId:D}/credit-notes?legalEntityId={seed.LegalEntityId:D}", correctionRequest);
        Assert.Equal(HttpStatusCode.Created, correction.StatusCode);
        var correctionId = (await correction.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var correctionPreview = await PostAsync(client, $"/api/v1/invoicing/supplier-invoices/{correctionId:D}/preview?legalEntityId={seed.LegalEntityId:D}", new { });
        Assert.Equal(HttpStatusCode.OK, correctionPreview.StatusCode);
        var correctionPreviewBody = await correctionPreview.Content.ReadFromJsonAsync<JsonElement>();
        var correctionTax = decimal.Parse(correctionPreviewBody.GetProperty("totals").GetProperty("taxAmount").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(correctionTax > 0m, await correctionPreview.Content.ReadAsStringAsync());
        Assert.Equal(decimal.Round(originalGross / 2m, 2), decimal.Parse(correctionPreviewBody.GetProperty("totals").GetProperty("grossAmount").GetString()!, System.Globalization.CultureInfo.InvariantCulture));

        var finalizeKey = "supplier-tax-credit-finalize-" + suffix;
        var correctionFinalize = await PostAsync(client, $"/api/v1/invoicing/supplier-invoices/{correctionId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
            new { version = 1, idempotencyKey = finalizeKey });
        Assert.Equal(HttpStatusCode.OK, correctionFinalize.StatusCode);
        var correctionFinalizeBody = await correctionFinalize.Content.ReadFromJsonAsync<JsonElement>();
        var correctionReplay = await PostAsync(client, $"/api/v1/invoicing/supplier-invoices/{correctionId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
            new { version = 1, idempotencyKey = finalizeKey });
        Assert.Equal(correctionFinalizeBody.GetProperty("journalEntryId").GetGuid(),
            (await correctionReplay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("journalEntryId").GetGuid());

        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using (var taxEvidence = new NpgsqlCommand("select (select coalesce(sum(tax_amount),0) from business_invoicing.invoice_tax_component where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3),(select coalesce(sum(tax_amount),0) from business_invoicing.invoice_tax_component where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$4)", admin))
        {
            taxEvidence.Parameters.AddWithValue(seed.TenantId);
            taxEvidence.Parameters.AddWithValue(seed.LegalEntityId);
            taxEvidence.Parameters.AddWithValue(invoiceId);
            taxEvidence.Parameters.AddWithValue(correctionId);
            await using var reader = await taxEvidence.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetDecimal(0) > 0m);
            Assert.Equal(decimal.Round(reader.GetDecimal(0) / 2m, 2), reader.GetDecimal(1));
            Assert.Equal(correctionTax, reader.GetDecimal(1));
        }
        await using (var ledger = new NpgsqlCommand("select count(*),coalesce(sum(l.debit),0),coalesce(sum(l.credit),0) from finance_ledger.journal_entry e join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.id=$3 and e.source_module='invoicing' and e.source_reference=$4 and e.status='Posted'", admin))
        {
            ledger.Parameters.AddWithValue(seed.TenantId);
            ledger.Parameters.AddWithValue(seed.LegalEntityId);
            ledger.Parameters.AddWithValue(correctionFinalizeBody.GetProperty("journalEntryId").GetGuid());
            ledger.Parameters.AddWithValue(correctionId.ToString("D"));
            await using var reader = await ledger.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetInt64(0) > 0);
            Assert.Equal(reader.GetDecimal(1), reader.GetDecimal(2));
        }
        await using (var subledger = new NpgsqlCommand("select count(*),count(*) filter (where direction='Payable' and polarity='Debit') from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and source_reference=$3", admin))
        {
            subledger.Parameters.AddWithValue(seed.TenantId);
            subledger.Parameters.AddWithValue(seed.LegalEntityId);
            subledger.Parameters.AddWithValue(correctionId.ToString("D"));
            await using var reader = await subledger.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetInt64(0) > 0);
            Assert.Equal(reader.GetInt64(0), reader.GetInt64(1));
        }
        Assert.Equal(originalFingerprint, await ReadIssuedInvoiceFingerprintAsync(adminConnection, seed.TenantId, seed.LegalEntityId, invoiceId));
        await AssertFinalizedInvoiceMutationsRejectedAsync(runtimeConnection, seed.TenantId, seed.LegalEntityId, invoiceId, originalLineId, hasTax: true);
    }

    [Fact]
    public async Task CountryPackDocumentRequirementsBlockInvoicesWithoutRequiredFields()
    {
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        Assert.StartsWith("todir_invoice_", new NpgsqlConnectionStringBuilder(runtimeConnection).Database, StringComparison.Ordinal);
        var seed = await ReadInvoiceSeedAsync(adminConnection, "Receivable");
        var invoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty)
            .ConfigureTestServices(services =>
            {
                services.RemoveAll<ITaxCountryPackRegistry>();
                services.AddSingleton<ITaxCountryPackRegistry>(_ => new TestOnlyCountryPackRegistry(requireSupplyDate: true));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await LoginAsync(client, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), seed.TenantId);
        var create = await PostAsync(client, "/api/v1/invoicing/customer-invoices", new
        {
            legalEntityId = seed.LegalEntityId,
            branchId = (Guid?)null,
            partyId = seed.PartyId,
            addressId = (Guid?)null,
            documentDate = invoiceDate,
            postingDate = invoiceDate,
            taxDate = invoiceDate,
            supplyDate = (DateOnly?)null,
            currencyCode = seed.CurrencyCode,
            paymentTermId = (Guid?)null,
            supplierInvoiceNumber = (string?)null,
            lines = new[] { new { productVariantId = seed.ProductVariantId, unitOfMeasureId = seed.UnitOfMeasureId, unitCode = seed.UnitCode,
                description = "Country Pack required date proof", quantity = "1", unitPrice = "125.00", discountAmount = "0", priceOverrideReason = "Country Pack validation integration proof" } },
            priceMode = "Exclusive"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var invoiceId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var preview = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{invoiceId:D}/preview?legalEntityId={seed.LegalEntityId:D}", new { });
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewBody = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(previewBody.GetProperty("readyToFinalize").GetBoolean());
        Assert.Contains(previewBody.GetProperty("blockers").EnumerateArray(), item => item.GetProperty("code").GetString() == "tax.invoice_document_field_required");
    }

    [Fact]
    public async Task SalesPermissionsCannotReadSupplierInvoicesOrFinalizeCustomerInvoices()
    {
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        Assert.StartsWith("todir_invoice_", new NpgsqlConnectionStringBuilder(runtimeConnection).Database, StringComparison.Ordinal);
        var seed = await ReadInvoiceSeedAsync(adminConnection, "Receivable");
        await using (var permissionConnection = new NpgsqlConnection(adminConnection))
        {
            await permissionConnection.OpenAsync();
            await using var revokePreview = new NpgsqlCommand("delete from platform.role_permission where tenant_id=$1 and role_id=(select id from platform.role where tenant_id=$1 and code='sales') and permission_code='invoicing.accounting_preview'", permissionConnection);
            revokePreview.Parameters.AddWithValue(seed.TenantId);
            await revokePreview.ExecuteNonQueryAsync();
        }
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await LoginAsync(client, "sales@atlas.com", Required("SEED_DEFAULT_PASSWORD"), seed.TenantId);

        var customerList = await client.GetAsync($"/api/v1/invoicing/customer-invoices?legalEntityId={seed.LegalEntityId:D}&page=1&pageSize=10");
        var supplierList = await client.GetAsync($"/api/v1/invoicing/supplier-invoices?legalEntityId={seed.LegalEntityId:D}&page=1&pageSize=10");
        var customerFinalize = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{seed.InvoiceId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
            new { version = 1, idempotencyKey = "sales-role-finalize-denial-" + Guid.CreateVersion7().ToString("N") });
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invoicePreviewRequest = new
        {
            legalEntityId = seed.LegalEntityId,
            branchId = (Guid?)null,
            partyId = seed.PartyId,
            addressId = (Guid?)null,
            documentDate = today,
            postingDate = today,
            taxDate = today,
            supplyDate = (DateOnly?)null,
            currencyCode = seed.CurrencyCode,
            paymentTermId = (Guid?)null,
            supplierInvoiceNumber = (string?)null,
            lines = new[] { new { productVariantId = seed.ProductVariantId, unitOfMeasureId = seed.UnitOfMeasureId, unitCode = seed.UnitCode,
                description = "Permission proof", quantity = "1", unitPrice = (string?)null, discountAmount = "0", priceOverrideReason = (string?)null } },
            priceMode = "Exclusive"
        };
        var unauthorizedAccountingPreview = await PostAsync(client, "/api/v1/invoicing/customer-invoices/preview", invoicePreviewRequest);
        var priceOverrideRequest = new
        {
            legalEntityId = seed.LegalEntityId,
            branchId = (Guid?)null,
            partyId = seed.PartyId,
            addressId = (Guid?)null,
            documentDate = today,
            postingDate = today,
            taxDate = today,
            supplyDate = (DateOnly?)null,
            currencyCode = seed.CurrencyCode,
            paymentTermId = (Guid?)null,
            supplierInvoiceNumber = (string?)null,
            lines = new[] { new { productVariantId = seed.ProductVariantId, unitOfMeasureId = seed.UnitOfMeasureId, unitCode = seed.UnitCode,
                description = "Permission proof price override", quantity = "1", unitPrice = "125.00", discountAmount = "0", priceOverrideReason = "Unauthorized override proof" } },
            priceMode = "Exclusive"
        };
        var unauthorizedPriceOverride = await PostAsync(client, "/api/v1/invoicing/customer-invoices", priceOverrideRequest);

        Assert.Equal(HttpStatusCode.OK, customerList.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, supplierList.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customerFinalize.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedAccountingPreview.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedPriceOverride.StatusCode);
    }

    [Fact]
    public async Task SalesInvoicePermissionsRespectLegalEntityAndBranchAssignments()
    {
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        var seed = await ReadInvoiceSeedAsync(adminConnection, "Receivable");
        var secondaryEntityId = await ReadSecondaryEntityIdAsync(adminConnection, seed.TenantId);
        Guid salesMembershipId;
        Guid salesRoleId;
        Guid branchId;
        var outOfScopeBranchId = Guid.CreateVersion7();
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using (var lookup = new NpgsqlCommand("select m.id,r.id,(select b.id from platform.branch b where b.tenant_id=m.tenant_id and b.legal_entity_id=$2 order by b.code,b.id limit 1) from platform.identity i join platform.membership m on m.tenant_id=i.tenant_id and m.identity_id=i.id join platform.membership_role_assignment a on a.tenant_id=m.tenant_id and a.membership_id=m.id join platform.role r on r.tenant_id=a.tenant_id and r.id=a.role_id where i.tenant_id=$1 and i.email='sales@atlas.com' and r.code='sales' limit 1", admin))
        {
            lookup.Parameters.AddWithValue(seed.TenantId);
            lookup.Parameters.AddWithValue(seed.LegalEntityId);
            await using var reader = await lookup.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "The Development Seed must provide a sales membership and a primary-entity branch.");
            Assert.False(reader.IsDBNull(2), "The primary legal entity must have a branch for the branch-scope proof.");
            salesMembershipId = reader.GetGuid(0);
            salesRoleId = reader.GetGuid(1);
            branchId = reader.GetGuid(2);
        }

        async Task SetSalesScopeAsync(string scopeType, Guid? scopeId)
        {
            await using var update = new NpgsqlCommand("update platform.membership_role_assignment set scope_type=$4,scope_id=$5 where tenant_id=$1 and membership_id=$2 and role_id=$3", admin);
            update.Parameters.AddWithValue(seed.TenantId);
            update.Parameters.AddWithValue(salesMembershipId);
            update.Parameters.AddWithValue(salesRoleId);
            update.Parameters.AddWithValue(scopeType);
            update.Parameters.AddWithValue((object?)scopeId ?? DBNull.Value);
            Assert.Equal(1, await update.ExecuteNonQueryAsync());
        }

        try
        {
            await SetSalesScopeAsync("LegalEntity", seed.LegalEntityId);
            using (var entityFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
                .UseSetting("TODIR_RABBITMQ_HOST", string.Empty)))
            using (var entityClient = entityFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true
            }))
            {
                await LoginAsync(entityClient, "sales@atlas.com", Required("SEED_DEFAULT_PASSWORD"), seed.TenantId);
                var allowed = await entityClient.GetAsync($"/api/v1/invoicing/customer-invoices?legalEntityId={seed.LegalEntityId:D}&page=1&pageSize=10");
                var denied = await entityClient.GetAsync($"/api/v1/invoicing/customer-invoices?legalEntityId={secondaryEntityId:D}&page=1&pageSize=10");
                Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            }

            await using (var insertBranch = new NpgsqlCommand("insert into platform.branch (id,tenant_id,legal_entity_id,code,name,status,created_at,version) values ($1,$2,$3,$4,'Permission boundary probe','Active',now(),1)", admin))
            {
                insertBranch.Parameters.AddWithValue(outOfScopeBranchId);
                insertBranch.Parameters.AddWithValue(seed.TenantId);
                insertBranch.Parameters.AddWithValue(seed.LegalEntityId);
                insertBranch.Parameters.AddWithValue("SCOPE-" + outOfScopeBranchId.ToString("N")[..12]);
                await insertBranch.ExecuteNonQueryAsync();
            }
            await SetSalesScopeAsync("Branch", branchId);
            using (var branchFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
                .UseSetting("TODIR_RABBITMQ_HOST", string.Empty)))
            using (var branchClient = branchFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true
            }))
            {
                await LoginAsync(branchClient, "sales@atlas.com", Required("SEED_DEFAULT_PASSWORD"), seed.TenantId);
                var denied = await PostAsync(branchClient, "/api/v1/invoicing/customer-invoices", new
                {
                    legalEntityId = seed.LegalEntityId,
                    branchId = outOfScopeBranchId,
                    partyId = seed.PartyId,
                    addressId = (Guid?)null,
                    documentDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    postingDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    taxDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    supplyDate = (DateOnly?)null,
                    currencyCode = seed.CurrencyCode,
                    paymentTermId = (Guid?)null,
                    supplierInvoiceNumber = (string?)null,
                    lines = new[] { new { productVariantId = seed.ProductVariantId, unitOfMeasureId = seed.UnitOfMeasureId, unitCode = seed.UnitCode,
                        description = "Out-of-scope branch permission proof", quantity = "1", unitPrice = (string?)null, discountAmount = "0", priceOverrideReason = (string?)null } },
                    priceMode = "Exclusive"
                });
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            }
        }
        finally
        {
            await SetSalesScopeAsync("Tenant", null);
            await using var removeBranch = new NpgsqlCommand("delete from platform.branch where tenant_id=$1 and legal_entity_id=$2 and id=$3", admin);
            removeBranch.Parameters.AddWithValue(seed.TenantId);
            removeBranch.Parameters.AddWithValue(seed.LegalEntityId);
            removeBranch.Parameters.AddWithValue(outOfScopeBranchId);
            await removeBranch.ExecuteNonQueryAsync();
        }
    }

    [Theory]
    [InlineData("Receivable", "customer-invoices", false, "")]
    [InlineData("Payable", "supplier-invoices", true, "")]
    [InlineData("Receivable", "customer-invoices", false, "InvoiceFinalization")]
    [InlineData("Receivable", "customer-invoices", false, "LedgerJournal")]
    [InlineData("Receivable", "customer-invoices", false, "SubledgerOpenItem")]
    [InlineData("Receivable", "customer-invoices", false, "OutboxMessage")]
    public async Task CustomerAndSupplierDocumentsFinalizeThroughApiAndReplayIdempotently(string direction, string route, bool supplier, string failurePoint)
    {
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        var runtimeDatabase = new NpgsqlConnectionStringBuilder(runtimeConnection).Database;
        Assert.StartsWith("todir_invoice_", runtimeDatabase, StringComparison.Ordinal);
        var password = Required("SEED_DEFAULT_PASSWORD");
        var seed = await ReadInvoiceSeedAsync(adminConnection, direction);
        var suffix = Guid.CreateVersion7().ToString("N");
        var invoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var documentCurrency = supplier ? "USD" : seed.CurrencyCode;
        var installmentTermId = await ReadInstallmentPaymentTermIdAsync(adminConnection, seed.TenantId);

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        await LoginAsync(client, "admin@atlas.com", password, seed.TenantId);
        var tax = factory.Services.GetRequiredService<ITaxCapabilities>();
        var disabledTax = await tax.CalculateBatchAsync([new(new TenantId(seed.TenantId), new(seed.LegalEntityId), supplier ? "Purchase" : "Sales", "Invoice", supplier ? "Purchase" : "Sale",
            invoiceDate, invoiceDate, null, invoiceDate, seed.PartyId, "EG", "EG", null, null, seed.ProductVariantId, 1m, seed.UnitOfMeasureId,
            "TaxExclusive", "125", "0", "0", documentCurrency, "EGP", null)]);
        Assert.Equal("NotEvaluatedBecauseCapabilityDisabled", disabledTax.Single().EvaluationState);

        var body = new
        {
            legalEntityId = seed.LegalEntityId,
            branchId = (Guid?)null,
            partyId = seed.PartyId,
            addressId = (Guid?)null,
            documentDate = invoiceDate,
            postingDate = invoiceDate,
            taxDate = invoiceDate,
            supplyDate = (DateOnly?)null,
            currencyCode = documentCurrency,
            paymentTermId = installmentTermId,
            supplierInvoiceNumber = supplier ? "SUP-" + suffix[..16] : null,
            lines = new[]
            {
                new
                {
                    productVariantId = supplier ? (Guid?)null : seed.ProductVariantId,
                    unitOfMeasureId = supplier ? (Guid?)null : seed.UnitOfMeasureId,
                    unitCode = supplier ? "HOUR" : seed.UnitCode,
                    description = supplier ? "Supplier consulting service " + suffix : "Integration invoice " + suffix,
                    quantity = "1",
                    unitPrice = "125.00",
                    discountAmount = "0",
                    priceOverrideReason = "Integration contract proof"
                }
            },
            priceMode = "Exclusive"
        };

        var create = await PostAsync(client, $"/api/v1/invoicing/{route}", body);
        Assert.True(create.StatusCode == HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var invoiceId = created.GetProperty("id").GetGuid();

        var preview = await PostAsync(client, $"/api/v1/invoicing/{route}/{invoiceId:D}/preview?legalEntityId={seed.LegalEntityId:D}", new { });
        Assert.True(preview.StatusCode == HttpStatusCode.OK, await preview.Content.ReadAsStringAsync());
        var previewBody = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(previewBody.GetProperty("readyToFinalize").GetBoolean(), await preview.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Object, previewBody.GetProperty("accountingPreview").ValueKind);

        var finalizeRequest = new { version = 1, idempotencyKey = "invoice-api-" + suffix };
        if (failurePoint.Length > 0)
        {
            var functionName = "invoice_failure_" + suffix;
            var triggerName = "invoice_failure_" + suffix;
            await InstallFinalizationFailpointAsync(adminConnection, failurePoint, functionName, triggerName);
            try
            {
                var failedFinalize = await PostAsync(client, $"/api/v1/invoicing/{route}/{invoiceId:D}/finalize?legalEntityId={seed.LegalEntityId:D}", finalizeRequest);
                Assert.Equal(HttpStatusCode.InternalServerError, failedFinalize.StatusCode);
                await AssertFinalizationRolledBackAsync(adminConnection, seed, invoiceId);
            }
            finally { await RemoveFinalizationFailpointAsync(adminConnection, failurePoint, functionName, triggerName); }
            var retriedFinalize = await PostAsync(client, $"/api/v1/invoicing/{route}/{invoiceId:D}/finalize?legalEntityId={seed.LegalEntityId:D}", finalizeRequest);
            Assert.Equal(HttpStatusCode.OK, retriedFinalize.StatusCode);
            await AssertFinalizationCommittedOnceAsync(adminConnection, seed, invoiceId, scheduleCount: 3);
            return;
        }
        var finalizePath = $"/api/v1/invoicing/{route}/{invoiceId:D}/finalize?legalEntityId={seed.LegalEntityId:D}";
        var csrfToken = await GetCsrfTokenAsync(client);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> ConcurrentFinalizeAsync()
        {
            await start.Task;
            return await PostWithCsrfAsync(client, finalizePath, finalizeRequest, csrfToken);
        }
        var firstFinalize = ConcurrentFinalizeAsync();
        var secondFinalize = ConcurrentFinalizeAsync();
        start.SetResult();
        var concurrentResults = await Task.WhenAll(firstFinalize, secondFinalize);
        var finalize = concurrentResults[0];
        var replay = concurrentResults[1];
        Assert.True(finalize.StatusCode == HttpStatusCode.OK, await finalize.Content.ReadAsStringAsync());
        Assert.True(replay.StatusCode == HttpStatusCode.OK, await replay.Content.ReadAsStringAsync());
        var finalized = await finalize.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Finalized", finalized.GetProperty("lifecycle").GetString());
        Assert.NotEqual(Guid.Empty, finalized.GetProperty("journalEntryId").GetGuid());
        var openItemIds = finalized.GetProperty("openItemIds").EnumerateArray().Select(item => item.GetGuid()).ToArray();
        var scheduleCount = finalized.GetProperty("paymentSchedule").GetArrayLength();
        Assert.True(scheduleCount > 0);
        Assert.Equal(3, scheduleCount);
        var scheduledAmount = finalized.GetProperty("paymentSchedule").EnumerateArray()
            .Sum(item => decimal.Parse(item.GetProperty("amount").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(decimal.Parse(finalized.GetProperty("totals").GetProperty("grossAmount").GetString()!, System.Globalization.CultureInfo.InvariantCulture), scheduledAmount);
        Assert.Equal(scheduleCount, openItemIds.Length);

        var replayed = await replay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(finalized.GetProperty("documentNumber").GetString(), replayed.GetProperty("documentNumber").GetString());
        Assert.Equal(finalized.GetProperty("journalEntryId").GetGuid(), replayed.GetProperty("journalEntryId").GetGuid());
        Assert.Equal(finalized.GetProperty("openItemIds").GetArrayLength(), replayed.GetProperty("openItemIds").GetArrayLength());

        var detail = await client.GetAsync($"/api/v1/invoicing/{route}/{invoiceId:D}/detail?legalEntityId={seed.LegalEntityId:D}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var detailBody = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Finalized", detailBody.GetProperty("lifecycle").GetString());
        Assert.Equal(finalized.GetProperty("documentNumber").GetString(), detailBody.GetProperty("documentNumber").GetString());

        var render = await client.GetAsync($"/api/v1/invoicing/{route}/{invoiceId:D}/render-model?legalEntityId={seed.LegalEntityId:D}");
        Assert.Equal(HttpStatusCode.OK, render.StatusCode);
        var renderBody = await render.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(finalized.GetProperty("documentNumber").GetString(), renderBody.GetProperty("documentNumber").GetString());
        Assert.Equal(detailBody.GetProperty("lines").GetArrayLength(), renderBody.GetProperty("lines").GetArrayLength());
        Assert.Equal(detailBody.GetProperty("totals").GetProperty("grossAmount").GetString(), renderBody.GetProperty("totals").GetProperty("grossAmount").GetString());

        var listed = await client.GetAsync($"/api/v1/invoicing/{route}?legalEntityId={seed.LegalEntityId:D}&search={Uri.EscapeDataString(finalized.GetProperty("documentNumber").GetString()!)}&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var listedBody = await listed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(listedBody.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == invoiceId);

        var compliance = await client.GetAsync($"/api/v1/invoicing/{route}/{invoiceId:D}/compliance-snapshot?legalEntityId={seed.LegalEntityId:D}");
        Assert.Equal(HttpStatusCode.OK, compliance.StatusCode);
        var complianceBody = await compliance.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, complianceBody.GetProperty("schemaVersion").GetInt32());
        var financialContext = complianceBody.GetProperty("financialContext");
        Assert.Equal(documentCurrency, financialContext.GetProperty("transactionCurrencyCode").GetString());
        Assert.Equal(supplier ? "EGP" : documentCurrency, financialContext.GetProperty("baseCurrencyCode").GetString());
        Assert.Equal(supplier ? "Purchase" : "Sales", financialContext.GetProperty("purpose").GetString());
        Assert.False(financialContext.TryGetProperty("tenantId", out _));
        Assert.False(financialContext.TryGetProperty("legalEntityId", out _));
        Assert.True(financialContext.GetProperty("roundingRules").GetArrayLength() > 0);
        var frozenRate = financialContext.GetProperty("exchangeRate");
        if (supplier)
        {
            Assert.Equal("USD", frozenRate.GetProperty("fromCurrency").GetString());
            Assert.Equal("EGP", frozenRate.GetProperty("toCurrency").GetString());
            await using var fxConnection = new NpgsqlConnection(adminConnection);
            await fxConnection.OpenAsync();
            await using var fx = new NpgsqlCommand("select rate from foundation_finbase.exchange_rate where tenant_id=$1 and legal_entity_id=$2 and from_currency='USD' and to_currency='EGP' and purpose='Commercial' and status='Approved' and effective_date<=$3 order by effective_date desc limit 1", fxConnection);
            fx.Parameters.AddWithValue(seed.TenantId);
            fx.Parameters.AddWithValue(seed.LegalEntityId);
            fx.Parameters.AddWithValue(invoiceDate);
            var approvedRate = (decimal)(await fx.ExecuteScalarAsync())!;
            Assert.Equal(approvedRate, decimal.Parse(frozenRate.GetProperty("rate").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
            await using var ledgerRate = new NpgsqlCommand("select count(*) from finance_ledger.journal_entry_line where tenant_id=$1 and legal_entity_id=$2 and journal_entry_id=$3 and currency_code='USD' and exchange_rate=$4", fxConnection);
            ledgerRate.Parameters.AddWithValue(seed.TenantId);
            ledgerRate.Parameters.AddWithValue(seed.LegalEntityId);
            ledgerRate.Parameters.AddWithValue(finalized.GetProperty("journalEntryId").GetGuid());
            ledgerRate.Parameters.AddWithValue(approvedRate);
            Assert.True((long)(await ledgerRate.ExecuteScalarAsync())! > 0L);
        }
        else Assert.Equal(JsonValueKind.Null, frozenRate.ValueKind);

        var fileReferences = factory.Services.GetRequiredService<Shared.Contracts.Invoicing.IInvoiceFileReferenceCapability>();
        var verifiedFile = new Shared.Contracts.Platform.VerifiedPlatformFileReference(new(seed.TenantId), Guid.CreateVersion7(), new string('A', 64), "application/pdf", 2048);
        var attachmentRequest = new Shared.Contracts.Invoicing.InvoiceAttachmentReferenceRequest(seed.LegalEntityId, invoiceId, verifiedFile,
            supplier ? Shared.Contracts.Invoicing.InvoiceAttachmentRole.SupplierOriginalInvoice : Shared.Contracts.Invoicing.InvoiceAttachmentRole.CustomerPO);
        var attachmentId = await fileReferences.AddAttachmentReferenceAsync(new(seed.TenantId), seed.ActorId, seed.MembershipId, attachmentRequest, Guid.CreateVersion7());
        var attachmentReplayId = await fileReferences.AddAttachmentReferenceAsync(new(seed.TenantId), seed.ActorId, seed.MembershipId, attachmentRequest, Guid.CreateVersion7());
        Assert.Equal(attachmentId, attachmentReplayId);
        var artifactRequest = new Shared.Contracts.Invoicing.InvoiceIssuedArtifactReferenceRequest(seed.LegalEntityId, invoiceId, verifiedFile,
            "invoice-standard", "3", "ar-EG");
        var artifactId = await fileReferences.RecordIssuedArtifactReferenceAsync(new(seed.TenantId), seed.ActorId, artifactRequest, Guid.CreateVersion7());
        var artifactReplayId = await fileReferences.RecordIssuedArtifactReferenceAsync(new(seed.TenantId), seed.ActorId, artifactRequest, Guid.CreateVersion7());
        Assert.Equal(artifactId, artifactReplayId);
        var originalArtifact = await fileReferences.GetOriginalIssuedArtifactAsync(new(seed.TenantId), seed.LegalEntityId, invoiceId,
            "invoice-standard", "3", "ar-EG");
        Assert.NotNull(originalArtifact);
        Assert.Equal(verifiedFile.FileRef, originalArtifact.FileRef);
        Assert.Equal(new string('a', 64), originalArtifact.Sha256);

        var originalLineId = previewBody.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var originalFingerprint = await ReadIssuedInvoiceFingerprintAsync(adminConnection, seed.TenantId, seed.LegalEntityId, invoiceId);
        if (!supplier)
        {
            var correctionRequest = new
            {
                documentType = "CreditNote",
                reason = "Return of part of the invoiced quantity",
                documentDate = invoiceDate,
                postingDate = invoiceDate,
                taxDate = invoiceDate,
                supplyDate = (DateOnly?)null,
                idempotencyKey = "invoice-correction-" + suffix,
                lines = new[] { new { originalLineId, quantity = "0.5" } }
            };
            var correction = await PostAsync(client, $"/api/v1/invoicing/{route}/{invoiceId:D}/credit-notes?legalEntityId={seed.LegalEntityId:D}", correctionRequest);
            Assert.Equal(HttpStatusCode.Created, correction.StatusCode);
            var mismatchedCorrectionType = await PostAsync(client, $"/api/v1/invoicing/{route}/{invoiceId:D}/credit-notes?legalEntityId={seed.LegalEntityId:D}", new
            {
                documentType = "DebitNote",
                reason = "The action path must determine the correction type",
                documentDate = invoiceDate,
                postingDate = invoiceDate,
                taxDate = invoiceDate,
                supplyDate = (DateOnly?)null,
                idempotencyKey = "invoice-correction-type-mismatch-" + suffix,
                lines = new[] { new { originalLineId, quantity = "0.25" } }
            });
            Assert.Equal(HttpStatusCode.BadRequest, mismatchedCorrectionType.StatusCode);
            var correctionId = (await correction.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var correctionReplay = await PostAsync(client, $"/api/v1/invoicing/{route}/{invoiceId:D}/credit-notes?legalEntityId={seed.LegalEntityId:D}", correctionRequest);
            Assert.Equal(correctionId, (await correctionReplay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
            var correctionFinalize = await PostAsync(client,
                $"/api/v1/invoicing/{route}/{correctionId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
                new { version = 1, idempotencyKey = "invoice-correction-finalize-" + suffix });
            Assert.True(correctionFinalize.StatusCode == HttpStatusCode.OK, await correctionFinalize.Content.ReadAsStringAsync());
            await using var correctionAdmin = new NpgsqlConnection(adminConnection);
            await correctionAdmin.OpenAsync();
            await using var relationship = new NpgsqlCommand("select correction.document_type,relation.relationship_type,relation.amount from business_invoicing.invoice_relationship relation join business_invoicing.invoice correction on correction.tenant_id=relation.tenant_id and correction.legal_entity_id=relation.legal_entity_id and correction.id=relation.invoice_id where relation.tenant_id=$1 and relation.legal_entity_id=$2 and relation.invoice_id=$3 and relation.related_invoice_id=$4", correctionAdmin);
            relationship.Parameters.AddWithValue(seed.TenantId);
            relationship.Parameters.AddWithValue(seed.LegalEntityId);
            relationship.Parameters.AddWithValue(correctionId);
            relationship.Parameters.AddWithValue(invoiceId);
            await using var relationshipReader = await relationship.ExecuteReaderAsync();
            Assert.True(await relationshipReader.ReadAsync());
            Assert.Equal("CreditNote", relationshipReader.GetString(0));
            Assert.Equal("Credits", relationshipReader.GetString(1));
            Assert.True(relationshipReader.GetDecimal(2) > 0m);
            await relationshipReader.CloseAsync();

            var subledger = factory.Services.GetRequiredService<ISubledgerCapabilities>();
            var originalItemId = openItemIds[0];
            var creditItemId = (await correctionFinalize.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("openItemIds")[0].GetGuid();
            var originalItem = await subledger.GetOpenItemAsync(new(seed.TenantId), new(seed.LegalEntityId), originalItemId);
            var creditItem = await subledger.GetOpenItemAsync(new(seed.TenantId), new(seed.LegalEntityId), creditItemId);
            Assert.NotNull(originalItem);
            Assert.NotNull(creditItem);
            var applicationAmount = Math.Min(decimal.Parse(originalItem!.OpenTransactionAmount, System.Globalization.CultureInfo.InvariantCulture),
                decimal.Parse(creditItem!.OpenTransactionAmount, System.Globalization.CultureInfo.InvariantCulture)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            await subledger.ApplyAsync(new(seed.TenantId), seed.MembershipId, new(new(seed.LegalEntityId), seed.PartyId, SubledgerDirectionContract.Receivable,
                "invoice-correction-application-" + suffix, invoiceDate, invoiceDate,
                [new(originalItemId, creditItemId, applicationAmount, ExpectedDebitVersion: originalItem.RowVersion, ExpectedCreditVersion: creditItem.RowVersion)],
                seed.ActorId, Guid.CreateVersion7()));
            var partiallySettled = await client.GetAsync($"/api/v1/invoicing/{route}/{invoiceId:D}/detail?legalEntityId={seed.LegalEntityId:D}");
            Assert.Equal(HttpStatusCode.OK, partiallySettled.StatusCode);
            Assert.Equal("PartiallySettled", (await partiallySettled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("settlementSummary").GetString());

            var excessRequest = new
            {
                documentType = "CreditNote",
                reason = "Return of an already partially credited quantity",
                documentDate = invoiceDate,
                postingDate = invoiceDate,
                taxDate = invoiceDate,
                supplyDate = (DateOnly?)null,
                idempotencyKey = "invoice-correction-excess-" + suffix,
                lines = new[] { new { originalLineId, quantity = "0.6" } }
            };
            var excess = await PostAsync(client, $"/api/v1/invoicing/{route}/{invoiceId:D}/credit-notes?legalEntityId={seed.LegalEntityId:D}", excessRequest);
            Assert.Equal(HttpStatusCode.BadRequest, excess.StatusCode);
            var excessError = await excess.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("invoicing.credit_quantity_exceeded", excessError.GetProperty("code").GetString());
        }

        if (!supplier)
        {
            var remainingCreditRequest = new
            {
                documentType = "CreditNote",
                reason = "Return the remaining invoiced quantity",
                documentDate = invoiceDate,
                postingDate = invoiceDate,
                taxDate = invoiceDate,
                supplyDate = (DateOnly?)null,
                idempotencyKey = "invoice-correction-remaining-" + suffix,
                lines = new[] { new { originalLineId, quantity = "0.5" } }
            };
            var remainingCredit = await PostAsync(client, $"/api/v1/invoicing/{route}/{invoiceId:D}/credit-notes?legalEntityId={seed.LegalEntityId:D}", remainingCreditRequest);
            Assert.Equal(HttpStatusCode.Created, remainingCredit.StatusCode);
            var remainingCreditId = (await remainingCredit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var remainingReplay = await PostAsync(client, $"/api/v1/invoicing/{route}/{invoiceId:D}/credit-notes?legalEntityId={seed.LegalEntityId:D}", remainingCreditRequest);
            Assert.Equal(remainingCreditId, (await remainingReplay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
            var remainingFinalizeKey = "invoice-correction-remaining-finalize-" + suffix;
            var remainingFinalize = await PostAsync(client, $"/api/v1/invoicing/{route}/{remainingCreditId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
                new { version = 1, idempotencyKey = remainingFinalizeKey });
            Assert.Equal(HttpStatusCode.OK, remainingFinalize.StatusCode);
            var remainingFinalizeBody = await remainingFinalize.Content.ReadFromJsonAsync<JsonElement>();
            var remainingFinalizeReplay = await PostAsync(client, $"/api/v1/invoicing/{route}/{remainingCreditId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
                new { version = 1, idempotencyKey = remainingFinalizeKey });
            Assert.Equal(remainingFinalizeBody.GetProperty("journalEntryId").GetGuid(),
                (await remainingFinalizeReplay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("journalEntryId").GetGuid());
        }
        else
        {
            var supplierCreditRequest = new
            {
                documentType = "CreditNote",
                reason = "Supplier issued a full credit for the original service invoice",
                documentDate = invoiceDate,
                postingDate = invoiceDate,
                taxDate = invoiceDate,
                supplyDate = (DateOnly?)null,
                idempotencyKey = "supplier-invoice-credit-" + suffix,
                lines = new[] { new { originalLineId, quantity = "1" } }
            };
            var supplierCredit = await PostAsync(client, $"/api/v1/invoicing/{route}/{invoiceId:D}/credit-notes?legalEntityId={seed.LegalEntityId:D}", supplierCreditRequest);
            Assert.Equal(HttpStatusCode.Created, supplierCredit.StatusCode);
            var supplierCreditId = (await supplierCredit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var supplierCreditReplay = await PostAsync(client, $"/api/v1/invoicing/{route}/{invoiceId:D}/credit-notes?legalEntityId={seed.LegalEntityId:D}", supplierCreditRequest);
            Assert.Equal(supplierCreditId, (await supplierCreditReplay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
            var supplierFinalizeKey = "supplier-invoice-credit-finalize-" + suffix;
            var supplierCreditFinalize = await PostAsync(client, $"/api/v1/invoicing/{route}/{supplierCreditId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
                new { version = 1, idempotencyKey = supplierFinalizeKey });
            Assert.Equal(HttpStatusCode.OK, supplierCreditFinalize.StatusCode);
            var supplierFinalizeBody = await supplierCreditFinalize.Content.ReadFromJsonAsync<JsonElement>();
            var supplierFinalizeReplay = await PostAsync(client, $"/api/v1/invoicing/{route}/{supplierCreditId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
                new { version = 1, idempotencyKey = supplierFinalizeKey });
            Assert.Equal(supplierFinalizeBody.GetProperty("journalEntryId").GetGuid(),
                (await supplierFinalizeReplay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("journalEntryId").GetGuid());
        }

        await AssertFinalizedInvoiceMutationsRejectedAsync(runtimeConnection, seed.TenantId, seed.LegalEntityId, invoiceId, originalLineId, hasTax: false);

        var debitRequest = new
        {
            documentType = "DebitNote",
            reason = "Additional approved service omitted from the original document",
            documentDate = invoiceDate,
            postingDate = invoiceDate,
            taxDate = invoiceDate,
            supplyDate = (DateOnly?)null,
            idempotencyKey = "invoice-debit-note-" + suffix,
            lines = new[] { new { originalLineId, quantity = "0.25" } }
        };
        var debitCreate = await PostAsync(client, $"/api/v1/invoicing/{route}/{invoiceId:D}/debit-notes?legalEntityId={seed.LegalEntityId:D}", debitRequest);
        Assert.Equal(HttpStatusCode.Created, debitCreate.StatusCode);
        var debitId = (await debitCreate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var debitFinalize = await PostAsync(client, $"/api/v1/invoicing/{route}/{debitId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
            new { version = 1, idempotencyKey = "invoice-debit-finalize-" + suffix });
        Assert.True(debitFinalize.StatusCode == HttpStatusCode.OK, await debitFinalize.Content.ReadAsStringAsync());

        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using (var debitRelation = new NpgsqlCommand("select relationship_type from business_invoicing.invoice_relationship where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 and related_invoice_id=$4", admin))
        {
            debitRelation.Parameters.AddWithValue(seed.TenantId);
            debitRelation.Parameters.AddWithValue(seed.LegalEntityId);
            debitRelation.Parameters.AddWithValue(debitId);
            debitRelation.Parameters.AddWithValue(invoiceId);
            Assert.Equal("Debits", await debitRelation.ExecuteScalarAsync());
        }
        await using (var outbox = new NpgsqlCommand("select count(*) from platform.outbox_message where tenant_id=$1 and causation_id=$2 and message_type in ('InvoiceFinalizedV1','InvoiceComplianceRequestedV1')", admin))
        {
            outbox.Parameters.AddWithValue(seed.TenantId);
            outbox.Parameters.AddWithValue(invoiceId);
            Assert.Equal(2L, (long)(await outbox.ExecuteScalarAsync())!);
        }
        await using var verify = new NpgsqlCommand("select count(*),count(distinct document_number),count(distinct journal_entry_id) from business_invoicing.invoice_finalization_record where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3", admin);
        verify.Parameters.AddWithValue(seed.TenantId);
        verify.Parameters.AddWithValue(seed.LegalEntityId);
        verify.Parameters.AddWithValue(invoiceId);
        await using var reader = await verify.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt64(0));
        Assert.Equal(1, reader.GetInt64(1));
        Assert.Equal(1, reader.GetInt64(2));
        await reader.CloseAsync();

        await using (var correctiveEvidence = new NpgsqlCommand("select count(*),coalesce(sum(relation.amount),0),coalesce(max(correction.gross_total),0) from business_invoicing.invoice_relationship relation join business_invoicing.invoice correction on correction.tenant_id=relation.tenant_id and correction.legal_entity_id=relation.legal_entity_id and correction.id=relation.invoice_id where relation.tenant_id=$1 and relation.legal_entity_id=$2 and relation.related_invoice_id=$3 and relation.relationship_type='Credits' and correction.document_type='CreditNote' and correction.lifecycle_state='Finalized'", admin))
        {
            correctiveEvidence.Parameters.AddWithValue(seed.TenantId);
            correctiveEvidence.Parameters.AddWithValue(seed.LegalEntityId);
            correctiveEvidence.Parameters.AddWithValue(invoiceId);
            await using var correctiveReader = await correctiveEvidence.ExecuteReaderAsync();
            Assert.True(await correctiveReader.ReadAsync());
            Assert.Equal(supplier ? 1L : 2L, correctiveReader.GetInt64(0));
            Assert.Equal(125m, correctiveReader.GetDecimal(1));
            Assert.Equal(supplier ? 125m : 62.5m, correctiveReader.GetDecimal(2));
        }
        Assert.Equal(originalFingerprint, await ReadIssuedInvoiceFingerprintAsync(adminConnection, seed.TenantId, seed.LegalEntityId, invoiceId));

        await using (var correctionJournal = new NpgsqlCommand("select e.source_reference,count(*),coalesce(sum(l.debit),0),coalesce(sum(l.credit),0) from finance_ledger.journal_entry e join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.source_module='invoicing' and e.source_reference in (select c.id::text from business_invoicing.invoice_relationship r join business_invoicing.invoice c on c.tenant_id=r.tenant_id and c.legal_entity_id=r.legal_entity_id and c.id=r.invoice_id where r.tenant_id=$1 and r.legal_entity_id=$2 and r.related_invoice_id=$3 and r.relationship_type='Credits' and c.lifecycle_state='Finalized') and e.status='Posted' group by e.source_reference", admin))
        {
            correctionJournal.Parameters.AddWithValue(seed.TenantId);
            correctionJournal.Parameters.AddWithValue(seed.LegalEntityId);
            correctionJournal.Parameters.AddWithValue(invoiceId);
            await using var correctionJournalReader = await correctionJournal.ExecuteReaderAsync();
            var correctionJournalCount = 0;
            while (await correctionJournalReader.ReadAsync())
            {
                correctionJournalCount++;
                Assert.True(correctionJournalReader.GetInt64(1) > 0);
                Assert.Equal(correctionJournalReader.GetDecimal(2), correctionJournalReader.GetDecimal(3));
            }
            Assert.Equal(supplier ? 1 : 2, correctionJournalCount);
        }

        await using (var correctionItems = new NpgsqlCommand("select count(*),count(*) filter (where direction=$4 and polarity=$5) from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and source_reference in (select invoice_id::text from business_invoicing.invoice_relationship where tenant_id=$1 and legal_entity_id=$2 and related_invoice_id=$3 and relationship_type='Credits')", admin))
        {
            correctionItems.Parameters.AddWithValue(seed.TenantId);
            correctionItems.Parameters.AddWithValue(seed.LegalEntityId);
            correctionItems.Parameters.AddWithValue(invoiceId);
            correctionItems.Parameters.AddWithValue(supplier ? "Payable" : "Receivable");
            correctionItems.Parameters.AddWithValue(supplier ? "Debit" : "Credit");
            await using var itemReader = await correctionItems.ExecuteReaderAsync();
            Assert.True(await itemReader.ReadAsync());
            Assert.True(itemReader.GetInt64(0) > 0);
            Assert.Equal(itemReader.GetInt64(0), itemReader.GetInt64(1));
        }

        await using (var journal = new NpgsqlCommand("select count(*),coalesce(sum(l.debit),0),coalesce(sum(l.credit),0) from finance_ledger.journal_entry e join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.id=$3 and e.source_module='invoicing' and e.source_reference=$4 and e.status='Posted'", admin))
        {
            journal.Parameters.AddWithValue(seed.TenantId);
            journal.Parameters.AddWithValue(seed.LegalEntityId);
            journal.Parameters.AddWithValue(finalized.GetProperty("journalEntryId").GetGuid());
            journal.Parameters.AddWithValue(invoiceId.ToString("D"));
            await using var journalReader = await journal.ExecuteReaderAsync();
            Assert.True(await journalReader.ReadAsync());
            Assert.True(journalReader.GetInt64(0) > 0);
            Assert.Equal(journalReader.GetDecimal(1), journalReader.GetDecimal(2));
        }

        await using (var openItems = new NpgsqlCommand("select count(*) from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and source_reference=$3 and id=any($4)", admin))
        {
            openItems.Parameters.AddWithValue(seed.TenantId);
            openItems.Parameters.AddWithValue(seed.LegalEntityId);
            openItems.Parameters.AddWithValue(invoiceId.ToString("D"));
            openItems.Parameters.AddWithValue(openItemIds);
            Assert.Equal((long)scheduleCount, (long)(await openItems.ExecuteScalarAsync())!);
        }
    }

    [Fact]
    public async Task ConcurrentSupplierInvoicesCannotReuseTheSameNormalizedExternalNumber()
    {
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        Assert.StartsWith("todir_invoice_", new NpgsqlConnectionStringBuilder(runtimeConnection).Database, StringComparison.Ordinal);
        var password = Required("SEED_DEFAULT_PASSWORD");
        var seed = await ReadInvoiceSeedAsync(adminConnection, "Payable");
        var suffix = Guid.CreateVersion7().ToString("N");
        var invoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var supplierNumber = "  race-" + suffix[..16] + "   2026 ";

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await LoginAsync(client, "admin@atlas.com", password, seed.TenantId);

        object DraftBody(string description) => new
        {
            legalEntityId = seed.LegalEntityId,
            branchId = (Guid?)null,
            partyId = seed.PartyId,
            addressId = (Guid?)null,
            documentDate = invoiceDate,
            postingDate = invoiceDate,
            taxDate = invoiceDate,
            supplyDate = (DateOnly?)null,
            currencyCode = "USD",
            paymentTermId = (Guid?)null,
            supplierInvoiceNumber = supplierNumber,
            lines = new[]
            {
                new
                {
                    productVariantId = (Guid?)null,
                    unitOfMeasureId = (Guid?)null,
                    unitCode = "HOUR",
                    description,
                    quantity = "1",
                    unitPrice = "125.00",
                    discountAmount = "0",
                    priceOverrideReason = "Duplicate-race integration proof"
                }
            },
            priceMode = "Exclusive"
        };

        var firstCreate = await PostAsync(client, "/api/v1/invoicing/supplier-invoices", DraftBody("First competing supplier invoice"));
        var secondCreate = await PostAsync(client, "/api/v1/invoicing/supplier-invoices", DraftBody("Second competing supplier invoice"));
        Assert.Equal(HttpStatusCode.Created, firstCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondCreate.StatusCode);
        var firstId = (await firstCreate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var secondId = (await secondCreate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var firstPath = $"/api/v1/invoicing/supplier-invoices/{firstId:D}/finalize?legalEntityId={seed.LegalEntityId:D}";
        var secondPath = $"/api/v1/invoicing/supplier-invoices/{secondId:D}/finalize?legalEntityId={seed.LegalEntityId:D}";
        var csrfToken = await GetCsrfTokenAsync(client);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> FinalizeAsync(string path, string key)
        {
            await start.Task;
            return await PostWithCsrfAsync(client, path, new { version = 1, idempotencyKey = key }, csrfToken);
        }
        var firstFinalize = FinalizeAsync(firstPath, "supplier-number-race-" + suffix + "-a");
        var secondFinalize = FinalizeAsync(secondPath, "supplier-number-race-" + suffix + "-b");
        start.SetResult();
        var results = await Task.WhenAll(firstFinalize, secondFinalize);

        Assert.Single(results, result => result.StatusCode == HttpStatusCode.OK);
        var conflict = Assert.Single(results, result => result.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("invoicing.supplier_duplicate", (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var verify = new NpgsqlCommand("select count(*) from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and party_id=$3 and supplier_invoice_number_key=$4 and lifecycle_state='Finalized'", connection);
        verify.Parameters.AddWithValue(seed.TenantId);
        verify.Parameters.AddWithValue(seed.LegalEntityId);
        verify.Parameters.AddWithValue(seed.PartyId);
        verify.Parameters.AddWithValue(string.Join(' ', supplierNumber.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant());
        Assert.Equal(1L, (long)(await verify.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task ConcurrentCreditNotesCannotOverCreditTheSameOriginalLine()
    {
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        Assert.StartsWith("todir_invoice_", new NpgsqlConnectionStringBuilder(runtimeConnection).Database, StringComparison.Ordinal);
        var seed = await ReadInvoiceSeedAsync(adminConnection, "Receivable");
        var suffix = Guid.CreateVersion7().ToString("N");
        var invoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await LoginAsync(client, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), seed.TenantId);

        var create = await PostAsync(client, "/api/v1/invoicing/customer-invoices", new
        {
            legalEntityId = seed.LegalEntityId,
            branchId = (Guid?)null,
            partyId = seed.PartyId,
            addressId = (Guid?)null,
            documentDate = invoiceDate,
            postingDate = invoiceDate,
            taxDate = invoiceDate,
            supplyDate = (DateOnly?)null,
            currencyCode = seed.CurrencyCode,
            paymentTermId = (Guid?)null,
            supplierInvoiceNumber = (string?)null,
            lines = new[] { new { productVariantId = seed.ProductVariantId, unitOfMeasureId = seed.UnitOfMeasureId, unitCode = seed.UnitCode,
                description = "Original invoice for concurrent credit proof " + suffix, quantity = "1", unitPrice = "125.00", discountAmount = "0",
                priceOverrideReason = "Credit-race integration proof" } },
            priceMode = "Exclusive"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var invoiceId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var preview = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{invoiceId:D}/preview?legalEntityId={seed.LegalEntityId:D}", new { });
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var originalLineId = (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("lines")[0].GetProperty("id").GetGuid();
        var finalized = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{invoiceId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
            new { version = 1, idempotencyKey = "credit-race-original-" + suffix });
        Assert.Equal(HttpStatusCode.OK, finalized.StatusCode);

        async Task<Guid> CreateCreditAsync(string idempotencyKey)
        {
            var response = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{invoiceId:D}/credit-notes?legalEntityId={seed.LegalEntityId:D}", new
            {
                documentType = "CreditNote",
                reason = "Concurrent partial return",
                documentDate = invoiceDate,
                postingDate = invoiceDate,
                taxDate = invoiceDate,
                supplyDate = (DateOnly?)null,
                idempotencyKey,
                lines = new[] { new { originalLineId, quantity = "0.6" } }
            });
            Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        var firstCreditId = await CreateCreditAsync("credit-race-a-" + suffix);
        var secondCreditId = await CreateCreditAsync("credit-race-b-" + suffix);
        var csrf = await GetCsrfTokenAsync(client);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> FinalizeCreditAsync(Guid creditId, string key)
        {
            await start.Task;
            return await PostWithCsrfAsync(client, $"/api/v1/invoicing/customer-invoices/{creditId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
                new { version = 1, idempotencyKey = key }, csrf);
        }
        var firstFinalize = FinalizeCreditAsync(firstCreditId, "credit-race-finalize-a-" + suffix);
        var secondFinalize = FinalizeCreditAsync(secondCreditId, "credit-race-finalize-b-" + suffix);
        start.SetResult();
        var results = await Task.WhenAll(firstFinalize, secondFinalize);
        Assert.Single(results, result => result.StatusCode == HttpStatusCode.OK);
        var rejected = Assert.Single(results, result => result.StatusCode == HttpStatusCode.BadRequest);
        Assert.Equal("invoicing.credit_quantity_exceeded", (await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var count = new NpgsqlCommand("select count(*),coalesce(sum(r.amount),0),(select gross_total from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and id=$3) from business_invoicing.invoice_relationship r join business_invoicing.invoice c on c.tenant_id=r.tenant_id and c.legal_entity_id=r.legal_entity_id and c.id=r.invoice_id where r.tenant_id=$1 and r.legal_entity_id=$2 and r.related_invoice_id=$3 and r.relationship_type='Credits' and c.lifecycle_state='Finalized'", connection);
        count.Parameters.AddWithValue(seed.TenantId);
        count.Parameters.AddWithValue(seed.LegalEntityId);
        count.Parameters.AddWithValue(invoiceId);
        await using var creditReader = await count.ExecuteReaderAsync();
        Assert.True(await creditReader.ReadAsync());
        Assert.Equal(1L, creditReader.GetInt64(0));
        Assert.True(creditReader.GetDecimal(1) <= creditReader.GetDecimal(2));
    }

    [Fact]
    public async Task DocumentDiscountAndTypedChargeAllocationsRemainExactThroughFinalization()
    {
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        Assert.StartsWith("todir_invoice_", new NpgsqlConnectionStringBuilder(runtimeConnection).Database, StringComparison.Ordinal);
        var seed = await ReadInvoiceSeedAsync(adminConnection, "Receivable");
        var suffix = Guid.CreateVersion7().ToString("N");
        var invoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await LoginAsync(client, "admin@atlas.com", Required("SEED_DEFAULT_PASSWORD"), seed.TenantId);
        var create = await PostAsync(client, "/api/v1/invoicing/customer-invoices", new
        {
            legalEntityId = seed.LegalEntityId,
            branchId = (Guid?)null,
            partyId = seed.PartyId,
            addressId = (Guid?)null,
            documentDate = invoiceDate,
            postingDate = invoiceDate,
            taxDate = invoiceDate,
            supplyDate = (DateOnly?)null,
            currencyCode = seed.CurrencyCode,
            paymentTermId = (Guid?)null,
            supplierInvoiceNumber = (string?)null,
            documentDiscountAmount = "1.00",
            charges = new[] { new { code = "SHIPPING", description = "Local delivery", amount = "5.00", taxTreatment = "NonTaxable" } },
            lines = Enumerable.Range(0, 3).Select(index => new
            {
                productVariantId = seed.ProductVariantId,
                unitOfMeasureId = seed.UnitOfMeasureId,
                unitCode = seed.UnitCode,
                description = $"Commercial allocation proof {suffix} line {index + 1}",
                quantity = "1",
                unitPrice = "100.00",
                discountAmount = "10.00",
                priceOverrideReason = "Commercial allocation integration proof"
            }).ToArray(),
            priceMode = "Exclusive"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var invoiceId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var preview = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{invoiceId:D}/preview?legalEntityId={seed.LegalEntityId:D}", new { });
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewBody = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(previewBody.GetProperty("readyToFinalize").GetBoolean(), await preview.Content.ReadAsStringAsync());
        var totals = previewBody.GetProperty("totals");
        Assert.Equal(31m, decimal.Parse(totals.GetProperty("discountAmount").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(5m, decimal.Parse(totals.GetProperty("chargeAmount").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(274m, decimal.Parse(totals.GetProperty("grossAmount").GetString()!, System.Globalization.CultureInfo.InvariantCulture));

        var finalized = await PostAsync(client, $"/api/v1/invoicing/customer-invoices/{invoiceId:D}/finalize?legalEntityId={seed.LegalEntityId:D}",
            new { version = 1, idempotencyKey = "commercial-allocation-" + suffix });
        Assert.Equal(HttpStatusCode.OK, finalized.StatusCode);
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var allocations = new NpgsqlCommand("select (select coalesce(sum(allocated_amount),0) from business_invoicing.invoice_discount_allocation where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3),(select coalesce(sum(allocated_amount),0) from business_invoicing.invoice_charge_allocation where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3)", connection);
        allocations.Parameters.AddWithValue(seed.TenantId);
        allocations.Parameters.AddWithValue(seed.LegalEntityId);
        allocations.Parameters.AddWithValue(invoiceId);
        await using var reader = await allocations.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(31m, reader.GetDecimal(0));
        Assert.Equal(5m, reader.GetDecimal(1));
    }

    [Fact]
    public async Task FinalizationLatencyAndFiveHundredLinePreviewUseBoundedBatchWork()
    {
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        var runtimeConnection = Required("TODIR_DATABASE_CONNECTION");
        Assert.StartsWith("todir_invoice_", new NpgsqlConnectionStringBuilder(runtimeConnection).Database, StringComparison.Ordinal);
        var password = Required("SEED_DEFAULT_PASSWORD");
        var seed = await ReadInvoiceSeedAsync(adminConnection, "Receivable");
        var invoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var suffix = Guid.CreateVersion7().ToString("N");

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await LoginAsync(client, "admin@atlas.com", password, seed.TenantId);
        var benchmarkCsrfToken = await GetCsrfTokenAsync(client);

        object DraftBody(int lineCount, string label) => new
        {
            legalEntityId = seed.LegalEntityId,
            branchId = (Guid?)null,
            partyId = seed.PartyId,
            addressId = (Guid?)null,
            documentDate = invoiceDate,
            postingDate = invoiceDate,
            taxDate = invoiceDate,
            supplyDate = (DateOnly?)null,
            currencyCode = seed.CurrencyCode,
            paymentTermId = (Guid?)null,
            supplierInvoiceNumber = (string?)null,
            lines = Enumerable.Range(0, lineCount).Select(index => new
            {
                productVariantId = seed.ProductVariantId,
                unitOfMeasureId = seed.UnitOfMeasureId,
                unitCode = seed.UnitCode,
                description = $"{label} line {index + 1}",
                quantity = "1",
                unitPrice = "125.00",
                discountAmount = "0",
                priceOverrideReason = "IV24 batch latency measurement"
            }).ToArray(),
            priceMode = "Exclusive"
        };

        async Task<Guid> CreateDraftAsync(int lineCount, string label)
        {
            var response = await PostWithCsrfAsync(client, "/api/v1/invoicing/customer-invoices", DraftBody(lineCount, label), benchmarkCsrfToken);
            Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        async Task<double> MeasureFinalizationAsync(int lineCount, string label)
        {
            var invoiceId = await CreateDraftAsync(lineCount, label);
            var path = $"/api/v1/invoicing/customer-invoices/{invoiceId:D}/finalize?legalEntityId={seed.LegalEntityId:D}";
            var timer = Stopwatch.StartNew();
            var response = await PostWithCsrfAsync(client, path, new { version = 1, idempotencyKey = "iv24-" + suffix + "-" + label }, benchmarkCsrfToken);
            timer.Stop();
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return timer.Elapsed.TotalMilliseconds;
        }

        _ = await MeasureFinalizationAsync(1, $"warm-small-{Guid.CreateVersion7():N}");
        for (var warmup = 0; warmup < 5; warmup++)
            _ = await MeasureFinalizationAsync(20, $"warm-medium-{warmup}-{Guid.CreateVersion7():N}");
        var smallSamples = new List<double>(10);
        var mediumSamples = new List<double>(30);
        for (var sample = 0; sample < 30; sample++)
        {
            if (sample < 10) smallSamples.Add(await MeasureFinalizationAsync(1, $"small-{sample}-{Guid.CreateVersion7():N}"));
            mediumSamples.Add(await MeasureFinalizationAsync(20, $"medium-{sample}-{Guid.CreateVersion7():N}"));
        }
        var small = Percentiles(smallSamples);
        var medium = Percentiles(mediumSamples);
        output.WriteLine($"IV24 finalization, 1 line, samples={smallSamples.Count}, p50={small.P50:F1}ms, p95={small.P95:F1}ms, p99={small.P99:F1}ms, errors=0");
        output.WriteLine($"IV24 finalization, 20 lines, samples={mediumSamples.Count}, min={mediumSamples.Min():F1}ms, p50={medium.P50:F1}ms, p95={medium.P95:F1}ms, p99={medium.P99:F1}ms, max={mediumSamples.Max():F1}ms, errors=0");
        Assert.True(medium.P95 <= 300d, $"20-line finalization p95 was {medium.P95:F1}ms; the local target is 300ms.");

        await using (var planConnection = new NpgsqlConnection(adminConnection))
        {
            await planConnection.OpenAsync();
            foreach (var (name, sql) in new[]
            {
                ("lifecycle", "explain (analyze, buffers, format text) select id,document_date,document_number,gross_total from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and lifecycle_state='Finalized' order by document_date desc,id desc limit 50"),
                ("party", "explain (analyze, buffers, format text) select id,document_date,document_number,gross_total from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and party_id=$3 and lifecycle_state='Finalized' order by document_date desc,id desc limit 50")
            })
            {
                await using var plan = new NpgsqlCommand(sql, planConnection);
                plan.Parameters.AddWithValue(seed.TenantId);
                plan.Parameters.AddWithValue(seed.LegalEntityId);
                if (name == "party") plan.Parameters.AddWithValue(seed.PartyId);
                await using var planReader = await plan.ExecuteReaderAsync();
                var lines = new List<string>();
                while (await planReader.ReadAsync()) lines.Add(planReader.GetString(0));
                output.WriteLine($"IV24 list plan ({name}):{Environment.NewLine}{string.Join(Environment.NewLine, lines)}");
            }
        }

        var largeInvoiceId = await CreateDraftAsync(500, "large-500-line");
        var previewTimer = Stopwatch.StartNew();
        var preview = await PostWithCsrfAsync(client, $"/api/v1/invoicing/customer-invoices/{largeInvoiceId:D}/preview?legalEntityId={seed.LegalEntityId:D}", new { }, benchmarkCsrfToken);
        previewTimer.Stop();
        Assert.True(preview.StatusCode == HttpStatusCode.OK, await preview.Content.ReadAsStringAsync());
        var previewBody = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(500, previewBody.GetProperty("lines").GetArrayLength());
        Assert.True(previewBody.GetProperty("readyToFinalize").GetBoolean(), await preview.Content.ReadAsStringAsync());
        output.WriteLine($"IV24 500-line preview={previewTimer.Elapsed.TotalMilliseconds:F1}ms; source inspection confirms Catalog, Pricing, and Tax are invoked through one batch contract per calculation.");
    }

    private static async Task<string> ReadIssuedInvoiceFingerprintAsync(string connectionString, Guid tenantId, Guid legalEntityId, Guid invoiceId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        const string sql = """
            select md5(concat_ws(chr(10),
                to_jsonb(i)::text,
                (select coalesce(jsonb_agg(to_jsonb(l) order by l.sequence), '[]'::jsonb)::text from business_invoicing.invoice_line l where l.tenant_id=i.tenant_id and l.legal_entity_id=i.legal_entity_id and l.invoice_id=i.id),
                (select coalesce(jsonb_agg(to_jsonb(s)), '[]'::jsonb)::text from business_invoicing.invoice_party_snapshot s where s.tenant_id=i.tenant_id and s.legal_entity_id=i.legal_entity_id and s.invoice_id=i.id),
                (select coalesce(jsonb_agg(to_jsonb(s) order by s.line_id), '[]'::jsonb)::text from business_invoicing.invoice_price_snapshot s where s.tenant_id=i.tenant_id and s.legal_entity_id=i.legal_entity_id and s.invoice_id=i.id),
                (select coalesce(jsonb_agg(to_jsonb(t) order by t.line_id,t.sequence), '[]'::jsonb)::text from business_invoicing.invoice_tax_component t where t.tenant_id=i.tenant_id and t.legal_entity_id=i.legal_entity_id and t.invoice_id=i.id),
                (select coalesce(jsonb_agg(to_jsonb(s) order by s.sequence), '[]'::jsonb)::text from business_invoicing.invoice_payment_schedule s where s.tenant_id=i.tenant_id and s.legal_entity_id=i.legal_entity_id and s.invoice_id=i.id),
                (select coalesce(jsonb_agg(to_jsonb(f)), '[]'::jsonb)::text from business_invoicing.invoice_finalization_record f where f.tenant_id=i.tenant_id and f.legal_entity_id=i.legal_entity_id and f.invoice_id=i.id),
                (select to_jsonb(e)::text from finance_ledger.journal_entry e where e.tenant_id=i.tenant_id and e.legal_entity_id=i.legal_entity_id and e.id=i.journal_entry_id),
                (select coalesce(jsonb_agg(to_jsonb(l) order by l.sequence_no), '[]'::jsonb)::text from finance_ledger.journal_entry_line l where l.tenant_id=i.tenant_id and l.legal_entity_id=i.legal_entity_id and l.journal_entry_id=i.journal_entry_id)
            ))
            from business_invoicing.invoice i
            where i.tenant_id=$1 and i.legal_entity_id=$2 and i.id=$3
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(legalEntityId);
        command.Parameters.AddWithValue(invoiceId);
        return (string)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("The issued invoice fingerprint query returned no row."));
    }

    private static async Task AssertFinalizedInvoiceMutationsRejectedAsync(string runtimeConnectionString, Guid tenantId, Guid legalEntityId,
        Guid invoiceId, Guid lineId, bool hasTax)
    {
        var mutations = new List<(string Name, string Sql)>
        {
            ("header totals and dates", "update business_invoicing.invoice set gross_total=gross_total+1, document_date=document_date+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3"),
            ("issued line quantity", "update business_invoicing.invoice_line set quantity=quantity+1 where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 and id=$4"),
            ("party financial snapshot", "update business_invoicing.invoice_party_snapshot set evidence=evidence || '{\"tamper\":true}'::jsonb where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3"),
            ("final commercial price", "update business_invoicing.invoice_price_snapshot set final_unit_price=final_unit_price+1 where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 and line_id=$4"),
            ("payment schedule", "update business_invoicing.invoice_payment_schedule set amount=amount+1 where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 and sequence=(select min(sequence) from business_invoicing.invoice_payment_schedule where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3)")
        };
        if (hasTax)
            mutations.Add(("Tax snapshot", "update business_invoicing.invoice_tax_component set tax_base=tax_base+1 where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 and sequence=(select min(tax.sequence) from business_invoicing.invoice_tax_component tax where tax.tenant_id=$1 and tax.legal_entity_id=$2 and tax.invoice_id=$3)"));

        foreach (var (name, sql) in mutations)
        {
            await using var connection = new NpgsqlConnection(runtimeConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using (var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
            {
                context.Parameters.AddWithValue(tenantId.ToString("D"));
                await context.ExecuteNonQueryAsync();
            }
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue(legalEntityId);
            command.Parameters.AddWithValue(invoiceId);
            if (sql.Contains("invoice_line", StringComparison.Ordinal) || sql.Contains("invoice_price_snapshot", StringComparison.Ordinal))
                command.Parameters.AddWithValue(lineId);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.True(exception.SqlState == "23514", $"Expected the finalized-document guard to reject {name}; got SQLSTATE {exception.SqlState}.");
            await transaction.RollbackAsync();
        }
    }

    private static (double P50, double P95, double P99) Percentiles(IReadOnlyCollection<double> samples)
    {
        var ordered = samples.Order().ToArray();
        double At(double percentile) => ordered[Math.Clamp((int)Math.Ceiling(percentile * ordered.Length) - 1, 0, ordered.Length - 1)];
        return (At(0.50), At(0.95), At(0.99));
    }

    private static async Task<SeedInvoiceContext> ReadInvoiceSeedAsync(string adminConnection, string direction, bool requireTaxClassifiedProduct = false)
    {
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        var taxProfileFilter = requireTaxClassifiedProduct
            ? "and exists(select 1 from foundation_tax.product_tax_profile p join foundation_tax.tax_category c on c.tenant_id=p.tenant_id and c.id=p.category_id where p.tenant_id=l.tenant_id and p.product_variant_id=l.product_variant_id and c.code='GENERAL' and p.effective_from<=current_date and (p.effective_to is null or p.effective_to>current_date))"
            : string.Empty;
        await using var command = new NpgsqlCommand($"select i.tenant_id,i.legal_entity_id,i.party_id,i.currency_code,l.product_variant_id,l.unit_of_measure_id,l.unit_code_snapshot,actor.id,m.id,i.id from business_invoicing.invoice i join business_invoicing.invoice_line l on l.tenant_id=i.tenant_id and l.legal_entity_id=i.legal_entity_id and l.invoice_id=i.id join platform.tenant t on t.id=i.tenant_id join platform.identity actor on actor.tenant_id=i.tenant_id and actor.email='admin@atlas.com' join platform.membership m on m.tenant_id=actor.tenant_id and m.identity_id=actor.id where t.normalized_login_domain='atlas.com' and i.lifecycle_state='Draft' and i.direction=$1 {taxProfileFilter} order by i.document_date,i.id limit 1", connection);
        command.Parameters.AddWithValue(direction);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), $"The Development Seed must contain an {direction} invoice scenario.");
        return new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3).Trim(), reader.GetGuid(4), reader.GetGuid(5), reader.GetString(6), reader.GetGuid(7), reader.GetGuid(8), reader.GetGuid(9));
    }

    private static async Task<Guid> ReadSecondaryEntityIdAsync(string adminConnection, Guid tenantId)
    {
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select id from platform.legal_entity where tenant_id=$1 and code='SECONDARY' and status='Active'", connection);
        command.Parameters.AddWithValue(tenantId);
        return (Guid)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("The Development Seed must contain an active secondary legal entity."));
    }

    private static async Task<Guid> ReadInstallmentPaymentTermIdAsync(string adminConnection, Guid tenantId)
    {
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select id from foundation_finbase.payment_term where tenant_id=$1 and code='INSTALL3' and status='Active'", connection);
        command.Parameters.AddWithValue(tenantId);
        return (Guid)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("The Development Seed must contain the three-installment payment term."));
    }

    private sealed class TestOnlyCountryPackRegistry(bool requireSupplyDate = false) : ITaxCountryPackRegistry
    {
        private readonly TestOnlyCountryPack pack = new(requireSupplyDate);

        public ITaxCountryPack? Find(string countryCode, LocalDate at) => countryCode == "EG" && at >= pack.Provenance.EffectiveFrom ? pack : null;
        public IReadOnlyCollection<ITaxCountryPack> GetReviewedPacks() => [];
    }

    private sealed class TestOnlyCountryPack(bool requireSupplyDate) : ITaxCountryPack
    {
        private static readonly LocalDate EffectiveFrom = new(2020, 1, 1);
        public TaxPackProvenance Provenance { get; } = new("Integration fixture", "test-only:invoicing-tax-enabled", "fixture-1", EffectiveFrom, null, "TestOnly", null, null);
        public string CountryCode => "EG";
        public string PackCode => "TEST_ONLY";
        public string Version => "fixture-1";
        public TaxCountryPackManifest Manifest { get; } = new("TEST_ONLY", "EG", "fixture-1", TaxCalculator.CalculationVersion,
            TaxCountryPackStatus.Installed, EffectiveFrom, null, "TestOnly", new(true, false, false, false, false), ["test-only"],
            "Integration fixture", "Non-statutory jurisdiction fixture", "test-only:invoicing-tax-enabled", null, null, null, true);
        public TaxCountryPackPackage ValidationPackage { get; } = new(
            new TaxCountryPackManifest("TEST_ONLY", "EG", "fixture-1", TaxCalculator.CalculationVersion,
                TaxCountryPackStatus.Installed, EffectiveFrom, null, "TestOnly", new(true, false, false, false, false), ["test-only"],
                "Integration fixture", "Non-statutory jurisdiction fixture", "test-only:invoicing-tax-enabled", null, null, null, true), [], []);
        public TaxDateProviderResult ResolveTaxDate(TaxCalculationContext context) => new(context.TaxDate ?? context.SupplyDate ?? context.DocumentDate,
            "Test-only effective date", Version, Provenance);
        public TaxJurisdictionResult ResolveJurisdiction(TaxCalculationContext context, LocalDate taxDate) =>
            new(CountryCode, "TEST-REGION", "Test", "Non-statutory integration fixture", Provenance);
        public TaxRegistrationValidation ValidateRegistration(string schemeCode, string? registrationNumber, LocalDate taxDate) =>
            new(false, "test_only_registration_unavailable", null, Provenance);
        public IReadOnlyList<TaxDocumentRequirement> GetDocumentRequirements(string documentKind, TaxTreatment treatment, LocalDate taxDate) => [];
        public IReadOnlyList<Shared.Contracts.Tax.InvoiceDocumentRequirement> GetInvoiceDocumentRequirements(string documentType, LocalDate taxDate) =>
            requireSupplyDate && documentType == "Invoice" ? [new("Header", "supply_date")] : [];
        public IReadOnlyList<TaxRuleCandidate> GetApprovedRules(LocalDate taxDate) => [];
    }

    private static async Task InstallFinalizationFailpointAsync(string adminConnection, string failurePoint, string functionName, string triggerName)
    {
        var (schema, table) = FinalizationFailpointTarget(failurePoint);
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"create function {schema}.{functionName}() returns trigger language plpgsql as $$ begin raise exception 'invoicing.integration_failpoint'; end $$; create trigger {triggerName} before insert on {schema}.{table} for each row execute function {schema}.{functionName}()", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RemoveFinalizationFailpointAsync(string adminConnection, string failurePoint, string functionName, string triggerName)
    {
        var (schema, table) = FinalizationFailpointTarget(failurePoint);
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"drop trigger if exists {triggerName} on {schema}.{table}; drop function if exists {schema}.{functionName}()", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static (string Schema, string Table) FinalizationFailpointTarget(string failurePoint) => failurePoint switch
    {
        "InvoiceFinalization" => ("business_invoicing", "invoice_finalization_record"),
        "LedgerJournal" => ("finance_ledger", "journal_entry"),
        "SubledgerOpenItem" => ("finance_subledger", "party_open_item"),
        "OutboxMessage" => ("platform", "outbox_message"),
        _ => throw new ArgumentOutOfRangeException(nameof(failurePoint), failurePoint, "Unknown finalization failpoint.")
    };

    private static async Task AssertFinalizationRolledBackAsync(string adminConnection, SeedInvoiceContext seed, Guid invoiceId)
    {
        const string sql = "select i.lifecycle_state, (select count(*) from business_invoicing.invoice_finalization_record f where f.tenant_id=i.tenant_id and f.invoice_id=i.id), (select count(*) from finance_ledger.journal_entry j where j.tenant_id=i.tenant_id and j.legal_entity_id=i.legal_entity_id and j.source_module='invoicing' and j.source_reference=i.id::text), (select count(*) from finance_subledger.party_open_item o where o.tenant_id=i.tenant_id and o.legal_entity_id=i.legal_entity_id and o.source_reference=i.id::text), (select count(*) from platform.outbox_message m where m.tenant_id=i.tenant_id and m.causation_id=i.id and m.message_type in ('InvoiceFinalizedV1','InvoiceComplianceRequestedV1')) from business_invoicing.invoice i where i.tenant_id=$1 and i.legal_entity_id=$2 and i.id=$3";
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(seed.TenantId);
        command.Parameters.AddWithValue(seed.LegalEntityId);
        command.Parameters.AddWithValue(invoiceId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Draft", reader.GetString(0));
        Assert.Equal(0L, reader.GetInt64(1));
        Assert.Equal(0L, reader.GetInt64(2));
        Assert.Equal(0L, reader.GetInt64(3));
        Assert.Equal(0L, reader.GetInt64(4));
    }

    private static async Task AssertFinalizationCommittedOnceAsync(string adminConnection, SeedInvoiceContext seed, Guid invoiceId, int scheduleCount)
    {
        const string sql = "select i.lifecycle_state, (select count(*) from business_invoicing.invoice_finalization_record f where f.tenant_id=i.tenant_id and f.invoice_id=i.id), (select count(*) from finance_ledger.journal_entry j where j.tenant_id=i.tenant_id and j.legal_entity_id=i.legal_entity_id and j.source_module='invoicing' and j.source_reference=i.id::text), (select count(*) from finance_subledger.party_open_item o where o.tenant_id=i.tenant_id and o.legal_entity_id=i.legal_entity_id and o.source_reference=i.id::text), (select count(*) from platform.outbox_message m where m.tenant_id=i.tenant_id and m.causation_id=i.id and m.message_type in ('InvoiceFinalizedV1','InvoiceComplianceRequestedV1')) from business_invoicing.invoice i where i.tenant_id=$1 and i.legal_entity_id=$2 and i.id=$3";
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(seed.TenantId);
        command.Parameters.AddWithValue(seed.LegalEntityId);
        command.Parameters.AddWithValue(invoiceId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Finalized", reader.GetString(0));
        Assert.Equal(1L, reader.GetInt64(1));
        Assert.Equal(1L, reader.GetInt64(2));
        Assert.Equal((long)scheduleCount, reader.GetInt64(3));
        Assert.Equal(2L, reader.GetInt64(4));
    }

    private static async Task LoginAsync(HttpClient client, string email, string password, Guid tenantId)
    {
        var response = await PostAsync(client, "/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Authenticated", result.GetProperty("state").GetString());
        Assert.Equal(tenantId, result.GetProperty("tenantId").GetGuid());
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body)
    {
        var csrfToken = await GetCsrfTokenAsync(client);
        return await PostWithCsrfAsync(client, path, body, csrfToken);
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<CsrfResponse>();
        return csrf!.Token;
    }

    private static async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string path, object body, string csrfToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return await client.SendAsync(request);
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Set {name} for the Invoicing PostgreSQL API integration test.");

    private sealed record SeedInvoiceContext(Guid TenantId, Guid LegalEntityId, Guid PartyId, string CurrencyCode,
        Guid ProductVariantId, Guid UnitOfMeasureId, string UnitCode, Guid ActorId, Guid MembershipId, Guid InvoiceId);
    private sealed record CsrfResponse(string Token);
}
