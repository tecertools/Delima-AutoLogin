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

    private static readonly SemaphoreSlim _downloadThrottle = new(6, 6);

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
    public static bool IsCached(string seed)
    {
        string path = GetCachedPath(seed);
        return File.Exists(path) && new FileInfo(path).Length > 0;
    }

    /// <summary>
    /// Downloads and caches the avatar PNG if not already cached.
    /// Falls back to generating a local procedural critter PNG on download failure or offline state.
    /// Returns the local file path.
    /// </summary>
    public static async Task<string?> EnsureCachedAsync(string seed, CancellationToken ct = default)
    {
        string path = GetCachedPath(seed);
        if (File.Exists(path) && new FileInfo(path).Length > 0)
            return path;

        await _downloadThrottle.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length > 0)
                return path;

            try
            {
                byte[] data = await _http.GetByteArrayAsync(GetAvatarUrl(seed), ct).ConfigureAwait(false);
                if (data.Length > 0)
                {
                    await File.WriteAllBytesAsync(path, data, ct).ConfigureAwait(false);
                    return path;
                }
            }
            catch
            {
                // Network unavailable, rate-limited, or offline: generate local procedural fallback
            }

            // Ensure fallback is written
            EnsureFallbackGenerated(seed);
            return path;
        }
        finally
        {
            _downloadThrottle.Release();
        }
    }

    /// <summary>
    /// Returns a local file:// URI for the avatar image.
    /// If the image is not yet cached, generates a colorful procedural fallback avatar immediately
    /// and initiates an async background download for the official DiceBear artwork.
    /// </summary>
    public static string GetLocalOrRemoteUri(string seed)
    {
        string path = GetCachedPath(seed);
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            EnsureFallbackGenerated(seed);
            _ = Task.Run(() => EnsureCachedAsync(seed));
        }

        return new Uri(path).AbsoluteUri;
    }

    /// <summary>
    /// Synchronously writes a procedural fallback avatar PNG if no file exists yet.
    /// </summary>
    public static void EnsureFallbackGenerated(string seed)
    {
        string path = GetCachedPath(seed);
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            try
            {
                byte[] fallbackPng = GenerateFallbackPng(seed);
                File.WriteAllBytes(path, fallbackPng);
            }
            catch
            {
                // Ignore file write conflicts if another thread is writing
            }
        }
    }

    /// <summary>
    /// Pre-caches a batch of seeds in parallel with concurrency throttling.
    /// </summary>
    public static async Task PreCacheBatchAsync(IEnumerable<string> seeds, CancellationToken ct = default)
    {
        EnsureCacheDir();
        var uniqueSeeds = seeds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // 1. Generate local fallbacks immediately so UI has 100% avatars loaded
        foreach (var s in uniqueSeeds)
        {
            EnsureFallbackGenerated(s);
        }

        // 2. Fetch remote avatars in background
        var tasks = uniqueSeeds.Select(s => EnsureCachedAsync(s, ct));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>Removes all cached PNGs.</summary>
    public static void ClearCache()
    {
        if (!Directory.Exists(CacheDir)) return;
        foreach (var f in Directory.GetFiles(CacheDir, "*.png"))
        {
            try { File.Delete(f); } catch { }
        }
    }

    // ── Procedural Fallback Avatar PNG Generator ───────────────────────────────

    private static readonly (byte R, byte G, byte B)[] ColorPalette =
    [
        (79, 70, 229),   // Indigo
        (5, 150, 105),   // Emerald
        (217, 119, 6),   // Amber
        (225, 29, 72),   // Rose
        (37, 99, 235),   // Blue
        (124, 58, 237),  // Violet
        (8, 145, 178),   // Cyan
        (234, 88, 12),   // Orange
        (192, 38, 211),  // Fuchsia
        (13, 148, 136),  // Teal
        (101, 163, 13),  // Lime
        (220, 38, 38),   // Red
        (99, 102, 241),  // Light Indigo
        (16, 185, 129),  // Light Emerald
        (245, 158, 11),  // Light Amber
        (236, 72, 153),  // Pink
    ];

    /// <summary>
    /// Generates an adorable 128x128 critter avatar PNG in pure C# (100% offline & zero external dependencies).
    /// </summary>
    public static byte[] GenerateFallbackPng(string seed)
    {
        uint hash = HashSeed(seed);
        int bgIdx = (int)(hash % (uint)ColorPalette.Length);
        var (bgR, bgG, bgB) = ColorPalette[bgIdx];

        int earType = (int)((hash >> 4) % 4);
        int eyeType = (int)((hash >> 6) % 3);

        const int size = 128;
        byte[] rgba = new byte[size * size * 4];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int offset = (y * size + x) * 4;

                // Rounded card border (radius 24)
                int cornerX = Math.Max(0, Math.Abs(x - 64) - (64 - 24));
                int cornerY = Math.Max(0, Math.Abs(y - 64) - (64 - 24));
                if (cornerX * cornerX + cornerY * cornerY > 24 * 24)
                {
                    // Transparent
                    continue;
                }

                // Default: Background color
                byte r = bgR, g = bgG, b = bgB, a = 255;

                // 1. Ears / Antennas
                bool isEar = false;
                if (earType == 0) // Round ears (Bear)
                {
                    isEar = DistSq(x, y, 36, 38) <= 15 * 15 || DistSq(x, y, 92, 38) <= 15 * 15;
                }
                else if (earType == 1) // Pointy ears (Cat)
                {
                    isEar = (x >= 24 && x <= 46 && y >= 18 && y <= 48 && (46 - x) >= (y - 18)) ||
                            (x >= 82 && x <= 104 && y >= 18 && y <= 48 && (x - 82) >= (y - 18));
                }
                else if (earType == 2) // Tall ears (Bunny)
                {
                    int clampY = y < 34 ? y : 34;
                    isEar = DistSq(x, clampY, 40, 26) <= 11 * 11 || DistSq(x, clampY, 88, 26) <= 11 * 11;
                }
                else // Antennas
                {
                    isEar = (Math.Abs(x - 42) <= 2 && y >= 20 && y <= 46) ||
                            (Math.Abs(x - 86) <= 2 && y >= 20 && y <= 46) ||
                            DistSq(x, y, 42, 18) <= 7 * 7 || DistSq(x, y, 86, 18) <= 7 * 7;
                }

                if (isEar)
                {
                    r = 255; g = 255; b = 255;
                }

                // 2. Main Head / Body (Cream / White)
                if (DistSq(x, y, 64, 70) <= 38 * 38)
                {
                    r = 255; g = 255; b = 255;
                }

                // 3. Rosy Cheeks
                if (DistSq(x, y, 38, 74) <= 6 * 6 || DistSq(x, y, 90, 74) <= 6 * 6)
                {
                    r = 255; g = 145; b = 170;
                }

                // 4. Eyes
                int leftEyeX = 48, rightEyeX = 80, eyeY = 62;
                bool inLeftEye = DistSq(x, y, leftEyeX, eyeY) <= 8 * 8;
                bool inRightEye = DistSq(x, y, rightEyeX, eyeY) <= 8 * 8;

                if (inLeftEye || inRightEye)
                {
                    // Eye Pupil (Charcoal)
                    r = 30; g = 41; b = 59;

                    // Eye Sparkle Highlight
                    if (DistSq(x, y, leftEyeX - 2, eyeY - 2) <= 2 * 2 || DistSq(x, y, rightEyeX - 2, eyeY - 2) <= 2 * 2)
                    {
                        r = 255; g = 255; b = 255;
                    }
                }

                // 5. Cute Snout / Nose
                if (DistSq(x, y, 64, 72) <= 3 * 3)
                {
                    r = 30; g = 41; b = 59;
                }

                // 6. Cute Smile
                if (y >= 76 && y <= 80 && Math.Abs(x - 64) <= 8)
                {
                    int dyMouth = (Math.Abs(x - 64) * Math.Abs(x - 64)) / 16;
                    if (y == 76 + dyMouth || y == 77 + dyMouth)
                    {
                        r = 30; g = 41; b = 59;
                    }
                }

                rgba[offset] = r;
                rgba[offset + 1] = g;
                rgba[offset + 2] = b;
                rgba[offset + 3] = a;
            }
        }

        return EncodePng(size, size, rgba);
    }

    private static int DistSq(int x1, int y1, int x2, int y2)
    {
        int dx = x1 - x2;
        int dy = y1 - y2;
        return dx * dx + dy * dy;
    }

    private static uint HashSeed(string seed)
    {
        uint hash = 2166136261;
        foreach (char c in seed ?? string.Empty)
        {
            hash = (hash ^ c) * 16777619;
        }
        return hash;
    }

    private static byte[] EncodePng(int width, int height, byte[] rgba)
    {
        using var ms = new MemoryStream();

        // 1. PNG Signature
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        // 2. IHDR Chunk
        byte[] ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, (uint)width);
        WriteBigEndian(ihdr, 4, (uint)height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // color type RGBA
        ihdr[10] = 0; // compression
        ihdr[11] = 0; // filter
        ihdr[12] = 0; // interlace
        WriteChunk(ms, "IHDR", ihdr);

        // 3. IDAT Chunk (Raw scanlines with 0-filter byte per row, compressed with ZLibStream)
        byte[] rawScanlines = new byte[height * (1 + width * 4)];
        int rawOffset = 0;
        int rgbaOffset = 0;
        int rowBytes = width * 4;

        for (int y = 0; y < height; y++)
        {
            rawScanlines[rawOffset++] = 0; // Filter: None
            Buffer.BlockCopy(rgba, rgbaOffset, rawScanlines, rawOffset, rowBytes);
            rawOffset += rowBytes;
            rgbaOffset += rowBytes;
        }

        using var idatMs = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(idatMs, System.IO.Compression.CompressionLevel.Fastest, true))
        {
            zlib.Write(rawScanlines, 0, rawScanlines.Length);
        }
        WriteChunk(ms, "IDAT", idatMs.ToArray());

        // 4. IEND Chunk
        WriteChunk(ms, "IEND", []);

        return ms.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        byte[] lenBytes = new byte[4];
        WriteBigEndian(lenBytes, 0, (uint)data.Length);
        stream.Write(lenBytes);

        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        if (data.Length > 0)
        {
            stream.Write(data);
        }

        byte[] crcPayload = new byte[typeBytes.Length + data.Length];
        Buffer.BlockCopy(typeBytes, 0, crcPayload, 0, typeBytes.Length);
        if (data.Length > 0)
        {
            Buffer.BlockCopy(data, 0, crcPayload, typeBytes.Length, data.Length);
        }

        uint crc = Crc32(crcPayload);
        byte[] crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, crc);
        stream.Write(crcBytes);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                crc = (crc >> 1) ^ (0xEDB88320 & ~((crc & 1) - 1));
            }
        }
        return ~crc;
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
        var sb = new StringBuilder(seed?.Length ?? 0);
        foreach (char c in seed ?? string.Empty)
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
