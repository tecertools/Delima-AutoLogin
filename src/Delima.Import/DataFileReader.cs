using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;

namespace Delima.Import;

/// <summary>
/// Reads tabular data from CSV, TSV, TXT, XLSX, and XLS files.
/// Features automatic delimiter sniffing, smart header row detection (skipping metadata/title blocks),
/// and robust cell formatting cleanup (stripping .0 from numbers, non-breaking spaces).
/// </summary>
public static class DataFileReader
{
    static DataFileReader()
    {
        // Support code pages like Windows-1252 in ExcelDataReader
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <returns>
    /// Headers, Rows, and TotalRawRows. TotalRawRows includes blank/whitespace rows so the
    /// number matches what the coordinator sees in their spreadsheet.
    /// </returns>
    public static (List<string> Headers, List<RawImportRow> Rows, int TotalRawRows) ReadFile(Stream stream, string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (ext == ".xlsx" || ext == ".xls")
            return ReadExcel(stream);

        string? forcedDelimiter = ext == ".tsv" ? "\t" : null;
        return ReadCsv(stream, forcedDelimiter);
    }

    public static (List<string> Headers, List<RawImportRow> Rows, int TotalRawRows) ReadCsv(Stream stream, string? forcedDelimiter = null)
    {
        Encoding encoding = FileEncodingDetector.DetectEncoding(stream);
        using var reader = new StreamReader(stream, encoding, leaveOpen: true);

        // Read sample content to detect delimiter and check structure
        long initialPos = stream.CanSeek ? stream.Position : 0;
        string fullContent = reader.ReadToEnd();
        if (stream.CanSeek)
            stream.Position = initialPos;

        if (string.IsNullOrWhiteSpace(fullContent))
            return ([], [], 0);

        string delimiter = forcedDelimiter ?? DetectDelimiter(fullContent);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
            IgnoreBlankLines = false,
            Delimiter = delimiter
        };

        using var stringReader = new StringReader(fullContent);
        using var csv = new CsvReader(stringReader, config);

        var allRawRecords = new List<List<string>>();
        while (csv.Read())
        {
            var record = new List<string>();
            int count = csv.Parser.Count;
            for (int i = 0; i < count; i++)
            {
                record.Add(CleanCellValue(csv.GetField(i)));
            }
            allRawRecords.Add(record);
        }

        if (allRawRecords.Count == 0)
            return ([], [], 0);

        // Smart Header Detection: Scan the first 15 rows to find the best header candidate row
        int headerRowIndex = FindBestHeaderRowIndex(allRawRecords);
        if (headerRowIndex < 0 || headerRowIndex >= allRawRecords.Count)
            headerRowIndex = 0;

        var headerRecord = allRawRecords[headerRowIndex];
        var headers = new List<string>();
        var seenHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headerRecord.Count; i++)
        {
            string rawHeader = headerRecord[i].Trim();
            if (string.IsNullOrWhiteSpace(rawHeader))
            {
                rawHeader = $"Column{i + 1}";
            }

            string uniqueHeader = rawHeader;
            int counter = 2;
            while (seenHeaders.Contains(uniqueHeader))
            {
                uniqueHeader = $"{rawHeader}_{counter++}";
            }

            seenHeaders.Add(uniqueHeader);
            headers.Add(uniqueHeader);
        }

        var rows = new List<RawImportRow>();
        int totalRawRows = 0;
        int rowNum = headerRowIndex + 1; // 1-based row number in spreadsheet

        for (int r = headerRowIndex + 1; r < allRawRecords.Count; r++)
        {
            rowNum++;
            totalRawRows++;

            var record = allRawRecords[r];
            var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool hasNonEmpty = false;

            for (int c = 0; c < headers.Count; c++)
            {
                string val = c < record.Count ? record[c] : "";
                cells[headers[c]] = val;
                if (!string.IsNullOrWhiteSpace(val))
                    hasNonEmpty = true;
            }

            if (hasNonEmpty)
            {
                rows.Add(new RawImportRow
                {
                    RowNumber = rowNum,
                    Cells = cells
                });
            }
        }

        return (headers, rows, totalRawRows);
    }

    public static (List<string> Headers, List<RawImportRow> Rows, int TotalRawRows) ReadExcel(Stream stream)
    {
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var ds = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = false // Read all rows so we can do smart header detection
            }
        });

        if (ds.Tables.Count == 0)
            return ([], [], 0);

        // Select the best table among sheets
        DataTable table = ds.Tables[0];
        int maxHeaderScore = -1;

        foreach (DataTable candidateTable in ds.Tables)
        {
            var rawRows = ConvertDataTableToRows(candidateTable, 15);
            int score = EvaluateBestHeaderScore(rawRows);
            if (score > maxHeaderScore)
            {
                maxHeaderScore = score;
                table = candidateTable;
            }
        }

        var allTableRows = ConvertDataTableToRows(table, table.Rows.Count);
        if (allTableRows.Count == 0)
            return ([], [], 0);

        int headerRowIndex = FindBestHeaderRowIndex(allTableRows);
        if (headerRowIndex < 0 || headerRowIndex >= allTableRows.Count)
            headerRowIndex = 0;

        var headerRecord = allTableRows[headerRowIndex];
        var headers = new List<string>();
        var seenHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Determine the actual column count by trimming trailing empty columns
        int activeColCount = headerRecord.Count;
        while (activeColCount > 0 && string.IsNullOrWhiteSpace(headerRecord[activeColCount - 1]))
        {
            activeColCount--;
        }
        if (activeColCount == 0) activeColCount = headerRecord.Count;

        for (int i = 0; i < activeColCount; i++)
        {
            string rawHeader = headerRecord[i].Trim();
            if (string.IsNullOrWhiteSpace(rawHeader))
            {
                rawHeader = $"Column{i + 1}";
            }

            string uniqueHeader = rawHeader;
            int counter = 2;
            while (seenHeaders.Contains(uniqueHeader))
            {
                uniqueHeader = $"{rawHeader}_{counter++}";
            }

            seenHeaders.Add(uniqueHeader);
            headers.Add(uniqueHeader);
        }

        var rows = new List<RawImportRow>();
        int totalRawRows = 0;
        int rowNum = headerRowIndex + 1;

        for (int r = headerRowIndex + 1; r < allTableRows.Count; r++)
        {
            rowNum++;
            totalRawRows++;

            var record = allTableRows[r];
            var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool hasNonEmpty = false;

            for (int c = 0; c < headers.Count; c++)
            {
                string val = c < record.Count ? record[c] : "";
                cells[headers[c]] = val;
                if (!string.IsNullOrWhiteSpace(val))
                    hasNonEmpty = true;
            }

            if (hasNonEmpty)
            {
                rows.Add(new RawImportRow
                {
                    RowNumber = rowNum,
                    Cells = cells
                });
            }
        }

        return (headers, rows, totalRawRows);
    }

    private static List<List<string>> ConvertDataTableToRows(DataTable table, int maxRows)
    {
        var result = new List<List<string>>();
        int limit = Math.Min(maxRows, table.Rows.Count);

        for (int r = 0; r < limit; r++)
        {
            DataRow dr = table.Rows[r];
            var rowList = new List<string>();
            for (int c = 0; c < table.Columns.Count; c++)
            {
                rowList.Add(CleanCellValue(dr[c]));
            }
            result.Add(rowList);
        }

        return result;
    }

    public static string DetectDelimiter(string text)
    {
        var sampleLines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                              .Take(10)
                              .ToList();

        if (sampleLines.Count == 0) return ",";

        char[] candidates = [',', ';', '\t', '|'];
        char bestDelimiter = ',';
        int maxScore = -1;

        foreach (char c in candidates)
        {
            int matchCount = 0;
            int consistentLines = 0;
            int firstCount = -1;

            foreach (var line in sampleLines)
            {
                int count = line.Count(ch => ch == c);
                if (count > 0)
                {
                    matchCount += count;
                    if (firstCount == -1) firstCount = count;
                    else if (firstCount == count) consistentLines++;
                }
            }

            // Score based on count + consistency bonus
            int score = matchCount + (consistentLines * 5);
            if (score > maxScore && matchCount > 0)
            {
                maxScore = score;
                bestDelimiter = c;
            }
        }

        return bestDelimiter.ToString();
    }

    public static int FindBestHeaderRowIndex(List<List<string>> rows)
    {
        int limit = Math.Min(15, rows.Count);
        int bestIndex = 0;
        int maxScore = -1;

        for (int i = 0; i < limit; i++)
        {
            var row = rows[i];
            int score = 0;
            int nonEmptyCells = 0;

            foreach (var cell in row)
            {
                if (!string.IsNullOrWhiteSpace(cell))
                {
                    nonEmptyCells++;
                    if (ColumnMapping.IsKnownHeaderKeyword(cell))
                    {
                        score += 3;
                    }
                }
            }

            // If the row has 2+ known header keywords, it's very likely the header row
            if (score > maxScore && nonEmptyCells >= 2)
            {
                maxScore = score;
                bestIndex = i;
            }
        }

        // If we found a row with matches, use it; otherwise fallback to the first row with >= 2 non-empty cells
        if (maxScore > 0)
            return bestIndex;

        for (int i = 0; i < limit; i++)
        {
            if (rows[i].Count(c => !string.IsNullOrWhiteSpace(c)) >= 2)
                return i;
        }

        return 0;
    }

    private static int EvaluateBestHeaderScore(List<List<string>> rows)
    {
        int limit = Math.Min(15, rows.Count);
        int maxScore = 0;

        for (int i = 0; i < limit; i++)
        {
            var row = rows[i];
            int score = 0;
            foreach (var cell in row)
            {
                if (!string.IsNullOrWhiteSpace(cell) && ColumnMapping.IsKnownHeaderKeyword(cell))
                {
                    score += 3;
                }
            }
            if (score > maxScore) maxScore = score;
        }

        return maxScore;
    }

    public static string CleanCellValue(object? val)
    {
        if (val == null || val is DBNull) return "";
        string s = val.ToString() ?? "";

        // Remove non-breaking spaces and zero-width spaces
        s = s.Replace("\u00A0", " ")
             .Replace("\u200B", "")
             .Trim();

        // If Excel formatted a number as float string ending with .0 (e.g. 12345678.0), strip .0
        if (s.EndsWith(".0", StringComparison.Ordinal) && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
        {
            s = s[..^2];
        }

        return s;
    }
}
