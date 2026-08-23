using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Delima.Admin.Models;

namespace Delima.Admin.ViewModels;

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

        if (Swatches.Count >= 2)
        {
            _state.Theme.Primary = Swatches[0].HexCode;
            _state.Theme.Accent = Swatches[1].HexCode;
            _state.Theme.ClassColours = Swatches.Skip(2).Select(s => s.HexCode).ToList();
        }
    }
}
