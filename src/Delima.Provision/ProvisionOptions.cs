namespace Delima.Provision;

/// <summary>
/// Command-line options for Delima.Provision per Technical Architecture §10.
/// </summary>
public sealed class ProvisionOptions
{
    public string? PackPath { get; set; }
    public bool Quiet { get; set; }
    public bool PassphraseStdin { get; set; }
    public string? TargetDirectory { get; set; }
    public string? ChecklistPath { get; set; }
    public string PupilAccount { get; set; } = "Murid";
    public bool ApplyAcls { get; set; } = true;
    public bool DryRun { get; set; }
    public bool ShowHelp { get; set; }

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
              --quiet, -q                 Mod senyap (tiada prompt, kod keluar 0 jika berjaya)
              --passphrase-stdin          Baca kata laluan pentadbir daripada stdin (bukan argumen baris perintah)
              --target-dir, -t <laluan>   Direktori storan setempat (lalai: %ProgramData%\DELIMa Launcher)
              --checklist, -c <laluan>    Laluan ke fail senarai semak makmal (lab_checklist.csv)
              --pupil-account, -a <nama>  Nama akaun murid Windows untuk ACL (lalai: Murid)
              --dry-run                   Nyahsulit dan sahkan pakej tanpa menulis storan ke cakera
              --help, -h                  Papar bantuan ini

            Contoh / Examples:
              # Interaktif dari pemacu USB / Interactive from USB:
              Delima.Provision.exe --pack E:\school.dlmpack

              # Skrip PDQ / GPO senyap melalui stdin / Silent scripting via stdin:
              type pass.txt | Delima.Provision.exe --quiet --pack "\\share\dlm\school.dlmpack" --passphrase-stdin
            """;
    }
}
