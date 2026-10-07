using System.Drawing;
using System.Drawing.Drawing2D;

namespace GameHub.Widget;

internal static class WidgetWeModArtworkLoader
{
    public static Image? TryLoad(Size targetSize)
    {
        if (!WidgetSystemActions.TryGetWeModExecutablePath(out string? targetPath) || string.IsNullOrWhiteSpace(targetPath))
        {
            return null;
        }

        try
        {
            using Icon? icon = Icon.ExtractAssociatedIcon(targetPath);
            if (icon is null)
            {
                return null;
            }

            using Bitmap bitmap = icon.ToBitmap();
            return Resize(bitmap, targetSize);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap Resize(Image image, Size targetSize)
    {
        Bitmap canvas = new(targetSize.Width, targetSize.Height);

        using Graphics graphics = Graphics.FromImage(canvas);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.Clear(Color.Transparent);
        graphics.DrawImage(image, Fit(image.Size, targetSize));

        return canvas;
    }

    private static Rectangle Fit(Size sourceSize, Size targetSize)
    {
        double ratio = Math.Min(
            targetSize.Width / (double)Math.Max(sourceSize.Width, 1),
            targetSize.Height / (double)Math.Max(sourceSize.Height, 1));

        int width = Math.Max(1, (int)Math.Round(sourceSize.Width * ratio));
        int height = Math.Max(1, (int)Math.Round(sourceSize.Height * ratio));
        int x = (targetSize.Width - width) / 2;
        int y = (targetSize.Height - height) / 2;

        return new Rectangle(x, y, width, height);
    }
}
