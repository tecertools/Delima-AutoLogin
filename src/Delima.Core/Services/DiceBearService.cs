using System.Net.Http;
using System.Text;

namespace Delima.Core.Services;

/// <summary>
/// Provides DiceBear Critters avatar URLs and manages a local PNG cache.
/// API: https://api.dicebear.com/10.x/critters/png?seed={seed}&amp;size=128
/// </summary>
public static class DiceBearService
{
    // ── Constants ────────────────────────────────────────────────────────────

    private const string BaseUrl = "https://api.dicebear.com/10.x/critters/png";
    private const int AvatarSize = 128;

    /// <summary>
    /// Old-style avatar keys from the previous animal-name system.
    /// These are treated as "no custom seed" and resolved to the student ID.
    /// </summary>
    public static readonly HashSet<string> LegacyKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "kucing", "cat",
        "buaya", "crocodile",
        "helang", "eagle",
        "gajah", "elephant",
        "memerang", "otter",
        "rakun", "raccoon",
        "kuda_belang", "kudabelang", "zebra",
        "semut", "ant",
        "bizon", "bison", "seladang",
        "ayam", "chicken",
        "anjing", "dog", "puppy",
        "doraemon",
        "itik", "duck",
        "musang", "fox", "rubah",
        "zirafah", "giraffe",
        "koala",
        "harimau_bintang", "harimaubintang", "leopard",
        "tikus", "mouse",
        "penguin",
        "pikachu",
        "biri_biri", "biribiri", "sheep", "kambing",
        "sloth",
        "ikan", "fish",
        "bunga", "flower",
        "kereta", "car",
        "bola", "ball",
        "bintang", "star",
        "burung", "bird",
        "rama_rama", "butterfly",
        "epal", "apple",
        "pokok", "tree",
        "awan", "cloud",
        "matahari", "sun",
        "buku", "book",
        "pensel", "pencil",
        "kapal", "ship",
        "bulan", "moon",
        "rumah", "house",
        "payung",
        "belon",
        "pisang",
        "jam",
        "topi",
        "avatar1", "avatar2", "avatar3", "avatar4",
        "avatar5", "avatar6", "avatar7", "avatar8",
        "missing",
    };

    // ── Cache ────────────────────────────────────────────────────────────────

    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Delima", "AvatarCache");

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the stored avatar value is a legacy animal key that
    /// should be ignored in favour of the student's ID as seed.
    /// </summary>
    public static bool IsLegacyKey(string? value) =>
        string.IsNullOrWhiteSpace(value) || LegacyKeys.Contains(value.Trim());

    /// <summary>
    /// Resolves the effective DiceBear seed for a student.
    /// If the stored avatar is blank or a legacy animal key, falls back to studentId.
    /// </summary>
    public static string ResolveSeed(string? storedAvatar, string studentId) =>
        IsLegacyKey(storedAvatar) ? studentId : storedAvatar!.Trim();

    /// <summary>
    /// Builds the HTTPS URL for a critters avatar PNG at the given seed.
    /// </summary>
    public static string GetAvatarUrl(string seed) =>
        $"{BaseUrl}?seed={Uri.EscapeDataString(seed)}&size={AvatarSize}";

    /// <summary>
    /// Returns the local cache path for a given seed.
    /// </summary>
    public static string GetCachedPath(string seed)
    {
        EnsureCacheDir();
        string safe = SanitiseSeed(seed);
        return Path.Combine(CacheDir, $"{safe}.png");
    }

    /// <summary>
    /// Returns true if a local cached PNG exists for the seed.
    /// </summary>
    public static bool IsCached(string seed) => File.Exists(GetCachedPath(seed));

    /// <summary>
    /// Downloads and caches the avatar PNG if not already cached.
    /// Returns the local file path on success, or null on failure.
    /// </summary>
    public static async Task<string?> EnsureCachedAsync(string seed, CancellationToken ct = default)
    {
        string path = GetCachedPath(seed);
        if (File.Exists(path))
            return path;

        try
        {
            byte[] data = await _http.GetByteArrayAsync(GetAvatarUrl(seed), ct).ConfigureAwait(false);
            await File.WriteAllBytesAsync(path, data, ct).ConfigureAwait(false);
            return path;
        }
        catch
        {
            // Download failure is non-fatal — caller falls back to the HTTP URL
            return null;
        }
    }

    /// <summary>
    /// Returns a local file:// URI if the image is cached; otherwise the HTTPS URL.
    /// WPF BitmapImage can load both.
    /// </summary>
    public static string GetLocalOrRemoteUri(string seed)
    {
        string path = GetCachedPath(seed);
        return File.Exists(path)
            ? new Uri(path).AbsoluteUri   // "file:///C:/..."
            : GetAvatarUrl(seed);
    }

    /// <summary>
    /// Pre-caches a batch of seeds in parallel (best-effort, ignores failures).
    /// Call fire-and-forget from the UI thread.
    /// </summary>
    public static async Task PreCacheBatchAsync(IEnumerable<string> seeds, CancellationToken ct = default)
    {
        EnsureCacheDir();
        var tasks = seeds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(s => !IsCached(s))
            .Select(s => EnsureCachedAsync(s, ct));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>Removes all cached PNGs.</summary>
    public static void ClearCache()
    {
        if (!Directory.Exists(CacheDir)) return;
        foreach (var f in Directory.GetFiles(CacheDir, "*.png"))
            File.Delete(f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void EnsureCacheDir()
    {
        if (!Directory.Exists(CacheDir))
            Directory.CreateDirectory(CacheDir);
    }

    /// <summary>Makes a seed safe for use as a filename (max 64 chars).</summary>
    private static string SanitiseSeed(string seed)
    {
        var sb = new StringBuilder(seed.Length);
        foreach (char c in seed)
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                sb.Append(c);
            else
                sb.Append('_');
        }
        string s = sb.ToString();
        return s.Length > 64 ? s[..64] : s;
    }
}
