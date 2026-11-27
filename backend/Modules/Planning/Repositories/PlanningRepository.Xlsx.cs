using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Shared.Primitives.Identifiers;

namespace Modules.Planning;

public sealed partial class PlanningRepository
{
    public Task<CsvImportView> ImportXlsxAsync(TenantId tenant, Guid actor, XlsxImportRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SpreadsheetContentBase64) || request.SpreadsheetContentBase64.Length > 7 * 1024 * 1024)
            throw new PlanningException("planning.import_file_invalid");
        return ImportCsvAsync(tenant, actor, request, ct);
    }

    private Task<CsvImportView> ImportCsvAsync(TenantId tenant, Guid actor, XlsxImportRequest request, CancellationToken ct)
    {
        var csv = ParseWorkbook(request.SpreadsheetContentBase64);
        return ImportCsvAsync(tenant, actor, new CsvImportRequest(request.PlanVersionId, csv, request.Commit), ct);
    }

    private static string ParseWorkbook(string contentBase64)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(contentBase64); }
        catch (FormatException) { throw new PlanningException("planning.import_excel_invalid"); }
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            if (archive.Entries.Count > 5000 || archive.Entries.Sum(x => x.Length) > 25 * 1024 * 1024 || archive.Entries.Any(x => x.FullName.Contains("externalLinks", StringComparison.OrdinalIgnoreCase) || x.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase)))
                throw new PlanningException("planning.import_excel_unsafe");
            XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace packageRel = "http://schemas.openxmlformats.org/package/2006/relationships";
            XDocument Read(string name)
            {
                var entry = archive.GetEntry(name) ?? throw new PlanningException("planning.import_excel_invalid");
                using var entryStream = entry.Open(); return XDocument.Load(entryStream, LoadOptions.None);
            }
            var workbook = Read("xl/workbook.xml");
            var sheet = workbook.Root?.Element(main + "sheets")?.Elements(main + "sheet").FirstOrDefault() ?? throw new PlanningException("planning.import_excel_invalid");
            var relationshipId = (string?)sheet.Attribute(rel + "id") ?? throw new PlanningException("planning.import_excel_invalid");
            var relationship = Read("xl/_rels/workbook.xml.rels").Root?.Elements(packageRel + "Relationship").FirstOrDefault(x => (string?)x.Attribute("Id") == relationshipId);
            var target = (string?)relationship?.Attribute("Target") ?? throw new PlanningException("planning.import_excel_invalid");
            target = target.Replace('\\', '/').TrimStart('/');
            if (target.Split('/').Contains("..", StringComparer.Ordinal)) throw new PlanningException("planning.import_excel_unsafe");
            var sheetDocument = Read(target.StartsWith("xl/", StringComparison.Ordinal) ? target : $"xl/{target}");
            var shared = archive.GetEntry("xl/sharedStrings.xml");
            var sharedStrings = new List<string>();
            if (shared is not null)
            {
                using var sharedStream = shared.Open();
                var sharedDocument = XDocument.Load(sharedStream, LoadOptions.None);
                sharedStrings.AddRange(sharedDocument.Root?.Elements(main + "si").Select(x => string.Concat(x.Descendants(main + "t").Select(t => t.Value))) ?? []);
            }
            var rows = new List<string>();
            foreach (var sourceRow in sheetDocument.Root?.Element(main + "sheetData")?.Elements(main + "row") ?? [])
            {
                if (rows.Count >= 5001) throw new PlanningException("planning.import_row_limit");
                var cells = new SortedDictionary<int, string>();
                foreach (var cell in sourceRow.Elements(main + "c"))
                {
                    if (cell.Element(main + "f") is not null) throw new PlanningException("planning.import_excel_formula_not_allowed");
                    var reference = (string?)cell.Attribute("r") ?? throw new PlanningException("planning.import_excel_invalid");
                    var column = ColumnIndex(reference);
                    if (column >= 16) throw new PlanningException("planning.import_excel_invalid");
                    var type = (string?)cell.Attribute("t");
                    var value = type switch
                    {
                        "inlineStr" => string.Concat(cell.Element(main + "is")?.Descendants(main + "t").Select(x => x.Value) ?? []),
                        "s" => ReadShared(cell.Element(main + "v")?.Value, sharedStrings),
                        _ => cell.Element(main + "v")?.Value ?? string.Empty
                    };
                    cells[column] = value;
                }
                if (cells.Count == 0) continue;
                var values = new string[cells.Keys.Max() + 1];
                foreach (var pair in cells) values[pair.Key] = pair.Value;
                rows.Add(string.Join(',', values.Select(EscapeCsv)));
            }
            if (rows.Count == 0) throw new PlanningException("planning.import_rows_missing");
            return string.Join(Environment.NewLine, rows);
        }
        catch (PlanningException) { throw; }
        catch (Exception exception) when (exception is InvalidDataException or IOException or XmlException or ArgumentException or OverflowException)
        { throw new PlanningException("planning.import_excel_invalid"); }
    }

    private static string EscapeCsv(string value) => value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal) || value.Contains('\n') ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;
    private static int ColumnIndex(string reference)
    {
        var result = 0; var letters = 0;
        foreach (var character in reference)
        {
            if (!char.IsAsciiLetter(character)) break;
            result = checked(result * 26 + char.ToUpperInvariant(character) - 'A' + 1); letters++;
        }
        if (letters == 0) throw new PlanningException("planning.import_excel_invalid");
        return result - 1;
    }
    private static string ReadShared(string? indexText, List<string> values)
    {
        if (!int.TryParse(indexText, out var index) || index < 0 || index >= values.Count) throw new PlanningException("planning.import_excel_invalid");
        return values[index];
    }
}
