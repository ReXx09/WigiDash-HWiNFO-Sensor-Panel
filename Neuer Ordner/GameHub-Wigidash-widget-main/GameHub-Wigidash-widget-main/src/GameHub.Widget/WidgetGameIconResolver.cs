using GameHub.Core.Models;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace GameHub.Widget;

internal static class WidgetGameIconResolver
{
    public static Image Load(GameEntry game, Size targetSize)
    {
        try
        {
            string? steamShortcutIconPath = WidgetDesktopShortcutIconLocator.FindSteamIconPath(game);
            if (!string.IsNullOrWhiteSpace(steamShortcutIconPath) && File.Exists(steamShortcutIconPath))
            {
                return LoadFromIcon(steamShortcutIconPath!, targetSize);
            }

            if (!string.IsNullOrWhiteSpace(game.IconPath) && File.Exists(game.IconPath))
            {
                string extension = Path.GetExtension(game.IconPath);
                if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    return LoadFromIcon(game.IconPath, targetSize);
                }

                using (Image source = Image.FromFile(game.IconPath))
                {
                    return Resize(source, targetSize);
                }
            }

            string? executablePath = FindExecutablePath(game);
            if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath))
            {
                return LoadFromIcon(executablePath!, targetSize);
            }
        }
        catch
        {
        }

        return CreatePlaceholder(game, targetSize);
    }

    private static string? FindExecutablePath(GameEntry game)
    {
        if (File.Exists(game.LaunchCommand) &&
            !Path.GetFileName(game.LaunchCommand).Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) &&
            Path.GetExtension(game.LaunchCommand).Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return game.LaunchCommand;
        }

        if (string.IsNullOrWhiteSpace(game.InstallPath) || !Directory.Exists(game.InstallPath))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(game.InstallPath, "*.exe", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path).IndexOf("unins", StringComparison.OrdinalIgnoreCase) < 0)
            .FirstOrDefault();
    }

    private static Image LoadFromIcon(string path, Size targetSize)
    {
        if (Path.GetExtension(path).Equals(".ico", StringComparison.OrdinalIgnoreCase))
        {
            using (Icon icon = new(path, new Size(Math.Max(targetSize.Width, 64), Math.Max(targetSize.Height, 64))))
            using (Bitmap bitmap = icon.ToBitmap())
            {
                return Resize(bitmap, targetSize);
            }
        }

        using (Icon icon = Icon.ExtractAssociatedIcon(path))
        using (Bitmap bitmap = icon.ToBitmap())
        {
            return Resize(bitmap, targetSize);
        }
    }

    private static Bitmap Resize(Image image, Size targetSize)
    {
        Bitmap canvas = new(targetSize.Width, targetSize.Height);

        using (Graphics graphics = Graphics.FromImage(canvas))
        {
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = image.Width < targetSize.Width || image.Height < targetSize.Height
                ? InterpolationMode.HighQualityBilinear
                : InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);

            Rectangle destination = Fit(image.Size, targetSize);
            graphics.DrawImage(image, destination);
        }

        return canvas;
    }

    private static Bitmap CreatePlaceholder(GameEntry game, Size targetSize)
    {
        Bitmap bitmap = new(targetSize.Width, targetSize.Height);

        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (Brush brush = new SolidBrush(GetPlatformColor(game.Platform)))
        using (Brush textBrush = new SolidBrush(Color.White))
        using (Font font = new("Segoe UI Semibold", 18f))
        using (StringFormat format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            graphics.Clear(Color.Transparent);
            graphics.FillRectangle(brush, new Rectangle(Point.Empty, targetSize));
            graphics.DrawString(GetInitials(game.Name), font, textBrush, new RectangleF(0, 0, targetSize.Width, targetSize.Height), format);
        }

        return bitmap;
    }

    private static Rectangle Fit(Size sourceSize, Size targetSize)
    {
        double ratio = Math.Min(
            targetSize.Width / (double)Math.Max(1, sourceSize.Width),
            targetSize.Height / (double)Math.Max(1, sourceSize.Height));

        int width = Math.Max(1, (int)Math.Round(sourceSize.Width * ratio));
        int height = Math.Max(1, (int)Math.Round(sourceSize.Height * ratio));

        return new Rectangle(
            (targetSize.Width - width) / 2,
            (targetSize.Height - height) / 2,
            width,
            height);
    }

    private static string GetInitials(string name)
    {
        string[] parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "?";
        }

        if (parts.Length == 1)
        {
            return parts[0].Substring(0, 1).ToUpperInvariant();
        }

        return string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
    }

    private static Color GetPlatformColor(GamePlatform platform)
    {
        switch (platform)
        {
            case GamePlatform.Steam:
                return Color.FromArgb(28, 43, 66);
            case GamePlatform.Epic:
                return Color.FromArgb(35, 35, 35);
            case GamePlatform.Ea:
                return Color.FromArgb(183, 54, 255);
            case GamePlatform.Gog:
                return Color.FromArgb(134, 72, 201);
            case GamePlatform.BattleNet:
                return Color.FromArgb(0, 174, 255);
            case GamePlatform.Xbox:
                return Color.FromArgb(16, 124, 16);
            case GamePlatform.Ubisoft:
                return Color.FromArgb(0, 92, 184);
            default:
                return Color.FromArgb(78, 87, 101);
        }
    }
}
