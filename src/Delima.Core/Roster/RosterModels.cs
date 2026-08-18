using System.Text.RegularExpressions;

namespace Delima.Core.Roster;

/// <summary>
/// School information.
/// </summary>
public sealed class School
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Motto { get; set; }
    public string Domain { get; set; } = "moe-dl.edu.my";
}

/// <summary>
/// Class information within a school.
/// </summary>
public sealed class ClassInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Grade { get; set; } // Tahun 1..6
    public int ColourIndex { get; set; }
}

/// <summary>
/// Student record in the roster.
/// </summary>
public sealed class Student
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ClassId { get; set; } = "";
    public string EmailLocal { get; set; } = "";
    public string Avatar { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int PasswordVersion { get; set; } = 1;
    public bool Active { get; set; } = true;

    /// <summary>
    /// Computes full MOE email address using the school domain.
    /// </summary>
    public string GetFullEmail(string schoolDomain = "moe-dl.edu.my")
    {
        if (EmailLocal.Contains('@'))
        {
            return EmailLocal;
        }
        return $"{EmailLocal}@{schoolDomain}";
    }

    /// <summary>
    /// Matches search queries against full name, display name, and email local part.
    /// Case-insensitive and accent-insensitive matching.
    /// </summary>
    public bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        string trimmed = query.Trim();
        return Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
               DisplayName.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
               EmailLocal.Contains(trimmed, StringComparison.OrdinalIgnoreCase);
    }
}
