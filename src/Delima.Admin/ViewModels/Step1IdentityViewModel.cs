using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Delima.Admin.Models;

namespace Delima.Admin.ViewModels;

public sealed record ThemePresetItem(
    string Name,
    string Primary,
    string Accent,
    string[] ClassColours
);

public sealed partial class Step1IdentityViewModel : ObservableObject
{
    private readonly AdminWizardState _state;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string _schoolCode = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string _schoolName = "";

    [ObservableProperty]
    private string _motto = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string _domain = "moe-dl.edu.my";

    [ObservableProperty]
    private string? _crestPath;

    public ObservableCollection<ColorSwatchItem> Swatches { get; } = [];

    public IReadOnlyList<ThemePresetItem> Presets { get; } =
    [
        new ThemePresetItem(
            "Hijau DELIMa (Piawai)",
            "#056839",
            "#F7941D",
            ["#C41118", "#9E2B0E", "#A85200", "#8A6100", "#056839", "#0A6265"]
        ),
        new ThemePresetItem(
            "Biru Korporat KPM",
            "#003B73",
            "#E87722",
            ["#005A9C", "#0072B2", "#008080", "#2E5B88", "#7D3C98", "#B85C00"]
        ),
        new ThemePresetItem(
            "Merah Mahogani",
            "#8B1E1E",
            "#D97706",
            ["#8B1E1E", "#A04000", "#2E4053", "#117864", "#7D6608", "#6C3483"]
        ),
        new ThemePresetItem(
            "Ungu Moden",
            "#4A235A",
            "#16A085",
            ["#5B2C6F", "#1A5276", "#196F3D", "#935116", "#78281F", "#283747"]
        ),
        new ThemePresetItem(
            "Zamrud & Emas",
            "#0E6655",
            "#B7950B",
            ["#0E6655", "#1B4F72", "#641E16", "#512E5F", "#784212", "#145A32"]
        )
    ];

    public bool CanProceed => ValidateInternal(out _);

    public string ValidationMessage
    {
        get
        {
            ValidateInternal(out string msg);
            return msg;
        }
    }

    public Step1IdentityViewModel(AdminWizardState state)
    {
        _state = state;
        _schoolCode = state.School.Code;
        _schoolName = state.School.Name;
        _motto = state.School.Motto ?? "";
        _domain = state.School.Domain;
        _crestPath = state.School.CrestPath;

        InitializeSwatches();
    }

    private void InitializeSwatches()
    {
        Swatches.Clear();

        // Primary
        AddSwatch(new ColorSwatchItem { HexCode = _state.Theme.Primary, Label = "Utama" });

        // Accent
        AddSwatch(new ColorSwatchItem { HexCode = _state.Theme.Accent, Label = "Aksen" });

        // Class colours
        int idx = 1;
        foreach (var col in _state.Theme.ClassColours)
        {
            if (Swatches.Count >= 8) break;
            AddSwatch(new ColorSwatchItem { HexCode = col, Label = $"Kelas {idx++}" });
        }
    }

    private void AddSwatch(ColorSwatchItem swatch)
    {
        swatch.Recalculate();
        swatch.PropertyChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(CanProceed));
            OnPropertyChanged(nameof(ValidationMessage));
        };
        Swatches.Add(swatch);
    }

    public void ApplyPreset(ThemePresetItem preset)
    {
        if (Swatches.Count >= 2)
        {
            Swatches[0].HexCode = preset.Primary;
            Swatches[1].HexCode = preset.Accent;
            for (int i = 0; i < preset.ClassColours.Length && i + 2 < Swatches.Count; i++)
            {
                Swatches[i + 2].HexCode = preset.ClassColours[i];
            }
        }
        OnPropertyChanged(nameof(CanProceed));
        OnPropertyChanged(nameof(ValidationMessage));
    }

    public void ClearCrest()
    {
        CrestPath = null;
    }

    public void UpdateSwatchColor(int index, string newHex)
    {
        if (index >= 0 && index < Swatches.Count)
        {
            Swatches[index].HexCode = newHex;
        }
    }

    public bool ValidateInternal(out string message)
    {
        if (string.IsNullOrWhiteSpace(SchoolCode))
        {
            message = "Kod Sekolah diperlukan (contoh: ABC1234).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SchoolName))
        {
            message = "Nama Sekolah diperlukan.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Domain))
        {
            message = "Domain E-mel MOE diperlukan (contoh: moe-dl.edu.my).";
            return false;
        }

        // FR-S1.4: Check contrast for all swatches
        var failingSwatch = Swatches.FirstOrDefault(s => !s.IsPass);
        if (failingSwatch != null)
        {
            message = $"Warna '{failingSwatch.Label}' ({failingSwatch.HexCode}) gagal kontras ({failingSwatch.ContrastRatio:F1}:1 < 4.5:1).";
            return false;
        }

        message = "";
        return true;
    }

    public void SaveToState()
    {
        _state.School.Code = SchoolCode.Trim().ToUpperInvariant();
        _state.School.Name = SchoolName.Trim();
        _state.School.Motto = string.IsNullOrWhiteSpace(Motto) ? null : Motto.Trim();
        _state.School.Domain = Domain.Trim().ToLowerInvariant();
        _state.School.CrestPath = CrestPath;

        if (Swatches.Count >= 2)
        {
            _state.Theme.Primary = Swatches[0].HexCode;
            _state.Theme.Accent = Swatches[1].HexCode;
            _state.Theme.ClassColours = Swatches.Skip(2).Select(s => s.HexCode).ToList();
        }
    }
}

