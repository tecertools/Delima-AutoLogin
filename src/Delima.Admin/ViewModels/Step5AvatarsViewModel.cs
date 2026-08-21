using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Delima.Admin.Models;
using Delima.Core.Audit;

namespace Delima.Admin.ViewModels;

public sealed partial class AvatarAssignmentItem : ObservableObject
{
    public string StudentId { get; init; } = "";
    public string StudentName { get; init; } = "";
    public string ClassName { get; init; } = "";

    [ObservableProperty]
    private string _avatar = "kucing";
}

public sealed partial class Step5AvatarsViewModel : ObservableObject
{
    private readonly AdminWizardState _state;

    private static readonly string[] StandardAvatars =
    [
        "kucing", "ikan", "bintang", "burung", "kereta", "bunga", "rama-rama", "epal",
        "gajah", "pokok", "bola", "awan", "matahari", "buku", "pensel", "kapal"
    ];

    public ObservableCollection<AvatarAssignmentItem> AvatarItems { get; } = [];
    public ObservableCollection<string> ClassNames { get; } = [];

    [ObservableProperty]
    private string? _selectedClassFilter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWarning))]
    private bool _picturePasswordRequired = true;

    public bool ShowWarning => !PicturePasswordRequired;

    public bool CanProceed => true;

    public Step5AvatarsViewModel(AdminWizardState state)
    {
        _state = state;
        _picturePasswordRequired = state.Config.PicturePasswordRequired;

        InitializeAvatars();
    }

    public void InitializeAvatars()
    {
        AvatarItems.Clear();
        ClassNames.Clear();

        var classes = _state.RosterStudents.Select(s => s.ClassName).Distinct().OrderBy(c => c).ToList();
        foreach (var c in classes)
            ClassNames.Add(c);

        if (ClassNames.Count > 0)
            SelectedClassFilter = ClassNames[0];

        // Group by class and assign unique avatars per class
        foreach (var group in _state.RosterStudents.GroupBy(s => s.ClassName))
        {
            int avatarIdx = 0;
            foreach (var student in group)
            {
                string avatar = StandardAvatars[avatarIdx % StandardAvatars.Length];
                avatarIdx++;

                AvatarItems.Add(new AvatarAssignmentItem
                {
                    StudentId = student.Id,
                    StudentName = student.FullName,
                    ClassName = student.ClassName,
                    Avatar = avatar
                });
            }
        }
    }

    public void CycleAvatar(AvatarAssignmentItem item)
    {
        int currIdx = Array.IndexOf(StandardAvatars, item.Avatar);
        int nextIdx = (currIdx + 1) % StandardAvatars.Length;
        item.Avatar = StandardAvatars[nextIdx];
    }

    public void TogglePicturePasswordPolicy(bool isRequired)
    {
        PicturePasswordRequired = isRequired;
        _state.Config.PicturePasswordRequired = isRequired;

        if (!isRequired)
        {
            AuditLogger.RecordEntry(new AuditLogEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Event = "picture_password_policy_disabled",
                Outcome = "WARNING",
                SchoolCode = _state.School.Code,
                WindowsUser = Environment.UserName,
                Details = "Administrator disabled picture password requirement, re-introducing single-factor collision risk (B1)."
            });
        }
    }

    public void SaveToState()
    {
        _state.Config.PicturePasswordRequired = PicturePasswordRequired;
    }
}
