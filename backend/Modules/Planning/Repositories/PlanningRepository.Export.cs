using System.IO.Compression;
using System.Security;
using System.Text;
using Shared.Primitives.Identifiers;

namespace Modules.Planning;

public sealed partial class PlanningRepository
{
    private static readonly string[] ExportHeader = ["legalEntityId", "periodStart", "metricCode", "amount", "currencyCode", "explicitZero", "sourceLabel"];

    public Task<XlsxFileView> ExportXlsxAsync(TenantId tenant, Guid planVersionId, Guid? legalEntityId, DateOnly? from, DateOnly? to, bool template, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (c, token) =>
    {
        var rows = new List<string[]> { ExportHeader };
        if (!template)
        {
            await using var command = Cmd(c, "select c.legal_entity_id,c.period_start,c.metric_code,coalesce(i.amount,cf.amount),coalesce(i.currency_code,cf.currency_code,'EGP'),coalesce(i.explicit_zero,false),coalesce(i.source_label,'') from business_planning.planning_coordinate c left join business_planning.planning_input_fact i on i.tenant_id=c.tenant_id and i.coordinate_id=c.id left join business_planning.planning_calculated_fact cf on cf.tenant_id=c.tenant_id and cf.coordinate_id=c.id where c.tenant_id=$1 and c.plan_version_id=$2 and ($3::uuid is null or c.legal_entity_id=$3) and ($4::date is null or c.period_start >= $4) and ($5::date is null or c.period_start <= $5) order by c.period_start,c.metric_code,c.id limit 5000", tenant.Value, planVersionId, Db(legalEntityId), Db(from), Db(to));
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
                rows.Add(new[] { reader.GetGuid(0).ToString("D"), reader.GetFieldValue<DateOnly>(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), reader.GetString(2), reader.IsDBNull(3) ? string.Empty : reader.GetDecimal(3).ToString(System.Globalization.CultureInfo.InvariantCulture), reader.GetString(4).Trim().ToUpperInvariant(), reader.GetBoolean(5).ToString().ToLowerInvariant(), SafeText(reader.GetString(6)) });
        }

        var content = BuildWorkbook(rows, template ? "planning-grid-template-v1" : "planning-grid-export-v1");
        return new XlsxFileView(template ? "planning-grid-template.xlsx" : "planning-grid-export.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Convert.ToBase64String(content), rows.Count - 1, template);
    }, ct);

    private static byte[] BuildWorkbook(List<string[]> rows, string templateId)
    {
        static string Xml(string value) => SecurityElement.Escape(value) ?? string.Empty;
        static string Cell(string reference, string value) => $"<c r=\"{reference}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Xml(value)}</t></is></c>";
        var sheetRows = new StringBuilder();
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            sheetRows.Append("<row r=\"").Append(rowIndex + 1).Append("\">");
            for (var column = 0; column < rows[rowIndex].Length; column++) sheetRows.Append(Cell(ColumnName(column) + (rowIndex + 1), rows[rowIndex][column]));
            sheetRows.Append("</row>");
        }
        var files = new Dictionary<string, string>
        {
            ["[Content_Types].xml"] = "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/docProps/custom.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.custom-properties+xml\"/></Types>",
            ["_rels/.rels"] = "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/custom-properties\" Target=\"docProps/custom.xml\"/></Relationships>",
            ["xl/workbook.xml"] = "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Planning Grid\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>",
            ["xl/_rels/workbook.xml.rels"] = "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>",
            ["xl/worksheets/sheet1.xml"] = $"<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>{sheetRows}</sheetData></worksheet>",
            ["docProps/custom.xml"] = $"<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/custom-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\"><property name=\"PlanningTemplateId\" fmtid=\"{{D5CDD505-2E9C-101B-9397-08002B2CF9AE}}\" pid=\"2\"><vt:lpwstr>{Xml(templateId)}</vt:lpwstr></property><property name=\"FormulaPolicy\" fmtid=\"{{D5CDD505-2E9C-101B-9397-08002B2CF9AE}}\" pid=\"3\"><vt:lpwstr>formulas-not-allowed</vt:lpwstr></property></Properties>"
        };
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var (name, content) in files)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        return stream.ToArray();
    }

    private static string ColumnName(int index)
    {
        var value = index + 1; var result = new StringBuilder();
        while (value > 0) { value--; result.Insert(0, (char)('A' + value % 26)); value /= 26; }
        return result.ToString();
    }

    private static string SafeText(string value) => value.Length > 0 && value[0] is '=' or '+' or '-' or '@' ? "'" + value : value;
}
