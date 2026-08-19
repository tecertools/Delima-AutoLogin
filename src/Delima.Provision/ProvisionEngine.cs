using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Delima.Core.Store;
using Delima.Win32.Store;

namespace Delima.Provision;

/// <summary>
/// Result of a provisioning execution.
/// </summary>
public sealed class ProvisionResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SchoolCode { get; set; }
    public string? DeviceId { get; set; }
    public int StudentCount { get; set; }
    public DateTimeOffset StoreGeneratedAt { get; set; }
    public bool ChecklistUpdated { get; set; }
}

/// <summary>
/// Implements the 7-step provisioning workflow specified in Technical Architecture §10.
/// </summary>
public static class ProvisionEngine
{
    public const int ExitSuccess = 0;
    public const int ExitInvalidArgsOrNotFound = 1;
    public const int ExitAuthenticationFailed = 2;
    public const int ExitStoreWriteFailed = 3;

    /// <summary>
    /// Executes the 7-step provisioning workflow.
    /// </summary>
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public static ProvisionResult Execute(
        ProvisionOptions options,
        TextReader? inReader = null,
        TextWriter? outWriter = null,
        TextWriter? errWriter = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        inReader ??= Console.In;
        outWriter ??= Console.Out;
        errWriter ??= Console.Error;

        if (options.ShowHelp)
        {
            outWriter.WriteLine(ProvisionOptions.GetHelpText());
            return new ProvisionResult { Success = true, ExitCode = ExitSuccess };
        }

        // ====================================================================
        // Step 1: Read school.dlmpack from USB, local path, or UNC path
        // ====================================================================
        string? packPath = ResolvePackPath(options, inReader, outWriter);
        if (string.IsNullOrWhiteSpace(packPath) || !File.Exists(packPath))
        {
            errWriter.WriteLine($"[RALAT] Fail pakej '{packPath ?? "school.dlmpack"}' tidak dijumpai.");
            return new ProvisionResult
            {
                Success = false,
                ExitCode = ExitInvalidArgsOrNotFound,
                ErrorMessage = $"Pack file not found: {packPath ?? "school.dlmpack"}"
            };
        }

        byte[] bundleBytes;
        try
        {
            bundleBytes = File.ReadAllBytes(packPath);
        }
        catch (Exception ex)
        {
            errWriter.WriteLine($"[RALAT] Gagal membaca fail pakej '{packPath}': {ex.Message}");
            return new ProvisionResult
            {
                Success = false,
                ExitCode = ExitInvalidArgsOrNotFound,
                ErrorMessage = $"Failed to read pack file: {ex.Message}"
            };
        }

        SecurePasswordBuffer? passphraseBuffer = null;
        try
        {
            // ================================================================
            // Step 2: Prompt for admin passphrase (memory only, zeroed on exit)
            // ================================================================
            if (options.PassphraseStdin)
            {
                string? line = inReader.ReadLine();
                if (line == null)
                {
                    errWriter.WriteLine("[RALAT] Tiada kata laluan dibekalkan melalui stdin.");
                    return new ProvisionResult
                    {
                        Success = false,
                        ExitCode = ExitInvalidArgsOrNotFound,
                        ErrorMessage = "No passphrase provided via stdin."
                    };
                }

                string clean = line.TrimEnd('\r', '\n');
                if (clean.Length == 0)
                {
                    errWriter.WriteLine("[RALAT] Kata laluan dari stdin adalah kosong.");
                    return new ProvisionResult
                    {
                        Success = false,
                        ExitCode = ExitInvalidArgsOrNotFound,
                        ErrorMessage = "Empty passphrase received from stdin."
                    };
                }

                passphraseBuffer = new SecurePasswordBuffer(clean);
            }
            else
            {
                if (options.Quiet)
                {
                    errWriter.WriteLine("[RALAT] Mod senyap (--quiet) memerlukan --passphrase-stdin.");
                    return new ProvisionResult
                    {
                        Success = false,
                        ExitCode = ExitInvalidArgsOrNotFound,
                        ErrorMessage = "Quiet mode requires --passphrase-stdin."
                    };
                }

                passphraseBuffer = ReadMaskedPassphrase(outWriter);
                if (passphraseBuffer.PasswordSpan.Length == 0)
                {
                    errWriter.WriteLine("[RALAT] Kata laluan tidak boleh kosong.");
                    return new ProvisionResult
                    {
                        Success = false,
                        ExitCode = ExitInvalidArgsOrNotFound,
                        ErrorMessage = "Passphrase cannot be empty."
                    };
                }
            }

            // ================================================================
            // Step 3: Argon2id -> decrypt -> validate HMAC and schema version
            // ================================================================
            if (!options.Quiet)
            {
                outWriter.WriteLine("[1/5] Menyahsulit dan mengesahkan pakej induk (Argon2id + AES-256-GCM)...");
            }

            MasterBundlePayload payload;
            try
            {
                payload = MasterBundle.Unpack(bundleBytes, passphraseBuffer.PasswordSpan);
            }
            catch (MasterBundleException ex)
            {
                errWriter.WriteLine($"[RALAT PENGESAHAN] Kata laluan salah atau fail pakej rosak: {ex.Message}");
                return new ProvisionResult
                {
                    Success = false,
                    ExitCode = ExitAuthenticationFailed,
                    ErrorMessage = "Authentication failed: invalid passphrase or corrupted bundle."
                };
            }
            catch (Exception ex)
            {
                errWriter.WriteLine($"[RALAT] Gagal menyahsulit pakej: {ex.Message}");
                return new ProvisionResult
                {
                    Success = false,
                    ExitCode = ExitAuthenticationFailed,
                    ErrorMessage = $"Failed to decrypt bundle: {ex.Message}"
                };
            }

            if (string.IsNullOrWhiteSpace(payload.School?.Code))
            {
                errWriter.WriteLine("[RALAT] Pakej tidak mengandungi kod sekolah yang sah.");
                return new ProvisionResult
                {
                    Success = false,
                    ExitCode = ExitAuthenticationFailed,
                    ErrorMessage = "Invalid school code in decrypted bundle."
                };
            }

            string targetDir = options.TargetDirectory ?? DpapiCredentialStore.GetDefaultStoreDirectory();

            // ================================================================
            // Step 4: Re-wrap with DPAPI LocalMachine + entropy -> write credentials.dat
            // ================================================================
            if (!options.Quiet)
            {
                outWriter.WriteLine($"[2/5] Menulis storan per-PC DPAPI (LocalMachine) untuk sekolah {payload.School.Code} ({payload.Students.Count} murid)...");
            }

            if (!options.DryRun)
            {
                try
                {
                    DpapiCredentialStore.WriteStore(payload, targetDir, pupilAccount: options.PupilAccount, applyAcls: options.ApplyAcls);
                }
                catch (Exception ex)
                {
                    errWriter.WriteLine($"[RALAT STORAN] Gagal menulis storan DPAPI: {ex.Message}");
                    return new ProvisionResult
                    {
                        Success = false,
                        ExitCode = ExitStoreWriteFailed,
                        ErrorMessage = $"Failed to write DPAPI store: {ex.Message}"
                    };
                }
            }

            // ================================================================
            // Step 5: Write device_id (GUID, first run only) and the store date
            // ================================================================
            if (!options.Quiet)
            {
                outWriter.WriteLine("[3/5] Menguruskan ID peranti dan metadata storan...");
            }

            string deviceId = ManageDeviceId(targetDir, options.PupilAccount, options.DryRun, options.ApplyAcls);
            if (!options.DryRun)
            {
                WriteProvisionMetadata(targetDir, deviceId, payload, options.PupilAccount, options.ApplyAcls);
            }

            // ================================================================
            // Step 6: Append to the lab checklist file on the share, if present
            // ================================================================
            if (!options.Quiet)
            {
                outWriter.WriteLine("[4/5] Mengemas kini senarai semak makmal (lab checklist)...");
            }

            bool checklistUpdated = UpdateLabChecklist(options, packPath, deviceId, payload.School.Code, payload.GeneratedAt, outWriter, errWriter);

            // ================================================================
            // Step 7: Zero everything. Exit code 0/non-zero for scripting.
            // ================================================================
            if (!options.Quiet)
            {
                outWriter.WriteLine("[5/5] Selesai membersihkan memori.");
                outWriter.WriteLine();
                outWriter.WriteLine($"[BERJAYA] Komputer ini berjaya diprovisikan untuk {payload.School.Name} ({payload.School.Code}).");
                outWriter.WriteLine($"  - Device ID      : {deviceId}");
                outWriter.WriteLine($"  - Bilangan Murid : {payload.Students.Count}");
                outWriter.WriteLine($"  - Tarikh Storan  : {payload.GeneratedAt:yyyy-MM-dd HH:mm:ss zzz}");
                outWriter.WriteLine($"  - Lokasi Storan  : {targetDir}");
            }

            return new ProvisionResult
            {
                Success = true,
                ExitCode = ExitSuccess,
                SchoolCode = payload.School.Code,
                DeviceId = deviceId,
                StudentCount = payload.Students.Count,
                StoreGeneratedAt = payload.GeneratedAt,
                ChecklistUpdated = checklistUpdated
            };
        }
        finally
        {
            // Decryption discipline: wipe all sensitive buffers from memory
            passphraseBuffer?.Dispose();
            CryptographicOperations.ZeroMemory(bundleBytes);
        }
    }

    private static string? ResolvePackPath(ProvisionOptions options, TextReader inReader, TextWriter outWriter)
    {
        if (!string.IsNullOrWhiteSpace(options.PackPath))
        {
            return Path.GetFullPath(options.PackPath);
        }

        // Check current directory
        string localDlm = Path.Combine(Environment.CurrentDirectory, "school.dlmpack");
        if (File.Exists(localDlm))
        {
            return localDlm;
        }

        // Check app directory
        string appDlm = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "school.dlmpack");
        if (File.Exists(appDlm))
        {
            return appDlm;
        }

        // Search for any *.dlmpack in current directory
        try
        {
            var files = Directory.GetFiles(Environment.CurrentDirectory, "*.dlmpack");
            if (files.Length == 1)
            {
                return files[0];
            }
        }
        catch
        {
            // Fall through
        }

        // If interactive mode, prompt user for path
        if (!options.Quiet)
        {
            outWriter.Write("Masukkan laluan fail school.dlmpack: ");
            string? input = inReader.ReadLine()?.Trim('\"', ' ');
            if (!string.IsNullOrWhiteSpace(input))
            {
                return Path.GetFullPath(input);
            }
        }

        return null;
    }

    private static SecurePasswordBuffer ReadMaskedPassphrase(TextWriter outWriter)
    {
        outWriter.Write("Sila masukkan kata laluan pentadbir: ");

        char[] buffer = new char[256];
        int length = 0;

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                outWriter.WriteLine();
                break;
            }
            else if (key.Key == ConsoleKey.Backspace)
            {
                if (length > 0)
                {
                    length--;
                    buffer[length] = '\0';
                    outWriter.Write("\b \b");
                }
            }
            else if (key.Key == ConsoleKey.Escape)
            {
                while (length > 0)
                {
                    length--;
                    buffer[length] = '\0';
                    outWriter.Write("\b \b");
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                if (length < buffer.Length)
                {
                    buffer[length++] = key.KeyChar;
                    outWriter.Write('*');
                }
            }
        }

        try
        {
            return new SecurePasswordBuffer(buffer.AsSpan(0, length));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(buffer.AsSpan()));
        }
    }

    private static string ManageDeviceId(string targetDir, string pupilAccount, bool dryRun, bool applyAcls)
    {
        string deviceIdFile = Path.Combine(targetDir, "device.id");
        string altDeviceIdFile = Path.Combine(targetDir, "device_id");

        try
        {
            if (File.Exists(deviceIdFile))
            {
                string existing = File.ReadAllText(deviceIdFile).Trim();
                if (Guid.TryParse(existing, out var guid))
                {
                    return guid.ToString("D");
                }
            }
            else if (File.Exists(altDeviceIdFile))
            {
                string existing = File.ReadAllText(altDeviceIdFile).Trim();
                if (Guid.TryParse(existing, out var guid))
                {
                    return guid.ToString("D");
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // If restricted by ACL, proceed with generating/preserving ID
        }

        // First run on this machine -> generate new unique GUID
        string newId = Guid.NewGuid().ToString("D");
        if (!dryRun)
        {
            Directory.CreateDirectory(targetDir);
            File.WriteAllText(deviceIdFile, newId);
            if (applyAcls && OperatingSystem.IsWindows())
            {
                StoreAclConfigurator.ApplyStoreFileAcls(deviceIdFile, pupilAccount);
            }
        }
        return newId;
    }

    private static void WriteProvisionMetadata(string targetDir, string deviceId, MasterBundlePayload payload, string pupilAccount, bool applyAcls)
    {
        string metaPath = Path.Combine(targetDir, "provision.json");
        var meta = new
        {
            device_id = deviceId,
            school_code = payload.School.Code,
            school_name = payload.School.Name,
            schema_version = payload.SchemaVersion,
            bundle_generated_at = payload.GeneratedAt,
            provisioned_at = DateTimeOffset.UtcNow,
            student_count = payload.Students.Count,
            app_version = typeof(ProvisionEngine).Assembly.GetName().Version?.ToString(3) ?? "2.0.0"
        };

        string json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(metaPath, json);
        if (applyAcls && OperatingSystem.IsWindows())
        {
            StoreAclConfigurator.ApplyStoreFileAcls(metaPath, pupilAccount);
        }
    }

    private static bool UpdateLabChecklist(
        ProvisionOptions options,
        string packPath,
        string deviceId,
        string schoolCode,
        DateTimeOffset bundleDate,
        TextWriter outWriter,
        TextWriter errWriter)
    {
        string? checklistFile = null;

        if (!string.IsNullOrWhiteSpace(options.ChecklistPath))
        {
            checklistFile = Path.GetFullPath(options.ChecklistPath);
        }
        else
        {
            string? packDir = Path.GetDirectoryName(packPath);
            if (!string.IsNullOrEmpty(packDir))
            {
                string candidate1 = Path.Combine(packDir, "lab_checklist.csv");
                string candidate2 = Path.Combine(packDir, "lab-checklist.csv");
                string candidate3 = Path.Combine(packDir, "checklist.csv");

                if (File.Exists(candidate1)) checklistFile = candidate1;
                else if (File.Exists(candidate2)) checklistFile = candidate2;
                else if (File.Exists(candidate3)) checklistFile = candidate3;
                else if (Directory.Exists(packDir))
                {
                    checklistFile = candidate1;
                }
            }
        }

        if (string.IsNullOrEmpty(checklistFile))
        {
            return false;
        }

        string machineName = Environment.MachineName;
        string appVersion = typeof(ProvisionEngine).Assembly.GetName().Version?.ToString(3) ?? "2.0.0";
        string timestamp = DateTimeOffset.UtcNow.ToString("O");
        string storeDate = bundleDate.ToString("O");

        string header = "Timestamp,PC_Name,Device_ID,School_Code,Software_Version,Store_Date,Status\r\n";
        string row = $"{timestamp},{EscapeCsv(machineName)},{deviceId},{EscapeCsv(schoolCode)},{appVersion},{storeDate},SUCCESS\r\n";

        const int maxRetries = 5;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                string? dir = Path.GetDirectoryName(checklistFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                bool needsHeader = !File.Exists(checklistFile) || new FileInfo(checklistFile).Length == 0;

                using (var stream = new FileStream(checklistFile, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                {
                    stream.Seek(0, SeekOrigin.End);
                    using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
                    {
                        if (needsHeader)
                        {
                            writer.Write(header);
                        }
                        writer.Write(row);
                        writer.Flush();
                    }
                }

                if (!options.Quiet)
                {
                    outWriter.WriteLine($"  Senarai semak dikemas kini: {checklistFile}");
                }
                return true;
            }
            catch (IOException) when (attempt < maxRetries)
            {
                Thread.Sleep(Random.Shared.Next(50, 200 * attempt));
            }
            catch (Exception ex)
            {
                if (!options.Quiet)
                {
                    errWriter.WriteLine($"  [Amaran] Tidak dapat mengemas kini fail senarai semak '{checklistFile}': {ex.Message}");
                }
                return false;
            }
        }

        return false;
    }

    private static string EscapeCsv(string field)
    {
        if (field.Contains(',') || field.Contains('\"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
}
