using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameHub.Widget;

internal static class WidgetStockIconLoader
{
    private static readonly Regex PathRegex = new("d=\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static System.Drawing.Image? TryLoadDefaultIcon(string iconFileName, System.Drawing.Size targetSize, System.Drawing.Color color)
    {
        string iconPath = Path.Combine(GetDefaultIconsPath(), iconFileName);
        if (!File.Exists(iconPath))
        {
            return null;
        }

        try
        {
            string svgContent = File.ReadAllText(iconPath);
            MatchCollection matches = PathRegex.Matches(svgContent);
            if (matches.Count == 0)
            {
                return null;
            }

            GeometryGroup geometryGroup = new();
            foreach (Match match in matches.Cast<Match>())
            {
                string pathData = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(pathData))
                {
                    continue;
                }

                Geometry geometry = Geometry.Parse(pathData);
                geometry.Freeze();
                geometryGroup.Children.Add(geometry);
            }

            if (geometryGroup.Children.Count == 0)
            {
                return null;
            }

            geometryGroup.Freeze();

            DrawingVisual visual = new();
            using (DrawingContext context = visual.RenderOpen())
            {
                double scaleX = targetSize.Width / 16.0;
                double scaleY = targetSize.Height / 16.0;
                context.PushTransform(new ScaleTransform(scaleX, scaleY));
                context.DrawGeometry(new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B)), null, geometryGroup);
                context.Pop();
            }

            RenderTargetBitmap bitmap = new(
                targetSize.Width,
                targetSize.Height,
                96,
                96,
                PixelFormats.Pbgra32);
            bitmap.Render(visual);

            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using MemoryStream stream = new();
            encoder.Save(stream);
            stream.Position = 0;

            using System.Drawing.Image image = System.Drawing.Image.FromStream(stream);
            return new System.Drawing.Bitmap(image);
        }
        catch
        {
            return null;
        }
    }

    private static string GetDefaultIconsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "G.SKILL",
            "WigiDash Manager",
            "DefaultIcons");
    }
}
