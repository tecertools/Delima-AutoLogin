using System.IO;
using System.Text.RegularExpressions;

namespace Delima.Launcher.Tests;

public class ForbiddenVocabularyTests
{
    private static readonly string[] ForbiddenWords =
    [
        "sso",
        "portal",
        "autentikasi",
        "sesi",
        "log masuk tunggal"
    ];

    [Fact]
    public void XamlFiles_DoNotContainForbiddenVocabulary()
    {
        string launcherDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Delima.Launcher"));
        if (!Directory.Exists(launcherDir))
        {
            launcherDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "src", "Delima.Launcher"));
        }

        Assert.True(Directory.Exists(launcherDir), $"Could not find Delima.Launcher directory at {launcherDir}");

        var xamlFiles = Directory.GetFiles(launcherDir, "*.xaml", SearchOption.AllDirectories);
        Assert.NotEmpty(xamlFiles);

        var violations = new List<string>();

        foreach (string file in xamlFiles)
        {
            string content = File.ReadAllText(file);
            foreach (string word in ForbiddenWords)
            {
                var regex = new Regex($@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase);
                if (regex.IsMatch(content))
                {
                    violations.Add($"{Path.GetFileName(file)} contains forbidden word '{word}'");
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void ViewModelStringProperties_DoNotContainForbiddenVocabulary()
    {
        string launcherDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Delima.Launcher", "ViewModels"));
        if (!Directory.Exists(launcherDir))
        {
            launcherDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "src", "Delima.Launcher", "ViewModels"));
        }

        Assert.True(Directory.Exists(launcherDir), $"Could not find ViewModels directory at {launcherDir}");

        var csFiles = Directory.GetFiles(launcherDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(csFiles);

        var violations = new List<string>();

        foreach (string file in csFiles)
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Contains('"'))
                {
                    foreach (string word in ForbiddenWords)
                    {
                        var regex = new Regex($@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase);
                        if (regex.IsMatch(line) && !line.TrimStart().StartsWith("//"))
                        {
                            violations.Add($"{Path.GetFileName(file)}:L{i + 1} contains forbidden word '{word}' in: {line.Trim()}");
                        }
                    }
                }
            }
        }

        Assert.Empty(violations);
    }
}
