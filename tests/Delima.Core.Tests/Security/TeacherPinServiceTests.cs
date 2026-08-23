using System.IO;
using Delima.Core.Audit;
using Delima.Core.Security;
using Xunit;

namespace Delima.Core.Tests.Security;

public class TeacherPinServiceTests : IDisposable
{
    private readonly string _testAuditDir;

    public TeacherPinServiceTests()
    {
        _testAuditDir = Path.Combine(Path.GetTempPath(), "Delima_TeacherPinServiceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testAuditDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testAuditDir))
            {
                Directory.Delete(_testAuditDir, true);
            }
        }
        catch
        {
            // Ignore cleanup failure
        }
    }

    [Fact]
    public void VerifyPin_DefaultPin_Succeeds()
    {
        var service = new TeacherPinService();

        bool result = service.VerifyPin("1234", "SKS24", _testAuditDir);

        Assert.True(result);
        Assert.Equal(5, service.GetRemainingAttempts());
        Assert.False(service.IsLockedOut(out _));

        string logFile = AuditLogger.GetAuditLogFilePath(DateTimeOffset.UtcNow, _testAuditDir);
        Assert.True(File.Exists(logFile));
        string logContent = File.ReadAllText(logFile);
        Assert.Contains("teacher_pin_success", logContent);
        Assert.Contains("SKS24", logContent);
        Assert.DoesNotContain("1234", logContent); // Never log the PIN!
    }

    [Fact]
    public void VerifyPin_WrongPin_DecrementsAttemptsAndRecordsFailure()
    {
        DateTimeOffset fixedTime = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
        var service = new TeacherPinService("5678", timeProvider: () => fixedTime);

        bool result = service.VerifyPin("1111", "SKS24", _testAuditDir);

        Assert.False(result);
        Assert.Equal(4, service.GetRemainingAttempts());
        Assert.False(service.IsLockedOut(out _));

        string logFile = AuditLogger.GetAuditLogFilePath(fixedTime, _testAuditDir);
        Assert.True(File.Exists(logFile));
        string logContent = File.ReadAllText(logFile);
        Assert.Contains("teacher_pin_failure", logContent);
        Assert.Contains("PIN_INVALID", logContent);
        Assert.DoesNotContain("5678", logContent);
        Assert.DoesNotContain("1111", logContent);
    }

    [Fact]
    public void VerifyPin_FiveFailures_EnforcesFiveMinuteLockout()
    {
        DateTimeOffset now = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
        var service = new TeacherPinService(
            configuredPin: "9999",
            maxFailedAttempts: 5,
            lockoutDuration: TimeSpan.FromMinutes(5),
            timeProvider: () => now);

        for (int i = 0; i < 4; i++)
        {
            bool res = service.VerifyPin("0000", "SKS24", _testAuditDir);
            Assert.False(res);
            Assert.Equal(4 - i, service.GetRemainingAttempts());
            Assert.False(service.IsLockedOut(out _));
        }

        // 5th failed attempt -> locks out
        bool fifthRes = service.VerifyPin("0000", "SKS24", _testAuditDir);
        Assert.False(fifthRes);
        Assert.Equal(0, service.GetRemainingAttempts());
        Assert.True(service.IsLockedOut(out TimeSpan remaining));
        Assert.Equal(TimeSpan.FromMinutes(5), remaining);

        // Subsequent attempt with correct PIN is denied during lockout
        bool lockedAttempt = service.VerifyPin("9999", "SKS24", _testAuditDir);
        Assert.False(lockedAttempt);

        string logFile = AuditLogger.GetAuditLogFilePath(now, _testAuditDir);
        string logContent = File.ReadAllText(logFile);
        Assert.Contains("teacher_pin_lockout", logContent);
    }

    [Fact]
    public void VerifyPin_LockoutExpires_AllowsSubsequentAttempts()
    {
        DateTimeOffset now = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
        var service = new TeacherPinService(
            configuredPin: "4321",
            maxFailedAttempts: 5,
            lockoutDuration: TimeSpan.FromMinutes(5),
            timeProvider: () => now);

        // Lock out
        for (int i = 0; i < 5; i++)
        {
            service.VerifyPin("0000", "SKS24", _testAuditDir);
        }

        Assert.True(service.IsLockedOut(out _));

        // Advance time by 5 minutes and 1 second
        now = now.AddMinutes(5).AddSeconds(1);

        Assert.False(service.IsLockedOut(out _));
        Assert.Equal(5, service.GetRemainingAttempts());

        // Correct PIN now succeeds
        bool success = service.VerifyPin("4321", "SKS24", _testAuditDir);
        Assert.True(success);
    }

    [Fact]
    public void ResetLockout_ClearsFailureCountImmediately()
    {
        var service = new TeacherPinService("1234");
        for (int i = 0; i < 5; i++)
        {
            service.VerifyPin("0000", "SKS24", _testAuditDir);
        }

        Assert.True(service.IsLockedOut(out _));

        service.ResetLockout();

        Assert.False(service.IsLockedOut(out _));
        Assert.Equal(5, service.GetRemainingAttempts());
        Assert.True(service.VerifyPin("1234", "SKS24", _testAuditDir));
    }
}
