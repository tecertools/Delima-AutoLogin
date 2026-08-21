using System.Diagnostics;
using System.IO;
using Delima.Core.Roster;
using Delima.Core.Store;
using Delima.Win32;
using Xunit;

namespace Delima.Win32.Tests;

public class IdleTrackerTests
{
    [Fact]
    public void IdleTracker_UnderThreshold_DoesNotTrigger()
    {
        var simulatedIdle = TimeSpan.FromSeconds(100);
        var tracker = new IdleTracker(
            idleThreshold: TimeSpan.FromSeconds(600),
            idleTimeProvider: () => simulatedIdle,
            autoStart: false
        );

        bool triggered = false;
        tracker.IdleTimeoutReached += (_, _) => triggered = true;

        bool evaluated = tracker.EvaluateNow();

        Assert.False(evaluated);
        Assert.False(triggered);
    }

    [Fact]
    public void IdleTracker_AtOrOverThreshold_TriggersTimeout()
    {
        var simulatedIdle = TimeSpan.FromSeconds(600);
        var tracker = new IdleTracker(
            idleThreshold: TimeSpan.FromSeconds(600),
            idleTimeProvider: () => simulatedIdle,
            autoStart: false
        );

        var eventFired = new ManualResetEventSlim(false);
        tracker.IdleTimeoutReached += (_, _) => eventFired.Set();

        bool evaluated = tracker.EvaluateNow();

        Assert.True(evaluated);
        Assert.True(eventFired.Wait(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void IdleTracker_Reset_AllowsSubsequentTrigger()
    {
        var simulatedIdle = TimeSpan.FromSeconds(700);
        var tracker = new IdleTracker(
            idleThreshold: TimeSpan.FromSeconds(600),
            idleTimeProvider: () => simulatedIdle,
            autoStart: false
        );

        Assert.True(tracker.EvaluateNow());

        // Second immediate evaluate while already triggered returns false
        Assert.False(tracker.EvaluateNow());

        // After reset, it can trigger again
        tracker.Reset();
        Assert.True(tracker.EvaluateNow());
    }

    [Fact]
    public void SessionWatchdog_OnIdleTimeout_WipesSession_ZeroesCredential_AndResetsUi()
    {
        var tempProfileDir = Path.Combine(Path.GetTempPath(), "test_session_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempProfileDir);

        using var exitProc = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit 0")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        })!;
        exitProc.WaitForExit(2000);

        var session = new ChromeSession(exitProc, tempProfileDir);
        var credential = new SecurePasswordBuffer("PupilSecretPass123!"u8);

        var student = new Student
        {
            Id = "s_0001",
            Name = "Nur Aishah Binti Ahmad",
            ClassId = "2_cemerlang",
            EmailLocal = "m-12345678"
        };

        bool uiResetCalled = false;
        var simulatedIdle = TimeSpan.FromSeconds(605);

        using var watchdog = new SessionWatchdog(
            idleThreshold: TimeSpan.FromSeconds(600),
            session: session,
            student: student,
            credential: credential,
            onResetAction: () => uiResetCalled = true,
            idleTimeProvider: () => simulatedIdle,
            autoStart: false
        );

        // Force watchdog evaluation
        bool triggered = watchdog.EvaluateNow();

        Assert.True(triggered);

        // Allow any async event handler tasks to complete
        Thread.Sleep(50);

        Assert.True(watchdog.IsReset);
        Assert.True(uiResetCalled);

        // Verify credential buffer was zeroed/disposed
        Assert.Throws<ObjectDisposedException>(() =>
        {
            var _ = credential.PasswordSpan;
        });

        // Verify temp profile directory is cleaned up
        Assert.False(Directory.Exists(tempProfileDir));
    }
}
