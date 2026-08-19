using System.Windows;
using System.Windows.Controls;

namespace Delima.Launcher.Theming;

/// <summary>
/// Selects vector DataTemplate for a given avatar identifier.
/// </summary>
public sealed class AvatarTemplateSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (container is not FrameworkElement element)
        {
            return null;
        }

        string avatarKey = item as string ?? "cat";
        string normalized = NormalizeAvatarKey(avatarKey);

        string templateKey = $"Avatar_{normalized}";
        if (element.TryFindResource(templateKey) is DataTemplate template)
        {
            return template;
        }

        // Fallback to cat template
        return element.TryFindResource("Avatar_cat") as DataTemplate;
    }

    public static string NormalizeAvatarKey(string key)
    {
        string lower = key.Trim().ToLowerInvariant();
        return lower switch
        {
            "kucing" or "cat" => "cat",
            "ikan" or "fish" => "fish",
            "bunga" or "flower" => "flower",
            "kereta" or "car" => "car",
            "bintang" or "star" => "star",
            "epal" or "apple" => "apple",
            "belon" or "balloon" => "balloon",
            "payung" or "umbrella" => "umbrella",
            "layang" or "layang-layang" or "kite" => "kite",
            "penyu" or "turtle" => "turtle",
            "missing" or "tiada" or "?" => "missing",
            _ => "cat"
        };
    }
}
