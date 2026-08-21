using System.IO;
using System.Text.Json;
using Delima.Core.Audit;
using Xunit;

namespace Delima.Core.Tests.Audit;

public class AuditLoggerTests : IDisposable
{
    private readonly string _testDirectory;

    public AuditLoggerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "DelimaAuditTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    [Fact]
    public void GetAuditDirectory_WithCustomBaseDir_ReturnsAuditSubdir()
    {
        string auditDir = AuditLogger.GetAuditDirectory(_testDirectory);
        Assert.Equal(Path.Combine(_testDirectory, "audit"), auditDir);
    }

    [Fact]
    public void GetAuditDirectory_WhenAlreadyNamedAudit_ReturnsPathDirectly()
    {
        string directAudit = Path.Combine(_testDirectory, "audit");
        string auditDir = AuditLogger.GetAuditDirectory(directAudit);
        Assert.Equal(Path.GetFullPath(directAudit), auditDir);
    }

    [Fact]
    public void GetAuditLogFilePath_UsesMonthlyNamingConvention()
    {
        var timestamp = new DateTimeOffset(2026, 8, 21, 10, 30, 0, TimeSpan.Zero);
        string logPath = AuditLogger.GetAuditLogFilePath(timestamp, _testDirectory);

        string expectedFileName = "audit-2026-08.log";
        Assert.EndsWith(expectedFileName, logPath);
        Assert.Contains("audit", logPath);
    }

    [Fact]
    public void RecordEntry_AppendsValidJsonLine()
    {
        var entry = new AuditLogEntry
        {
            Timestamp = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero),
            Event = "test_event",
            Outcome = "SUCCESS",
            OutcomeCode = "OK",
            Target = "test_target.dat",
            PupilAccount = "Murid",
            SchoolCode = "TEST01",
            DeviceId = "11111111-1111-1111-1111-111111111111",
            Details = "Test details"
        };

        AuditLogger.RecordEntry(entry, _testDirectory);

        string logPath = AuditLogger.GetAuditLogFilePath(entry.Timestamp, _testDirectory);
        Assert.True(File.Exists(logPath));

        string[] lines = File.ReadAllLines(logPath);
        Assert.Single(lines);

        using var doc = JsonDocument.Parse(lines[0]);
        var root = doc.RootElement;

        Assert.Equal("test_event", root.GetProperty("event").GetString());
        Assert.Equal("SUCCESS", root.GetProperty("outcome").GetString());
        Assert.Equal("OK", root.GetProperty("outcome_code").GetString());
        Assert.Equal("test_target.dat", root.GetProperty("target").GetString());
        Assert.Equal("Murid", root.GetProperty("pupil_account").GetString());
        Assert.Equal("TEST01", root.GetProperty("school_code").GetString());
        Assert.Equal("11111111-1111-1111-1111-111111111111", root.GetProperty("device_id").GetString());
        Assert.Equal("Test details", root.GetProperty("details").GetString());
    }

    [Fact]
    public void RecordAclFailure_WritesExpectedAclDeniedProperties()
    {
        string targetFile = Path.Combine(_testDirectory, "credentials.dat");
        string errorMsg = "Access is denied (5)";

        AuditLogger.RecordAclFailure(targetFile, errorMsg, pupilAccount: "Murid", auditDirectory: _testDirectory, schoolCode: "SKS24");

        string logPath = AuditLogger.GetAuditLogFilePath(DateTimeOffset.UtcNow, _testDirectory);
        Assert.True(File.Exists(logPath));

        string content = File.ReadAllText(logPath);
        Assert.Contains("\"event\":\"acl_failure\"", content);
        Assert.Contains("\"outcome\":\"FAILURE\"", content);
        Assert.Contains("\"outcome_code\":\"ACL_DENIED\"", content);
        Assert.Contains(errorMsg, content);
        Assert.Contains("Murid", content);
        Assert.Contains("SKS24", content);
    }

    [Fact]
    public void RecordWarning_WritesWarningEntry()
    {
        AuditLogger.RecordWarning("Test warning message", targetPath: "test.dat", auditDirectory: _testDirectory);

        string logPath = AuditLogger.GetAuditLogFilePath(DateTimeOffset.UtcNow, _testDirectory);
        Assert.True(File.Exists(logPath));

        string content = File.ReadAllText(logPath);
        Assert.Contains("\"event\":\"warning\"", content);
        Assert.Contains("\"outcome\":\"WARNING\"", content);
        Assert.Contains("Test warning message", content);
    }

    [Fact]
    public async Task RecordEntry_ConcurrentAppends_WritesAllLinesWithoutCorruption()
    {
        const int threadCount = 20;
        var tasks = new Task[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            int index = i;
            tasks[i] = Task.Run(() =>
            {
                AuditLogger.RecordEntry(new AuditLogEntry
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Event = $"concurrent_event_{index}",
                    Details = $"Message from thread {index}"
                }, _testDirectory);
            });
        }

        await Task.WhenAll(tasks);

        string logPath = AuditLogger.GetAuditLogFilePath(DateTimeOffset.UtcNow, _testDirectory);
        Assert.True(File.Exists(logPath));

        string[] lines = File.ReadAllLines(logPath);
        Assert.Equal(threadCount, lines.Length);

        foreach (string line in lines)
        {
            using var doc = JsonDocument.Parse(line);
            Assert.StartsWith("concurrent_event_", doc.RootElement.GetProperty("event").GetString()!);
        }
    }
}
