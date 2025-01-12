using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LockScreenImages;

public static class Extensions
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// An updated string truncation helper.
    /// </summary>
    public static string Truncate(this string text, int maxLength, string mesial = "…")
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (maxLength > 0 && text.Length > maxLength)
        {
            var limit = maxLength / 2;
            if (limit > 1)
            {
                return String.Format("{0}{1}{2}", text.Substring(0, limit).Trim(), mesial, text.Substring(text.Length - limit).Trim());
            }
            else
            {
                var tmp = text.Length <= maxLength ? text : text.Substring(0, maxLength).Trim();
                return String.Format("{0}{1}", tmp, mesial);
            }
        }
        return text;
    }

    /// <summary>
    /// Creates an <see cref="Icon"/> from the given <see cref="Image"/>.
    /// </summary>
    /// <param name="image"><see cref="Image"/></param>
    /// <returns><see cref="Icon"/></returns>
    public static Icon? ToIcon(this Image image)
    {
        if (image is null)
            return null;

        // Create a bitmap from the image
        using (Bitmap bitmap = new Bitmap(image))
        {
            IntPtr hIcon = bitmap.GetHicon();

            // Create an icon from the HICON
            Icon icon = Icon.FromHandle(hIcon);

            // NOTE: We don't want to clean up the hIcon since accessing
            //       it could cause the System.ObjectDisposedException.
            //DestroyIcon(hIcon);

            return icon;
        }
    }

    public static string HumanReadableSize(this long length)
    {
        const int unit = 1024;
        var mu = new List<string> { "B", "KB", "MB", "GB", "PT" };
        while (length > unit)
        {
            mu.RemoveAt(0);
            length /= unit;
        }
        return $"{length}{mu[0]}";
    }

    /// <summary>
    /// Determines the type of an image file by inspecting its header.
    /// </summary>
    /// <param name="filePath">Path to the image file.</param>
    /// <returns>The type of the image (e.g., "jpg", "png", "gif", etc.) or "Unknown" if not recognized.</returns>
    public static string DetermineType(this string imageFilePath, bool dumpHeader = false)
    {
        if (!File.Exists(imageFilePath)) { return string.Empty; }

        try
        {
            using (var stream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new BinaryReader(stream))
                {
                    byte[] header = reader.ReadBytes(16);

                    if (dumpHeader)
                    {
                        Debug.WriteLine($"[IMAGE HEADER]");
                        foreach (var b in header)
                        {
                            if (b > 31)
                                Debug.Write($"{(char)b}");
                        }
                        Debug.WriteLine($"");
                    }
                    // Check for JPEG signature (bytes 6-9 should be 'J', 'F', 'I', 'F' or 'E' 'x' 'i' 'f')
                    if (header.Length >= 10 &&
                        header[6] == 'J' && header[7] == 'F' && header[8] == 'I' && header[9] == 'F')
                    {
                        return "jpg";
                    }
                    if (header.Length >= 9 && 
                        header[6] == 'E' && 
                       (header[7] == 'x' || header[7] == 'X') && 
                       (header[8] == 'i' || header[8] == 'I') && 
                       (header[9] == 'f' || header[7] == 'F'))
                    {
                        return "jpg";
                    }
                    if (header.Length >= 9 && 
                        header[6] == 'J' && 
                       (header[7] == 'P' || header[7] == 'p') && 
                       (header[8] == 'E' || header[8] == 'e') && 
                       (header[9] == 'G' || header[9] == 'g'))
                    {
                        return "jpg";
                    }
                    // Check for PNG signature (bytes 0-7: 89 50 4E 47 0D 0A 1A 0A)
                    if (header.Length >= 8 &&
                        header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E &&
                        header[3] == 0x47 && header[4] == 0x0D && header[5] == 0x0A &&
                        header[6] == 0x1A && header[7] == 0x0A)
                    {
                        return "png";
                    }
                    // Check for GIF signature (bytes 0-2: "GIF")
                    if (header.Length >= 6 &&
                        header[0] == 'G' && header[1] == 'I' && header[2] == 'F')
                    {
                        return "gif";
                    }
                    // Check for BMP signature (bytes 0-1: "BM")
                    if (header.Length >= 2 &&
                        header[0] == 'B' && header[1] == 'M')
                    {
                        return "bmp";
                    }
                    // Check for TIFF signature (bytes 0-3: "II*" or "MM*")
                    if (header.Length >= 4 &&
                        ((header[0] == 'I' && header[1] == 'I' && header[2] == 0x2A && header[3] == 0x00) ||
                         (header[0] == 'M' && header[1] == 'M' && header[2] == 0x00 && header[3] == 0x2A)))
                    {
                        return "tiff";
                    }
                    // Check for WebP signature (bytes 0-3: "RIFF", bytes 8-11: "WEBP")
                    if (header.Length >= 12 &&
                        header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F' &&
                        header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P')
                    {
                        return "webp";
                    }
                    // Check for HEIC/HEIF signature (bytes 4-11: "ftypheic" or "ftypheif")
                    if (header.Length >= 12 &&
                        header[4] == 'f' && header[5] == 't' && header[6] == 'y' && header[7] == 'p' &&
                       (header[8] == 'h' && header[9] == 'e' && header[10] == 'i' && header[11] == 'c'))
                    {
                        return "heic";
                    }
                    if (header.Length >= 12 &&
                        header[4] == 'f' && header[5] == 't' && header[6] == 'y' && header[7] == 'p' &&
                       (header[8] == 'h' && header[9] == 'e' && header[10] == 'i' && header[11] == 'f'))
                    {
                        return "heif";
                    }
                    // Signature not defined
                    return "Unknown"; 
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] DetermineImageType: {ex.Message}");
        }

        return string.Empty;
    }
}
