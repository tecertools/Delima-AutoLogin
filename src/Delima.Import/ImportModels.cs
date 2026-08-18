using System.Text;
using Delima.Core.Roster;

namespace Delima.Import;

public enum TargetField
{
    FullName,
    ClassName,
    Grade,
    DelimaId,
    RegisterNo,
    Password
}

public sealed class RawImportRow
{
    public int RowNumber { get; set; }
    public Dictionary<string, string> Cells { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string GetValue(string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName)) return "";
        return Cells.TryGetValue(columnName, out var val) ? val.Trim() : "";
    }
}

public sealed class ImportedStudent
{
    public int RowNumber { get; set; }
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string ClassName { get; set; } = "";
    public int Grade { get; set; }
    public string EmailLocal { get; set; } = "";
    public string DelimaDigits { get; set; } = "";
    /// <summary>
    /// KP number kept only as a join key for the Step 4 password import; never written to the store.
    /// </summary>
    public string? RegisterNoJoinKey { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsNew { get; set; } = true;
    public bool IsLeaver { get; set; } = false;
}

public sealed class ImportWarning
{
    public int RowNumber { get; set; }
    public string? DelimaId { get; set; }
    public string? StudentName { get; set; }
    public string Message { get; set; } = "";
    public string Resolution { get; set; } = "";
}

public sealed class ImportReject
{
    public int RowNumber { get; set; }
    public string StudentName { get; set; } = "";
    public string RawId { get; set; } = "";
    public string Reason { get; set; } = "";
    public TargetField? Field { get; set; }
}

public sealed class UnknownClassWarning
{
    public string RawClassName { get; set; } = "";
    public int OccurrenceCount { get; set; }
    public string Message => $"Unknown class \"{RawClassName}\" — no recognisable tahun (1–6); confirm or fix before import.";
}

public sealed class ClassSummary
{
    public string ClassName { get; set; } = "";
    public int Grade { get; set; }
    public int StudentCount { get; set; }
    public bool WillScrollOnStandardDisplay => StudentCount > 44;
}

public sealed class DryRunReport
{
    public int TotalRowsRead { get; set; }
    public List<ImportedStudent> ReadyToImport { get; set; } = [];
    public List<ImportWarning> Warnings { get; set; } = [];
    public List<UnknownClassWarning> UnknownClasses { get; set; } = [];
    public List<ImportReject> Rejects { get; set; } = [];
    public List<ClassSummary> Classes { get; set; } = [];
    public List<ImportedStudent> Leavers { get; set; } = [];

    public int ValidCount => ReadyToImport.Count;
    public int DuplicateIdCount => Warnings.Count(w => w.Message.Contains("Duplicate ID DELIMa", StringComparison.OrdinalIgnoreCase));
    public int MalformedIdCount => Rejects.Count(r => r.Field == TargetField.DelimaId && !r.Reason.StartsWith("Missing", StringComparison.Ordinal));
    public int MissingIdCount => Rejects.Count(r => r.Field == TargetField.DelimaId && r.Reason.StartsWith("Missing", StringComparison.Ordinal));
    public int MissingFieldCount => Rejects.Count(r => r.Field != TargetField.DelimaId);

    public string GenerateSummaryText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{TotalRowsRead:N0} rows read");
        sb.AppendLine($"  {ValidCount:N0} valid");

        if (DuplicateIdCount > 0)
            sb.AppendLine($"     {DuplicateIdCount,2} duplicate ID DELIMa      → listed, first occurrence kept");

        if (MalformedIdCount > 0)
            sb.AppendLine($"     {MalformedIdCount,2} malformed ID DELIMa      → listed with row numbers");

        if (UnknownClasses.Count > 0)
        {
            foreach (var uc in UnknownClasses)
                sb.AppendLine($"      1 unknown class \"{uc.RawClassName}\" → not in any tahun; confirm or fix");
        }

        int totalClasses = Classes.Count;
        int totalGrades = Classes.Select(c => c.Grade).Distinct().Count();
        if (totalClasses > 0)
            sb.AppendLine($"     {totalClasses,2} classes across {totalGrades} tahun");

        var scrollingClasses = Classes.Where(c => c.WillScrollOnStandardDisplay).ToList();
        if (scrollingClasses.Count > 0)
            sb.AppendLine($"      {scrollingClasses.Count} classes over 44 pupils   → grid will scroll on 1366×768 (see §7.2)");

        if (Leavers.Count > 0)
            sb.AppendLine($"     {Leavers.Count,2} existing pupils not in export → flagged as leavers (kept active=false)");

        return sb.ToString();
    }
}
