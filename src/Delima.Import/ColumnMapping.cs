namespace Delima.Import;

/// <summary>
/// Defines mapping from source spreadsheet/CSV column headers to target domain fields.
/// </summary>
public sealed class ColumnMapping
{
    public string? FullNameColumn { get; set; }
    public string? ClassNameColumn { get; set; }
    public string? GradeColumn { get; set; }
    public string? DelimaIdColumn { get; set; }
    public string? RegisterNoColumn { get; set; }
    public string? PasswordColumn { get; set; }

    public bool HasRequiredMappings =>
        !string.IsNullOrWhiteSpace(FullNameColumn) &&
        !string.IsNullOrWhiteSpace(ClassNameColumn) &&
        !string.IsNullOrWhiteSpace(DelimaIdColumn);

    /// <summary>
    /// Checks whether a given header string contains known keywords indicating a table header row.
    /// </summary>
    public static bool IsKnownHeaderKeyword(string header)
    {
        if (string.IsNullOrWhiteSpace(header)) return false;
        string clean = header.Trim().ToLowerInvariant()
            .Replace("_", " ")
            .Replace("-", " ")
            .Replace("/", " ");
        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+", " ").Trim();

        // Ignore common metadata row text
        if (clean.Contains("kementerian") || clean.Contains("sekolah kebangsaan") ||
            clean.Contains("senarai nama murid") || clean.Contains("sistem pengurusan"))
        {
            return false;
        }

        return clean == "bil" || clean == "no" || clean == "no." ||
               clean.Contains("nama") || clean.Contains("name") || clean.Contains("murid") || clean.Contains("pelajar") || clean.Contains("student") ||
               clean.Contains("kelas") || clean.Contains("class") || clean.Contains("tingkatan") || clean.Contains("tahun") || clean.Contains("darjah") || clean.Contains("grade") ||
               clean.Contains("delima") || clean.Contains("emel") || clean.Contains("email") || clean.Contains("pengguna") ||
               clean.Contains("kp") || clean.Contains("kad pengenalan") || clean.Contains("ic") || clean.Contains("mykid") || clean.Contains("mykad") || clean.Contains("sijil lahir") || clean.Contains("surat beranak") ||
               clean.Contains("kata laluan") || clean.Contains("katalaluan") || clean.Contains("password");
    }

    /// <summary>
    /// Suggests best-guess column mappings based on common MOE / School export header names (including APDM exports).
    /// </summary>
    public static ColumnMapping AutoDetect(IReadOnlyList<string> headers)
    {
        var mapping = new ColumnMapping();

        // 1. First pass: High-precision / exact matching
        foreach (var header in headers)
        {
            string clean = header.Trim().ToLowerInvariant()
                .Replace("_", " ")
                .Replace("-", " ")
                .Replace("/", " ");
            clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+", " ").Trim();

            // Password
            if (mapping.PasswordColumn == null &&
                (clean == "kata laluan" || clean == "katalaluan" || clean == "password" || clean == "pw" || clean == "pass" || clean.Contains("kata laluan") || clean.Contains("katalaluan")))
            {
                mapping.PasswordColumn = header;
            }

            // DELIMa ID / Email (explicit)
            if (mapping.DelimaIdColumn == null &&
                (clean.Contains("delima") || clean.Contains("emel") || clean.Contains("email") || clean.Contains("id pengguna") || clean == "user id" || clean == "google id" || clean == "id murid" || clean == "akaun delima" || clean == "id" || clean == "id emel" || clean == "emel" || clean == "id/emel"))
            {
                mapping.DelimaIdColumn = header;
            }

            // Register / IC number
            if (mapping.RegisterNoColumn == null &&
                (clean.Contains("kad pengenalan") || clean.Contains("surat beranak") || clean.Contains("sijil lahir") || clean.Contains("mykid") || clean.Contains("mykad") || clean.Contains("nric") || clean.Contains("nokp") || clean.Contains("no kp") || clean.Contains("no. kp") || clean == "ic" || clean.Contains("pendaftaran") || clean.Contains("pengenalan") || clean.Contains("register")))
            {
                mapping.RegisterNoColumn = header;
            }

            // Grade / Year (explicit - APDM headers like "TINGKATAN/TAHUN", "TAHUN/TINGKATAN", "TAHUN", "TINGKATAN", "DARJAH")
            if (mapping.GradeColumn == null &&
                (clean == "tahun" || clean == "darjah" || clean == "tingkatan" || clean == "grade" || clean == "year" || clean == "standard" || clean == "level" ||
                 clean == "tingkatan tahun" || clean == "tahun tingkatan" || clean == "aliran" || clean == "tahun persekolahan") &&
                !clean.Contains("lahir") && !clean.Contains("kelahiran"))
            {
                mapping.GradeColumn = header;
            }

            // Class Name (check for class keywords)
            if (mapping.ClassNameColumn == null &&
                (clean == "kelas" || clean == "nama kelas" || clean == "class" || clean == "class name" || clean == "nama tingkatan" || clean == "bilik darjah" || clean == "nama bilik darjah"))
            {
                mapping.ClassNameColumn = header;
            }

            // Full Name (specific)
            if (mapping.FullNameColumn == null &&
                (clean == "nama murid" || clean == "nama pelajar" || clean == "nama penuh" || clean == "student name" || clean == "full name" || clean == "pupil name" || clean == "nama anak" || clean == "nama"))
            {
                mapping.FullNameColumn = header;
            }
        }

        // 2. Second pass: Fallback matching for any remaining unset columns
        foreach (var header in headers)
        {
            string clean = header.Trim().ToLowerInvariant()
                .Replace("_", " ")
                .Replace("-", " ")
                .Replace("/", " ");
            clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+", " ").Trim();

            // Fallback for Grade
            if (mapping.GradeColumn == null &&
                (clean.Contains("tahun") || clean.Contains("darjah") || clean.Contains("tingkatan") || clean.Contains("grade") || clean.Contains("year")) &&
                !clean.Contains("kelas") && !clean.Contains("lahir") && !clean.Contains("kelahiran") && !clean.Contains("masuk") && !clean.Contains("nama") &&
                header != mapping.ClassNameColumn && header != mapping.FullNameColumn)
            {
                mapping.GradeColumn = header;
            }

            // Fallback for Class
            if (mapping.ClassNameColumn == null &&
                (clean.Contains("kelas") || clean.Contains("class")) &&
                !clean.Contains("guru") && header != mapping.GradeColumn && header != mapping.FullNameColumn)
            {
                mapping.ClassNameColumn = header;
            }

            // Fallback for Full Name
            if (mapping.FullNameColumn == null &&
                (clean.Contains("nama") || clean.Contains("name") || clean.Contains("murid") || clean.Contains("student") || clean.Contains("pelajar")) &&
                !clean.Contains("kelas") && !clean.Contains("sekolah") && !clean.Contains("guru") && !clean.Contains("bapa") && !clean.Contains("ibu") && !clean.Contains("penjaga") && !clean.Contains("waris") &&
                header != mapping.ClassNameColumn && header != mapping.GradeColumn)
            {
                mapping.FullNameColumn = header;
            }

            // Fallback for DELIMa ID
            if (mapping.DelimaIdColumn == null &&
                (clean.Contains("id") || clean.Contains("login") || clean.Contains("akaun")) &&
                !clean.Contains("kad pengenalan") && !clean.Contains("kp") && !clean.Contains("mykid") && !clean.Contains("mykad") && !clean.Contains("ic") && !clean.Contains("kelas") &&
                header != mapping.RegisterNoColumn)
            {
                mapping.DelimaIdColumn = header;
            }
        }

        return mapping;
    }
}
