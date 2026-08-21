using CommunityToolkit.Mvvm.ComponentModel;
using Delima.Admin.Models;
using Delima.Core.Audit;

namespace Delima.Admin.ViewModels;

public sealed partial class FirstRunDisclaimerViewModel : ObservableObject
{
    private readonly AdminWizardState _state;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    private bool _hasReadNotice;

    public bool CanProceed => HasReadNotice;

    public FirstRunDisclaimerViewModel(AdminWizardState state)
    {
        _state = state;
    }

    public void Acknowledge()
    {
        _state.HasAcknowledgedDisclaimer = true;

        AuditLogger.RecordEntry(new AuditLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Event = "disclaimer_acknowledged",
            Outcome = "SUCCESS",
            SchoolCode = _state.School.Code,
            Details = "T0.1 responsibility statement acknowledged by administrator on first run."
        });
    }
}
