using GameHub.Core.Models;

namespace GameHub.Widget;

internal static class WidgetDesktopShortcutIconLocator
{
    private static readonly Lazy<IReadOnlyList<ShortcutRecord>> Shortcuts = new(LoadShortcuts);

    public static string? FindSteamIconPath(GameEntry game)
    {
        if (game.Platform != GamePlatform.Steam)
        {
            return null;
        }

        string normalizedGameName = Normalize(game.Name);

        return Shortcuts.Value
            .Where(shortcut => shortcut.NormalizedName.Equals(normalizedGameName, StringComparison.Ordinal))
            .Select(shortcut => shortcut.IconSourcePath)
            .FirstOrDefault(File.Exists);
    }

    private static IReadOnlyList<ShortcutRecord> LoadShortcuts()
    {
        List<ShortcutRecord> shortcuts = [];

        foreach (string folderPath in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        })
        {
            if (!Directory.Exists(folderPath))
            {
                continue;
            }

            foreach (string shortcutPath in Directory.EnumerateFiles(folderPath, "*.url", SearchOption.TopDirectoryOnly))
            {
                Dictionary<string, string> values = File
                    .ReadAllLines(shortcutPath)
                    .Select(line => line.Split(new[] { '=' }, 2))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);

                string name = Path.GetFileNameWithoutExtension(shortcutPath);
                if (values.TryGetValue("IconFile", out string? iconSourcePath) &&
                    !string.IsNullOrWhiteSpace(iconSourcePath))
                {
                    shortcuts.Add(new ShortcutRecord(name, Normalize(name), iconSourcePath));
                }
            }
        }

        return shortcuts;
    }

    private static string Normalize(string value)
    {
        char[] buffer = value
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray();

        return string.Join(
            " ",
            new string(buffer).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed class ShortcutRecord
    {
        public ShortcutRecord(string name, string normalizedName, string iconSourcePath)
        {
            Name = name;
            NormalizedName = normalizedName;
            IconSourcePath = iconSourcePath;
        }

        public string Name { get; }

        public string NormalizedName { get; }

        public string IconSourcePath { get; }
    }
}
