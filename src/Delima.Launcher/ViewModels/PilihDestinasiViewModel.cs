using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Delima.Core.Roster;
using Delima.Core.Store;

namespace Delima.Launcher.ViewModels;

/// <summary>
/// ViewModel for Skrin 4: Pilih Destinasi ("Hai, {Nama}! Nak pergi ke mana?").
/// Allows pupils to choose which learning portal to launch into upon successful picture password verification.
/// </summary>
public sealed partial class PilihDestinasiViewModel : ObservableObject
{
    private readonly Action<DestinationConfig> _onDestinationSelected;
    private readonly Action _onBackRequested;
    private readonly Action? _onTeacherModeRequested;

    public School School { get; }
    public Student Student { get; }
    public ClassInfo ClassInfo { get; }
    public ICredential Credential { get; }

    [ObservableProperty]
    private string _schoolName;

    [ObservableProperty]
    private string _schoolMotto;

    [ObservableProperty]
    private string _schoolCode;

    [ObservableProperty]
    private string _pupilName;

    [ObservableProperty]
    private string _pupilAvatar;

    [ObservableProperty]
    private string _className;

    [ObservableProperty]
    private string _greetingTitle;

    public ObservableCollection<DestinationCardViewModel> AvailableDestinations { get; } = [];

    public PilihDestinasiViewModel(
        School school,
        ClassInfo classInfo,
        Student student,
        ICredential credential,
        IReadOnlyList<DestinationConfig>? destinations,
        Action<DestinationConfig> onDestinationSelected,
        Action onBackRequested,
        Action? onTeacherModeRequested = null)
    {
        School = school;
        ClassInfo = classInfo;
        Student = student;
        Credential = credential;
        _onDestinationSelected = onDestinationSelected;
        _onBackRequested = onBackRequested;
        _onTeacherModeRequested = onTeacherModeRequested;

        _schoolName = school.Name;
        _schoolMotto = school.Motto ?? "Berilmu Berdisiplin";
        _schoolCode = string.IsNullOrWhiteSpace(school.Code) ? "SK" : school.Code;

        _pupilName = student.DisplayName;
        _pupilAvatar = student.Avatar;
        _className = classInfo.Name;
        _greetingTitle = $"Hai, {student.DisplayName}! Nak pergi ke mana?";

        var destList = (destinations != null && destinations.Count > 0)
            ? destinations
            : GetDefaultDestinations();

        foreach (var dest in destList)
        {
            AvailableDestinations.Add(new DestinationCardViewModel(dest));
        }
    }

    public static IReadOnlyList<DestinationConfig> GetDefaultDestinations() =>
    [
        new DestinationConfig { Id = "delima", Label = "DELIMa 3.0 Portal", Url = "https://d3.delima.edu.my/landing" },
        new DestinationConfig { Id = "classroom", Label = "Google Classroom", Url = "https://classroom.google.com/" },
        new DestinationConfig { Id = "ains", Label = "AINS (NILAM)", Url = "https://ains.moe.gov.my/" },
        new DestinationConfig { Id = "canva", Label = "Canva for Education", Url = "https://www.canva.com/education/" }
    ];

    [RelayCommand]
    private void SelectDestination(DestinationCardViewModel? card)
    {
        if (card != null)
        {
            _onDestinationSelected(card.Config);
        }
    }

    [RelayCommand]
    private void Back()
    {
        _onBackRequested();
    }

    [RelayCommand]
    private void OpenTeacherMode()
    {
        _onTeacherModeRequested?.Invoke();
    }
}
