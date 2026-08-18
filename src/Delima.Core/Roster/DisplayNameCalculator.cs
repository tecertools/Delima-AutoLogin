namespace Delima.Core.Roster;

/// <summary>
/// Computes unambiguous, culturally appropriate display names for a class of students.
/// Implements Technical Architecture §5 and Normal_SSO §4.3.
/// </summary>
public static class DisplayNameCalculator
{
    public const int MaxDisplayLengthWarning = 18;

    /// <summary>
    /// Computes unambiguous calling names (the floor) for all students in a class.
    /// Disambiguates collisions with initials.
    /// </summary>
    public static Dictionary<string, string> ComputeDisplayNames(IReadOnlyList<Student> students)
    {
        var parsed = students.ToDictionary(s => s.Id, s => NameSplitter.Split(s.Name));

        string FormatLabel(string studentId, int disambiguationDepth)
        {
            var p = parsed[studentId];
            string givenName = string.Join(" ", p.Given);

            if (disambiguationDepth <= 0 || p.Rest.Length == 0)
            {
                return givenName;
            }

            int count = Math.Min(disambiguationDepth, p.Rest.Length);
            string initials = string.Concat(p.Rest.Take(count).Select(w => char.ToUpperInvariant(w[0]) + "."));
            return $"{givenName} {initials}";
        }

        var result = new Dictionary<string, string>();

        foreach (var s in students)
        {
            var p = parsed[s.Id];
            int depth = 0;
            int maxDepth = p.Rest.Length;

            while (depth <= maxDepth)
            {
                string currentLabel = FormatLabel(s.Id, depth);
                bool hasCollision = students.Any(other => other.Id != s.Id && FormatLabel(other.Id, depth) == currentLabel);

                if (!hasCollision)
                {
                    break;
                }
                depth++;
            }

            result[s.Id] = FormatLabel(s.Id, Math.Min(depth, maxDepth));
        }

        return result;
    }

    /// <summary>
    /// Computes an adaptive display name considering card width.
    /// On large cards (179px, 7 cols), fits the full name across 2 lines if possible;
    /// on smaller cards (137px, 9 cols), falls back to the disambiguated calling name floor.
    /// </summary>
    public static string ComputeAdaptiveDisplayName(
        Student student,
        IReadOnlyList<Student> classStudents,
        int cardWidthPx)
    {
        // 7-column card (~179px) has room for full names on small classes
        if (cardWidthPx >= 170 && FitsInTwoLines(student.Name, 19))
        {
            return student.Name;
        }

        // Standard/tight cards (137px - 156px) use the calling name + disambiguation initial floor
        var map = ComputeDisplayNames(classStudents);
        return map.TryGetValue(student.Id, out var displayName) ? displayName : student.Name;
    }

    /// <summary>
    /// Determines whether a full name can be wrapped across at most 2 lines with a maximum character limit per line.
    /// </summary>
    public static bool FitsInTwoLines(string fullName, int maxCharsPerLine)
    {
        string[] words = fullName.Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return true;

        int lines = 1;
        int currentLineLength = 0;

        foreach (string word in words)
        {
            if (word.Length > maxCharsPerLine)
            {
                return false; // Single word exceeds line width
            }

            if (currentLineLength == 0)
            {
                currentLineLength = word.Length;
            }
            else if (currentLineLength + 1 + word.Length <= maxCharsPerLine)
            {
                currentLineLength += 1 + word.Length;
            }
            else
            {
                lines++;
                if (lines > 2)
                {
                    return false;
                }
                currentLineLength = word.Length;
            }
        }

        return lines <= 2;
    }
}
