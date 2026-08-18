using System.Text.RegularExpressions;
using Delima.Core.Roster;

namespace Delima.Admin.Import;

/// <summary>
/// Implements APDM roster import, column mapping validation, dry-run analysis, and idempotent roster updates.
/// Conforms to PRD §6 Step 3 and Technical Architecture §11.
/// </summary>
public static partial class RosterImporter
{
    private static readonly Regex DelimaIdRegex = new(
        @"^(?:m-)?(\d{8})(?:@.*)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GradeExtractRegex = new(
        @"(?:tahun\s*|darjah\s*|tingkatan\s*|year\s*|grade\s*)?([1-6])(?!\d)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static (string? DelimaDigits, string? EmailLocal) NormalizeDelimaId(string rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
            return (null, null);

        string trimmed = rawInput.Trim().ToLowerInvariant();
        var match = DelimaIdRegex.Match(trimmed);
        if (match.Success)
        {
            string digits = match.Groups[1].Value;
            return (digits, $"m-{digits}");
        }

        return (null, null);
    }

    /// <returns>
    /// The numeric grade (1–6), the clean class string, and whether the grade could be determined.
    /// If grade is 0, the caller should treat the class as "unknown" and surface it to the coordinator.
    /// </returns>
    public static (int Grade, string CleanClass, bool GradeKnown) NormalizeClassAndGrade(string rawClass, string? rawGrade)
    {
        int grade = 0;

        // 1. Try explicit grade column first
        if (!string.IsNullOrWhiteSpace(rawGrade))
        {
            var matchGrade = GradeExtractRegex.Match(rawGrade.Trim());
            if (matchGrade.Success && int.TryParse(matchGrade.Groups[1].Value, out int g))
                grade = g;
        }

        string cleanClass = rawClass.Trim();

        // 2. Derive grade from class name if not yet found (e.g. "2 Cemerlang", "2C", "Tahun 2")
        if (grade == 0)
        {
            var matchClass = GradeExtractRegex.Match(cleanClass);
            if (matchClass.Success && int.TryParse(matchClass.Groups[1].Value, out int g))
                grade = g;
        }

        bool gradeKnown = grade >= 1 && grade <= 6;
        return (grade, cleanClass, gradeKnown);
    }

    public static DryRunReport AnalyzeDryRun(
        Stream stream,
        string fileName,
        ColumnMapping mapping,
        IReadOnlyList<Student>? existingRoster = null)
    {
        var (headers, rows, totalRawRows) = DataFileReader.ReadFile(stream, fileName);

        var report = new DryRunReport
        {
            // TotalRowsRead = all raw data rows including blank/whitespace, so the coordinator
            // can verify the number matches their spreadsheet row count (PRD §6 Step 3).
            TotalRowsRead = totalRawRows
        };

        var seenDelimaIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingStudentMap = existingRoster?
            .ToDictionary(s => ExtractDigitsFromId(s.EmailLocal), s => s, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, Student>(StringComparer.OrdinalIgnoreCase);

        var importedStudents = new List<ImportedStudent>();
        // Track classes with unrecognisable grade (PRD §6 Step 3, "unknown class" line)
        var unknownClassNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            string rawName  = row.GetValue(mapping.FullNameColumn);
            string rawClass = row.GetValue(mapping.ClassNameColumn);
            string rawGrade = row.GetValue(mapping.GradeColumn);
            string rawDelima = row.GetValue(mapping.DelimaIdColumn);
            string rawReg   = row.GetValue(mapping.RegisterNoColumn);

            // 1. Name — required, ≤ 100 chars
            if (string.IsNullOrWhiteSpace(rawName))
            {
                report.Rejects.Add(new ImportReject
                {
                    RowNumber = row.RowNumber,
                    StudentName = "",
                    RawId = rawDelima,
                    Reason = "Missing pupil name (Nama penuh is required).",
                    Field = TargetField.FullName
                });
                continue;
            }

            if (rawName.Length > 100)
            {
                report.Rejects.Add(new ImportReject
                {
                    RowNumber = row.RowNumber,
                    StudentName = rawName,
                    RawId = rawDelima,
                    Reason = "Pupil name exceeds maximum length of 100 characters.",
                    Field = TargetField.FullName
                });
                continue;
            }

            // 2. Class — required
            if (string.IsNullOrWhiteSpace(rawClass))
            {
                report.Rejects.Add(new ImportReject
                {
                    RowNumber = row.RowNumber,
                    StudentName = rawName,
                    RawId = rawDelima,
                    Reason = "Missing class name (Kelas is required).",
                    Field = TargetField.ClassName
                });
                continue;
            }

            // 3. DELIMa ID — required and must parse to 8 digits
            if (string.IsNullOrWhiteSpace(rawDelima))
            {
                report.Rejects.Add(new ImportReject
                {
                    RowNumber = row.RowNumber,
                    StudentName = rawName,
                    RawId = "",
                    Reason = "Missing ID DELIMa (ID DELIMa is required).",
                    Field = TargetField.DelimaId
                });
                continue;
            }

            var (delimaDigits, emailLocal) = NormalizeDelimaId(rawDelima);
            if (delimaDigits == null || emailLocal == null)
            {
                report.Rejects.Add(new ImportReject
                {
                    RowNumber = row.RowNumber,
                    StudentName = rawName,
                    RawId = rawDelima,
                    Reason = $"Malformed ID DELIMa: '{rawDelima}'. Expected 8 digits or m-XXXXXXXX.",
                    Field = TargetField.DelimaId
                });
                continue;
            }

            // 4. Duplicate ID within this import — non-blocking warning, first occurrence kept
            if (seenDelimaIds.Contains(delimaDigits))
            {
                report.Warnings.Add(new ImportWarning
                {
                    RowNumber = row.RowNumber,
                    DelimaId = emailLocal,
                    StudentName = rawName,
                    Message = $"Duplicate ID DELIMa '{emailLocal}'.",
                    Resolution = "First occurrence kept; subsequent row skipped."
                });
                continue;
            }

            seenDelimaIds.Add(delimaDigits);

            // 5. Normalise class and grade; track classes with unrecognisable grade
            var (grade, cleanClass, gradeKnown) = NormalizeClassAndGrade(rawClass, rawGrade);
            if (!gradeKnown)
            {
                unknownClassNames.TryGetValue(cleanClass, out int count);
                unknownClassNames[cleanClass] = count + 1;
                // Grade defaults to 0; the class is still accepted into ReadyToImport so the coordinator
                // can fix the class name and re-import without losing the row entirely.
            }

            bool isNew = !existingStudentMap.ContainsKey(delimaDigits);

            var imported = new ImportedStudent
            {
                RowNumber   = row.RowNumber,
                Id          = $"s_{delimaDigits}",
                FullName    = rawName.Trim(),
                ClassName   = cleanClass,
                Grade       = grade,
                EmailLocal  = emailLocal,
                DelimaDigits = delimaDigits,
                // RegisterNoJoinKey: only kept as a join key for Step 4 (password import); never persisted.
                RegisterNoJoinKey = string.IsNullOrWhiteSpace(rawReg) ? null : rawReg.Trim(),
                IsNew       = isNew
            };

            importedStudents.Add(imported);
        }

        // 6. Compute display names per class
        foreach (var group in importedStudents.GroupBy(s => s.ClassName))
        {
            var tempStudents = group.Select(g => new Student { Id = g.Id, Name = g.FullName }).ToList();
            var displayNames = DisplayNameCalculator.ComputeDisplayNames(tempStudents);

            foreach (var student in group)
            {
                if (displayNames.TryGetValue(student.Id, out var dn))
                    student.DisplayName = dn;
                // No length warning — not in spec. The Admin UI will show the
                // display name in the preview grid for the coordinator to review.
            }
        }

        report.ReadyToImport = importedStudents;

        // 7. Unknown-class warnings (PRD §6 Step 3: "unknown class … → not in any tahun; confirm or fix")
        foreach (var (className, count) in unknownClassNames)
        {
            report.UnknownClasses.Add(new UnknownClassWarning
            {
                RawClassName = className,
                OccurrenceCount = count
            });
        }

        // 8. Class summaries
        report.Classes = importedStudents
            .Where(s => s.Grade > 0) // exclude unknown-grade classes from the summary count
            .GroupBy(s => new { s.ClassName, s.Grade })
            .Select(g => new ClassSummary
            {
                ClassName    = g.Key.ClassName,
                Grade        = g.Key.Grade,
                StudentCount = g.Count()
            })
            .OrderBy(c => c.Grade)
            .ThenBy(c => c.ClassName)
            .ToList();

        // 9. Leavers — FR-S3.7
        if (existingRoster != null)
        {
            foreach (var existing in existingRoster)
            {
                string digits = ExtractDigitsFromId(existing.EmailLocal);
                if (!seenDelimaIds.Contains(digits))
                {
                    report.Leavers.Add(new ImportedStudent
                    {
                        Id           = existing.Id,
                        FullName     = existing.Name,
                        ClassName    = existing.ClassId,
                        EmailLocal   = existing.EmailLocal,
                        DelimaDigits = digits,
                        DisplayName  = existing.DisplayName,
                        IsNew        = false,
                        IsLeaver     = true
                    });
                }
            }
        }

        return report;
    }

    /// <summary>
    /// Applies a confirmed dry-run report to create / update Student records.
    /// Leavers are set Active = false rather than deleted (FR-S3.7).
    /// </summary>
    public static List<Student> ApplyImport(DryRunReport dryRun, IReadOnlyList<Student>? existingRoster = null)
    {
        var result = new List<Student>();
        var existingMap = existingRoster?
            .ToDictionary(s => ExtractDigitsFromId(s.EmailLocal), s => s, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, Student>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in dryRun.ReadyToImport)
        {
            if (existingMap.TryGetValue(item.DelimaDigits, out var existing))
            {
                existing.Name        = item.FullName;
                existing.ClassId     = item.ClassName;
                existing.EmailLocal  = item.EmailLocal;
                existing.DisplayName = item.DisplayName;
                existing.Active      = true;
                result.Add(existing);
            }
            else
            {
                result.Add(new Student
                {
                    Id          = item.Id,
                    Name        = item.FullName,
                    ClassId     = item.ClassName,
                    EmailLocal  = item.EmailLocal,
                    DisplayName = item.DisplayName,
                    Avatar      = "kucing",
                    Active      = true
                });
            }
        }

        // Retain leavers as Active = false per FR-S3.7
        foreach (var leaver in dryRun.Leavers)
        {
            if (existingMap.TryGetValue(leaver.DelimaDigits, out var existing))
            {
                existing.Active = false;
                result.Add(existing);
            }
        }

        return result;
    }

    private static string ExtractDigitsFromId(string emailOrId)
    {
        var match = DelimaIdRegex.Match(emailOrId.Trim());
        return match.Success ? match.Groups[1].Value : emailOrId;
    }
}
