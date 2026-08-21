using System.Security.Cryptography;
using System.Text;
using Delima.Core.Audit;

namespace Delima.Core.Security;

/// <summary>
/// Service interface for teacher PIN verification and lockout management per Technical Architecture §7.4 and Appendix B.
/// Enforces 4-digit PIN policy with 5-attempt threshold and 5-minute lockout.
/// </summary>
public interface ITeacherPinService
{
    int MaxFailedAttempts { get; }
    TimeSpan LockoutDuration { get; }
    int GetRemainingAttempts();
    bool IsLockedOut(out TimeSpan remainingTime);
    bool VerifyPin(string enteredPin, string? schoolCode = null, string? auditDirectory = null);
    void ResetLockout();
}

/// <summary>
/// Enforces the teacher PIN policy: 4 digits, lock after 5 attempts for 5 minutes.
/// Every attempt (success, failure, lockout) is recorded to the local audit log.
/// </summary>
public sealed class TeacherPinService : ITeacherPinService
{
    public const string DefaultPin = "1234";
    public const int DefaultMaxFailedAttempts = 5;
    public static readonly TimeSpan DefaultLockoutDuration = TimeSpan.FromMinutes(5);

    private readonly object _lock = new();
    private readonly string _configuredPin;
    private readonly Func<DateTimeOffset> _timeProvider;
    private int _failedAttempts;
    private DateTimeOffset? _lockedUntil;

    public int MaxFailedAttempts { get; }
    public TimeSpan LockoutDuration { get; }

    public static TeacherPinService Instance { get; } = new();

    public TeacherPinService(
        string? configuredPin = null,
        int maxFailedAttempts = DefaultMaxFailedAttempts,
        TimeSpan? lockoutDuration = null,
        Func<DateTimeOffset>? timeProvider = null)
    {
        _configuredPin = string.IsNullOrWhiteSpace(configuredPin) ? DefaultPin : configuredPin.Trim();
        MaxFailedAttempts = maxFailedAttempts > 0 ? maxFailedAttempts : DefaultMaxFailedAttempts;
        LockoutDuration = lockoutDuration ?? DefaultLockoutDuration;
        _timeProvider = timeProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public int GetRemainingAttempts()
    {
        lock (_lock)
        {
            DateTimeOffset now = _timeProvider();
            if (_lockedUntil.HasValue && _lockedUntil.Value <= now)
            {
                _lockedUntil = null;
                _failedAttempts = 0;
                return MaxFailedAttempts;
            }

            if (_lockedUntil.HasValue)
            {
                return 0;
            }

            return Math.Max(0, MaxFailedAttempts - _failedAttempts);
        }
    }

    public bool IsLockedOut(out TimeSpan remainingTime)
    {
        lock (_lock)
        {
            DateTimeOffset now = _timeProvider();
            if (_lockedUntil.HasValue)
            {
                if (_lockedUntil.Value > now)
                {
                    remainingTime = _lockedUntil.Value - now;
                    return true;
                }
                else
                {
                    _lockedUntil = null;
                    _failedAttempts = 0;
                }
            }

            remainingTime = TimeSpan.Zero;
            return false;
        }
    }

    public bool VerifyPin(string enteredPin, string? schoolCode = null, string? auditDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(enteredPin);

        string? currentUserName = null;
        try
        {
            currentUserName = Environment.UserName;
        }
        catch
        {
            // Ignore environment query error
        }

        lock (_lock)
        {
            DateTimeOffset now = _timeProvider();

            // Check if currently locked out
            if (_lockedUntil.HasValue && _lockedUntil.Value > now)
            {
                return false;
            }
            else if (_lockedUntil.HasValue && _lockedUntil.Value <= now)
            {
                _lockedUntil = null;
                _failedAttempts = 0;
            }

            // Constant-time PIN comparison
            byte[] enteredBytes = Encoding.UTF8.GetBytes(enteredPin.Trim());
            byte[] expectedBytes = Encoding.UTF8.GetBytes(_configuredPin);

            bool matches;
            try
            {
                matches = enteredBytes.Length == expectedBytes.Length &&
                          CryptographicOperations.FixedTimeEquals(enteredBytes, expectedBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(enteredBytes);
                CryptographicOperations.ZeroMemory(expectedBytes);
            }

            if (matches)
            {
                _failedAttempts = 0;
                _lockedUntil = null;

                AuditLogger.RecordEntry(new AuditLogEntry
                {
                    Timestamp = now,
                    Event = "teacher_pin_success",
                    Outcome = "SUCCESS",
                    SchoolCode = schoolCode,
                    WindowsUser = currentUserName,
                    Details = "Teacher PIN verification successful."
                }, auditDirectory);

                return true;
            }
            else
            {
                _failedAttempts++;

                if (_failedAttempts >= MaxFailedAttempts)
                {
                    _lockedUntil = now.Add(LockoutDuration);

                    AuditLogger.RecordEntry(new AuditLogEntry
                    {
                        Timestamp = now,
                        Event = "teacher_pin_lockout",
                        Outcome = "FAILURE",
                        OutcomeCode = "PIN_LOCKOUT",
                        SchoolCode = schoolCode,
                        WindowsUser = currentUserName,
                        Details = $"Teacher PIN locked out for {LockoutDuration.TotalMinutes:F0} minutes after {MaxFailedAttempts} failed attempts."
                    }, auditDirectory);
                }
                else
                {
                    int remaining = MaxFailedAttempts - _failedAttempts;

                    AuditLogger.RecordEntry(new AuditLogEntry
                    {
                        Timestamp = now,
                        Event = "teacher_pin_failure",
                        Outcome = "FAILURE",
                        OutcomeCode = "PIN_INVALID",
                        SchoolCode = schoolCode,
                        WindowsUser = currentUserName,
                        Details = $"Invalid teacher PIN attempt. Remaining attempts: {remaining}."
                    }, auditDirectory);
                }

                return false;
            }
        }
    }

    public void ResetLockout()
    {
        lock (_lock)
        {
            _failedAttempts = 0;
            _lockedUntil = null;
        }
    }
}
