using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Delima.Core.Audit;

/// <summary>
/// Append-only local audit log writer per Technical Architecture §8.
/// Stores monthly rotated log files (audit-yyyy-MM.log) in %ProgramData%\DELIMa Launcher\audit\.
/// </summary>
public static class AuditLogger
{
    public const string DefaultAuditDirectoryName = "audit";
    public const string DefaultProgramDataSubdir = "DELIMa Launcher";

    private static readonly object FileLock = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Resolves the audit log directory path from a base directory or system default (%ProgramData%\DELIMa Launcher\audit\).
    /// </summary>
    public static string GetAuditDirectory(string? baseDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            // If baseDirectory is already named "audit" or is an audit path, use it directly
            if (Path.GetFileName(baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                    .Equals(DefaultAuditDirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(baseDirectory);
            }
            return Path.Combine(Path.GetFullPath(baseDirectory), DefaultAuditDirectoryName);
        }

        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(programData, DefaultProgramDataSubdir, DefaultAuditDirectoryName);
    }

    /// <summary>
    /// Returns the monthly audit log file path (audit-yyyy-MM.log) for the specified timestamp and audit directory.
    /// </summary>
    public static string GetAuditLogFilePath(DateTimeOffset timestamp, string? auditDirectory = null)
    {
        string dir = GetAuditDirectory(auditDirectory);
        string filename = $"audit-{timestamp:yyyy-MM}.log";
        return Path.Combine(dir, filename);
    }

    /// <summary>
    /// Appends a structured audit entry to the monthly audit log file in an append-only, thread-safe and process-safe manner.
    /// </summary>
    public static void RecordEntry(AuditLogEntry entry, string? auditDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            string auditDir = GetAuditDirectory(auditDirectory);
            if (!Directory.Exists(auditDir))
            {
                Directory.CreateDirectory(auditDir);
            }

            string logFilePath = GetAuditLogFilePath(entry.Timestamp, auditDir);
            string jsonLine = JsonSerializer.Serialize(entry, JsonOpts) + Environment.NewLine;
            byte[] lineBytes = Encoding.UTF8.GetBytes(jsonLine);

            lock (FileLock)
            {
                using var stream = new FileStream(
                    logFilePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite);
                stream.Write(lineBytes, 0, lineBytes.Length);
                stream.Flush();
            }
        }
        catch (Exception ex)
        {
            // Prevent audit logging failures from masking primary exceptions or crashing callers,
            // while surfacing the failure to standard error for operational visibility.
            try
            {
                Console.Error.WriteLine($"[AUDIT LOG ERROR] Failed to append entry to audit log: {ex.Message}");
            }
            catch
            {
                // In case Console.Error is inaccessible
            }
        }
    }

    /// <summary>
    /// Records an ACL application or permission failure event to the audit log per Technical Architecture §3.5 and §8.
    /// </summary>
    public static void RecordAclFailure(
        string targetPath,
        string errorMessage,
        string? pupilAccount = null,
        string? auditDirectory = null,
        string? schoolCode = null,
        string? deviceId = null)
    {
        string? currentUserName = null;
        try
        {
            currentUserName = Environment.UserName;
        }
        catch
        {
            // Ignore environment query failure
        }

        var entry = new AuditLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Event = "acl_failure",
            Outcome = "FAILURE",
            OutcomeCode = "ACL_DENIED",
            Target = targetPath,
            PupilAccount = pupilAccount,
            SchoolCode = schoolCode,
            DeviceId = deviceId,
            WindowsUser = currentUserName,
            Details = errorMessage
        };

        RecordEntry(entry, auditDirectory);
    }

    /// <summary>
    /// Records a general warning or system event to the audit log.
    /// </summary>
    public static void RecordWarning(string message, string? targetPath = null, string? auditDirectory = null)
    {
        var entry = new AuditLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Event = "warning",
            Outcome = "WARNING",
            Target = targetPath,
            Details = message
        };

        RecordEntry(entry, auditDirectory);
    }

    /// <summary>
    /// Records a pupil OAuth consent refusal event (G2 identity check) to the audit log per Technical Architecture §8.
    /// </summary>
    public static void RecordConsentRefused(
        string studentId,
        string pupilAccount,
        string? schoolCode = null,
        string? deviceId = null,
        string? details = null,
        string? auditDirectory = null)
    {
        string? currentUserName = null;
        try
        {
            currentUserName = Environment.UserName;
        }
        catch
        {
            // Ignore environment query failure
        }

        var entry = new AuditLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Event = "consent_refused",
            Outcome = "REFUSED",
            OutcomeCode = "G2_CONSENT_REFUSED",
            StudentId = studentId,
            PupilAccount = pupilAccount,
            SchoolCode = schoolCode,
            DeviceId = deviceId,
            WindowsUser = currentUserName,
            SoftwareVersion = "2.0.0",
            Details = details ?? "Pupil pressed Cancel on OAuth consent screen (identity check G2). Session torn down and returned to Pilih Kelas."
        };

        RecordEntry(entry, auditDirectory);
    }
}
