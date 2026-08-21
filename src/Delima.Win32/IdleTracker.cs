using System.Diagnostics;
using Delima.Core.Audit;
using Delima.Core.Roster;
using Delima.Core.Store;

namespace Delima.Win32;

/// <summary>
/// Monitors user inactivity and triggers idle reset actions when the idle duration
/// exceeds the configured threshold (default 600s / 10 minutes per Appendix B).
/// </summary>
public sealed class IdleTracker : IDisposable
{
    private readonly Func<TimeSpan> _idleTimeProvider;
    private readonly TimeSpan _checkInterval;
    private readonly Timer _timer;
    private readonly object _lock = new();
    private bool _disposed;
    private bool _isTriggered;

    public TimeSpan IdleThreshold { get; set; }
    public bool IsRunning { get; private set; }

    public event EventHandler? IdleTimeoutReached;

    /// <summary>
    /// Creates a new IdleTracker with the specified threshold and optional custom idle time provider.
    /// </summary>
    /// <param name="idleThreshold">Inactivity duration before timeout triggers.</param>
    /// <param name="idleTimeProvider">Optional delegate returning current system/user idle time.</param>
    /// <param name="checkInterval">Optional polling interval (defaults to 1 second).</param>
    /// <param name="autoStart">If true, starts periodic timer on creation.</param>
    public IdleTracker(
        TimeSpan idleThreshold,
        Func<TimeSpan>? idleTimeProvider = null,
        TimeSpan? checkInterval = null,
        bool autoStart = true)
    {
        IdleThreshold = idleThreshold;
        _idleTimeProvider = idleTimeProvider ?? NativeMethods.GetSystemIdleDuration;
        _checkInterval = checkInterval ?? TimeSpan.FromSeconds(1);

        _timer = new Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);

        if (autoStart)
        {
            Start();
        }
    }

    /// <summary>
    /// Starts the idle monitor.
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_disposed || IsRunning) return;
            _isTriggered = false;
            IsRunning = true;
            _timer.Change(_checkInterval, _checkInterval);
        }
    }

    /// <summary>
    /// Stops the idle monitor.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            IsRunning = false;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    /// <summary>
    /// Resets the triggered state and restarts monitoring.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _isTriggered = false;
        }
    }

    /// <summary>
    /// Gets the current idle duration reported by the provider.
    /// </summary>
    public TimeSpan GetCurrentIdleDuration()
    {
        return _idleTimeProvider();
    }

    /// <summary>
    /// Evaluates current idle duration and triggers timeout if threshold is reached or exceeded.
    /// Returns true if timeout was triggered on this evaluation.
    /// </summary>
    public bool EvaluateNow()
    {
        lock (_lock)
        {
            if (_disposed || _isTriggered) return false;

            var currentIdle = _idleTimeProvider();
            if (currentIdle >= IdleThreshold)
            {
                _isTriggered = true;
                IdleTimeoutReached?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }
    }

    private void OnTimerTick(object? state)
    {
        if (_disposed) return;
        EvaluateNow();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            IsRunning = false;
            _timer.Dispose();
        }
    }
}

/// <summary>
/// Session watchdog that coordinates auto-logout, Chrome throwaway profile wipe,
/// credential zeroing, and audit logging when idle timeout occurs per Architecture §9 and Appendix B.
/// </summary>
public sealed class SessionWatchdog : IDisposable
{
    private readonly ChromeSession? _session;
    private readonly Student? _student;
    private readonly ICredential? _credential;
    private readonly Action? _onResetAction;
    private readonly IdleTracker _idleTracker;
    private readonly object _lock = new();
    private bool _disposed;
    private bool _isReset;

    public TimeSpan IdleThreshold => _idleTracker.IdleThreshold;
    public bool IsReset => _isReset;

    public SessionWatchdog(
        TimeSpan idleThreshold,
        ChromeSession? session = null,
        Student? student = null,
        ICredential? credential = null,
        Action? onResetAction = null,
        Func<TimeSpan>? idleTimeProvider = null,
        bool autoStart = true)
    {
        _session = session;
        _student = student;
        _credential = credential;
        _onResetAction = onResetAction;

        _idleTracker = new IdleTracker(idleThreshold, idleTimeProvider, autoStart: autoStart);
        _idleTracker.IdleTimeoutReached += OnIdleTimeout;
    }

    private void OnIdleTimeout(object? sender, EventArgs e)
    {
        PerformIdleReset();
    }

    /// <summary>
    /// Executes the full idle reset sequence: wipes Chrome profile, zeroes credentials,
    /// logs the audit event, and triggers UI reset.
    /// </summary>
    public void PerformIdleReset()
    {
        lock (_lock)
        {
            if (_disposed || _isReset) return;
            _isReset = true;
        }

        // 1. Audit log the idle timeout event
        try
        {
            if (_student != null)
            {
                AuditLogger.RecordEntry(new AuditLogEntry
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    StudentId = _student.Id,
                    Event = "idle_reset",
                    Outcome = "TIMEOUT",
                    WindowsUser = Environment.UserName,
                    SoftwareVersion = "2.0.0",
                    Details = $"Session automatically reset after {_idleTracker.IdleThreshold.TotalSeconds}s idle timeout. Profile wiped and credentials zeroed."
                });
            }
        }
        catch
        {
            // Best effort audit logging
        }

        // 2. Wipe temporary Chrome profile and terminate scoped processes
        try
        {
            _session?.Dispose();
        }
        catch
        {
            // Best effort session disposal
        }

        // 3. Securely zero credentials in memory
        try
        {
            _credential?.Dispose();
        }
        catch
        {
            // Best effort credential wipe
        }

        // 4. Trigger UI reset
        try
        {
            _onResetAction?.Invoke();
        }
        catch
        {
            // Best effort UI reset callback
        }
    }

    /// <summary>
    /// Evaluates the watchdog immediately (useful for testing or polling loops).
    /// </summary>
    public bool EvaluateNow()
    {
        return _idleTracker.EvaluateNow();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _idleTracker.Dispose();
        }
    }
}
