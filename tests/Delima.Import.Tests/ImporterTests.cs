using System.Reflection;
using System.Text;
using Delima.Import;
using Delima.Core.Roster;
using Xunit;
using Xunit.Abstractions;

namespace Delima.Import.Tests;

public class ImporterTests
{
    private readonly ITestOutputHelper _output;

    public ImporterTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static Stream FixtureCsv(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        string fullName = asm.GetManifestResourceNames()
            .First(n => n.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));
        return asm.GetManifestResourceStream(fullName)!;
    }

    private static Stream FromString(string content, Encoding? encoding = null)
        => new MemoryStream((encoding ?? Encoding.UTF8).GetBytes(content));

    // ---------------------------------------------------------------------------
    // 1. Messy APDM fixture — real-world-shaped file from embedded resource
    //    Covers: duplicate IDs, malformed IDs, missing fields, unknown class,
    //    blank rows, m-/bare/full-email IDs, diacritics.
    // ---------------------------------------------------------------------------

    [Fact]
    public void Importer_RealisticApdmFixture_ProducesCorrectDryRunReport()
    {
        var mapping = new ColumnMapping
        {
            FullNameColumn   = "NAMA MURID",
            GradeColumn      = "TAHUN",
            ClassNameColumn  = "KELAS",
            RegisterNoColumn = "NO. KAD PENGENALAN / SURAT BERANAK",
            DelimaIdColumn   = "ID PENGGUNA DELIMA"
        };

        using var stream = FixtureCsv("apdm_realistic_messy.csv");
        var report = RosterImporter.AnalyzeDryRun(stream, "apdm_realistic_messy.csv", mapping);

        string summary = report.GenerateSummaryText();
        _output.WriteLine("=== DRY RUN VALIDATION REPORT ===");
        _output.WriteLine(summary);

        _output.WriteLine("=== UNKNOWN CLASSES ===");
        foreach (var uc in report.UnknownClasses)
            _output.WriteLine($"  {uc.Message}");

        _output.WriteLine("=== WARNINGS ===");
        foreach (var w in report.Warnings)
            _output.WriteLine($"  [Row {w.RowNumber}] {w.StudentName} ({w.DelimaId}): {w.Message}");

        _output.WriteLine("=== REJECTS ===");
        foreach (var r in report.Rejects)
            _output.WriteLine($"  [Row {r.RowNumber}] {r.Field}: {r.Reason}");

        // 25 raw data rows (26 lines minus the header line; the blank whitespace row is one raw row)
        Assert.Equal(25, report.TotalRowsRead);

        // Valid: 10 (2 Cemerlang clean) + 1 (unknown class, still in ReadyToImport) + 5 (3 Amanah) = 16
        Assert.Equal(16, report.ValidCount);

        // Warnings: 1 duplicate
        Assert.Equal(1, report.DuplicateIdCount);

        // Malformed IDs: 2 (5-digit, non-numeric)
        Assert.Equal(2, report.MalformedIdCount);

        // Missing fields: 3 (missing name, missing class, missing ID)
        Assert.Equal(2, report.MissingFieldCount); // missing name + missing class; missing ID is counted under MissingIdCount
        Assert.Equal(1, report.MissingIdCount); // row 17

        // Unknown class: 1 ("6 Amamah")
        Assert.Single(report.UnknownClasses);
        Assert.Equal("Amamah Cemerlang", report.UnknownClasses[0].RawClassName);

        // Diacritics preserved
        var diacriticStudent = report.ReadyToImport.First(s => s.DelimaDigits == "12345682");
        Assert.Equal("Nurul A'in Binti Dato' Yusof", diacriticStudent.FullName);

        // RegisterNoJoinKey present (used for Step 4 password join) but not persisted to store
        var withReg = report.ReadyToImport.First(s => s.DelimaDigits == "12345678");
        Assert.Equal("170101-10-1234", withReg.RegisterNoJoinKey);
    }

    // ---------------------------------------------------------------------------
    // 2. Encoding tests — four encodings must all preserve diacritics
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("utf8_bom")]
    [InlineData("utf8_nobom")]
    [InlineData("utf16_le")]
    [InlineData("windows_1252")]
    public void FileEncodingDetector_HandlesDiverseEncodings_PreservingDiacritics(string encodingType)
    {
        const string sampleCsv = "Nama Penuh,Kelas,ID DELIMa\nNurul A'in Binti Dato' Yusof,2 Cemerlang,m-12345678\n";
        byte[] bytes = encodingType switch
        {
            "utf8_bom"      => new UTF8Encoding(true).GetBytes(sampleCsv),
            "utf8_nobom"    => new UTF8Encoding(false).GetBytes(sampleCsv),
            "utf16_le"      => new UnicodeEncoding(bigEndian: false, byteOrderMark: true).GetBytes(sampleCsv),
            "windows_1252"  => Encoding.GetEncoding(1252).GetBytes(sampleCsv),
            _               => throw new ArgumentException()
        };

        using var stream = new MemoryStream(bytes);
        var (headers, rows, _) = DataFileReader.ReadCsv(stream);

        Assert.Equal(3, headers.Count);
        Assert.Single(rows);
        Assert.Equal("Nurul A'in Binti Dato' Yusof", rows[0].GetValue("Nama Penuh"));
    }

    // ---------------------------------------------------------------------------
    // 3. Line endings — CRLF and LF must both work
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    public void DataFileReader_ReadCsv_HandlesLineEndings(string lineEnding)
    {
        string csv = $"Nama,Kelas,ID{lineEnding}Aisyah,1A,m-10000001{lineEnding}Danial,1A,m-10000002{lineEnding}";
        using var stream = FromString(csv);
        var (headers, rows, totalRaw) = DataFileReader.ReadCsv(stream);

        Assert.Equal(3, headers.Count);
        Assert.Equal(2, rows.Count);
        Assert.Equal(2, totalRaw);
    }

    // ---------------------------------------------------------------------------
    // 4. TotalRowsRead includes blank rows (matches spreadsheet row count)
    // ---------------------------------------------------------------------------

    [Fact]
    public void DataFileReader_TotalRowsRead_IncludesBlankRows()
    {
        // 3 data rows + 1 blank row
        const string csv = "Nama,Kelas,ID\nAisyah,1A,m-10000001\n\nDanial,1A,m-10000002\nChong,1A,m-10000003\n";
        using var stream = FromString(csv);
        var (_, rows, totalRaw) = DataFileReader.ReadCsv(stream);

        Assert.Equal(4, totalRaw); // 4 raw rows including the blank
        Assert.Equal(3, rows.Count); // 3 non-blank rows parsed
    }

    // ---------------------------------------------------------------------------
    // 5. BOM fixture — file beginning with UTF-8 BOM
    // ---------------------------------------------------------------------------

    [Fact]
    public void DataFileReader_BomPrefixed_ParsedCorrectly()
    {
        // UTF-8 BOM: EF BB BF
        byte[] bom = [0xEF, 0xBB, 0xBF];
        byte[] content = Encoding.UTF8.GetBytes("Nama,ID\nAisyah,m-10000001\n");
        byte[] combined = [..bom, ..content];

        using var stream = new MemoryStream(combined);
        var (headers, rows, _) = DataFileReader.ReadCsv(stream);

        Assert.Equal(2, headers.Count);
        Assert.Single(rows);
    }

    // ---------------------------------------------------------------------------
    // 6. Auto-detection of common APDM header patterns
    // ---------------------------------------------------------------------------

    [Fact]
    public void ColumnMapping_AutoDetect_FindsCommonHeaders()
    {
        var headers = new List<string>
        {
            "Bil", "Nama Murid", "Tahun", "Kelas", "No Kad Pengenalan", "Emel DELIMa", "Kata Laluan"
        };

        var mapping = ColumnMapping.AutoDetect(headers);

        Assert.Equal("Nama Murid", mapping.FullNameColumn);
        Assert.Equal("Tahun", mapping.GradeColumn);
        Assert.Equal("Kelas", mapping.ClassNameColumn);
        Assert.Equal("No Kad Pengenalan", mapping.RegisterNoColumn);
        Assert.Equal("Emel DELIMa", mapping.DelimaIdColumn);
        Assert.Equal("Kata Laluan", mapping.PasswordColumn);
        Assert.True(mapping.HasRequiredMappings);
    }

    // ---------------------------------------------------------------------------
    // 7. DELIMa ID normalisation — all three accepted forms
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("12345678",                  "12345678", "m-12345678")]
    [InlineData("m-12345678",                "12345678", "m-12345678")]
    [InlineData("M-12345678",                "12345678", "m-12345678")]
    [InlineData("m-12345678@moe-dl.edu.my",  "12345678", "m-12345678")]
    [InlineData("m-1234",                    null, null)]           // too short
    [InlineData("m-abcdefgh",                null, null)]           // non-numeric
    [InlineData("",                          null, null)]
    public void RosterImporter_NormalizeDelimaId_AllForms(
        string raw, string? expectedDigits, string? expectedLocal)
    {
        var (digits, local) = RosterImporter.NormalizeDelimaId(raw);
        Assert.Equal(expectedDigits, digits);
        Assert.Equal(expectedLocal, local);
    }

    // ---------------------------------------------------------------------------
    // 8. Unknown-class detection
    // ---------------------------------------------------------------------------

    [Fact]
    public void RosterImporter_UnknownClass_IsListedNotRejected()
    {
        // "6 Amamah" cannot be parsed as grade 1-6 after removing the digit from the class name
        // because "Amamah" doesn't contain a grade digit at all — wait, "6 Amamah" does contain "6".
        // The real unknown-class case: grade column says "0" and class is "6 Amamah" with grade=0.
        // Let's use a class name with no digit at all.
        const string csv = "Nama,Kelas,ID\nAisyah,Bulan Sabit,m-10000001\n";
        using var stream = FromString(csv);

        var mapping = new ColumnMapping { FullNameColumn = "Nama", ClassNameColumn = "Kelas", DelimaIdColumn = "ID" };
        var report = RosterImporter.AnalyzeDryRun(stream, "test.csv", mapping);

        // Row is accepted (not rejected)
        Assert.Single(report.ReadyToImport);
        Assert.Equal(0, report.ReadyToImport[0].Grade);

        // But surfaced as an unknown-class warning
        Assert.Single(report.UnknownClasses);
        Assert.Equal("Bulan Sabit", report.UnknownClasses[0].RawClassName);
    }

    // ---------------------------------------------------------------------------
    // 9. Idempotent re-import — FR-S3.7
    // ---------------------------------------------------------------------------

    [Fact]
    public void RosterImporter_IdempotentReimport_FlagsLeaversRatherThanDeleting()
    {
        var existingRoster = new List<Student>
        {
            new() { Id = "s_10000001", Name = "Aisyah Binti Ahmad",   ClassId = "1 Cemerlang", EmailLocal = "m-10000001", Active = true },
            new() { Id = "s_10000002", Name = "Danial Bin Rahim",      ClassId = "1 Cemerlang", EmailLocal = "m-10000002", Active = true },
            new() { Id = "s_10000003", Name = "Murid Berpindah Keluar",ClassId = "1 Cemerlang", EmailLocal = "m-10000003", Active = true }
        };

        const string newCsv = "Nama,Kelas,ID\nAisyah Binti Ahmad,2 Cemerlang,m-10000001\nDanial Bin Rahim,2 Cemerlang,m-10000002\nChong Mei Ling,2 Cemerlang,m-10000004\n";
        using var stream = FromString(newCsv);
        var mapping = new ColumnMapping { FullNameColumn = "Nama", ClassNameColumn = "Kelas", DelimaIdColumn = "ID" };

        var report = RosterImporter.AnalyzeDryRun(stream, "new_term.csv", mapping, existingRoster);

        Assert.Equal(3, report.ReadyToImport.Count);
        Assert.Single(report.Leavers);
        Assert.Equal("s_10000003", report.Leavers[0].Id);

        var updated = RosterImporter.ApplyImport(report, existingRoster);

        var aisyah = updated.First(s => s.EmailLocal == "m-10000001");
        Assert.Equal("2 Cemerlang", aisyah.ClassId);
        Assert.True(aisyah.Active);

        var leaver = existingRoster.First(s => s.EmailLocal == "m-10000003");
        Assert.False(leaver.Active);
    }

    // ---------------------------------------------------------------------------
    // 10. RegisterNoJoinKey is not exposed as a storable field on Student
    // ---------------------------------------------------------------------------

    [Fact]
    public void RosterImporter_RegisterNo_IsJoinKeyOnlyNotStoredOnStudent()
    {
        const string csv = "Nama,Kelas,ID,KP\nAisyah,1A,m-10000001,010101-14-1234\n";
        using var stream = FromString(csv);
        var mapping = new ColumnMapping
        {
            FullNameColumn   = "Nama",
            ClassNameColumn  = "Kelas",
            DelimaIdColumn   = "ID",
            RegisterNoColumn = "KP"
        };

        var report  = RosterImporter.AnalyzeDryRun(stream, "test.csv", mapping);
        var students = RosterImporter.ApplyImport(report);

        // Present on ImportedStudent for Step 4 join
        Assert.Equal("010101-14-1234", report.ReadyToImport[0].RegisterNoJoinKey);

        // NOT present on the persisted Student record (Student has no RegisterNo property)
        var student = students.Single();
        Assert.DoesNotContain("010101-14-1234", student.Name);
        Assert.DoesNotContain("010101-14-1234", student.EmailLocal);
    }

    // ---------------------------------------------------------------------------
    // 11. ColumnMapping: "Nama Kelas" does not steal FullNameColumn
    // ---------------------------------------------------------------------------

    [Fact]
    public void ColumnMapping_AutoDetect_NamaKelasDoesNotStealFullName()
    {
        var headers = new List<string> { "Bil", "Nama Kelas", "Nama Murid", "ID DELIMa" };
        var mapping = ColumnMapping.AutoDetect(headers);

        Assert.Equal("Nama Murid", mapping.FullNameColumn);
        Assert.Equal("Nama Kelas", mapping.ClassNameColumn);
        Assert.Equal("ID DELIMa", mapping.DelimaIdColumn);
    }

    // ---------------------------------------------------------------------------
    // 12. Delimiter detection: Semicolon and Tab delimited CSV/TSV
    // ---------------------------------------------------------------------------

    [Fact]
    public void DataFileReader_DetectsSemicolonAndTabDelimiters()
    {
        string semicolonCsv = "Bil;Nama Murid;Kelas;ID DELIMa\n1;Danial Rahim;2 Cemerlang;m-12345678\n2;Aisyah Ahmad;2 Cemerlang;m-12345679\n";
        using var semiStream = FromString(semicolonCsv);
        var (semiHeaders, semiRows, _) = DataFileReader.ReadCsv(semiStream);

        Assert.Equal(4, semiHeaders.Count);
        Assert.Equal("Nama Murid", semiHeaders[1]);
        Assert.Equal(2, semiRows.Count);
        Assert.Equal("Danial Rahim", semiRows[0].GetValue("Nama Murid"));

        string tsv = "Bil\tNama Murid\tKelas\tID DELIMa\n1\tDanial Rahim\t2 Cemerlang\tm-12345678\n";
        using var tsvStream = FromString(tsv);
        var (tsvHeaders, tsvRows, _) = DataFileReader.ReadCsv(tsvStream);

        Assert.Equal(4, tsvHeaders.Count);
        Assert.Single(tsvRows);
        Assert.Equal("Danial Rahim", tsvRows[0].GetValue("Nama Murid"));
    }

    // ---------------------------------------------------------------------------
    // 13. Smart header detection: Skipping metadata title rows
    // ---------------------------------------------------------------------------

    [Fact]
    public void DataFileReader_SkipsMetadataTitleRowsBeforeHeader()
    {
        string messyCsv =
            "KEMENTERIAN PENDIDIKAN MALAYSIA\n" +
            "SEKOLAH KEBANGSAAN SERI BINTANG UTARA\n" +
            "SENARAI NAMA MURID APDM 2026\n" +
            "\n" +
            "Bil,Nama Murid,Kelas,Tahun,ID DELIMa\n" +
            "1,Muhammad Danial,1 Cemerlang,1,m-12345678\n" +
            "2,Nur Aishah,1 Cemerlang,1,m-12345679\n";

        using var stream = FromString(messyCsv);
        var (headers, rows, totalRaw) = DataFileReader.ReadCsv(stream);

        Assert.Equal(5, headers.Count);
        Assert.Equal("Nama Murid", headers[1]);
        Assert.Equal("Kelas", headers[2]);
        Assert.Equal(2, rows.Count);
        Assert.Equal("Muhammad Danial", rows[0].GetValue("Nama Murid"));
    }

    // ---------------------------------------------------------------------------
    // 14. TemplateGenerator: Roster template generates valid parsable content
    // ---------------------------------------------------------------------------

    [Fact]
    public void TemplateGenerator_ProducesValidParsableRosterTemplate()
    {
        string content = TemplateGenerator.GenerateRosterTemplateContent();
        using var stream = FromString(content);
        var (headers, rows, _) = DataFileReader.ReadCsv(stream);

        var mapping = ColumnMapping.AutoDetect(headers);
        Assert.True(mapping.HasRequiredMappings);
        Assert.Equal("NAMA MURID", mapping.FullNameColumn);
        Assert.Equal("KELAS", mapping.ClassNameColumn);
        Assert.Equal("ID DELIMA", mapping.DelimaIdColumn);

        using var stream2 = FromString(content);
        var report = RosterImporter.AnalyzeDryRun(stream2, "template.csv", mapping);
        Assert.Equal(5, report.ValidCount);
        Assert.Empty(report.Rejects);
    }

    // ---------------------------------------------------------------------------
    // 15. TemplateGenerator: Password template pre-populated with roster
    // ---------------------------------------------------------------------------

    [Fact]
    public void TemplateGenerator_ProducesValidPasswordTemplateWithRoster()
    {
        var roster = new List<ImportedStudent>
        {
            new() { Id = "s_12345678", FullName = "Danial", ClassName = "2C", EmailLocal = "m-12345678", DelimaDigits = "12345678", RegisterNoJoinKey = "170101-10-1234" },
            new() { Id = "s_12345679", FullName = "Aishah", ClassName = "2C", EmailLocal = "m-12345679", DelimaDigits = "12345679", RegisterNoJoinKey = "170202-10-2345" }
        };

        string content = TemplateGenerator.GeneratePasswordTemplateContent(roster);
        Assert.Contains("Danial", content);
        Assert.Contains("Aishah", content);
        Assert.Contains("m-12345678", content);

        using var stream = FromString(content);
        var (headers, rows, _) = DataFileReader.ReadCsv(stream);
        Assert.Contains("KATA LALUAN", headers);
        Assert.Equal(2, rows.Count);
    }

    // ---------------------------------------------------------------------------
    // 16. DELIMa ID normalisation: Supports without hyphen, spaces, and .0
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("m12345678", "12345678", "m-12345678")]
    [InlineData("M12345678", "12345678", "m-12345678")]
    [InlineData("m - 12345678", "12345678", "m-12345678")]
    [InlineData("12345678.0", "12345678", "m-12345678")]
    public void RosterImporter_NormalizeDelimaId_EnhancedFormats(string raw, string? expectedDigits, string? expectedLocal)
    {
        var (digits, local) = RosterImporter.NormalizeDelimaId(raw);
        Assert.Equal(expectedDigits, digits);
        Assert.Equal(expectedLocal, local);
    }

    // ---------------------------------------------------------------------------
    // 17. Class and Grade normalisation: Words, Roman Numerals, T1/T2
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("1 Cemerlang", "SATU", 1, "1 Cemerlang", true)]
    [InlineData("2 Amanah", "II", 2, "2 Amanah", true)]
    [InlineData("T3 Bestari", null, 3, "T3 Bestari", true)]
    public void RosterImporter_NormalizeClassAndGrade_EnhancedInputs(string rawClass, string? rawGrade, int expectedGrade, string expectedClass, bool expectedKnown)
    {
        var (grade, cleanClass, gradeKnown) = RosterImporter.NormalizeClassAndGrade(rawClass, rawGrade);
        Assert.Equal(expectedGrade, grade);
        Assert.Equal(expectedClass, cleanClass);
        Assert.Equal(expectedKnown, gradeKnown);
    }
}
