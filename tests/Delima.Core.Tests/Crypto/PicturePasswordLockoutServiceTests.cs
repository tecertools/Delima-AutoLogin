using Delima.Core.Crypto;

namespace Delima.Core.Tests.Crypto;

public class PicturePasswordLockoutServiceTests
{
    [Fact]
    public void InitialState_HasMaxAttempts_NotLockedOut()
    {
        var service = new PicturePasswordLockoutService();
        string studentId = "s_0001";

        Assert.Equal(5, service.GetRemainingAttempts(studentId));
        Assert.False(service.IsLockedOut(studentId, out var remainingTime));
        Assert.Equal(TimeSpan.Zero, remainingTime);
    }

    [Fact]
    public void RecordFailedAttempt_DecrementsAttemptsCorrectly()
    {
        var service = new PicturePasswordLockoutService();
        string studentId = "s_0001";

        int remaining1 = service.RecordFailedAttempt(studentId, out bool isLocked1, out _);
        Assert.Equal(4, remaining1);
        Assert.False(isLocked1);
        Assert.Equal(4, service.GetRemainingAttempts(studentId));

        int remaining2 = service.RecordFailedAttempt(studentId, out bool isLocked2, out _);
        Assert.Equal(3, remaining2);
        Assert.False(isLocked2);
        Assert.Equal(3, service.GetRemainingAttempts(studentId));
    }

    [Fact]
    public void FifthFailedAttempt_TriggersLockoutFor5Minutes()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var service = new PicturePasswordLockoutService(timeProvider: () => now);
        string studentId = "s_0001";

        for (int i = 0; i < 4; i++)
        {
            service.RecordFailedAttempt(studentId, out bool isLocked, out _);
            Assert.False(isLocked);
        }

        // 5th attempt
        int remaining = service.RecordFailedAttempt(studentId, out bool isNowLocked, out TimeSpan lockoutDuration);
        Assert.Equal(0, remaining);
        Assert.True(isNowLocked);
        Assert.Equal(TimeSpan.FromMinutes(5), lockoutDuration);

        Assert.True(service.IsLockedOut(studentId, out var activeRemaining));
        Assert.True(activeRemaining > TimeSpan.FromMinutes(4));
        Assert.Equal(0, service.GetRemainingAttempts(studentId));
    }

    [Fact]
    public void LockoutExpires_After5Minutes()
    {
        DateTimeOffset simulatedTime = DateTimeOffset.UtcNow;
        var service = new PicturePasswordLockoutService(timeProvider: () => simulatedTime);
        string studentId = "s_0001";

        // Trigger lockout
        for (int i = 0; i < 5; i++)
        {
            service.RecordFailedAttempt(studentId, out _, out _);
        }
        Assert.True(service.IsLockedOut(studentId, out _));

        // Advance simulated time by 4 minutes 59 seconds (still locked)
        simulatedTime = simulatedTime.AddMinutes(4).AddSeconds(59);
        Assert.True(service.IsLockedOut(studentId, out _));

        // Advance past 5 minutes (lockout expired)
        simulatedTime = simulatedTime.AddSeconds(2);
        Assert.False(service.IsLockedOut(studentId, out _));
        Assert.Equal(5, service.GetRemainingAttempts(studentId));
    }

    [Fact]
    public void ResetAttempts_ClearsLockoutAndFailedAttempts()
    {
        var service = new PicturePasswordLockoutService();
        string studentId = "s_0001";

        for (int i = 0; i < 5; i++)
        {
            service.RecordFailedAttempt(studentId, out _, out _);
        }
        Assert.True(service.IsLockedOut(studentId, out _));

        service.ResetAttempts(studentId);

        Assert.False(service.IsLockedOut(studentId, out _));
        Assert.Equal(5, service.GetRemainingAttempts(studentId));
    }

    [Fact]
    public void MultiStudentIsolation_PupilsHaveIndependentAttempts()
    {
        var service = new PicturePasswordLockoutService();
        string student1 = "s_0001";
        string student2 = "s_0002";

        for (int i = 0; i < 5; i++)
        {
            service.RecordFailedAttempt(student1, out _, out _);
        }

        Assert.True(service.IsLockedOut(student1, out _));
        Assert.False(service.IsLockedOut(student2, out _));
        Assert.Equal(5, service.GetRemainingAttempts(student2));
    }
}
