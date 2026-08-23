using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Delima.Admin.Models;
using Delima.Core.Store;

namespace Delima.Admin.ViewModels;

public sealed partial class Step6SettingsViewModel : ObservableObject
{
    private readonly AdminWizardState _state;

    public ObservableCollection<DestinationConfig> Destinations { get; } = [];

    [ObservableProperty]
    private string _newDestLabel = "";

    [ObservableProperty]
    private string _newDestUrl = "";

    [ObservableProperty]
    private int _idleResetSeconds = 600;

    [ObservableProperty]
    private string _teacherPinPolicy = "4 digit, kunci selepas 5 percubaan";

    [ObservableProperty]
    private string _selectedLanguage = "Bahasa Melayu";

    [ObservableProperty]
    private int _storeMaxAgeDays = 30;

    [ObservableProperty]
    private string _selectedBrowser = "Automatik (Utamakan Edge)";

    public ObservableCollection<string> PinPolicyOptions { get; } =
    [
        "4 digit, kunci selepas 5 percubaan",
        "6 digit, kunci selepas 3 percubaan"
    ];

    public ObservableCollection<string> LanguageOptions { get; } =
    [
        "Bahasa Melayu",
        "English"
    ];

    public ObservableCollection<string> BrowserOptions { get; } =
    [
        "Automatik (Utamakan Edge)",
        "Microsoft Edge",
        "Google Chrome"
    ];

    public bool CanProceed => Destinations.Count > 0;

    public Step6SettingsViewModel(AdminWizardState state)
    {
        _state = state;
        _idleResetSeconds = state.Config.IdleResetSeconds;
        _storeMaxAgeDays = state.Config.StoreMaxAgeDays;
        _selectedBrowser = state.Config.PreferredBrowser switch
        {
            "edge" => "Microsoft Edge",
            "chrome" => "Google Chrome",
            _ => "Automatik (Utamakan Edge)"
        };

        foreach (var dest in state.Config.Destinations)
        {
            Destinations.Add(new DestinationConfig
            {
                Id = dest.Id,
                Label = dest.Label,
                Url = dest.Url
            });
        }

        if (Destinations.Count == 0)
        {
            Destinations.Add(new DestinationConfig { Id = "delima", Label = "DELIMa 3.0", Url = "https://d3.delima.edu.my/" });
            Destinations.Add(new DestinationConfig { Id = "classroom", Label = "Google Classroom", Url = "https://classroom.google.com/" });
        }

        Destinations.CollectionChanged += (s, e) => OnPropertyChanged(nameof(CanProceed));
    }

    public void AddDestination()
    {
        if (string.IsNullOrWhiteSpace(NewDestLabel) || string.IsNullOrWhiteSpace(NewDestUrl))
            return;

        string id = NewDestLabel.Trim().ToLowerInvariant().Replace(" ", "_");
        Destinations.Add(new DestinationConfig
        {
            Id = id,
            Label = NewDestLabel.Trim(),
            Url = NewDestUrl.Trim()
        });

        NewDestLabel = "";
        NewDestUrl = "";
        OnPropertyChanged(nameof(CanProceed));
    }

    public void RemoveDestination(DestinationConfig item)
    {
        Destinations.Remove(item);
        OnPropertyChanged(nameof(CanProceed));
    }

    public void SaveToState()
    {
        _state.Config.Destinations = Destinations.ToList();
        _state.Config.IdleResetSeconds = IdleResetSeconds;
        _state.Config.StoreMaxAgeDays = StoreMaxAgeDays;
        _state.Config.PreferredBrowser = SelectedBrowser switch
        {
            "Microsoft Edge" => "edge",
            "Google Chrome" => "chrome",
            _ => "auto"
        };
    }
}
