using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace Delima.Provision.ViewModels;

public sealed partial class ProvisionViewModel : ObservableObject
{
    private readonly ProvisionOptions _options;

    [ObservableProperty]
    private string _packPath = "";

    [ObservableProperty]
    private bool _packFound;

    [ObservableProperty]
    private string _passphrase = "";

    [ObservableProperty]
    private bool _isPasswordVisible;

    [ObservableProperty]
    private bool _installLauncher = true;

    [ObservableProperty]
    private bool _createDesktopShortcut = true;

    [ObservableProperty]
    private bool _enableKioskStartup = false;

    [ObservableProperty]
    private bool _applyBrowserPolicies = false; // Strictly opt-in per PRD §8.3

    [ObservableProperty]
    private string _selectedBrowser = "Google Chrome (Disyorkan)";

    public ObservableCollection<string> BrowserOptionsList { get; } =
    [
        "Google Chrome (Disyorkan)",
        "Microsoft Edge",
        "Automatik"
    ];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _currentStepDescription = "";

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private string _resultSchoolName = "";

    [ObservableProperty]
    private string _resultSchoolCode = "";

    [ObservableProperty]
    private int _resultStudentCount;

    [ObservableProperty]
    private string _resultStoreDate = "";

    [ObservableProperty]
    private string _resultBrowserName = "";

    [ObservableProperty]
    private string? _resultLauncherPath;

    public ProvisionViewModel(ProvisionOptions options)
    {
        _options = options;
        InitializeFromOptions();
    }

    private void InitializeFromOptions()
    {
        InstallLauncher = _options.InstallLauncher;
        CreateDesktopShortcut = _options.CreateDesktopShortcut;
        EnableKioskStartup = _options.EnableKioskStartup;
        ApplyBrowserPolicies = _options.ApplyBrowserPolicies;
        SelectedBrowser = _options.PreferredBrowser switch
        {
            "edge" => "Microsoft Edge",
            "auto" => "Automatik",
            _ => "Google Chrome (Disyorkan)"
        };

        AutoDetectPack();
    }

    public string GetBrowserKey()
    {
        return SelectedBrowser switch
        {
            "Microsoft Edge" => "edge",
            "Automatik" => "auto",
            _ => "chrome"
        };
    }

    public void AutoDetectPack()
    {
        string? resolved = ProvisionEngine.ResolvePackPath(_options);
        if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
        {
            PackPath = resolved;
            PackFound = true;
            HasError = false;
            ErrorMessage = "";
        }
        else
        {
            PackFound = false;
        }
    }

    [RelayCommand]
    private void BrowsePack()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Pilih Fail Bungkusan DELIMa (*.dlmpack)",
            Filter = "DELIMa Master Pack (*.dlmpack)|*.dlmpack|Semua Fail (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            PackPath = dialog.FileName;
            PackFound = true;
            HasError = false;
            ErrorMessage = "";
        }
    }

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    [RelayCommand]
    private async Task ExecuteProvisioningAsync()
    {
        if (string.IsNullOrWhiteSpace(PackPath) || !File.Exists(PackPath))
        {
            HasError = true;
            ErrorMessage = "Sila pilih fail bungkusan 'school.dlmpack' yang sah.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Passphrase))
        {
            HasError = true;
            ErrorMessage = "Sila masukkan kata laluan pentadbir yang telah ditetapkan semasa membina pakej.";
            return;
        }

        IsBusy = true;
        HasError = false;
        ErrorMessage = "";
        ProgressPercentage = 5;
        CurrentStepDescription = "Memulakan proses persediaan...";

        string browserKey = GetBrowserKey();

        var execOptions = new ProvisionOptions
        {
            PackPath = PackPath,
            Passphrase = Passphrase,
            InstallLauncher = InstallLauncher,
            CreateDesktopShortcut = CreateDesktopShortcut,
            EnableKioskStartup = EnableKioskStartup,
            ApplyBrowserPolicies = ApplyBrowserPolicies,
            PreferredBrowser = browserKey,
            Quiet = true,
            PupilAccount = _options.PupilAccount,
            ApplyAcls = _options.ApplyAcls
        };

        try
        {
            var result = await Task.Run(() =>
            {
                return ProvisionEngine.Execute(
                    execOptions,
                    progressCallback: (step, desc) =>
                    {
                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            CurrentStepDescription = desc;
                            ProgressPercentage = step switch
                            {
                                1 => 15,
                                2 => 30,
                                3 => 50,
                                4 => 70,
                                5 => 90,
                                6 => 100,
                                _ => 100
                            };
                        });
                    });
            });

            if (result.Success)
            {
                IsSuccess = true;
                ResultSchoolName = result.SchoolName ?? "Sekolah";
                ResultSchoolCode = result.SchoolCode ?? "";
                ResultStudentCount = result.StudentCount;
                ResultStoreDate = result.StoreGeneratedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                ResultBrowserName = browserKey switch
                {
                    "edge" => "Microsoft Edge",
                    "auto" => "Automatik",
                    _ => "Google Chrome"
                };
                ResultLauncherPath = result.InstalledLauncherPath;
            }
            else
            {
                HasError = true;
                ErrorMessage = result.ErrorMessage ?? "Ralat tidak diketahui semasa menyediakan komputer ini.";
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Ralat tidak dijangka: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void LaunchLauncher()
    {
        try
        {
            string defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "DELIMa Launcher",
                "Delima.Launcher.exe");

            string exeToStart = !string.IsNullOrEmpty(ResultLauncherPath) && File.Exists(ResultLauncherPath)
                ? ResultLauncherPath
                : defaultPath;

            if (File.Exists(exeToStart))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exeToStart,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show(
                    $"Fail pelancar tidak dijumpai di '{exeToStart}'.",
                    "Ralat Melancarkan",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Gagal melancarkan aplikasi: {ex.Message}",
                "Ralat",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void RemoveBrowserPolicies()
    {
        try
        {
            var chromeRes = Delima.Win32.BrowserPolicyConfigurator.RemovePolicies(Delima.Win32.BrowserKind.Chrome);
            var edgeRes = Delima.Win32.BrowserPolicyConfigurator.RemovePolicies(Delima.Win32.BrowserKind.Edge);

            if (chromeRes.Success && edgeRes.Success)
            {
                MessageBox.Show(
                    "Semua sekatan dasar pelayar Google Chrome dan Microsoft Edge telah berjaya dipadamkan.\n\nPelayar kini boleh melayari semua laman web seperti biasa.",
                    "Sekatan Pelayar Dipadamkan",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                string err = $"{chromeRes.ErrorMessage} {edgeRes.ErrorMessage}".Trim();
                MessageBox.Show(
                    $"Gagal memadamkan sebahagian dasar pelayar: {err}\n\nPastikan anda menjalankan aplikasi ini sebagai Pentadbir (Administrator).",
                    "Amaran",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ralat memadamkan dasar pelayar: {ex.Message}",
                "Ralat",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void CloseApplication()
    {
        Application.Current?.Shutdown();
    }
}
