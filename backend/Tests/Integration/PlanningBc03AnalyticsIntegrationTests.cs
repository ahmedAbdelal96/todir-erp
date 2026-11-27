using Core.Transactions;
using Modules.Planning;
using Npgsql;
using System.Text.Json;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class PlanningBc03AnalyticsIntegrationTests
{
    private static readonly Guid TenantId = Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b");

    [Fact(DisplayName = "BC3 integrated statements reconcile and CSV import remains atomic")]
    public async Task IntegratedStatementsReconcileAndCsvImportRemainsAtomic()
    {
        var runtime = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        await using var dataSource = NpgsqlDataSource.Create(runtime);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        var sourceVersion = (Guid)(await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by created_at desc,id desc limit 1", TenantId))!;
        var entity = (Guid)(await ScalarAsync(admin, "select legal_entity_id from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2 order by id limit 1", TenantId, sourceVersion))!;
        var actor = (Guid)(await ScalarAsync(admin, "select id from platform.identity where tenant_id=$1 and email='admin@atlas.com'", TenantId))!;
        var repository = new PlanningRepository(new NpgsqlTransactionCoordinator(dataSource));
        var draft = await repository.ClonePlanVersionAsync(new(TenantId), actor, sourceVersion, $"BC03 analytics {Guid.NewGuid():N}", default);

        var period = new DateOnly(2027, 1, 1);
        var json = JsonDocument.Parse("{}").RootElement;
        await repository.WriteGridAsync(new(TenantId), actor, new(draft.Id,
        [
            new(entity, period, "Revenue", json, 1000m, "EGP", false, "golden-vector"),
            new(entity, period, "Cogs", json, 400m, "EGP", false, "golden-vector"),
            new(entity, period, "Opex", json, 100m, "EGP", false, "golden-vector"),
            new(entity, period, "OpeningCash", json, 1000m, "EGP", false, "golden-vector"),
            new(entity, period, "OpeningEquity", json, 1000m, "EGP", false, "golden-vector")
        ], null), default);
        await repository.AddWorkingCapitalAsync(new(TenantId), new(draft.Id, entity, period, 30m, 30m, 0m), default);

        var pnl = await repository.QueryIntegratedStatementAsync(new(TenantId), new(draft.Id, entity, period, period, "P&L", "EGP"), default);
        Assert.Equal(500m, Assert.Single(pnl, x => x.LineCode == "NetIncome").Amount);
        var balanceSheet = await repository.QueryIntegratedStatementAsync(new(TenantId), new(draft.Id, entity, period, period, "BalanceSheet", "EGP"), default);
        Assert.All(balanceSheet, line => Assert.True(line.Balanced, $"{line.LineCode} was not balanced: {line.Amount}"));
        var cashFlow = await repository.QueryIntegratedStatementAsync(new(TenantId), new(draft.Id, entity, period, period, "CashFlow", "EGP"), default);
        Assert.Equal(1000m, Assert.Single(cashFlow, x => x.LineCode == "ClosingCash").Amount);

        var csv = $"legalEntityId,periodStart,metricCode,amount,currencyCode,explicitZero,sourceLabel\n{entity:D},2027-02-01,Opex,250.00,EGP,false,csv-test";
        var preview = await repository.ImportCsvAsync(new(TenantId), actor, new(draft.Id, csv, false), default);
        Assert.False(preview.Committed);
        Assert.Equal(1, preview.AcceptedRows);
        var committed = await repository.ImportCsvAsync(new(TenantId), actor, new(draft.Id, csv, true), default);
        Assert.True(committed.Committed);
        Assert.Equal(1, committed.AcceptedRows);

        var opexPeriod = new DateOnly(2027, 3, 1);
        await repository.WriteGridAsync(new(TenantId), actor, new(draft.Id,
        [new(entity, opexPeriod, "Revenue", json, 2000m, "EGP", false, "opex-vector"), new(entity, opexPeriod, "Cogs", json, 500m, "EGP", false, "opex-vector")], null), default);
        var fixedLine = await repository.AddOpexLineAsync(new(TenantId), actor, new(draft.Id, entity, null, "Facilities", "6100", "HQ", opexPeriod, "Fixed", 1000m, 0m, 0m, 10m, 0m, "EGP", "golden-vector"), default);
        var driverLine = await repository.AddOpexLineAsync(new(TenantId), actor, new(draft.Id, entity, null, "Utilities", "6200", "HQ", opexPeriod, "DriverBased", 0m, 10m, 20m, 0m, 5m, "EGP", "golden-vector"), default);
        Assert.Equal(1100m, fixedLine.PlannedAmount);
        Assert.Equal(210m, driverLine.PlannedAmount);
        var opexPnl = await repository.QueryIntegratedStatementAsync(new(TenantId), new(draft.Id, entity, opexPeriod, opexPeriod, "P&L", "EGP"), default);
        Assert.Equal(1310m, Assert.Single(opexPnl, x => x.LineCode == "Opex").Amount);
        Assert.Equal(190m, Assert.Single(opexPnl, x => x.LineCode == "NetIncome").Amount);

        var xlsx = CreateXlsx(entity);
        var xlsxPreview = await repository.ImportXlsxAsync(new(TenantId), actor, new(draft.Id, xlsx), default);
        Assert.False(xlsxPreview.Committed);
        Assert.Equal(1, xlsxPreview.AcceptedRows);
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : throw new InvalidOperationException($"{name} is required.");

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        for (var index = 0; index < values.Length; index++) command.Parameters.AddWithValue($"p{index}", values[index]);
        command.CommandText = sql.Replace("$1", "@p0", StringComparison.Ordinal).Replace("$2", "@p1", StringComparison.Ordinal).Replace("$3", "@p2", StringComparison.Ordinal);
        return await command.ExecuteScalarAsync();
    }

    private static string CreateXlsx(Guid entity)
    {
        var values = new[] { "legalEntityId", "periodStart", "metricCode", "amount", "currencyCode", "explicitZero", "sourceLabel", entity.ToString("D"), "2027-04-01", "Opex", "333.25", "EGP", "false", "xlsx-test" };
        static string Cell(string reference, string value) => $"<c r=\"{reference}\" t=\"inlineStr\"><is><t>{System.Security.SecurityElement.Escape(value)}</t></is></c>";
        var row1 = string.Join(string.Empty, values.Take(7).Select((value, index) => Cell($"{(char)('A' + index)}1", value)));
        var row2 = string.Join(string.Empty, values.Skip(7).Select((value, index) => index == 3 ? $"<c r=\"D2\"><v>{value}</v></c>" : Cell($"{(char)('A' + index)}2", value)));
        var files = new Dictionary<string, string>
        {
            ["[Content_Types].xml"] = "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>",
            ["xl/workbook.xml"] = "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>",
            ["xl/_rels/workbook.xml.rels"] = "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>",
            ["xl/worksheets/sheet1.xml"] = $"<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\">{row1}</row><row r=\"2\">{row2}</row></sheetData></worksheet>"
        };
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var (name, content) in files)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open(), Encoding.UTF8);
                writer.Write(content);
            }
        return Convert.ToBase64String(stream.ToArray());
    }
}
