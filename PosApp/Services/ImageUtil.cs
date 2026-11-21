using Avalonia.Media.Imaging;

namespace PosApp.Services;

public static class ImageUtil
{
    public static Bitmap? ToBitmap(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0) return null;
        try
        {
            using var ms = new MemoryStream(bytes);
            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Shrinks a picked image so the database stays small. Returns PNG bytes, or null if unreadable.</summary>
    public static byte[]? Downscale(Stream input, int width = 480)
    {
        try
        {
            using var buffer = new MemoryStream();
            input.CopyTo(buffer);
            buffer.Position = 0;
            using var bmp = Bitmap.DecodeToWidth(buffer, width);
            using var output = new MemoryStream();
            bmp.Save(output);
            return output.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
