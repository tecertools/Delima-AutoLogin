namespace Delima.Admin.Import;

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
    /// Suggests best-guess column mappings based on common APDM / MOE export header names.
    /// </summary>
    public static ColumnMapping AutoDetect(IReadOnlyList<string> headers)
    {
        var mapping = new ColumnMapping();

        foreach (var header in headers)
        {
            string clean = header.Trim().ToLowerInvariant();

            if (mapping.FullNameColumn == null &&
                (clean.Contains("nama") || clean.Contains("name") || clean.Contains("murid") || clean.Contains("student")))
            {
                mapping.FullNameColumn = header;
            }
            else if (mapping.ClassNameColumn == null &&
                (clean == "kelas" || clean == "class" || clean == "nama kelas" || clean.Contains("tingkatan/tahun") || clean.Contains("tingkatan_tahun")))
            {
                mapping.ClassNameColumn = header;
            }
            else if (mapping.GradeColumn == null &&
                (clean == "tahun" || clean == "darjah" || clean == "tingkatan" || clean == "grade" || clean == "year"))
            {
                mapping.GradeColumn = header;
            }
            else if (mapping.DelimaIdColumn == null &&
                (clean.Contains("delima") || clean.Contains("emel") || clean.Contains("email") || clean == "id_pengguna" || clean == "user_id"))
            {
                mapping.DelimaIdColumn = header;
            }
            else if (mapping.RegisterNoColumn == null &&
                (clean.Contains("kp") || clean.Contains("nokp") || clean.Contains("ic") || clean.Contains("pendaftaran") || clean.Contains("pengenalan") || clean.Contains("register")))
            {
                mapping.RegisterNoColumn = header;
            }
            else if (mapping.PasswordColumn == null &&
                (clean.Contains("kata_laluan") || clean.Contains("katalaluan") || clean.Contains("kata laluan") || clean.Contains("password") || clean == "pw"))
            {
                mapping.PasswordColumn = header;
            }
        }

        return mapping;
    }
}
