using Modules.Catalog.DTOs;
using Shared.Primitives.Identifiers;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Modules.Catalog.Services;

public sealed class CatalogImportService(Repositories.CatalogImportRepository repository)
{
    private static readonly HashSet<string> CsvFields = new(["templateKey", "sku", "name", "type", "nameAr", "nameEn", "category", "brand", "baseUom", "barcode", "externalReference", "uomCode", "factorToBase", "effectiveFrom", "effectiveTo", "uomBarcode", "usageFlags", "roundingPrecision", "additionalUomsJson", "attributesJson"], StringComparer.Ordinal);
    private static readonly System.Text.Json.JsonSerializerOptions ImportJsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

    public Task<CatalogImportSummary> PreviewAsync(TenantId tenantId, Guid actorId, Guid membershipId, CatalogImportPreviewRequest request, CancellationToken cancellationToken)
    {
        if (request.CsvContent is null && request.SpreadsheetContentBase64 is null)
            return request.Rows is { Count: > 0 } ? repository.PreviewAsync(tenantId, actorId, membershipId, request, cancellationToken) : throw new CatalogException("catalog.import_rows_missing", "Provide product rows or a CSV file to preview.");
        if (request.CsvContent?.Length > 5 * 1024 * 1024 || request.SpreadsheetContentBase64?.Length > 7 * 1024 * 1024) throw new CatalogException("catalog.import_file_too_large", "Import files cannot exceed 5 MB.");
        if (request.CsvContent is not null && request.SpreadsheetContentBase64 is not null) throw new CatalogException("catalog.import_file_invalid", "Provide either CSV or Excel file content.");
        var columnMapping = request.ColumnMapping ?? new Dictionary<string, string>();
        if (columnMapping.Count == 0 || columnMapping.Keys.Any(field => !CsvFields.Contains(field) && !IsAttributeField(field)) || !columnMapping.ContainsKey("name") || !columnMapping.ContainsKey("baseUom"))
            throw new CatalogException("catalog.import_mapping_invalid", "Map product name and base unit to valid CSV columns before previewing.");
        var table = request.CsvContent is not null ? ParseCsv(request.CsvContent) : ParseXlsx(request.SpreadsheetContentBase64!);
        if (table.Count < 2) throw new CatalogException("catalog.import_rows_missing", "The CSV file must contain a header and at least one product row.");
        if (table[0].Count is 0 or > 50) throw new CatalogException("catalog.import_csv_invalid", "The CSV header must contain between 1 and 50 columns.");
        var normalizedHeaders = table[0].Select(value => value.Trim().TrimStart('\uFEFF')).ToArray();
        if (normalizedHeaders.Any(string.IsNullOrWhiteSpace) || normalizedHeaders.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedHeaders.Length)
            throw new CatalogException("catalog.import_csv_invalid", "CSV column names must be present and unique.");
        var headers = normalizedHeaders.Select((value, index) => (Name: value, Index: index)).ToDictionary(item => item.Name, item => item.Index, StringComparer.OrdinalIgnoreCase);
        var mapping = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (field, header) in columnMapping)
        {
            if (string.IsNullOrWhiteSpace(header) || !headers.TryGetValue(header.Trim(), out var index)) throw new CatalogException("catalog.import_mapping_invalid", $"Mapped column for {field} does not exist in the CSV header.");
            mapping[field] = index;
        }
        string? Read(List<string> values, string field) => mapping.TryGetValue(field, out var index) && index < values.Count && !string.IsNullOrWhiteSpace(values[index]) ? values[index].Trim() : null;
        var rows = table.Skip(1).Select(values => values.ToList()).Where(values => values.Any(value => !string.IsNullOrWhiteSpace(value))).Select(values =>
        {
            var nature = Enum.TryParse<Entities.ProductNature>(Read(values, "type"), true, out var parsed) ? parsed : (Entities.ProductNature)(-1);
            var attributes = columnMapping.Keys.Where(IsAttributeField).Select(field => (Code: field[10..], Value: Read(values, field))).Where(item => item.Value is not null).ToDictionary(item => item.Code, item => item.Value!, StringComparer.OrdinalIgnoreCase);
            if (Read(values, "attributesJson") is { } attributesJson)
            {
                try
                {
                    var imported = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(attributesJson, ImportJsonOptions);
                    if (imported is null || imported.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))) throw new System.Text.Json.JsonException();
                    foreach (var (code, value) in imported)
                        if (!attributes.TryAdd(code, value)) throw new System.Text.Json.JsonException();
                }
                catch (System.Text.Json.JsonException) { throw new CatalogException("catalog.import_attributes_invalid", "Product attributes must be a JSON object with one value per attribute."); }
            }
            IReadOnlyList<CatalogImportUomRequest>? additionalUoms = null;
            if (Read(values, "additionalUomsJson") is { } additionalUomsJson)
            {
                try
                {
                    additionalUoms = System.Text.Json.JsonSerializer.Deserialize<IReadOnlyList<CatalogImportUomRequest>>(additionalUomsJson, ImportJsonOptions);
                    if (additionalUoms is null) throw new System.Text.Json.JsonException();
                }
                catch (System.Text.Json.JsonException) { throw new CatalogException("catalog.import_uoms_invalid", "Additional units must be a JSON array of unit conversions."); }
            }
            if (Read(values, "uomCode") is { } uomCode)
            {
                if (additionalUoms is not null) throw new CatalogException("catalog.import_mapping_invalid", "Map either additional UOM columns or the additional UOMs JSON column, not both.");
                var factor = decimal.TryParse(Read(values, "factorToBase"), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsedFactor) ? parsedFactor : 0m;
                var from = DateTimeOffset.TryParse(Read(values, "effectiveFrom"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsedFrom) ? parsedFrom : DateTimeOffset.MinValue;
                var to = DateTimeOffset.TryParse(Read(values, "effectiveTo"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsedTo) ? parsedTo : (DateTimeOffset?)null;
                var usageFlags = (Read(values, "usageFlags") ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var roundingPrecision = int.TryParse(Read(values, "roundingPrecision"), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsedPrecision) ? parsedPrecision : (int?)null;
                additionalUoms = [new CatalogImportUomRequest(uomCode, factor, from, to, Read(values, "uomBarcode"), usageFlags, roundingPrecision)];
            }
            return new CatalogImportRowRequest(Read(values, "templateKey"), Read(values, "name") ?? string.Empty, nature, Read(values, "sku"), Read(values, "nameAr"), Read(values, "nameEn"), Read(values, "category"), Read(values, "brand"), Read(values, "baseUom") ?? string.Empty, Read(values, "barcode"), Read(values, "externalReference"), additionalUoms, attributes);
        }).ToArray();
        return repository.PreviewAsync(tenantId, actorId, membershipId, new CatalogImportPreviewRequest(request.FileName, rows), cancellationToken);
    }
    public Task<CatalogImportSummary> CommitAsync(TenantId tenantId, Guid batchId, CancellationToken cancellationToken) => repository.CommitAsync(tenantId, batchId, cancellationToken);
    public Task<CatalogImportSummary> RequestCommitAsync(TenantId tenantId, Guid batchId, CancellationToken cancellationToken) => repository.RequestCommitAsync(tenantId, batchId, cancellationToken);
    public Task<CatalogImportSummary> GetSummaryAsync(TenantId tenantId, Guid batchId, CancellationToken cancellationToken) => repository.GetSummaryAsync(tenantId, batchId, cancellationToken);
    public Task WriteExportCsvAsync(TenantId tenantId, Guid? legalEntityId, Stream output, CancellationToken cancellationToken) => repository.WriteExportCsvAsync(tenantId, legalEntityId, output, cancellationToken);

    private static List<IReadOnlyList<string>> ParseCsv(string input)
    {
        var rows = new List<IReadOnlyList<string>>(); var row = new List<string>(); var field = new System.Text.StringBuilder(); var quoted = false;
        for (var index = 0; index < input.Length; index++)
        {
            var character = input[index];
            if (quoted)
            {
                if (character == '"' && index + 1 < input.Length && input[index + 1] == '"') { field.Append('"'); index++; }
                else if (character == '"') quoted = false;
                else field.Append(character);
            }
            else if (character == '"' && field.Length == 0) quoted = true;
            else if (character == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (character is '\r' or '\n')
            {
                if (character == '\r' && index + 1 < input.Length && input[index + 1] == '\n') index++;
                row.Add(field.ToString()); field.Clear(); rows.Add(row); row = [];
            }
            else field.Append(character);
        }
        if (quoted) throw new CatalogException("catalog.import_csv_invalid", "The CSV file contains an unclosed quoted value.");
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }

    private static List<IReadOnlyList<string>> ParseXlsx(string contentBase64)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(contentBase64); }
        catch (FormatException) { throw new CatalogException("catalog.import_excel_invalid", "The Excel file content is invalid."); }
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            if (archive.Entries.Count > 5000 || archive.Entries.Sum(entry => entry.Length) > 25 * 1024 * 1024) throw new CatalogException("catalog.import_file_too_large", "The expanded Excel file is too large to preview.");
            XDocument ReadXml(string name)
            {
                var entry = archive.GetEntry(name) ?? throw new CatalogException("catalog.import_excel_invalid", "The workbook is missing required Excel data.");
                using var entryStream = entry.Open(); return XDocument.Load(entryStream, LoadOptions.None);
            }
            XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace packageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
            var workbook = ReadXml("xl/workbook.xml");
            var firstSheet = workbook.Root?.Element(main + "sheets")?.Elements(main + "sheet").FirstOrDefault() ?? throw new CatalogException("catalog.import_excel_invalid", "The workbook does not contain a worksheet.");
            var relationshipId = (string?)firstSheet.Attribute(relationships + "id") ?? throw new CatalogException("catalog.import_excel_invalid", "The workbook worksheet reference is invalid.");
            var relationship = ReadXml("xl/_rels/workbook.xml.rels").Root?.Elements(packageRelationships + "Relationship").FirstOrDefault(item => (string?)item.Attribute("Id") == relationshipId);
            var target = (string?)relationship?.Attribute("Target") ?? throw new CatalogException("catalog.import_excel_invalid", "The workbook worksheet reference is missing.");
            target = target.Replace('\\', '/').TrimStart('/');
            if (target.Split('/').Contains("..", StringComparer.Ordinal)) throw new CatalogException("catalog.import_excel_invalid", "The workbook worksheet path is invalid.");
            var sheetPath = target.StartsWith("xl/", StringComparison.Ordinal) ? target : $"xl/{target}";
            var sheet = ReadXml(sheetPath);
            var shared = archive.GetEntry("xl/sharedStrings.xml");
            var sharedStrings = new List<string>();
            if (shared is not null)
            {
                using var sharedStream = shared.Open(); var sharedDocument = XDocument.Load(sharedStream, LoadOptions.None);
                sharedStrings.AddRange(sharedDocument.Root?.Elements(main + "si").Select(item => string.Concat(item.Descendants(main + "t").Select(text => text.Value))) ?? []);
            }
            var rows = new List<IReadOnlyList<string>>();
            foreach (var sourceRow in sheet.Root?.Element(main + "sheetData")?.Elements(main + "row") ?? [])
            {
                if (rows.Count >= 5001) throw new CatalogException("catalog.import_row_limit", "An import can contain at most 5,000 product rows.");
                var cells = new SortedDictionary<int, string>();
                foreach (var cell in sourceRow.Elements(main + "c"))
                {
                    var reference = (string?)cell.Attribute("r") ?? throw new CatalogException("catalog.import_excel_invalid", "An Excel cell has no column reference.");
                    var column = ColumnIndex(reference);
                    if (column >= 50) throw new CatalogException("catalog.import_csv_invalid", "The worksheet can contain at most 50 columns.");
                    var type = (string?)cell.Attribute("t");
                    var value = type switch
                    {
                        "inlineStr" => string.Concat(cell.Element(main + "is")?.Descendants(main + "t").Select(text => text.Value) ?? []),
                        "s" => ReadSharedString(cell.Element(main + "v")?.Value, sharedStrings),
                        _ => cell.Element(main + "v")?.Value ?? string.Empty
                    };
                    cells[column] = value;
                }
                if (cells.Count == 0) continue;
                var values = new string[cells.Keys.Max() + 1];
                foreach (var (column, value) in cells) values[column] = value;
                rows.Add(values);
            }
            return rows;
        }
        catch (CatalogException) { throw; }
        catch (Exception exception) when (exception is InvalidDataException or IOException or XmlException or ArgumentException or OverflowException)
        { throw new CatalogException("catalog.import_excel_invalid", "The Excel workbook could not be read."); }
    }

    private static int ColumnIndex(string reference)
    {
        var result = 0; var letters = 0;
        foreach (var character in reference)
        {
            if (!char.IsAsciiLetter(character)) break;
            result = checked(result * 26 + char.ToUpperInvariant(character) - 'A' + 1); letters++;
        }
        if (letters == 0) throw new CatalogException("catalog.import_excel_invalid", "The worksheet contains an invalid cell reference.");
        return result - 1;
    }

    private static string ReadSharedString(string? indexText, List<string> values)
    {
        if (!int.TryParse(indexText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index) || index < 0 || index >= values.Count)
            throw new CatalogException("catalog.import_excel_invalid", "The workbook contains an invalid shared text reference.");
        return values[index];
    }

    private static bool IsAttributeField(string field) => field.StartsWith("attribute:", StringComparison.OrdinalIgnoreCase) && field.Length is > 10 and <= 74 && field[10..].All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
}
