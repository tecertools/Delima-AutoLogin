using System.Text.Json.Serialization;

namespace Delima.Core.Store;

public sealed class MasterBundlePayload
{
    [JsonPropertyName("schema_version")]
    public ushort SchemaVersion { get; set; } = 2;

    [JsonPropertyName("school")]
    public SchoolInfo School { get; set; } = new();

    [JsonPropertyName("theme")]
    public ThemeInfo Theme { get; set; } = new();

    [JsonPropertyName("config")]
    public AppConfig Config { get; set; } = new();

    [JsonPropertyName("generated_at")]
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("classes")]
    public List<ClassInfo> Classes { get; set; } = [];

    [JsonPropertyName("students")]
    public List<StudentInfo> Students { get; set; } = [];
}

public sealed class SchoolInfo
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("motto")]
    public string? Motto { get; set; }

    [JsonPropertyName("domain")]
    public string Domain { get; set; } = "moe-dl.edu.my";
}

public sealed class ThemeInfo
{
    [JsonPropertyName("primary")]
    public string Primary { get; set; } = "#056839";

    [JsonPropertyName("accent")]
    public string Accent { get; set; } = "#F7941D";

    [JsonPropertyName("class_colours")]
    public List<string> ClassColours { get; set; } = [];
}

public sealed class AppConfig
{
    [JsonPropertyName("destinations")]
    public List<DestinationConfig> Destinations { get; set; } = [];

    [JsonPropertyName("picture_password_required")]
    public bool PicturePasswordRequired { get; set; } = true;

    [JsonPropertyName("idle_reset_seconds")]
    public int IdleResetSeconds { get; set; } = 600;

    [JsonPropertyName("injection_settle_ms")]
    public int InjectionSettleMs { get; set; } = 400;

    [JsonPropertyName("window_wait_timeout_ms")]
    public int WindowWaitTimeoutMs { get; set; } = 30000;

    [JsonPropertyName("store_max_age_days")]
    public int StoreMaxAgeDays { get; set; } = 30;
}

public sealed class DestinationConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
}

public sealed class ClassInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("grade")]
    public int Grade { get; set; }

    [JsonPropertyName("colour_index")]
    public int ColourIndex { get; set; }
}

public sealed class StudentInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("class_id")]
    public string ClassId { get; set; } = "";

    [JsonPropertyName("email_local")]
    public string EmailLocal { get; set; } = "";

    [JsonPropertyName("avatar")]
    public string Avatar { get; set; } = "";

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("password_version")]
    public int PasswordVersion { get; set; } = 1;

    [JsonPropertyName("password_updated_at")]
    public DateTimeOffset? PasswordUpdatedAt { get; set; }

    [JsonPropertyName("picture_password")]
    public PicturePasswordInfo? PicturePassword { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; } = true;

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PicturePasswordInfo
{
    [JsonPropertyName("algo")]
    public string Algo { get; set; } = "argon2id";

    [JsonPropertyName("salt")]
    public string Salt { get; set; } = "";

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = "";
}
