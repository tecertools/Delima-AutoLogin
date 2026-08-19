using CommunityToolkit.Mvvm.ComponentModel;

namespace Delima.Launcher.ViewModels;

/// <summary>
/// Represents a single selectable picture password icon in the 16-icon grid.
/// </summary>
public sealed partial class PicturePasswordIconViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _templateKey;

    [ObservableProperty]
    private string _accessibleName;

    public PicturePasswordIconViewModel(string id, string name, string? accessibleName = null)
    {
        _id = id;
        _name = name;
        _templateKey = $"Picture_{id}";
        _accessibleName = accessibleName ?? $"Gambar {name.ToLowerInvariant()}";
    }

    /// <summary>
    /// Returns the standard set of 16 concrete BM-nameable picture password icons.
    /// </summary>
    public static List<PicturePasswordIconViewModel> GetAllStandardIcons()
    {
        return
        [
            new("bola", "Bola", "Pilih gambar bola"),
            new("buku", "Buku", "Pilih gambar buku"),
            new("bintang", "Bintang", "Pilih gambar bintang"),
            new("pokok", "Pokok", "Pilih gambar pokok"),
            new("kereta", "Kereta", "Pilih gambar kereta"),
            new("rumah", "Rumah", "Pilih gambar rumah"),
            new("ikan", "Ikan", "Pilih gambar ikan"),
            new("kucing", "Kucing", "Pilih gambar kucing"),
            new("bunga", "Bunga", "Pilih gambar bunga"),
            new("epal", "Epal", "Pilih gambar epal"),
            new("pisang", "Pisang", "Pilih gambar pisang"),
            new("payung", "Payung", "Pilih gambar payung"),
            new("belon", "Belon", "Pilih gambar belon"),
            new("jam", "Jam", "Pilih gambar jam"),
            new("pensel", "Pensel", "Pilih gambar pensel"),
            new("topi", "Topi", "Pilih gambar topi")
        ];
    }
}
