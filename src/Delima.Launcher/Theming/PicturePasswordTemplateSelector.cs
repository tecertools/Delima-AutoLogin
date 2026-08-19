using System.Windows;
using System.Windows.Controls;

namespace Delima.Launcher.Theming;

/// <summary>
/// Selects vector DataTemplate for a given picture password icon identifier.
/// </summary>
public sealed class PicturePasswordTemplateSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (container is not FrameworkElement element)
        {
            return null;
        }

        string iconKey = item as string ?? "bola";
        string normalized = NormalizeIconKey(iconKey);

        string templateKey = $"Picture_{normalized}";
        if (element.TryFindResource(templateKey) is DataTemplate template)
        {
            return template;
        }

        // Fallback to bola template
        return element.TryFindResource("Picture_bola") as DataTemplate;
    }

    public static string NormalizeIconKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "bola";
        }

        string lower = key.Trim().ToLowerInvariant();
        if (lower.StartsWith("picture_"))
        {
            lower = lower["picture_".Length..];
        }

        return lower switch
        {
            "bola" or "ball" => "bola",
            "buku" or "book" => "buku",
            "bintang" or "star" => "bintang",
            "pokok" or "tree" => "pokok",
            "kereta" or "car" => "kereta",
            "rumah" or "house" => "rumah",
            "ikan" or "fish" => "ikan",
            "kucing" or "cat" => "kucing",
            "bunga" or "flower" => "bunga",
            "epal" or "apple" => "epal",
            "pisang" or "banana" => "pisang",
            "payung" or "umbrella" => "payung",
            "belon" or "balloon" => "belon",
            "jam" or "clock" => "jam",
            "pensel" or "pencil" => "pensel",
            "topi" or "hat" or "cap" => "topi",
            _ => "bola"
        };
    }
}
