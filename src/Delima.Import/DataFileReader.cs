using System.Data;
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;

namespace Delima.Import;

/// <summary>
/// Reads tabular data from CSV, TSV, XLSX, and XLS files.
/// Returns the total row count (including blank rows, excluding the header) alongside data rows,
/// so the coordinator can verify the number matches their spreadsheet row count.
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

        return ReadCsv(stream);
    }

    public static (List<string> Headers, List<RawImportRow> Rows, int TotalRawRows) ReadCsv(Stream stream)
    {
        Encoding encoding = FileEncodingDetector.DetectEncoding(stream);
        using var reader = new StreamReader(stream, encoding, leaveOpen: true);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
            // Do NOT set IgnoreBlankLines — we want to count every row
            IgnoreBlankLines = false
        };

        using var csv = new CsvReader(reader, config);

        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord == null)
            return ([], [], 0);

        var headers = csv.HeaderRecord.Where(h => !string.IsNullOrWhiteSpace(h)).Select(h => h.Trim()).ToList();
        var rows = new List<RawImportRow>();
        int totalRawRows = 0;
        int rowNum = 1; // header was row 1; data rows start at 2

        while (csv.Read())
        {
            rowNum++;
            totalRawRows++;

            var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool hasNonEmpty = false;

            foreach (var h in headers)
            {
                string val = csv.GetField(h) ?? "";
                cells[h] = val;
                if (!string.IsNullOrWhiteSpace(val))
                    hasNonEmpty = true;
            }

            // Only include rows that have at least one non-empty cell
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
                UseHeaderRow = true
            }
        });

        if (ds.Tables.Count == 0)
            return ([], [], 0);

        var table = ds.Tables[0];
        var headers = new List<string>();

        foreach (DataColumn col in table.Columns)
            headers.Add(col.ColumnName.Trim());

        var rows = new List<RawImportRow>();
        int totalRawRows = table.Rows.Count;
        int rowNum = 1;

        foreach (DataRow dr in table.Rows)
        {
            rowNum++;
            var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool hasNonEmpty = false;

            foreach (var h in headers)
            {
                string val = dr[h]?.ToString() ?? "";
                cells[h] = val;
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
}
