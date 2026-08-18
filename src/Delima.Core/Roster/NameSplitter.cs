using System.Text.RegularExpressions;

namespace Delima.Core.Roster;

/// <summary>
/// Splits a Malaysian student name into a calling (given) name and disambiguation rest parts.
/// Handles Malay, Chinese, Indian, and general naming conventions.
/// </summary>
public static partial class NameSplitter
{
    // Recognises patronymic particles: bin, binti, bt, bte, a/l, a/p, s/o, d/o, anak, ak
    private static readonly Regex ParticleRegex = new(
        @"^(bin|binti|bt|bte|a\/l|a\/p|s\/o|d\/o|anak|ak)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public record ParsedName(string[] Given, string[] Rest);

    public static ParsedName Split(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return new ParsedName([], []);
        }

        string[] words = fullName.Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return new ParsedName([], []);
        }

        // 1. Check for Malay/Indian patronymic particles (bin, binti, a/l, etc.)
        int particleIndex = -1;
        for (int i = 0; i < words.Length; i++)
        {
            if (ParticleRegex.IsMatch(words[i]))
            {
                particleIndex = i;
                break;
            }
        }

        if (particleIndex > 0)
        {
            // Words before the particle are the given name; words after are the father's name (rest)
            string[] given = words[..particleIndex];
            string[] rest = words[(particleIndex + 1)..];
            return new ParsedName(given, rest);
        }

        // 2. Chinese convention (3 or more words with no particle: e.g. "Tan Wei Ming")
        // The first word is the surname (rest); the subsequent words are the given/calling name.
        if (words.Length >= 3)
        {
            string[] given = words[1..];
            string[] rest = [words[0]];
            return new ParsedName(given, rest);
        }

        // 3. 1 or 2 words with no particle (e.g. "Adam Daniel", "Siti")
        return new ParsedName(words, []);
    }
}
