using System.Text;
using UtfUnknown;

namespace Delima.Import;

/// <summary>
/// Detects text file encoding (BOM sniffer, null-byte UTF-16 heuristics, and UTF.Unknown).
/// </summary>
public static class FileEncodingDetector
{
    public static Encoding DetectEncoding(Stream stream)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Stream must be seekable to detect encoding.", nameof(stream));
        }

        long originalPosition = stream.Position;
        try
        {
            // 1. Check for standard Byte Order Marks (BOM)
            Span<byte> bom = stackalloc byte[4];
            int read = stream.Read(bom);
            stream.Position = originalPosition;

            if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            {
                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            }
            if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
            {
                return Encoding.Unicode; // UTF-16 LE with BOM
            }
            if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode; // UTF-16 BE with BOM
            }

            // 2. Heuristic check for UTF-16 without BOM (interspersed null bytes in text)
            byte[] sample = new byte[Math.Min(1024, stream.Length)];
            int sampleRead = stream.Read(sample, 0, sample.Length);
            stream.Position = originalPosition;

            if (sampleRead >= 4)
            {
                int oddNulls = 0, evenNulls = 0;
                for (int i = 0; i < sampleRead; i++)
                {
                    if (sample[i] == 0)
                    {
                        if (i % 2 == 1) oddNulls++;
                        else evenNulls++;
                    }
                }

                if (oddNulls > (sampleRead / 4) && evenNulls == 0)
                {
                    return Encoding.Unicode; // UTF-16 LE
                }
                if (evenNulls > (sampleRead / 4) && oddNulls == 0)
                {
                    return Encoding.BigEndianUnicode; // UTF-16 BE
                }
            }

            // 3. Heuristic detection using UTF.Unknown
            var result = CharsetDetector.DetectFromStream(stream);
            stream.Position = originalPosition;

            if (result.Detected != null && result.Detected.Encoding != null && result.Detected.Confidence > 0.5f)
            {
                return result.Detected.Encoding;
            }

            // 4. Default fallback: UTF-8 without BOM (compatible with ASCII / CP1252 basic Latin)
            return Encoding.UTF8;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    public static Encoding DetectEncoding(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        return DetectEncoding(ms);
    }
}
