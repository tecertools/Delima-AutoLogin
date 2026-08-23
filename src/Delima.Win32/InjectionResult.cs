namespace Delima.Win32;

/// <summary>
/// Represents the outcome of an injection operation.
/// </summary>
public sealed record InjectionResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? PupilMessage { get; init; }
    public string? TeacherAction { get; init; }
    public int CharactersInjected { get; init; }
    public bool BlockInputGranted { get; init; }
    public TimeSpan Elapsed { get; init; }

    public static InjectionResult Succeeded(int charsInjected, bool blockInputGranted, TimeSpan elapsed) =>
        new()
        {
            Success = true,
            CharactersInjected = charsInjected,
            BlockInputGranted = blockInputGranted,
            Elapsed = elapsed
        };

    public static InjectionResult Aborted(int charsInjected, TimeSpan elapsed) =>
        new()
        {
            Success = false,
            ErrorCode = FailureCodes.E03_InjectionAborted,
            PupilMessage = FailureCodes.GetPupilMessageBm(FailureCodes.E03_InjectionAborted),
            TeacherAction = FailureCodes.GetTeacherAction(FailureCodes.E03_InjectionAborted),
            CharactersInjected = charsInjected,
            Elapsed = elapsed
        };

    public static InjectionResult WindowTimeout(TimeSpan elapsed) =>
        new()
        {
            Success = false,
            ErrorCode = FailureCodes.E02_WindowNotVerified,
            PupilMessage = FailureCodes.GetPupilMessageBm(FailureCodes.E02_WindowNotVerified),
            TeacherAction = FailureCodes.GetTeacherAction(FailureCodes.E02_WindowNotVerified),
            CharactersInjected = 0,
            Elapsed = elapsed
        };

    public static InjectionResult WindowLost(int charsInjected, TimeSpan elapsed) =>
        new()
        {
            Success = false,
            ErrorCode = FailureCodes.E02_WindowNotVerified,
            PupilMessage = FailureCodes.GetPupilMessageBm(FailureCodes.E02_WindowNotVerified),
            TeacherAction = FailureCodes.GetTeacherAction(FailureCodes.E02_WindowNotVerified),
            CharactersInjected = charsInjected,
            Elapsed = elapsed
        };

    public static InjectionResult BrowserNotFound() =>
        new()
        {
            Success = false,
            ErrorCode = FailureCodes.E01_NoBrowserFound,
            PupilMessage = FailureCodes.GetPupilMessageBm(FailureCodes.E01_NoBrowserFound),
            TeacherAction = FailureCodes.GetTeacherAction(FailureCodes.E01_NoBrowserFound),
            CharactersInjected = 0,
            Elapsed = TimeSpan.Zero
        };

    [Obsolete("Use BrowserNotFound() instead.")]
    public static InjectionResult ChromeNotFound() => BrowserNotFound();

    public static InjectionResult Failure(string errorCode, int charsInjected, bool blockInputGranted, TimeSpan elapsed) =>
        new()
        {
            Success = false,
            ErrorCode = errorCode,
            PupilMessage = FailureCodes.GetPupilMessageBm(errorCode),
            TeacherAction = FailureCodes.GetTeacherAction(errorCode),
            CharactersInjected = charsInjected,
            BlockInputGranted = blockInputGranted,
            Elapsed = elapsed
        };
}
