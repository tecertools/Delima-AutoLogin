using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Delima.Core.Roster;

namespace Delima.Launcher.ViewModels;

/// <summary>
/// ViewModel for Skrin 1: Pilih Kelas.
/// Handles Tahun/Kelas selection, "Kelas Terakhir" shortcut, and language toggle.
/// </summary>
public sealed partial class PilihKelasViewModel : ObservableObject
{
    private readonly Action<ClassInfo> _onClassConfirmed;
    private readonly Action? _onTeacherModeRequested;
    private readonly List<ClassInfo> _allClasses;

    [ObservableProperty]
    private string _schoolName;

    [ObservableProperty]
    private string _schoolMotto;

    [ObservableProperty]
    private string _schoolCode;

    [ObservableProperty]
    private ClassInfo? _lastClass;

    [ObservableProperty]
    private bool _hasLastClass;

    [ObservableProperty]
    private string _lastClassDisplayText = "";

    [ObservableProperty]
    private int? _selectedTahun;

    [ObservableProperty]
    private bool _isKelasDropdownEnabled;

    [ObservableProperty]
    private ClassInfo? _selectedClass;

    [ObservableProperty]
    private bool _canProceed;

    [ObservableProperty]
    private string _selectedLanguage = "BM";

    [ObservableProperty]
    private string _updatedDateText;

    public ObservableCollection<int> AvailableTahun { get; } = [];
    public ObservableCollection<ClassInfo> AvailableClasses { get; } = [];

    public PilihKelasViewModel(
        School school,
        IReadOnlyList<ClassInfo> classes,
        ClassInfo? lastClass,
        Action<ClassInfo> onClassConfirmed,
        Action? onTeacherModeRequested = null,
        DateTimeOffset? updatedDate = null)
    {
        _onClassConfirmed = onClassConfirmed;
        _onTeacherModeRequested = onTeacherModeRequested;
        _allClasses = [.. classes];

        _schoolName = school.Name;
        _schoolMotto = school.Motto ?? "Berilmu Berdisiplin";
        _schoolCode = string.IsNullOrWhiteSpace(school.Code) ? "SK" : school.Code;

        DateTimeOffset date = updatedDate ?? DateTimeOffset.UtcNow;
        _updatedDateText = $"Senarai dikemas kini {date:d MMMM yyyy}";

        // Setup Last Class shortcut
        _lastClass = lastClass;
        _hasLastClass = lastClass != null;
        if (lastClass != null)
        {
            _lastClassDisplayText = $"Tahun {lastClass.Grade} {lastClass.Name}";
        }

        // Setup available grades
        var distinctGrades = _allClasses.Select(c => c.Grade).Distinct().OrderBy(g => g).ToList();
        if (distinctGrades.Count == 0)
        {
            distinctGrades = [1, 2, 3, 4, 5, 6];
        }

        foreach (int g in distinctGrades)
        {
            AvailableTahun.Add(g);
        }
    }

    partial void OnSelectedTahunChanged(int? value)
    {
        AvailableClasses.Clear();
        SelectedClass = null;

        if (value.HasValue)
        {
            IsKelasDropdownEnabled = true;
            var matching = _allClasses.Where(c => c.Grade == value.Value).OrderBy(c => c.Name);
            foreach (var c in matching)
            {
                AvailableClasses.Add(c);
            }
        }
        else
        {
            IsKelasDropdownEnabled = false;
        }

        UpdateCanProceed();
    }

    partial void OnSelectedClassChanged(ClassInfo? value)
    {
        UpdateCanProceed();
    }

    private void UpdateCanProceed()
    {
        CanProceed = SelectedClass != null;
    }

    [RelayCommand]
    private void Proceed()
    {
        if (SelectedClass != null)
        {
            _onClassConfirmed(SelectedClass);
        }
    }

    [RelayCommand]
    private void SelectLastClass()
    {
        if (LastClass != null)
        {
            _onClassConfirmed(LastClass);
        }
    }

    [RelayCommand]
    private void ToggleLanguage()
    {
        SelectedLanguage = SelectedLanguage == "BM" ? "EN" : "BM";
    }

    [RelayCommand]
    private void OpenTeacherMode()
    {
        _onTeacherModeRequested?.Invoke();
    }
}
