using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Delima.Core.Services;

namespace Delima.Launcher.Theming;

/// <summary>
/// Converts an avatar seed string (e.g. stored in student.Avatar) into a
/// <see cref="BitmapImage"/> for display in WPF Image controls.
///
/// Resolution order:
///  1. If the seed is blank or a legacy animal key → use the value bound from
///     PupilCardViewModel.AvatarKey (the student ID is the effective seed).
///  2. If a local PNG cache file exists → load from file:// (fast, offline-safe).
///  3. Otherwise → load directly from the DiceBear HTTPS URL (WPF async).
/// </summary>
[ValueConversion(typeof(string), typeof(BitmapImage))]
public sealed class AvatarSeedToImageConverter : IValueConverter
{
    private static readonly BitmapImage _placeholder = MakePlaceholder();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string seed = value as string ?? string.Empty;

        // If seed is blank or legacy → WPF binding will have passed the raw AvatarKey
        // which may itself be an old animal name. DiceBearService.ResolveSeed handles this
        // by returning the seed unchanged when it is already a valid new-style seed.
        // We pass the seed as-is; if it happens to be a legacy name, DiceBear will simply
        // generate a critter for that literal string (still looks fine and is stable).
        if (string.IsNullOrWhiteSpace(seed))
            return _placeholder;

        try
        {
            string uri = DiceBearService.GetLocalOrRemoteUri(seed);
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri(uri, UriKind.Absolute);
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.CreateOptions = BitmapCreateOptions.DelayCreation;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch
        {
            return _placeholder;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    // ── Placeholder ────────────────────────────────────────────────────────────

    /// <summary>Creates a tiny soft-grey PNG as an in-memory placeholder.</summary>
    private static BitmapImage MakePlaceholder()
    {
        try
        {
            // Attempt to load from URL with a transparent/neutral placeholder
            var img = new BitmapImage();
            img.BeginInit();
            // Use the DiceBear default (no seed) as placeholder
            img.UriSource = new Uri(DiceBearService.GetAvatarUrl("placeholder"));
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.CreateOptions = BitmapCreateOptions.DelayCreation;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch
        {
            return new BitmapImage();
        }
    }
}
