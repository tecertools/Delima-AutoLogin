using System.Text.Json.Serialization;

namespace Delima.Core.Audit;

/// <summary>
/// Represents a structured entry in the local append-only DELIMa audit log per Technical Architecture §8.
/// Sensitive fields (passwords, picture-password hashes, keys, passphrases, full emails) are strictly prohibited.
/// </summary>
public sealed class AuditLogEntry
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = "SUCCESS";

    [JsonPropertyName("outcome_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutcomeCode { get; set; }

    [JsonPropertyName("target")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Target { get; set; }

    [JsonPropertyName("pupil_account")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PupilAccount { get; set; }

    [JsonPropertyName("student_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StudentId { get; set; }

    [JsonPropertyName("device_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeviceId { get; set; }

    [JsonPropertyName("school_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SchoolCode { get; set; }

    [JsonPropertyName("duration_ms")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? DurationMs { get; set; }

    [JsonPropertyName("software_version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SoftwareVersion { get; set; }

    [JsonPropertyName("store_version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ushort? StoreVersion { get; set; }

    [JsonPropertyName("windows_user")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WindowsUser { get; set; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Details { get; set; }
}
