using System.Text;

namespace Delima.Import;

/// <summary>
/// Generates ready-made CSV template files with UTF-8 BOM encoding for Roster and Password imports.
/// </summary>
public static class TemplateGenerator
{
    public static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>
    /// Generates a standard Roster CSV template with sample data.
    /// </summary>
    public static string GenerateRosterTemplateContent()
    {
        var sb = new StringBuilder();
        sb.AppendLine("BIL,NAMA MURID,KELAS,TAHUN,ID DELIMA,NO KAD PENGENALAN");
        sb.AppendLine("1,Muhammad Danial Bin Rahim,1 Cemerlang,1,m-12345678,170101-10-1234");
        sb.AppendLine("2,Nur Aishah Binti Ahmad,1 Cemerlang,1,m-12345679,170202-10-2345");
        sb.AppendLine("3,Tan Wei Ming,2 Amanah,2,m-12345680,170303-10-3456");
        sb.AppendLine("4,Nurul A'in Binti Dato' Yusof,2 Amanah,2,m-12345681,170404-10-4567");
        sb.AppendLine("5,Arjun A/L Kumaran,3 Bestari,3,m-12345682,170505-10-5678");
        return sb.ToString();
    }

    /// <summary>
    /// Saves the Roster template to a file path.
    /// </summary>
    public static void SaveRosterTemplate(string filePath)
    {
        string content = GenerateRosterTemplateContent();
        File.WriteAllText(filePath, content, Utf8WithBom);
    }

    /// <summary>
    /// Generates a Password CSV template, optionally pre-filled with imported roster pupils.
    /// </summary>
    public static string GeneratePasswordTemplateContent(IEnumerable<ImportedStudent>? rosterStudents = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BIL,NAMA MURID,KELAS,ID DELIMA,NO KAD PENGENALAN,KATA LALUAN");

        var studentList = rosterStudents?.Where(s => !s.IsLeaver).ToList();
        if (studentList != null && studentList.Count > 0)
        {
            int bil = 1;
            foreach (var student in studentList)
            {
                string safeName = EscapeCsv(student.FullName);
                string safeClass = EscapeCsv(student.ClassName);
                string delimaId = student.EmailLocal;
                string regNo = EscapeCsv(student.RegisterNoJoinKey ?? "");
                sb.AppendLine($"{bil++},{safeName},{safeClass},{delimaId},{regNo},");
            }
        }
        else
        {
            sb.AppendLine("1,Muhammad Danial Bin Rahim,1 Cemerlang,m-12345678,170101-10-1234,Delima2026!");
            sb.AppendLine("2,Nur Aishah Binti Ahmad,1 Cemerlang,m-12345679,170202-10-2345,Sekolah#123");
            sb.AppendLine("3,Tan Wei Ming,2 Amanah,m-12345680,170303-10-3456,Bintang@888");
            sb.AppendLine("4,Nurul A'in Binti Dato' Yusof,2 Amanah,m-12345681,170404-10-4567,Maju2026#");
            sb.AppendLine("5,Arjun A/L Kumaran,3 Bestari,m-12345682,170505-10-5678,Gemilang!99");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Saves the Password template to a file path.
    /// </summary>
    public static void SavePasswordTemplate(string filePath, IEnumerable<ImportedStudent>? rosterStudents = null)
    {
        string content = GeneratePasswordTemplateContent(rosterStudents);
        File.WriteAllText(filePath, content, Utf8WithBom);
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
