namespace Delima.Provision;

/// <summary>
/// Command-line and GUI options for Delima.Provision per Technical Architecture §10.
/// </summary>
public sealed class ProvisionOptions
{
    public string? PackPath { get; set; }
    public bool Quiet { get; set; }
    public bool PassphraseStdin { get; set; }
    public string? Passphrase { get; set; }
    public string? TargetDirectory { get; set; }
    public string? ChecklistPath { get; set; }
    public string PupilAccount { get; set; } = "Murid";
    public bool ApplyAcls { get; set; } = true;
    public bool DryRun { get; set; }
    public bool ShowHelp { get; set; }

    // Streamlined Installation & Shortcut Options
    public bool InstallLauncher { get; set; } = true;
    public bool CreateDesktopShortcut { get; set; } = true;
    public bool EnableKioskStartup { get; set; } = false;
    public bool ApplyBrowserPolicies { get; set; } = false; // Strictly opt-in per PRD §8.3
    public bool RemoveBrowserPolicies { get; set; } = false;
    public string PreferredBrowser { get; set; } = "chrome"; // "chrome", "edge", "auto"
    public string? LauncherSourcePath { get; set; }
    public string? InstallDestinationPath { get; set; }

    /// <summary>
    /// Checks if any command line arguments were provided.
    /// </summary>
    public static bool HasCommandLineArgs(string[] args)
    {
        return args != null && args.Length > 0;
    }

    /// <summary>
    /// Parses command line arguments into ProvisionOptions.
    /// </summary>
    public static ProvisionOptions Parse(string[] args)
    {
        var options = new ProvisionOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-?", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/?", StringComparison.OrdinalIgnoreCase))
            {
                options.ShowHelp = true;
                return options;
            }
            else if (arg.Equals("--quiet", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("-q", StringComparison.OrdinalIgnoreCase))
            {
                options.Quiet = true;
            }
            else if (arg.Equals("--passphrase-stdin", StringComparison.OrdinalIgnoreCase))
            {
                options.PassphraseStdin = true;
            }
            else if (arg.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                options.DryRun = true;
            }
            else if (arg.Equals("--no-acl", StringComparison.OrdinalIgnoreCase))
            {
                options.ApplyAcls = false;
            }
            else if (arg.Equals("--no-install", StringComparison.OrdinalIgnoreCase))
            {
                options.InstallLauncher = false;
            }
            else if (arg.Equals("--no-shortcut", StringComparison.OrdinalIgnoreCase))
            {
                options.CreateDesktopShortcut = false;
            }
            else if (arg.Equals("--kiosk", StringComparison.OrdinalIgnoreCase))
            {
                options.EnableKioskStartup = true;
            }
            else if (arg.Equals("--apply-policy", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--policy", StringComparison.OrdinalIgnoreCase))
            {
                options.ApplyBrowserPolicies = true;
            }
            else if (arg.Equals("--no-policy", StringComparison.OrdinalIgnoreCase))
            {
                options.ApplyBrowserPolicies = false;
            }
            else if (arg.Equals("--remove-policy", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--remove-policies", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--clean-policies", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--unblock-browser", StringComparison.OrdinalIgnoreCase))
            {
                options.RemoveBrowserPolicies = true;
            }
            else if ((arg.Equals("--browser", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("-b", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                options.PreferredBrowser = args[++i].ToLowerInvariant();
            }
            else if ((arg.Equals("--pack", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("-p", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                options.PackPath = args[++i];
            }
            else if ((arg.Equals("--target-dir", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("-t", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                options.TargetDirectory = args[++i];
            }
            else if ((arg.Equals("--install-dir", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                options.InstallDestinationPath = args[++i];
            }
            else if ((arg.Equals("--checklist", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("-c", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                options.ChecklistPath = args[++i];
            }
            else if ((arg.Equals("--pupil-account", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("-a", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                options.PupilAccount = args[++i];
            }
            else if (!arg.StartsWith('-') && string.IsNullOrEmpty(options.PackPath))
            {
                // Positional argument for pack path
                options.PackPath = arg;
            }
        }

        return options;
    }

    public static string GetHelpText()
    {
        return """
            DELIMa Provisioning Tool v2.0
            Kegunaan / Usage:
              Delima.Provision [pilihan / options]

            Pilihan / Options:
              --pack, -p <laluan>         Laluan ke fail school.dlmpack (USB atau perkongsian fail / UNC path)
              --browser, -b <nama>        Pilihan pelayar: chrome, edge, atau auto (lalai: chrome)
              --quiet, -q                 Mod senyap (tiada prompt, kod keluar 0 jika berjaya)
              --passphrase-stdin          Baca kata laluan pentadbir daripada stdin (bukan argumen baris perintah)
              --target-dir, -t <laluan>   Direktori storan setempat (lalai: %ProgramData%\DELIMa Launcher)
              --install-dir <laluan>      Direktori pemasangan aplikasi (lalai: %ProgramFiles%\DELIMa Launcher)
              --no-install                Jangan pasang/salin Delima.Launcher.exe ke Program Files
              --no-shortcut               Jangan cipta pintasan pada Desktop
              --kiosk                     Daftar autostart semasa log masuk (Mod Kiosk)
              --apply-policy              Kuatkuasakan sekatan pelayar makmal (sekat semua URL luar di seluruh PC)
              --remove-policy             Padam sekatan pelayar makmal (nyahkan dasar sekatan HKLM Chrome & Edge)
              --no-policy                 Jangan kuatkuasakan dasar keselamatan pelayar makmal
              --checklist, -c <laluan>    Laluan ke fail senarai semak makmal (lab_checklist.csv)
              --pupil-account, -a <nama>  Nama akaun murid Windows untuk ACL (lalai: Murid)
              --dry-run                   Nyahsulit dan sahkan pakej tanpa menulis storan ke cakera
              --help, -h                  Papar bantuan ini

            Contoh / Examples:
              # Interaktif (Antaramuka GUI secara automatik jika dilancarkan tanpa argumen)
              Delima.Provision.exe

              # Mod baris perintah menggunakan Google Chrome:
              Delima.Provision.exe --pack E:\school.dlmpack --browser chrome

              # Buka sekatan pelayar (unblock browser):
              Delima.Provision.exe --remove-policy

              # Skrip PDQ / GPO senyap melalui stdin:
              type pass.txt | Delima.Provision.exe --quiet --pack "\\share\dlm\school.dlmpack" --passphrase-stdin
            """;
    }
}
