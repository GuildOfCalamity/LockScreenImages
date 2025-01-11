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

    public static bool IsJPG(this string filePath, bool dumpHeader = false)
    {
        try
        {
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                using (FileStream fsStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    using (BinaryReader brReader = new BinaryReader(fsStream, System.Text.Encoding.Default))
                    {
                        //fsStream.Seek(0x06, SeekOrigin.Begin);
                        var sample = brReader.ReadBytes(16);
                        if (dumpHeader)
                        {
                            Debug.WriteLine($"[IMAGE HEADER]");
                            foreach (var b in sample)
                            {
                                if (b > 31)
                                    Debug.Write($"{(char)b}");
                            }
                            Debug.WriteLine($"");
                        }
                        if (sample[6] == 'E' && (sample[7] == 'x' || sample[7] == 'X') && (sample[8] == 'i' || sample[8] == 'I') && (sample[9] == 'f' || sample[7] == 'F'))
                            return true;
                        else if (sample[6] == 'J' && (sample[7] == 'F' || sample[7] == 'f') && (sample[8] == 'I' || sample[8] == 'i') && (sample[9] == 'F' || sample[9] == 'f'))
                            return true;
                        else if (sample[6] == 'J' && (sample[7] == 'P' || sample[7] == 'p') && (sample[8] == 'E' || sample[8] == 'e') && (sample[9] == 'G' || sample[9] == 'g'))
                            return true;
                        else if (sample[6] == 'J' && (sample[7] == 'P' || sample[7] == 'p') && (sample[8] == 'G' || sample[8] == 'g'))
                            return true;
                        else if (sample[0] == 'P' && sample[1] == 'N' && sample[2] == 'G')
                            return false;
                    }
                }
            }
        }
        catch (Exception) { }

        return false;
    }
}
