using CommunityToolkit.Mvvm.ComponentModel;
using Delima.Core.Store;

namespace Delima.Launcher.ViewModels;

/// <summary>
/// ViewModel representing a single learning portal destination card in the Pilih Destinasi screen.
/// </summary>
public sealed partial class DestinationCardViewModel : ObservableObject
{
    public DestinationConfig Config { get; }
    public string Id => Config.Id;
    public string Label => Config.Label;
    public string Url => Config.Url;
    public string IconEmoji { get; }
    public string Subtitle { get; }
    public string AccentColor { get; }
    public string BadgeText { get; }

    public string AccessibleName => $"{Label}. {Subtitle}. Tekan untuk buka {Label}.";

    public DestinationCardViewModel(DestinationConfig config)
    {
        Config = config;
        (IconEmoji, Subtitle, AccentColor, BadgeText) = InferMetadata(config.Id, config.Label, config.Url);
    }

    public static (string Icon, string Subtitle, string AccentColor, string BadgeText) InferMetadata(string? id, string label, string url)
    {
        string text = $"{id} {label} {url}".ToLowerInvariant();

        if (text.Contains("classroom"))
        {
            return ("📚", "Buka tugasan dan bahan pembelajaran guru anda", "#0F9D58", "Google");
        }
        if (text.Contains("ains") || text.Contains("nilam"))
        {
            return ("📖", "Pangkalan data bahan bacaan & rekod NILAM KPM", "#E65100", "KPM");
        }
        if (text.Contains("canva"))
        {
            return ("🎨", "Reka cipta poster, slaid persembahan dan lembaran kerja", "#7928CA", "Kreatif");
        }
        if (text.Contains("textbook") || text.Contains("buku teks"))
        {
            return ("📕", "Buku teks digital KSSR dan modul interaktif", "#0288D1", "KPM");
        }
        if (text.Contains("delima") || text.Contains("d3") || text.Contains("moe"))
        {
            return ("🎓", "Portal pembelajaran digital utama Kementerian Pendidikan", "#1A73E8", "Utama");
        }

        return ("🌐", "Akses pantas ke portal pembelajaran sekolah", "#2B579A", "Portal");
    }
}
