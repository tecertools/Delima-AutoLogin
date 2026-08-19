using System.Collections.Concurrent;

namespace Delima.Core.Crypto;

/// <summary>
/// Tracks failed picture password attempts per pupil and enforces a 5-minute lockout after 5 failures.
/// Adheres to PRD §7.3 and Technical Architecture §7 (E12).
/// </summary>
public interface IPicturePasswordLockoutService
{
    int MaxFailedAttempts { get; }
    TimeSpan LockoutDuration { get; }
    int GetRemainingAttempts(string studentId);
    bool IsLockedOut(string studentId, out TimeSpan remainingTime);
    int RecordFailedAttempt(string studentId, out bool isNowLockedOut, out TimeSpan remainingTime);
    void ResetAttempts(string studentId);
    void ClearAll();
}

public sealed class PicturePasswordLockoutService : IPicturePasswordLockoutService
{
    public const int DefaultMaxFailedAttempts = 5;
    public static readonly TimeSpan DefaultLockoutDuration = TimeSpan.FromMinutes(5);

    private readonly object _lock = new();
    private readonly Dictionary<string, PupilLockoutState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<DateTimeOffset> _timeProvider;

    public int MaxFailedAttempts { get; }
    public TimeSpan LockoutDuration { get; }

    public static PicturePasswordLockoutService Instance { get; } = new();

    public PicturePasswordLockoutService(
        int maxFailedAttempts = DefaultMaxFailedAttempts,
        TimeSpan? lockoutDuration = null,
        Func<DateTimeOffset>? timeProvider = null)
    {
        MaxFailedAttempts = maxFailedAttempts > 0 ? maxFailedAttempts : DefaultMaxFailedAttempts;
        LockoutDuration = lockoutDuration ?? DefaultLockoutDuration;
        _timeProvider = timeProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public int GetRemainingAttempts(string studentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentId);

        lock (_lock)
        {
            DateTimeOffset now = _timeProvider();
            if (_states.TryGetValue(studentId, out var state))
            {
                // Check if lockout has expired
                if (state.LockedUntil.HasValue && state.LockedUntil.Value <= now)
                {
                    _states.Remove(studentId);
                    return MaxFailedAttempts;
                }

                if (state.LockedUntil.HasValue)
                {
                    return 0;
                }

                return Math.Max(0, MaxFailedAttempts - state.FailedAttempts);
            }

            return MaxFailedAttempts;
        }
    }

    public bool IsLockedOut(string studentId, out TimeSpan remainingTime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentId);

        lock (_lock)
        {
            DateTimeOffset now = _timeProvider();
            if (_states.TryGetValue(studentId, out var state) && state.LockedUntil.HasValue)
            {
                if (state.LockedUntil.Value > now)
                {
                    remainingTime = state.LockedUntil.Value - now;
                    return true;
                }
                else
                {
                    // Lockout period expired
                    _states.Remove(studentId);
                }
            }

            remainingTime = TimeSpan.Zero;
            return false;
        }
    }

    public int RecordFailedAttempt(string studentId, out bool isNowLockedOut, out TimeSpan remainingTime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentId);

        lock (_lock)
        {
            DateTimeOffset now = _timeProvider();
            if (!_states.TryGetValue(studentId, out var state))
            {
                state = new PupilLockoutState();
                _states[studentId] = state;
            }

            // If already locked out
            if (state.LockedUntil.HasValue && state.LockedUntil.Value > now)
            {
                isNowLockedOut = true;
                remainingTime = state.LockedUntil.Value - now;
                return 0;
            }

            // If previous lockout expired, reset count
            if (state.LockedUntil.HasValue && state.LockedUntil.Value <= now)
            {
                state.FailedAttempts = 0;
                state.LockedUntil = null;
            }

            state.FailedAttempts++;

            if (state.FailedAttempts >= MaxFailedAttempts)
            {
                state.LockedUntil = now.Add(LockoutDuration);
                isNowLockedOut = true;
                remainingTime = LockoutDuration;
                return 0;
            }
            else
            {
                isNowLockedOut = false;
                remainingTime = TimeSpan.Zero;
                return MaxFailedAttempts - state.FailedAttempts;
            }
        }
    }

    public void ResetAttempts(string studentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentId);

        lock (_lock)
        {
            _states.Remove(studentId);
        }
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            _states.Clear();
        }
    }

    private sealed class PupilLockoutState
    {
        public int FailedAttempts { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
    }
}
