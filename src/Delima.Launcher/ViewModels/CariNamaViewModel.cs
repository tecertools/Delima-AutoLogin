using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Delima.Core.Roster;
using Delima.Launcher.Theming;

namespace Delima.Launcher.ViewModels;

/// <summary>
/// ViewModel for Skrin 2: Cari Nama.
/// Computes adaptive grid columns, calling name display labels, and live search filtering.
/// </summary>
public sealed partial class CariNamaViewModel : ObservableObject
{
    private readonly Action _onBackRequested;
    private readonly Action<Student> _onStudentSelected;
    private readonly Action _onMissingStudentRequested;
    private readonly List<Student> _classStudents;
    private readonly List<PupilCardViewModel> _allPupilCards = [];

    [ObservableProperty]
    private ClassInfo _currentClass;

    [ObservableProperty]
    private string _className;

    [ObservableProperty]
    private string _schoolCode;

    [ObservableProperty]
    private Brush _classColourBrush;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchQuery))]
    private string _searchQuery = "";

    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchQuery);

    [RelayCommand]
    private void ClearSearch() => SearchQuery = "";

    [ObservableProperty]
    private int _columnCount = 7;

    [ObservableProperty]
    private int _cardWidthPx = 137;

    [ObservableProperty]
    private int _cardHeightPx = 99;

    [ObservableProperty]
    private string _updatedDateText;

    public ObservableCollection<PupilCardViewModel> FilteredPupilCards { get; } = [];

    public CariNamaViewModel(
        School school,
        ClassInfo classInfo,
        IReadOnlyList<Student> studentsInClass,
        Action onBackRequested,
        Action<Student> onStudentSelected,
        Action onMissingStudentRequested,
        DateTimeOffset? updatedDate = null)
    {
        _currentClass = classInfo;
        _className = $"Tahun {classInfo.Grade} {classInfo.Name}";
        _schoolCode = string.IsNullOrWhiteSpace(school.Code) ? "SK" : school.Code;
        _onBackRequested = onBackRequested;
        _onStudentSelected = onStudentSelected;
        _onMissingStudentRequested = onMissingStudentRequested;
        _classStudents = [.. studentsInClass.Where(s => s.Active)];

        DateTimeOffset date = updatedDate ?? DateTimeOffset.UtcNow;
        _updatedDateText = $"Senarai dikemas kini {date:d MMMM yyyy}";

        // Get class colour brush
        string colourKey = Tokens.GetClassColourKey(classInfo.ColourIndex);
        _classColourBrush = ThemeBuilder.CreateFrozenBrush(
            ThemeBuilder.DefaultClassColours[Math.Clamp(classInfo.ColourIndex, 0, ThemeBuilder.DefaultClassColours.Length - 1)]);

        // Calculate initial grid dimensions (Technical Architecture §5 & §6.3)
        var gridDim = GridCalculator.Calculate(_classStudents.Count);
        _columnCount = gridDim.Columns;
        _cardWidthPx = gridDim.CardWidthPx;
        _cardHeightPx = gridDim.CardHeightPx;

        // Build pupil card ViewModels
        var displayNames = DisplayNameCalculator.ComputeDisplayNames(_classStudents);
        foreach (var s in _classStudents)
        {
            string label = displayNames.TryGetValue(s.Id, out var dn) ? dn : s.Name;
            _allPupilCards.Add(new PupilCardViewModel(s, label, _classColourBrush));
        }

        RefreshFilteredCards();
    }

    partial void OnSearchQueryChanged(string value)
    {
        RefreshFilteredCards();
    }

    private void RefreshFilteredCards()
    {
        FilteredPupilCards.Clear();

        IEnumerable<PupilCardViewModel> matching;
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            matching = _allPupilCards;
        }
        else
        {
            matching = _allPupilCards.Where(c => c.Student != null && c.Student.MatchesSearch(SearchQuery));
        }

        foreach (var card in matching)
        {
            FilteredPupilCards.Add(card);
        }

        // Always append the escape hatch card ("Nama saya tiada") as the final item
        FilteredPupilCards.Add(PupilCardViewModel.CreateMissingEscapeCard());
    }

    [RelayCommand]
    private void SelectCard(PupilCardViewModel card)
    {
        if (card.IsMissingEscapeCard)
        {
            _onMissingStudentRequested();
        }
        else if (card.Student != null)
        {
            _onStudentSelected(card.Student);
        }
    }

    [RelayCommand]
    private void Back()
    {
        _onBackRequested();
    }
}
