using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Delima.Core.Roster;

namespace Delima.Launcher.ViewModels;

/// <summary>
/// ViewModel representing a pupil card on the Cari Nama grid or the escape hatch card.
/// </summary>
public sealed partial class PupilCardViewModel : ObservableObject
{
    public Student? Student { get; }
    public string DisplayName { get; }
    public string AvatarKey { get; }
    public bool IsMissingEscapeCard { get; }
    public Brush? ClassColourBrush { get; }

    public string AccessibleName => IsMissingEscapeCard
        ? "Nama saya tiada. Panggil cikgu untuk bantuan."
        : $"Nama murid: {DisplayName}. Gambar avatar: {AvatarKey}.";

    public PupilCardViewModel(
        Student student,
        string displayName,
        Brush? classColourBrush = null)
    {
        Student = student;
        DisplayName = displayName;
        AvatarKey = student.Avatar;
        IsMissingEscapeCard = false;
        ClassColourBrush = classColourBrush;
    }

    /// <summary>
    /// Factory for the "Nama saya tiada" escape hatch card.
    /// </summary>
    public static PupilCardViewModel CreateMissingEscapeCard()
    {
        return new PupilCardViewModel();
    }

    private PupilCardViewModel()
    {
        Student = null;
        DisplayName = "Nama saya tiada";
        AvatarKey = "missing";
        IsMissingEscapeCard = true;
        ClassColourBrush = null;
    }
}
