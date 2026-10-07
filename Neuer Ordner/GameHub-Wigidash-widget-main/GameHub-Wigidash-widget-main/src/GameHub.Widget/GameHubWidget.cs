using GameHub.Core.BattleNet;
using GameHub.Core.Epic;
using GameHub.Core.EA;
using GameHub.Core.Gog;
using GameHub.Core.Models;
using GameHub.Core.Services;
using GameHub.Core.Steam;
using GameHub.Core.Ubisoft;
using GameHub.Core.Xbox;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;
using WigiDashWidgetFramework;
using WigiDashWidgetFramework.WidgetUtility;

namespace GameHub.Widget;

public sealed class GameHubWidgetServer : IWidgetObject, IWidgetBase
{
    private static readonly Guid WidgetId = new("8A9E2A7E-6B91-4D93-8C87-03B93BC0A6B7");
    private static readonly WidgetSize DefaultPreviewSize = new(5, 4);

    private readonly Dictionary<Guid, GameHubWidgetInstance> _instances = new();

    public Guid Guid => WidgetId;

    public string Name => "Game Hub";

    public string Author => "OpenAI Codex";

    public string Website => "https://github.com/openai";

    public string Description => "Paged launcher widget for Steam, Epic, EA, GOG, Battle.net, Xbox/Game Pass, and Ubisoft libraries.";

    public Version Version => new(1, 0, 0, 0);

    public SdkVersion TargetSdk => SdkVersion.Version_1;

    public List<WidgetSize> SupportedSizes { get; } =
    [
        new WidgetSize(5, 4),
    ];

    public Bitmap PreviewImage => GetWidgetPreview(DefaultPreviewSize);

    public IWidgetManager WidgetManager { get; set; } = null!;

    public Bitmap WidgetThumbnail => RenderThumbnail();

    public string LastErrorMessage { get; set; } = string.Empty;

    public WidgetError Load(string resourcePath)
    {
        return WidgetError.NO_ERROR;
    }

    public WidgetError Unload()
    {
        foreach (GameHubWidgetInstance instance in _instances.Values.ToArray())
        {
            instance.Dispose();
        }

        _instances.Clear();
        return WidgetError.NO_ERROR;
    }

    public IWidgetInstance CreateWidgetInstance(WidgetSize widgetSize, Guid instanceGuid)
    {
        GameHubWidgetInstance instance = new(this, widgetSize, instanceGuid);
        _instances[instanceGuid] = instance;
        return instance;
    }

    public bool RemoveWidgetInstance(Guid instanceGuid)
    {
        if (!_instances.TryGetValue(instanceGuid, out GameHubWidgetInstance instance))
        {
            return false;
        }

        instance.Dispose();
        _instances.Remove(instanceGuid);
        return true;
    }

    public Bitmap GetWidgetPreview(WidgetSize widgetSize)
    {
        return RenderPreview(widgetSize);
    }

    private static Bitmap RenderPreview(WidgetSize widgetSize)
    {
        Size bitmapSize = GameHubWidgetInstance.GetBitmapSize(widgetSize);
        Bitmap bitmap = new(bitmapSize.Width, bitmapSize.Height);

        Color accentColor = Color.FromArgb(255, 142, 52);
        Color accentSoftColor = Color.FromArgb(163, 74, 36);
        Color panelColor = Color.FromArgb(48, 32, 28);
        Color selectedPanelColor = Color.FromArgb(82, 50, 40);
        Color backgroundColor = Color.FromArgb(16, 10, 9);
        Color cardColor = Color.FromArgb(33, 20, 18);
        Color sidebarColor = Color.FromArgb(24, 15, 14);
        Color borderColor = Color.FromArgb(142, 94, 72);

        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (Brush background = new SolidBrush(backgroundColor))
        using (Brush card = new SolidBrush(cardColor))
        using (Brush sidebar = new SolidBrush(sidebarColor))
        using (Brush panel = new SolidBrush(panelColor))
        using (Brush selectedPanel = new SolidBrush(selectedPanelColor))
        using (Brush accent = new SolidBrush(accentColor))
        using (Brush accentSoft = new SolidBrush(accentSoftColor))
        using (Brush text = new SolidBrush(Color.White))
        using (Brush mutedText = new SolidBrush(Color.FromArgb(218, 201, 192)))
        using (Pen border = new(borderColor))
        using (Font titleFont = new("Segoe UI Semibold", 15f))
        using (Font bodyFont = new("Segoe UI Semibold", 9.5f))
        using (Font smallFont = new("Segoe UI", 8.5f))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.FillRectangle(background, new Rectangle(Point.Empty, bitmapSize));

            Rectangle outer = new(10, 10, bitmapSize.Width - 20, bitmapSize.Height - 20);
            graphics.FillRectangle(card, outer);
            graphics.DrawRectangle(border, outer);

            int sidebarWidth = Math.Max(72, bitmapSize.Width / 5);
            Rectangle sidebarRect = new(outer.X + 10, outer.Y + 10, sidebarWidth, outer.Height - 20);
            Rectangle mainRect = new(sidebarRect.Right + 10, outer.Y + 10, outer.Right - sidebarRect.Right - 20, outer.Height - 20);

            graphics.FillRectangle(sidebar, sidebarRect);
            graphics.DrawRectangle(border, sidebarRect);

            Rectangle clockRect = new(sidebarRect.X + 8, sidebarRect.Y + 8, sidebarRect.Width - 16, 48);
            Rectangle actionOne = new(sidebarRect.X + 8, clockRect.Bottom + 8, sidebarRect.Width - 16, 42);
            Rectangle actionTwo = new(sidebarRect.X + 8, actionOne.Bottom + 8, sidebarRect.Width - 16, 42);
            Rectangle themeRect = new(sidebarRect.X + 8, sidebarRect.Bottom - 58, sidebarRect.Width - 16, 24);
            Rectangle configRect = new(sidebarRect.X + 8, sidebarRect.Bottom - 30, sidebarRect.Width - 16, 22);

            graphics.FillRectangle(panel, clockRect);
            graphics.DrawRectangle(border, clockRect);
            graphics.DrawString("16:42", titleFont, text, new PointF(clockRect.X + 8, clockRect.Y + 10));
            graphics.DrawString("CPU 61C", smallFont, mutedText, new PointF(clockRect.X + 8, clockRect.Bottom + 10 > actionOne.Y ? clockRect.Y + 30 : clockRect.Y + 30));

            graphics.FillRectangle(panel, actionOne);
            graphics.DrawRectangle(border, actionOne);
            graphics.DrawString("Steam", bodyFont, text, new RectangleF(actionOne.X, actionOne.Y + 12, actionOne.Width, 18), new StringFormat { Alignment = StringAlignment.Center });

            graphics.FillRectangle(panel, actionTwo);
            graphics.DrawRectangle(border, actionTwo);
            graphics.DrawString("Temps", bodyFont, text, new RectangleF(actionTwo.X, actionTwo.Y + 12, actionTwo.Width, 18), new StringFormat { Alignment = StringAlignment.Center });

            graphics.FillRectangle(panel, themeRect);
            graphics.DrawRectangle(border, themeRect);
            graphics.DrawString("Theme", smallFont, mutedText, new RectangleF(themeRect.X, themeRect.Y + 3, themeRect.Width, 10), new StringFormat { Alignment = StringAlignment.Center });
            graphics.DrawString("Steel", smallFont, text, new RectangleF(themeRect.X, themeRect.Y + 11, themeRect.Width, 10), new StringFormat { Alignment = StringAlignment.Center });

            graphics.FillRectangle(panel, configRect);
            graphics.DrawRectangle(border, configRect);
            graphics.DrawString("Config", smallFont, text, new RectangleF(configRect.X, configRect.Y + 4, configRect.Width, 12), new StringFormat { Alignment = StringAlignment.Center });

            Rectangle headerRect = new(mainRect.X, mainRect.Y, mainRect.Width, 24);
            graphics.DrawString("Game Hub", titleFont, text, new PointF(headerRect.X, headerRect.Y));
            graphics.DrawString("v1.0.0", smallFont, mutedText, new PointF(headerRect.Right - 42, headerRect.Y + 4));

            Rectangle filtersRect = new(mainRect.X, headerRect.Bottom + 6, mainRect.Width, 32);
            int filterWidth = (filtersRect.Width - 24) / 4;
            for (int index = 0; index < 4; index++)
            {
                Rectangle filter = new(filtersRect.X + index * (filterWidth + 8), filtersRect.Y, filterWidth, filtersRect.Height);
                graphics.FillRectangle(index == 0 ? accent : panel, filter);
                graphics.DrawRectangle(index == 0 ? new Pen(accentColor, 2f) : border, filter);
                string label = index switch
                {
                    0 => "All",
                    1 => "Steam",
                    2 => "EA",
                    _ => "GOG",
                };
                graphics.DrawString(label, bodyFont, text, new RectangleF(filter.X, filter.Y + 8, filter.Width, 16), new StringFormat { Alignment = StringAlignment.Center });
            }

            Rectangle gridRect = new(mainRect.X, filtersRect.Bottom + 8, mainRect.Width, mainRect.Height - 110);
            int columns = 3;
            int rows = 2;
            int gap = 8;
            int tileWidth = (gridRect.Width - (gap * (columns - 1))) / columns;
            int tileHeight = (gridRect.Height - (gap * (rows - 1))) / rows;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int index = row * columns + column;
                    Rectangle tile = new(
                        gridRect.X + column * (tileWidth + gap),
                        gridRect.Y + row * (tileHeight + gap),
                        tileWidth,
                        tileHeight);

                    graphics.FillRectangle(index == 0 ? selectedPanel : panel, tile);
                    graphics.DrawRectangle(border, tile);

                    Rectangle artRect = new(tile.X + 10, tile.Y + 10, tile.Width - 20, tile.Height - 34);
                    using Brush artBrush = new SolidBrush(index switch
                    {
                        0 => Color.FromArgb(95, 58, 46),
                        1 => Color.FromArgb(59, 71, 93),
                        2 => Color.FromArgb(71, 48, 83),
                        3 => Color.FromArgb(44, 76, 65),
                        4 => Color.FromArgb(85, 61, 42),
                        _ => Color.FromArgb(67, 46, 46),
                    });
                    graphics.FillRectangle(artBrush, artRect);

                    graphics.FillRectangle(accentSoft, new Rectangle(artRect.X + 12, artRect.Y + 12, artRect.Width - 24, artRect.Height - 24));

                    string gameName = index switch
                    {
                        0 => "Overwatch",
                        1 => "Sea of Thieves",
                        2 => "Dungeons 2",
                        3 => "StarCraft",
                        4 => "Assassin's Creed",
                        _ => "Cyberpunk 2077",
                    };
                    graphics.DrawString(gameName, bodyFont, text, new RectangleF(tile.X + 8, tile.Bottom - 22, tile.Width - 16, 16), new StringFormat { Trimming = StringTrimming.EllipsisCharacter });
                }
            }

            Rectangle footerRect = new(mainRect.X, mainRect.Bottom - 34, mainRect.Width, 28);
            graphics.FillRectangle(panel, footerRect);
            graphics.DrawRectangle(border, footerRect);
            graphics.FillRectangle(accent, new Rectangle(footerRect.Right - 92, footerRect.Y + 4, 84, footerRect.Height - 8));
            graphics.DrawString("Overwatch selected", bodyFont, mutedText, new PointF(footerRect.X + 10, footerRect.Y + 6));
            graphics.DrawString("Launch", bodyFont, text, new RectangleF(footerRect.Right - 92, footerRect.Y + 6, 84, 14), new StringFormat { Alignment = StringAlignment.Center });
        }

        return bitmap;
    }

    private static Bitmap RenderThumbnail()
    {
        Bitmap bitmap = new(145, 145);

        Color accentColor = Color.FromArgb(255, 142, 52);
        Color backgroundColor = Color.FromArgb(28, 28, 30);
        Color panelColor = Color.FromArgb(38, 38, 42);
        Color borderColor = Color.FromArgb(72, 72, 78);

        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (Brush background = new SolidBrush(backgroundColor))
        using (Brush panel = new SolidBrush(panelColor))
        using (Brush accent = new SolidBrush(accentColor))
        using (Brush white = new SolidBrush(Color.White))
        using (Brush muted = new SolidBrush(Color.FromArgb(188, 188, 194)))
        using (Pen border = new(borderColor))
        using (Font monogramFont = new("Segoe UI Semibold", 31f))
        using (Font titleFont = new("Segoe UI Semibold", 10f))
        using (Font subtitleFont = new("Segoe UI", 8f))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(backgroundColor);
            graphics.FillRectangle(background, new Rectangle(0, 0, bitmap.Width, bitmap.Height));

            Rectangle outer = new(8, 8, bitmap.Width - 16, bitmap.Height - 16);
            graphics.FillRectangle(panel, outer);
            graphics.DrawRectangle(border, outer);

            Rectangle topBar = new(outer.X, outer.Y, outer.Width, 8);
            graphics.FillRectangle(accent, topBar);

            Rectangle badge = new(outer.X + 18, outer.Y + 24, outer.Width - 36, 58);
            using (GraphicsPath badgePath = RoundedRect(badge, 14))
            {
                graphics.FillPath(new SolidBrush(Color.FromArgb(48, 52, 60)), badgePath);
            }

            using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString("GH", monogramFont, white, badge, centered);

            Rectangle accentDot = new(badge.Right - 18, badge.Y + 10, 8, 8);
            graphics.FillEllipse(accent, accentDot);
            graphics.FillEllipse(accent, new Rectangle(accentDot.X - 14, accentDot.Bottom + 8, 8, 8));
            graphics.FillEllipse(accent, new Rectangle(accentDot.Right + 6, accentDot.Bottom + 8, 8, 8));

            graphics.DrawString("GAME HUB", titleFont, white, new RectangleF(outer.X, outer.Y + 93, outer.Width, 16), centered);
            graphics.DrawString("Launcher widget", subtitleFont, muted, new RectangleF(outer.X, outer.Y + 108, outer.Width, 14), centered);
        }

        return bitmap;
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        GraphicsPath path = new();
        int diameter = radius * 2;

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public sealed class GameHubWidgetInstance : IWidgetInstance, IWidgetInstanceWithRemoval, IDisposable
{
    private const int CellWidth = 202;
    private const int CellHeight = 145;
    private const int ScannerTimeoutSeconds = 12;
    private const int MaxEnabledPlatforms = 6;
    private const int LocalGamesPerConfigPage = 4;
    private const int QuickActionSlotCount = 3;
    private const string WidgetVersionLabel = "v1.0.0";
    private const string WidgetStateFolderName = "GameHub Config";
    private const string LegacyWidgetStateFolderName = "GameHub";
    private static readonly ThemePalette[] ThemePalettes =
    [
        new("Blue", Color.FromArgb(70, 160, 255), Color.FromArgb(39, 79, 145), Color.FromArgb(29, 45, 74), Color.FromArgb(41, 67, 112), Color.FromArgb(7, 14, 28), Color.FromArgb(18, 29, 49), Color.FromArgb(11, 21, 39), Color.FromArgb(74, 110, 159)),
        new("Neon", Color.FromArgb(0, 245, 255), Color.FromArgb(137, 32, 93), Color.FromArgb(44, 16, 52), Color.FromArgb(75, 24, 78), Color.FromArgb(8, 4, 18), Color.FromArgb(25, 10, 35), Color.FromArgb(14, 8, 27), Color.FromArgb(120, 62, 129)),
        new("Xbox", Color.FromArgb(73, 255, 141), Color.FromArgb(24, 112, 58), Color.FromArgb(20, 42, 28), Color.FromArgb(33, 72, 45), Color.FromArgb(5, 15, 8), Color.FromArgb(13, 31, 18), Color.FromArgb(9, 22, 13), Color.FromArgb(74, 129, 88)),
        new("Synth", Color.FromArgb(255, 196, 71), Color.FromArgb(193, 72, 141), Color.FromArgb(53, 19, 63), Color.FromArgb(86, 34, 92), Color.FromArgb(16, 7, 28), Color.FromArgb(33, 14, 47), Color.FromArgb(24, 11, 36), Color.FromArgb(140, 78, 138)),
        new("Steel", Color.FromArgb(255, 142, 52), Color.FromArgb(163, 74, 36), Color.FromArgb(48, 32, 28), Color.FromArgb(82, 50, 40), Color.FromArgb(16, 10, 9), Color.FromArgb(33, 20, 18), Color.FromArgb(24, 15, 14), Color.FromArgb(142, 94, 72)),
        new("Crimson", Color.FromArgb(190, 52, 52), Color.FromArgb(92, 32, 36), Color.FromArgb(43, 43, 47), Color.FromArgb(70, 46, 52), Color.FromArgb(14, 14, 16), Color.FromArgb(26, 26, 30), Color.FromArgb(19, 19, 22), Color.FromArgb(112, 74, 78)),
        new("Volt", Color.FromArgb(232, 255, 72), Color.FromArgb(94, 112, 28), Color.FromArgb(24, 39, 71), Color.FromArgb(38, 60, 101), Color.FromArgb(6, 12, 26), Color.FromArgb(15, 24, 45), Color.FromArgb(10, 18, 35), Color.FromArgb(110, 126, 66)),
        new("Candy", Color.FromArgb(214, 96, 178), Color.FromArgb(128, 54, 140), Color.FromArgb(66, 33, 77), Color.FromArgb(95, 49, 108), Color.FromArgb(24, 9, 31), Color.FromArgb(42, 18, 53), Color.FromArgb(31, 13, 41), Color.FromArgb(150, 87, 154)),
        new("Field", Color.FromArgb(136, 146, 82), Color.FromArgb(124, 88, 52), Color.FromArgb(72, 58, 38), Color.FromArgb(96, 78, 50), Color.FromArgb(22, 20, 14), Color.FromArgb(40, 32, 22), Color.FromArgb(31, 25, 18), Color.FromArgb(146, 118, 78)),
    ];

    private readonly GameHubWidgetServer _widgetObject;
    private readonly IReadOnlyDictionary<GamePlatform, Func<IGameLibraryScanner>> _scannerFactories;
    private readonly Dictionary<string, Image> _iconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<GamePlatform, Image?> _launcherIconCache = [];
    private readonly Dictionary<QuickActionKind, Image?> _quickActionIconCache = [];
    private readonly Dictionary<QuickActionKind, bool> _quickActionAvailabilityCache = [];
    private readonly Dictionary<string, Image?> _stockIconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<HitTarget> _hitTargets = [];
    private readonly CancellationTokenSource _disposeCancellation = new();
    private CancellationTokenSource? _refreshCancellation;
    private SensorItem? _cpuSensor;
    private SensorItem? _gpuSensor;

    private IReadOnlyList<GameEntry> _games = [];
    private string _status = "Loading library...";
    private int _pageIndex;
    private GamePlatform? _platformFilter;
    private bool _isSleeping;
    private string _clockText = string.Empty;
    private string _cpuTempText = "--";
    private string _gpuTempText = "--";
    private bool _isMicMuted;
    private bool _useTwelveHourClock;
    private string? _selectedGameId;
    private int _themeIndex;
    private bool _isConfigOpen;
    private ConfigPage _configPage = ConfigPage.Launchers;
    private bool _isDisposed;
    private HashSet<GamePlatform> _enabledPlatforms = new();
    private QuickActionKind[] _quickActionSlots = CreateDefaultQuickActionSlots();
    private List<ManualGameEntry> _manualGames = [];
    private int _localGamesPageIndex;

    public GameHubWidgetInstance(GameHubWidgetServer widgetObject, WidgetSize widgetSize, Guid guid)
    {
        _widgetObject = widgetObject;
        WidgetSize = widgetSize;
        Guid = guid;

        _scannerFactories = new Dictionary<GamePlatform, Func<IGameLibraryScanner>>
        {
            [GamePlatform.Steam] = static () => new SteamLibraryScanner(),
            [GamePlatform.Epic] = static () => new EpicGameScanner(),
            [GamePlatform.Ea] = static () => new EaAppScanner(),
            [GamePlatform.Gog] = static () => new GogGalaxyScanner(),
            [GamePlatform.BattleNet] = static () => new BattleNetScanner(),
            [GamePlatform.Xbox] = static () => new XboxGameScanner(),
            [GamePlatform.Ubisoft] = static () => new UbisoftConnectScanner(),
        };

        _enabledPlatforms = CreateDefaultEnabledPlatforms();
        LoadThemeSelection();
        LoadWidgetConfiguration();
        InitializeSensorMonitoring();
        StartRefresh();
    }

    public IWidgetObject WidgetObject => _widgetObject;

    public Guid Guid { get; }

    public WidgetSize WidgetSize { get; }

    public event WidgetUpdatedEventHandler? WidgetUpdated;

    public void RequestUpdate()
    {
        if (_isDisposed)
        {
            return;
        }

        RaiseWidgetUpdated();
    }

    public void ClickEvent(ClickType clickType, int x, int y)
    {
        if (_isDisposed || clickType != ClickType.Single)
        {
            return;
        }

        HitTarget? target = _hitTargets.FirstOrDefault(candidate => candidate.Bounds.Contains(x, y));
        if (target is null)
        {
            return;
        }

        switch (target.Kind)
        {
            case HitTargetKind.UtilityQuickActionSlot1:
                TryExecuteQuickAction(_quickActionSlots[0], out _status, out bool slotOneChanged);
                if (slotOneChanged)
                {
                    RaiseWidgetUpdated();
                    return;
                }
                RaiseWidgetUpdated();
                break;
            case HitTargetKind.UtilityQuickActionSlot2:
                TryExecuteQuickAction(_quickActionSlots[1], out _status, out bool slotTwoChanged);
                if (slotTwoChanged)
                {
                    RaiseWidgetUpdated();
                    return;
                }
                RaiseWidgetUpdated();
                break;
            case HitTargetKind.UtilityQuickActionSlot3:
                TryExecuteQuickAction(_quickActionSlots[2], out _status, out bool slotThreeChanged);
                RaiseWidgetUpdated();
                break;
            case HitTargetKind.UtilityMicMute:
                if (WidgetSystemActions.TryToggleMicrophoneMute(out _status, out bool isMuted))
                {
                    _isMicMuted = isMuted;
                }
                RaiseWidgetUpdated();
                break;
            case HitTargetKind.ToggleClockFormat:
                _useTwelveHourClock = !_useTwelveHourClock;
                _status = _useTwelveHourClock ? "Clock set to 12h" : "Clock set to 24h";
                SaveWidgetConfiguration();
                RaiseWidgetUpdated();
                break;
            case HitTargetKind.PreviousPage:
                if (_pageIndex > 0)
                {
                    _pageIndex -= 1;
                }
                RaiseWidgetUpdated();
                break;
            case HitTargetKind.NextPage:
                (int _, int pageCount) = GetPageMetrics(GetFilteredGames());
                if (_pageIndex < pageCount - 1)
                {
                    _pageIndex += 1;
                }
                RaiseWidgetUpdated();
                break;
            case HitTargetKind.FilterAll:
                _platformFilter = null;
                _pageIndex = 0;
                EnsureSelectedGameVisible();
                RaiseWidgetUpdated();
                break;
            case HitTargetKind.FilterSteam:
                ApplyFilter(GamePlatform.Steam);
                break;
            case HitTargetKind.FilterEpic:
                ApplyFilter(GamePlatform.Epic);
                break;
            case HitTargetKind.FilterEa:
                ApplyFilter(GamePlatform.Ea);
                break;
            case HitTargetKind.FilterGog:
                ApplyFilter(GamePlatform.Gog);
                break;
            case HitTargetKind.FilterBattleNet:
                ApplyFilter(GamePlatform.BattleNet);
                break;
            case HitTargetKind.FilterXbox:
                ApplyFilter(GamePlatform.Xbox);
                break;
            case HitTargetKind.FilterUbisoft:
                ApplyFilter(GamePlatform.Ubisoft);
                break;
            case HitTargetKind.CycleTheme:
                _themeIndex = (_themeIndex + 1) % ThemePalettes.Length;
                SaveThemeSelection();
                RaiseWidgetUpdated();
                break;
            case HitTargetKind.OpenConfig:
                _isConfigOpen = true;
                _configPage = ConfigPage.Launchers;
                RaiseWidgetUpdated();
                break;
            case HitTargetKind.CloseConfig:
                _isConfigOpen = false;
                RaiseWidgetUpdated();
                break;
            case HitTargetKind.ConfigLaunchers:
                _configPage = ConfigPage.Launchers;
                RaiseWidgetUpdated();
                break;
            case HitTargetKind.ConfigQuickActions:
                _configPage = ConfigPage.QuickActions;
                RaiseWidgetUpdated();
                break;
            case HitTargetKind.ConfigLocalGames:
                _configPage = ConfigPage.LocalGames;
                RaiseWidgetUpdated();
                break;
            case HitTargetKind.ToggleSteam:
                TogglePlatform(GamePlatform.Steam);
                break;
            case HitTargetKind.ToggleEpic:
                TogglePlatform(GamePlatform.Epic);
                break;
            case HitTargetKind.ToggleEa:
                TogglePlatform(GamePlatform.Ea);
                break;
            case HitTargetKind.ToggleGog:
                TogglePlatform(GamePlatform.Gog);
                break;
            case HitTargetKind.ToggleBattleNet:
                TogglePlatform(GamePlatform.BattleNet);
                break;
            case HitTargetKind.ToggleXbox:
                TogglePlatform(GamePlatform.Xbox);
                break;
            case HitTargetKind.ToggleUbisoft:
                TogglePlatform(GamePlatform.Ubisoft);
                break;
            case HitTargetKind.PreviousQuickActionSlot1:
                CycleQuickActionSlot(0, -1);
                break;
            case HitTargetKind.NextQuickActionSlot1:
                CycleQuickActionSlot(0, 1);
                break;
            case HitTargetKind.PreviousQuickActionSlot2:
                CycleQuickActionSlot(1, -1);
                break;
            case HitTargetKind.NextQuickActionSlot2:
                CycleQuickActionSlot(1, 1);
                break;
            case HitTargetKind.PreviousQuickActionSlot3:
                CycleQuickActionSlot(2, -1);
                break;
            case HitTargetKind.NextQuickActionSlot3:
                CycleQuickActionSlot(2, 1);
                break;
            case HitTargetKind.PreviousLocalGamesPage:
                if (_localGamesPageIndex > 0)
                {
                    _localGamesPageIndex -= 1;
                    RaiseWidgetUpdated();
                }
                break;
            case HitTargetKind.NextLocalGamesPage:
                if (_localGamesPageIndex < GetLocalGamesPageCount() - 1)
                {
                    _localGamesPageIndex += 1;
                    RaiseWidgetUpdated();
                }
                break;
            case HitTargetKind.AddLocalGame:
                AddLocalGame();
                break;
            case HitTargetKind.RemoveLocalGame:
                if (target.ManualGame is not null)
                {
                    RemoveLocalGame(target.ManualGame);
                }
                break;
            case HitTargetKind.Game:
                if (target.Game is not null)
                {
                    _selectedGameId = target.Game.Id;
                    _status = $"Selected {target.Game.Name}";
                    RaiseWidgetUpdated();
                }
                break;
            case HitTargetKind.LaunchSelected:
                GameEntry? selectedGame = GetSelectedGame(GetFilteredGames());
                if (selectedGame is not null)
                {
                    LaunchGame(selectedGame);
                    _status = $"Launched {selectedGame.Name}";
                    RaiseWidgetUpdated();
                }
                break;
        }
    }

    public System.Windows.Controls.UserControl GetSettingsControl()
    {
        return new System.Windows.Controls.UserControl
        {
            Content = new System.Windows.Controls.TextBlock
            {
                Text = "Game Hub widget has no settings yet.\nUse taps to change launcher filters and page through the library.",
                TextAlignment = System.Windows.TextAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                Margin = new System.Windows.Thickness(12),
                TextWrapping = System.Windows.TextWrapping.Wrap,
            },
        };
    }

    public void EnterSleep()
    {
        if (_isDisposed)
        {
            return;
        }

        _isSleeping = true;
    }

    public void ExitSleep()
    {
        if (_isDisposed)
        {
            return;
        }

        _isSleeping = false;
        RaiseWidgetUpdated();
    }

    public void OnRemove()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _disposeCancellation.Cancel();
        _refreshCancellation?.Cancel();
        TearDownSensorMonitoring();

        foreach (Image image in _iconCache.Values)
        {
            image.Dispose();
        }

        _iconCache.Clear();

        foreach (Image? image in _launcherIconCache.Values)
        {
            image?.Dispose();
        }

        _launcherIconCache.Clear();

        foreach (Image? image in _quickActionIconCache.Values)
        {
            image?.Dispose();
        }

        _quickActionIconCache.Clear();

        foreach (Image? image in _stockIconCache.Values)
        {
            image?.Dispose();
        }

        _stockIconCache.Clear();
        _quickActionAvailabilityCache.Clear();
        _refreshCancellation?.Dispose();
        _disposeCancellation.Dispose();
    }

    internal static Size GetBitmapSize(WidgetSize widgetSize)
    {
        return new Size(widgetSize.Width * CellWidth, widgetSize.Height * CellHeight);
    }

    private void ApplyFilter(GamePlatform platform)
    {
        if (!_enabledPlatforms.Contains(platform))
        {
            return;
        }

        _platformFilter = platform;
        _pageIndex = 0;
        EnsureSelectedGameVisible();
        RaiseWidgetUpdated();
    }

    private void TogglePlatform(GamePlatform platform)
    {
        if (!IsLauncherAvailable(platform))
        {
            _status = $"{GetPlatformLabel(platform)} was not found on this PC.";
            RaiseWidgetUpdated();
            return;
        }

        if (!_enabledPlatforms.Add(platform))
        {
            _enabledPlatforms.Remove(platform);
        }
        else if (_enabledPlatforms.Count > MaxEnabledPlatforms)
        {
            _enabledPlatforms.Remove(platform);
            _status = $"Up to {MaxEnabledPlatforms} launchers can be enabled.";
            RaiseWidgetUpdated();
            return;
        }

        if (_platformFilter == platform && !_enabledPlatforms.Contains(platform))
        {
            _platformFilter = null;
        }

        EnsureSelectedGameVisible();
        SaveWidgetConfiguration();
        StartRefresh();
        RaiseWidgetUpdated();
    }

    private void CycleQuickActionSlot(int slotIndex, int direction)
    {
        if (slotIndex < 0 || slotIndex >= _quickActionSlots.Length)
        {
            return;
        }

        QuickActionKind[] options =
        [
            QuickActionKind.None,
            QuickActionKind.WeMod,
            QuickActionKind.SteamLauncher,
            QuickActionKind.EpicLauncher,
            QuickActionKind.EaLauncher,
            QuickActionKind.GogLauncher,
            QuickActionKind.BattleNetLauncher,
            QuickActionKind.XboxLauncher,
            QuickActionKind.UbisoftLauncher,
            QuickActionKind.Discord,
            QuickActionKind.Browser,
            QuickActionKind.MicMute,
            QuickActionKind.Temps,
        ];

        int currentIndex = Array.IndexOf(options, _quickActionSlots[slotIndex]);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        for (int step = 0; step < options.Length; step++)
        {
            currentIndex = (currentIndex + direction + options.Length) % options.Length;
            QuickActionKind candidate = options[currentIndex];
            if (candidate != QuickActionKind.None && _quickActionSlots.Where((_, index) => index != slotIndex).Contains(candidate))
            {
                continue;
            }

            _quickActionSlots[slotIndex] = candidate;
            SaveWidgetConfiguration();
            RaiseWidgetUpdated();
            return;
        }
    }

    private static bool TryExecuteQuickAction(QuickActionKind quickAction, out string statusMessage, out bool stateChanged)
    {
        stateChanged = false;
        switch (quickAction)
        {
            case QuickActionKind.WeMod:
                return WidgetSystemActions.TryLaunchWeMod(out statusMessage);
            case QuickActionKind.SteamLauncher:
                return WidgetSystemActions.TryLaunchLauncher(GamePlatform.Steam, out statusMessage);
            case QuickActionKind.EpicLauncher:
                return WidgetSystemActions.TryLaunchLauncher(GamePlatform.Epic, out statusMessage);
            case QuickActionKind.EaLauncher:
                return WidgetSystemActions.TryLaunchLauncher(GamePlatform.Ea, out statusMessage);
            case QuickActionKind.GogLauncher:
                return WidgetSystemActions.TryLaunchLauncher(GamePlatform.Gog, out statusMessage);
            case QuickActionKind.BattleNetLauncher:
                return WidgetSystemActions.TryLaunchLauncher(GamePlatform.BattleNet, out statusMessage);
            case QuickActionKind.XboxLauncher:
                return WidgetSystemActions.TryLaunchLauncher(GamePlatform.Xbox, out statusMessage);
            case QuickActionKind.UbisoftLauncher:
                return WidgetSystemActions.TryLaunchLauncher(GamePlatform.Ubisoft, out statusMessage);
            case QuickActionKind.Discord:
                return WidgetSystemActions.TryLaunchDiscord(out statusMessage);
            case QuickActionKind.Browser:
                return WidgetSystemActions.TryLaunchDefaultBrowser(out statusMessage);
            case QuickActionKind.MicMute:
                stateChanged = WidgetSystemActions.TryToggleMicrophoneMute(out statusMessage);
                return stateChanged;
            case QuickActionKind.Temps:
                statusMessage = "Temps is a status tile.";
                return false;
            default:
                statusMessage = "No quick action selected.";
                return false;
        }
    }

    private void AddLocalGame()
    {
        try
        {
            string? executablePath = ShowExecutablePicker();
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return;
            }

            if (_manualGames.Any(game => string.Equals(game.ExecutablePath, executablePath, StringComparison.OrdinalIgnoreCase)))
            {
                _status = "That local game is already added.";
                RaiseWidgetUpdated();
                return;
            }

            _manualGames.Add(new ManualGameEntry(
                $"manual:{executablePath}",
                Path.GetFileNameWithoutExtension(executablePath),
                executablePath));

            _localGamesPageIndex = GetLocalGamesPageCount() - 1;
            SaveWidgetConfiguration();
            StartRefresh();
        }
        catch (Exception ex)
        {
            _status = $"Could not add local game: {ex.Message}";
            RaiseWidgetUpdated();
        }
    }

    private static string? ShowExecutablePicker()
    {
        string? selectedPath = null;
        Exception? dialogException = null;

        Thread dialogThread = new(() =>
        {
            try
            {
                using Forms.OpenFileDialog dialog = new()
                {
                    Filter = "Programs (*.exe)|*.exe|All files (*.*)|*.*",
                    Title = "Select a local game executable",
                    CheckFileExists = true,
                    Multiselect = false,
                };

                if (dialog.ShowDialog() == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    selectedPath = dialog.FileName;
                }
            }
            catch (Exception ex)
            {
                dialogException = ex;
            }
        });

        dialogThread.SetApartmentState(ApartmentState.STA);
        dialogThread.IsBackground = true;
        dialogThread.Start();
        dialogThread.Join();

        if (dialogException is not null)
        {
            throw dialogException;
        }

        return selectedPath;
    }

    private void RemoveLocalGame(ManualGameEntry manualGame)
    {
        if (_manualGames.RemoveAll(candidate => string.Equals(candidate.Id, manualGame.Id, StringComparison.OrdinalIgnoreCase)) == 0)
        {
            return;
        }

        _localGamesPageIndex = Math.Min(_localGamesPageIndex, Math.Max(0, GetLocalGamesPageCount() - 1));
        SaveWidgetConfiguration();
        StartRefresh();
    }

    private int GetLocalGamesPageCount()
    {
        return Math.Max(1, (int)Math.Ceiling(_manualGames.Count / (double)LocalGamesPerConfigPage));
    }

    private bool IsLauncherAvailable(GamePlatform platform)
    {
        return WidgetLauncherArtworkLoader.IsAvailable(platform);
    }

    private static string GetPlatformLabel(GamePlatform platform)
    {
        switch (platform)
        {
            case GamePlatform.Steam:
                return "Steam";
            case GamePlatform.Epic:
                return "Epic";
            case GamePlatform.Ea:
                return "EA";
            case GamePlatform.Gog:
                return "GOG";
            case GamePlatform.BattleNet:
                return "Battle.net";
            case GamePlatform.Xbox:
                return "Xbox";
            case GamePlatform.Ubisoft:
                return "Ubisoft";
            default:
                return platform.ToString();
        }
    }

    private static QuickActionKind[] CreateDefaultQuickActionSlots()
    {
        return
        [
            WidgetSystemActions.TryGetWeModExecutablePath(out _) ? QuickActionKind.WeMod : QuickActionKind.None,
            QuickActionKind.MicMute,
            QuickActionKind.Temps,
        ];
    }

    private HashSet<GamePlatform> CreateDefaultEnabledPlatforms()
    {
        return new HashSet<GamePlatform>(
            _scannerFactories.Keys
                .Where(IsLauncherAvailable)
                .Take(MaxEnabledPlatforms));
    }

    private void StartRefresh()
    {
        if (_isDisposed)
        {
            return;
        }

        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellation.Token);
        _ = RefreshLibraryAsync(_refreshCancellation.Token);
    }

    private async Task RefreshLibraryAsync(CancellationToken cancellationToken)
    {
        List<GameEntry> aggregatedGames = CreateManualGameEntries().ToList();
        List<string> failures = [];

        try
        {
            _games = [];
            foreach (KeyValuePair<GamePlatform, Func<IGameLibraryScanner>> scannerEntry in _scannerFactories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_enabledPlatforms.Contains(scannerEntry.Key))
                {
                    continue;
                }

                IGameLibraryScanner scanner = scannerEntry.Value();

                try
                {
                    _status = $"Scanning {scanner.SourceName}...";
                    RaiseWidgetUpdated();

                    using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(ScannerTimeoutSeconds));
                    using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken,
                        timeout.Token);

                    Task<IReadOnlyList<GameEntry>> scanTask = scanner.ScanAsync(linkedCancellation.Token);
                    Task completedTask = await Task.WhenAny(
                        scanTask,
                        Task.Delay(TimeSpan.FromSeconds(ScannerTimeoutSeconds), linkedCancellation.Token));

                    if (completedTask != scanTask)
                    {
                        failures.Add($"{scanner.SourceName} timed out");
                        LogMessage($"{scanner.SourceName} scan timed out after {ScannerTimeoutSeconds} seconds.");
                        linkedCancellation.Cancel();
                        continue;
                    }

                    IReadOnlyList<GameEntry> results = await scanTask;
                    aggregatedGames.AddRange(results);
                    _games = aggregatedGames
                        .DistinctBy(game => game.Id)
                        .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    _status = _games.Count == 0
                        ? $"No games found yet ({scanner.SourceName})."
                        : $"Loaded {_games.Count} games";

                    RaiseWidgetUpdated();
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        LogMessage("Widget refresh canceled during disposal.");
                        return;
                    }

                    failures.Add($"{scanner.SourceName} timed out");
                    LogMessage($"{scanner.SourceName} scan canceled due to timeout.");
                }
                catch (Exception ex)
                {
                    failures.Add($"{scanner.SourceName} failed");
                    LogMessage($"{scanner.SourceName} scan failed: {ex}");
                }
            }

            _games = aggregatedGames
                .DistinctBy(game => game.Id)
                .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            EnsureSelectedGameVisible();

            _status = _games.Count == 0
                ? failures.Count == 0 ? "No games found." : "No games found. Check widget log."
                : failures.Count == 0 ? $"Loaded {_games.Count} games" : $"Loaded {_games.Count} games ({failures.Count} source issues)";
        }
        catch (OperationCanceledException)
        {
            LogMessage("Widget refresh canceled.");
            return;
        }
        catch (Exception ex)
        {
            _status = $"Scan failed: {ex.Message}";
            LogMessage($"Composite widget scan failed: {ex}");
        }

        RaiseWidgetUpdated();
    }

    private void RaiseWidgetUpdated()
    {
        if (_isSleeping || _isDisposed)
        {
            return;
        }

        Bitmap bitmap = Render();
        WidgetUpdated?.Invoke(this, new WidgetUpdatedEventArgs
        {
            Offset = Point.Empty,
            WidgetBitmap = bitmap,
            WaitMax = 0,
        });
    }

    private Bitmap Render()
    {
        if (_isDisposed)
        {
            return new Bitmap(1, 1);
        }

        Size bitmapSize = GetBitmapSize(WidgetSize);
        Bitmap bitmap = new(bitmapSize.Width, bitmapSize.Height);
        _hitTargets.Clear();
        UpdateUtilityState();

        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (Brush background = new SolidBrush(CurrentTheme.BackgroundColor))
        using (Brush card = new SolidBrush(CurrentTheme.CardColor))
        using (Brush accent = new SolidBrush(CurrentTheme.AccentColor))
        using (Brush text = new SolidBrush(Color.White))
        using (Brush mutedText = new SolidBrush(Color.FromArgb(180, 190, 210)))
        using (Pen border = new(CurrentTheme.BorderColor))
        using (Font titleFont = new("Segoe UI Semibold", 12f))
        using (Font smallFont = new("Segoe UI", 10f))
        using (Font labelFont = new("Segoe UI Semibold", 10f))
        using (Font filterFont = new("Segoe UI Semibold", 11f))
        using (Font footerFont = new("Segoe UI Semibold", 10.5f))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Black);
            graphics.FillRectangle(background, new Rectangle(Point.Empty, bitmapSize));

            int utilityWidth = Math.Max(168, Math.Min(228, bitmapSize.Width / 5));
            Rectangle utilityArea = new(0, 0, utilityWidth, bitmapSize.Height);
            DrawUtilityRail(graphics, utilityArea, titleFont, smallFont, labelFont, text, mutedText, accent, border);

            Rectangle mainArea = new(utilityArea.Right, 0, bitmapSize.Width - utilityArea.Width, bitmapSize.Height);
            Rectangle header = new(mainArea.X, 0, mainArea.Width, 40);
            graphics.FillRectangle(card, header);
            graphics.DrawString("Game Hub", titleFont, text, new PointF(header.X + 12, 9));
            graphics.DrawString(WidgetVersionLabel, smallFont, mutedText, new RectangleF(header.X + 120, 11, header.Width - 132, 18));

            if (_isConfigOpen)
            {
                Rectangle configArea = new(mainArea.X, header.Bottom, mainArea.Width, bitmapSize.Height - header.Bottom);
                DrawConfigScreen(graphics, configArea, titleFont, smallFont, text, mutedText, border);
            }
            else
            {
                Rectangle filterRow = new(mainArea.X, header.Bottom, mainArea.Width, 60);
                DrawFilters(graphics, filterRow, filterFont, text, accent, card, border);

                Rectangle contentArea = new(mainArea.X, filterRow.Bottom, mainArea.Width, bitmapSize.Height - filterRow.Bottom - 70);
                DrawGames(graphics, contentArea, labelFont, text, border);

                Rectangle footer = new(mainArea.X, bitmapSize.Height - 70, mainArea.Width, 70);
                graphics.FillRectangle(card, footer);
                DrawFooter(graphics, footer, footerFont, text, border);
            }
        }

        return bitmap;
    }

    private void DrawUtilityRail(
        Graphics graphics,
        Rectangle bounds,
        Font titleFont,
        Font smallFont,
        Font labelFont,
        Brush text,
        Brush mutedText,
        Brush accent,
        Pen border)
    {
        graphics.FillRectangle(new SolidBrush(CurrentTheme.SidebarColor), bounds);

        Rectangle topCard = new(bounds.X + 10, bounds.Y + 10, bounds.Width - 20, 114);
        graphics.FillRectangle(new SolidBrush(CurrentTheme.PanelColor), topCard);
        graphics.DrawRectangle(border, topCard);
        graphics.FillRectangle(accent, topCard.X, topCard.Y, topCard.Width, 12);

        using (StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            Rectangle clockRect = new(topCard.X + 6, topCard.Y + 16, topCard.Width - 12, 50);
            Rectangle dateRect = new(topCard.X + 6, topCard.Y + 68, topCard.Width - 12, 24);

            if (_useTwelveHourClock)
            {
                string timePart = DateTime.Now.ToString("hh:mm");
                string suffixPart = DateTime.Now.ToString("tt");
                using Font timeFont = new("Segoe UI Semibold", 24f);
                using Font suffixFont = new("Segoe UI Semibold", 11.5f);

                SizeF timeSize = graphics.MeasureString(timePart, timeFont);
                SizeF suffixSize = graphics.MeasureString(suffixPart, suffixFont);
                float totalWidth = timeSize.Width + 6f + suffixSize.Width;
                float startX = clockRect.X + ((clockRect.Width - totalWidth) / 2f);
                float baselineY = clockRect.Y + 6f;

                graphics.DrawString(timePart, timeFont, text, startX, baselineY);
                graphics.DrawString(suffixPart, suffixFont, mutedText, startX + timeSize.Width + 6f, baselineY + 10f);
            }
            else
            {
                using Font clockFont = new("Segoe UI Semibold", 29f);
                graphics.DrawString(_clockText, clockFont, text, clockRect, centered);
            }

            graphics.DrawString(DateTime.Now.ToString("ddd dd MMM"), smallFont, mutedText, dateRect, centered);
        }
        _hitTargets.Add(new HitTarget(HitTargetKind.ToggleClockFormat, topCard, null));

        int currentY = topCard.Bottom + 14;
        for (int slotIndex = 0; slotIndex < _quickActionSlots.Length; slotIndex++)
        {
            QuickActionKind quickAction = _quickActionSlots[slotIndex];
            if (quickAction == QuickActionKind.None || !IsQuickActionAvailable(quickAction))
            {
                continue;
            }

            Rectangle slotBounds = new(bounds.X + 10, currentY, bounds.Width - 20, 98);
            HitTargetKind? hitTargetKind = slotIndex switch
            {
                0 => HitTargetKind.UtilityQuickActionSlot1,
                1 => HitTargetKind.UtilityQuickActionSlot2,
                2 => HitTargetKind.UtilityQuickActionSlot3,
                _ => null,
            };

            DrawQuickActionSlot(graphics, slotBounds, quickAction, labelFont, text, mutedText, border);
            if (hitTargetKind is not null && quickAction != QuickActionKind.Temps && quickAction != QuickActionKind.MicMute)
            {
                _hitTargets.Add(new HitTarget(hitTargetKind.Value, slotBounds, null));
            }
            else if (quickAction == QuickActionKind.MicMute)
            {
                _hitTargets.Add(new HitTarget(HitTargetKind.UtilityMicMute, slotBounds, null));
            }

            currentY = slotBounds.Bottom + 12;
        }

        Rectangle themeButton = new(bounds.X + 10, bounds.Bottom - 108, bounds.Width - 20, 44);
        Rectangle configButton = new(bounds.X + 10, bounds.Bottom - 58, bounds.Width - 20, 44);
        DrawThemeButton(graphics, themeButton, smallFont, text, mutedText, border);
        DrawConfigButton(graphics, configButton, smallFont, text, mutedText, border);
        _hitTargets.Add(new HitTarget(HitTargetKind.CycleTheme, themeButton, null));
        _hitTargets.Add(new HitTarget(HitTargetKind.OpenConfig, configButton, null));
    }

    private void DrawLauncherTileButton(
        Graphics graphics,
        Rectangle bounds,
        string title,
        Font titleFont,
        Brush text,
        Pen border,
        Image? icon = null)
    {
        graphics.FillRectangle(new SolidBrush(CurrentTheme.PanelColor), bounds);
        graphics.DrawRectangle(border, bounds);

        if (icon is not null)
        {
            Rectangle iconRect = new(bounds.X + (bounds.Width - 46) / 2, bounds.Y + 10, 46, 46);
            graphics.DrawImage(icon, iconRect);
        }

        using StringFormat centered = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
        };
        Rectangle textRect = new(bounds.X + 8, bounds.Bottom - 34, bounds.Width - 16, 24);
        graphics.DrawString(title, titleFont, text, textRect, centered);
    }

    private void DrawQuickActionSlot(Graphics graphics, Rectangle bounds, QuickActionKind quickAction, Font titleFont, Brush text, Brush mutedText, Pen border)
    {
        switch (quickAction)
        {
            case QuickActionKind.MicMute:
                DrawMicIconButton(graphics, bounds, border, text, mutedText);
                break;
            case QuickActionKind.Temps:
                DrawTemperatureTile(graphics, bounds, titleFont, text, border);
                break;
            default:
                DrawLauncherTileButton(
                    graphics,
                    bounds,
                    GetQuickActionLabel(quickAction),
                    titleFont,
                    text,
                    border,
                    GetQuickActionIcon(quickAction, new Size(42, 42)));
                break;
        }
    }

    private void DrawMicIconButton(Graphics graphics, Rectangle bounds, Pen border, Brush text, Brush mutedText)
    {
        graphics.FillRectangle(new SolidBrush(CurrentTheme.PanelColor), bounds);
        graphics.DrawRectangle(border, bounds);

        Rectangle iconBounds = new(bounds.X + (bounds.Width - 34) / 2, bounds.Y + 8, 34, 40);
        Image? stockIcon = GetStockMicIcon(_isMicMuted, iconBounds.Size, ((SolidBrush)text).Color);
        if (stockIcon is not null)
        {
            graphics.DrawImage(stockIcon, iconBounds);
        }
        else
        {
            DrawMicrophoneGlyph(graphics, iconBounds, ((SolidBrush)text).Color, _isMicMuted);
        }

        using Font captionFont = new("Segoe UI Semibold", 10.5f);
        using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        Rectangle textRect = new(bounds.X + 4, bounds.Bottom - 26, bounds.Width - 8, 18);
        graphics.DrawString(_isMicMuted ? "Muted" : "Mic", captionFont, text, textRect, centered);
    }

    private void DrawTemperatureTile(Graphics graphics, Rectangle bounds, Font labelFont, Brush text, Pen border)
    {
        graphics.FillRectangle(new SolidBrush(CurrentTheme.PanelColor), bounds);
        graphics.DrawRectangle(border, bounds);
        graphics.DrawString("Temps", labelFont, text, new PointF(bounds.X + 10, bounds.Y + 10));
        using Font tempFont = new("Segoe UI Semibold", 13f);
        graphics.DrawString($"CPU  {_cpuTempText}", tempFont, text, new PointF(bounds.X + 10, bounds.Y + 42));
        graphics.DrawString($"GPU  {_gpuTempText}", tempFont, text, new PointF(bounds.X + 10, bounds.Y + 70));
    }

    private static void DrawMicrophoneGlyph(Graphics graphics, Rectangle bounds, Color color, bool isMuted)
    {
        using Pen pen = new(color, 2.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };

        int centerX = bounds.X + bounds.Width / 2;
        Rectangle capsule = new(centerX - 7, bounds.Y + 3, 14, 20);
        graphics.DrawArc(pen, capsule, 180, 180);
        graphics.DrawLine(pen, capsule.Left, capsule.Y + capsule.Height / 2, capsule.Left, capsule.Bottom - 4);
        graphics.DrawLine(pen, capsule.Right, capsule.Y + capsule.Height / 2, capsule.Right, capsule.Bottom - 4);
        graphics.DrawArc(pen, new Rectangle(capsule.X, capsule.Bottom - 8, capsule.Width, 10), 0, 180);

        Rectangle pickupArc = new(centerX - 13, capsule.Bottom - 2, 26, 18);
        graphics.DrawArc(pen, pickupArc, 20, 140);

        int stemTop = pickupArc.Bottom - 2;
        int stemBottom = bounds.Bottom - 8;
        graphics.DrawLine(pen, centerX, stemTop, centerX, stemBottom);
        graphics.DrawLine(pen, centerX - 8, stemBottom, centerX + 8, stemBottom);

        if (isMuted)
        {
            graphics.DrawLine(pen, bounds.X + 5, bounds.Bottom - 5, bounds.Right - 5, bounds.Y + 5);
        }
    }

    private void DrawFilters(Graphics graphics, Rectangle bounds, Font font, Brush text, Brush accent, Brush background, Pen border)
    {
        List<(string Label, HitTargetKind Kind, GamePlatform? Platform)> filters =
        [
            ("All", HitTargetKind.FilterAll, null),
        ];

        if (_enabledPlatforms.Contains(GamePlatform.Steam))
        {
            filters.Add(("Steam", HitTargetKind.FilterSteam, GamePlatform.Steam));
        }

        if (_enabledPlatforms.Contains(GamePlatform.Epic))
        {
            filters.Add(("Epic", HitTargetKind.FilterEpic, GamePlatform.Epic));
        }

        if (_enabledPlatforms.Contains(GamePlatform.Ea))
        {
            filters.Add(("EA", HitTargetKind.FilterEa, GamePlatform.Ea));
        }

        if (_enabledPlatforms.Contains(GamePlatform.Gog))
        {
            filters.Add(("GOG", HitTargetKind.FilterGog, GamePlatform.Gog));
        }

        if (_enabledPlatforms.Contains(GamePlatform.BattleNet))
        {
            filters.Add(("Battle.net", HitTargetKind.FilterBattleNet, GamePlatform.BattleNet));
        }

        if (_enabledPlatforms.Contains(GamePlatform.Xbox))
        {
            filters.Add(("Xbox", HitTargetKind.FilterXbox, GamePlatform.Xbox));
        }

        if (_enabledPlatforms.Contains(GamePlatform.Ubisoft))
        {
            filters.Add(("Ubisoft", HitTargetKind.FilterUbisoft, GamePlatform.Ubisoft));
        }

        int buttonWidth = bounds.Width / filters.Count;
        for (int index = 0; index < filters.Count; index++)
        {
            Rectangle button = new(bounds.X + index * buttonWidth + 7, bounds.Y + 8, buttonWidth - 14, bounds.Height - 16);
            bool selected = filters[index].Platform == _platformFilter || (filters[index].Platform is null && _platformFilter is null);

            graphics.FillRectangle(selected ? accent : background, button);
            graphics.DrawRectangle(border, button);

            if (filters[index].Platform is null)
            {
                using StringFormat format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                graphics.DrawString(filters[index].Label, font, text, button, format);
            }
            else if (filters[index].Platform is GamePlatform platform)
            {
                DrawPlatformFilterIcon(graphics, button, platform, ((SolidBrush)text).Color);
            }

            _hitTargets.Add(new HitTarget(filters[index].Kind, button, null));
        }
    }

    private void DrawConfigScreen(
        Graphics graphics,
        Rectangle bounds,
        Font titleFont,
        Font bodyFont,
        Brush text,
        Brush mutedText,
        Pen border)
    {
        Rectangle contentCard = new(bounds.X + 14, bounds.Y + 14, bounds.Width - 28, bounds.Height - 28);
        graphics.FillRectangle(new SolidBrush(CurrentTheme.CardColor), contentCard);
        graphics.DrawRectangle(border, contentCard);

        Rectangle closeButton = new(contentCard.Right - 128, contentCard.Y + 12, 112, 34);
        graphics.FillRectangle(new SolidBrush(CurrentTheme.AccentSoftColor), closeButton);
        graphics.DrawRectangle(new Pen(CurrentTheme.AccentColor, 2f), closeButton);
        using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString("Done", bodyFont, text, closeButton, centered);
        _hitTargets.Add(new HitTarget(HitTargetKind.CloseConfig, closeButton, null));

        graphics.DrawString("Configuration", titleFont, text, new PointF(contentCard.X + 16, contentCard.Y + 14));
        graphics.DrawString("Enable the launchers and quick actions you actually use.", bodyFont, mutedText, new PointF(contentCard.X + 16, contentCard.Y + 44));

        Rectangle tabRow = new(contentCard.X + 16, contentCard.Y + 78, contentCard.Width - 32, 40);
        DrawConfigTabs(graphics, tabRow, bodyFont, text, border);

        Rectangle pageArea = new(contentCard.X + 16, tabRow.Bottom + 12, contentCard.Width - 32, contentCard.Bottom - tabRow.Bottom - 28);
        switch (_configPage)
        {
            case ConfigPage.Launchers:
                DrawLauncherConfigPage(graphics, pageArea, titleFont, bodyFont, text, mutedText, border);
                break;
            case ConfigPage.QuickActions:
                DrawQuickActionsConfigPage(graphics, pageArea, titleFont, bodyFont, text, mutedText, border);
                break;
            case ConfigPage.LocalGames:
                DrawLocalGamesConfigPage(graphics, pageArea, titleFont, bodyFont, text, mutedText, border);
                break;
        }
    }

    private void DrawConfigTabs(Graphics graphics, Rectangle bounds, Font font, Brush text, Pen border)
    {
        (string Label, HitTargetKind Kind, ConfigPage Page)[] tabs =
        [
            ("Launchers", HitTargetKind.ConfigLaunchers, ConfigPage.Launchers),
            ("Quick", HitTargetKind.ConfigQuickActions, ConfigPage.QuickActions),
            ("Local", HitTargetKind.ConfigLocalGames, ConfigPage.LocalGames),
        ];

        int tabWidth = bounds.Width / tabs.Length;
        for (int index = 0; index < tabs.Length; index++)
        {
            Rectangle tab = new(bounds.X + index * tabWidth, bounds.Y, tabWidth - 8, bounds.Height);
            bool selected = tabs[index].Page == _configPage;
            using Brush fill = new SolidBrush(selected ? CurrentTheme.AccentSoftColor : CurrentTheme.PanelColor);
            using Pen tabBorder = new(selected ? CurrentTheme.AccentColor : CurrentTheme.BorderColor, selected ? 2f : 1f);
            graphics.FillRectangle(fill, tab);
            graphics.DrawRectangle(tabBorder, tab);
            using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString(tabs[index].Label, font, text, tab, centered);
            _hitTargets.Add(new HitTarget(tabs[index].Kind, tab, null));
        }
    }

    private void DrawLauncherConfigPage(Graphics graphics, Rectangle bounds, Font titleFont, Font bodyFont, Brush text, Brush mutedText, Pen border)
    {
        const int toggleHeight = 38;
        const int toggleSpacing = 8;

        Rectangle launchersHeader = new(bounds.X, bounds.Y, bounds.Width, 22);
        graphics.DrawString("Launchers", titleFont, text, launchersHeader.Location);
        using StringFormat rightAligned = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near };
        graphics.DrawString($"{_enabledPlatforms.Count}/{MaxEnabledPlatforms}", bodyFont, mutedText, launchersHeader, rightAligned);

        Rectangle steamToggle = new(bounds.X, bounds.Y + 30, bounds.Width, toggleHeight);
        Rectangle epicToggle = new(bounds.X, steamToggle.Bottom + toggleSpacing, bounds.Width, toggleHeight);
        Rectangle eaToggle = new(bounds.X, epicToggle.Bottom + toggleSpacing, bounds.Width, toggleHeight);
        Rectangle gogToggle = new(bounds.X, eaToggle.Bottom + toggleSpacing, bounds.Width, toggleHeight);
        Rectangle battleNetToggle = new(bounds.X, gogToggle.Bottom + toggleSpacing, bounds.Width, toggleHeight);
        Rectangle xboxToggle = new(bounds.X, battleNetToggle.Bottom + toggleSpacing, bounds.Width, toggleHeight);
        Rectangle ubisoftToggle = new(bounds.X, xboxToggle.Bottom + toggleSpacing, bounds.Width, toggleHeight);

        DrawConfigToggle(graphics, steamToggle, "Steam", _enabledPlatforms.Contains(GamePlatform.Steam), IsLauncherAvailable(GamePlatform.Steam), text, mutedText, border);
        DrawConfigToggle(graphics, epicToggle, "Epic", _enabledPlatforms.Contains(GamePlatform.Epic), IsLauncherAvailable(GamePlatform.Epic), text, mutedText, border);
        DrawConfigToggle(graphics, eaToggle, "EA", _enabledPlatforms.Contains(GamePlatform.Ea), IsLauncherAvailable(GamePlatform.Ea), text, mutedText, border);
        DrawConfigToggle(graphics, gogToggle, "GOG", _enabledPlatforms.Contains(GamePlatform.Gog), IsLauncherAvailable(GamePlatform.Gog), text, mutedText, border);
        DrawConfigToggle(graphics, battleNetToggle, "Battle.net", _enabledPlatforms.Contains(GamePlatform.BattleNet), IsLauncherAvailable(GamePlatform.BattleNet), text, mutedText, border);
        DrawConfigToggle(graphics, xboxToggle, "Xbox", _enabledPlatforms.Contains(GamePlatform.Xbox), IsLauncherAvailable(GamePlatform.Xbox), text, mutedText, border);
        DrawConfigToggle(graphics, ubisoftToggle, "Ubisoft", _enabledPlatforms.Contains(GamePlatform.Ubisoft), IsLauncherAvailable(GamePlatform.Ubisoft), text, mutedText, border);

        if (IsLauncherAvailable(GamePlatform.Steam))
        {
            _hitTargets.Add(new HitTarget(HitTargetKind.ToggleSteam, steamToggle, null));
        }

        if (IsLauncherAvailable(GamePlatform.Epic))
        {
            _hitTargets.Add(new HitTarget(HitTargetKind.ToggleEpic, epicToggle, null));
        }

        if (IsLauncherAvailable(GamePlatform.Ea))
        {
            _hitTargets.Add(new HitTarget(HitTargetKind.ToggleEa, eaToggle, null));
        }

        if (IsLauncherAvailable(GamePlatform.Gog))
        {
            _hitTargets.Add(new HitTarget(HitTargetKind.ToggleGog, gogToggle, null));
        }

        if (IsLauncherAvailable(GamePlatform.BattleNet))
        {
            _hitTargets.Add(new HitTarget(HitTargetKind.ToggleBattleNet, battleNetToggle, null));
        }

        if (IsLauncherAvailable(GamePlatform.Xbox))
        {
            _hitTargets.Add(new HitTarget(HitTargetKind.ToggleXbox, xboxToggle, null));
        }

        if (IsLauncherAvailable(GamePlatform.Ubisoft))
        {
            _hitTargets.Add(new HitTarget(HitTargetKind.ToggleUbisoft, ubisoftToggle, null));
        }
    }

    private void DrawQuickActionsConfigPage(Graphics graphics, Rectangle bounds, Font titleFont, Font bodyFont, Brush text, Brush mutedText, Pen border)
    {
        graphics.DrawString("Quick Actions", titleFont, text, bounds.Location);
        graphics.DrawString("Pick up to three quickbar slots. Tap arrows to cycle options.", bodyFont, mutedText, new PointF(bounds.X, bounds.Y + 28));

        Rectangle slotOne = new(bounds.X, bounds.Y + 70, bounds.Width, 54);
        Rectangle slotTwo = new(bounds.X, slotOne.Bottom + 12, bounds.Width, 54);
        Rectangle slotThree = new(bounds.X, slotTwo.Bottom + 12, bounds.Width, 54);

        DrawQuickActionSlotSelector(graphics, slotOne, "Slot 1", _quickActionSlots[0], text, mutedText, border);
        DrawQuickActionSlotSelector(graphics, slotTwo, "Slot 2", _quickActionSlots[1], text, mutedText, border);
        DrawQuickActionSlotSelector(graphics, slotThree, "Slot 3", _quickActionSlots[2], text, mutedText, border);

        AddQuickActionSelectorTargets(slotOne, HitTargetKind.PreviousQuickActionSlot1, HitTargetKind.NextQuickActionSlot1);
        AddQuickActionSelectorTargets(slotTwo, HitTargetKind.PreviousQuickActionSlot2, HitTargetKind.NextQuickActionSlot2);
        AddQuickActionSelectorTargets(slotThree, HitTargetKind.PreviousQuickActionSlot3, HitTargetKind.NextQuickActionSlot3);
    }

    private void DrawLocalGamesConfigPage(Graphics graphics, Rectangle bounds, Font titleFont, Font bodyFont, Brush text, Brush mutedText, Pen border)
    {
        Rectangle localHeader = new(bounds.X, bounds.Y, bounds.Width, 22);
        graphics.DrawString("Local Games", titleFont, text, localHeader.Location);
        using StringFormat rightAligned = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near };
        graphics.DrawString($"{_manualGames.Count}", bodyFont, mutedText, localHeader, rightAligned);

        Rectangle addLocalGameButton = new(bounds.X, bounds.Y + 32, bounds.Width, 42);
        DrawConfigActionButton(graphics, addLocalGameButton, "Add Local Game", text, border);
        _hitTargets.Add(new HitTarget(HitTargetKind.AddLocalGame, addLocalGameButton, null));

        int listY = addLocalGameButton.Bottom + 10;
        int pageCount = GetLocalGamesPageCount();
        _localGamesPageIndex = Math.Min(_localGamesPageIndex, pageCount - 1);

        List<ManualGameEntry> visibleGames = _manualGames
            .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(_localGamesPageIndex * LocalGamesPerConfigPage)
            .Take(LocalGamesPerConfigPage)
            .ToList();

        foreach (ManualGameEntry manualGame in visibleGames)
        {
            Rectangle rowBounds = new(bounds.X, listY, bounds.Width, 38);
            DrawManualGameRow(graphics, rowBounds, manualGame, bodyFont, text, mutedText, border);
            Rectangle removeButton = new(rowBounds.Right - 70, rowBounds.Y + 6, 58, rowBounds.Height - 12);
            _hitTargets.Add(new HitTarget(HitTargetKind.RemoveLocalGame, removeButton, null, manualGame));
            listY = rowBounds.Bottom + 8;
        }

        Rectangle pagerBounds = new(bounds.X, bounds.Bottom - 38, bounds.Width, 32);
        DrawLocalGamesPager(graphics, pagerBounds, bodyFont, text, mutedText, border, pageCount);

        if (_manualGames.Count == 0)
        {
            graphics.DrawString("No local games added yet.", bodyFont, mutedText, new PointF(bounds.X, listY + 2));
        }
    }

    private void DrawConfigToggle(Graphics graphics, Rectangle bounds, string label, bool isEnabled, bool isAvailable, Brush text, Brush mutedText, Pen border)
    {
        graphics.FillRectangle(new SolidBrush(CurrentTheme.PanelColor), bounds);
        graphics.DrawRectangle(border, bounds);
        using Font labelFont = new("Segoe UI Semibold", 10f);
        graphics.DrawString(label, labelFont, isAvailable ? text : mutedText, new PointF(bounds.X + 12, bounds.Y + 11));

        Rectangle statePill = new(bounds.Right - 92, bounds.Y + 7, 76, bounds.Height - 14);
        using Brush pillBrush = new SolidBrush(!isAvailable ? Color.FromArgb(42, 46, 56) : isEnabled ? CurrentTheme.AccentSoftColor : Color.FromArgb(50, 56, 68));
        graphics.FillRectangle(pillBrush, statePill);
        graphics.DrawRectangle(border, statePill);
        using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using Font stateFont = new("Segoe UI Semibold", 9f);
        string stateText = !isAvailable ? "N/A" : isEnabled ? "ON" : "OFF";
        graphics.DrawString(stateText, stateFont, !isAvailable ? mutedText : isEnabled ? text : mutedText, statePill, centered);
    }

    private void DrawConfigActionButton(Graphics graphics, Rectangle bounds, string label, Brush text, Pen border)
    {
        graphics.FillRectangle(new SolidBrush(CurrentTheme.PanelColor), bounds);
        graphics.DrawRectangle(border, bounds);
        using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString(label, new Font("Segoe UI Semibold", 10f), text, bounds, centered);
    }

    private void DrawQuickActionSlotSelector(Graphics graphics, Rectangle bounds, string slotLabel, QuickActionKind quickAction, Brush text, Brush mutedText, Pen border)
    {
        using Brush fill = new SolidBrush(CurrentTheme.PanelColor);
        graphics.FillRectangle(fill, bounds);
        graphics.DrawRectangle(border, bounds);

        Rectangle previousButton = new(bounds.X + 8, bounds.Y + 8, 34, bounds.Height - 16);
        Rectangle nextButton = new(bounds.Right - 42, bounds.Y + 8, 34, bounds.Height - 16);
        Rectangle center = new(previousButton.Right + 10, bounds.Y + 6, bounds.Width - previousButton.Width - nextButton.Width - 36, bounds.Height - 12);

        DrawPagerButton(graphics, previousButton, "<", true, text, mutedText, border);
        DrawPagerButton(graphics, nextButton, ">", true, text, mutedText, border);

        using Font slotFont = new("Segoe UI Semibold", 9f);
        using Font actionFont = new("Segoe UI Semibold", 10f);
        using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        Rectangle slotRect = new(center.X, center.Y, center.Width, 16);
        Rectangle actionRect = new(center.X, center.Y + 16, center.Width, 18);
        Rectangle helperRect = new(center.X, center.Y + 32, center.Width, 12);
        graphics.DrawString(slotLabel, slotFont, mutedText, slotRect, centered);
        graphics.DrawString(GetQuickActionLabel(quickAction), actionFont, text, actionRect, centered);
        using Font helperFont = new("Segoe UI", 7.5f);
        using Brush unavailableBrush = new SolidBrush(Color.FromArgb(196, 142, 142));
        graphics.DrawString(GetQuickActionHelperText(quickAction), helperFont, IsQuickActionAvailable(quickAction) ? mutedText : unavailableBrush, helperRect, centered);
    }

    private void AddQuickActionSelectorTargets(Rectangle bounds, HitTargetKind previousKind, HitTargetKind nextKind)
    {
        Rectangle previousButton = new(bounds.X + 8, bounds.Y + 8, 34, bounds.Height - 16);
        Rectangle nextButton = new(bounds.Right - 42, bounds.Y + 8, 34, bounds.Height - 16);
        _hitTargets.Add(new HitTarget(previousKind, previousButton, null));
        _hitTargets.Add(new HitTarget(nextKind, nextButton, null));
    }

    private void DrawLocalGamesPager(Graphics graphics, Rectangle bounds, Font bodyFont, Brush text, Brush mutedText, Pen border, int pageCount)
    {
        bool hasPrevious = _localGamesPageIndex > 0;
        bool hasNext = _localGamesPageIndex < pageCount - 1;

        Rectangle previousButton = new(bounds.X, bounds.Y, 42, bounds.Height);
        Rectangle nextButton = new(bounds.Right - 42, bounds.Y, 42, bounds.Height);
        Rectangle pageLabel = new(previousButton.Right + 8, bounds.Y, bounds.Width - 100, bounds.Height);

        DrawPagerButton(graphics, previousButton, "<", hasPrevious, text, mutedText, border);
        DrawPagerButton(graphics, nextButton, ">", hasNext, text, mutedText, border);

        using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString($"{Math.Min(_localGamesPageIndex + 1, pageCount)}/{pageCount}", bodyFont, mutedText, pageLabel, centered);

        if (hasPrevious)
        {
            _hitTargets.Add(new HitTarget(HitTargetKind.PreviousLocalGamesPage, previousButton, null));
        }

        if (hasNext)
        {
            _hitTargets.Add(new HitTarget(HitTargetKind.NextLocalGamesPage, nextButton, null));
        }
    }

    private void DrawManualGameRow(Graphics graphics, Rectangle bounds, ManualGameEntry manualGame, Font bodyFont, Brush text, Brush mutedText, Pen border)
    {
        graphics.FillRectangle(new SolidBrush(CurrentTheme.PanelColor), bounds);
        graphics.DrawRectangle(border, bounds);

        Rectangle labelBounds = new(bounds.X + 10, bounds.Y + 8, bounds.Width - 92, 20);
        graphics.DrawString(manualGame.Name, bodyFont, text, labelBounds);

        Rectangle removeBounds = new(bounds.Right - 70, bounds.Y + 6, 58, bounds.Height - 12);
        graphics.FillRectangle(new SolidBrush(Color.FromArgb(50, 56, 68)), removeBounds);
        graphics.DrawRectangle(border, removeBounds);
        using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString("Remove", bodyFont, mutedText, removeBounds, centered);
    }

    private void DrawGames(Graphics graphics, Rectangle bounds, Font labelFont, Brush text, Pen border)
    {
        IReadOnlyList<GameEntry> filteredGames = GetFilteredGames();

        (int itemsPerPage, int pageCount) = GetPageMetrics(filteredGames);
        int columns = Math.Max(1, WidgetSize.Width);
        int rows = Math.Max(1, WidgetSize.Height - 1);
        _pageIndex = Math.Min(_pageIndex, pageCount - 1);

        IReadOnlyList<GameEntry> pageItems = filteredGames
            .Skip(_pageIndex * itemsPerPage)
            .Take(itemsPerPage)
            .ToArray();

        int tileWidth = Math.Max(1, bounds.Width / columns);
        int tileHeight = Math.Max(1, bounds.Height / rows);

        for (int index = 0; index < pageItems.Count; index++)
        {
            int row = index / columns;
            int column = index % columns;
            GameEntry game = pageItems[index];
            bool isSelected = string.Equals(_selectedGameId, game.Id, StringComparison.OrdinalIgnoreCase);
            Rectangle tile = new(bounds.X + column * tileWidth + 8, bounds.Y + row * tileHeight + 8, tileWidth - 16, tileHeight - 16);

            using Brush tileBrush = new SolidBrush(isSelected ? CurrentTheme.SelectedPanelColor : CurrentTheme.PanelColor);
            using Pen tileBorder = new(isSelected ? CurrentTheme.AccentColor : CurrentTheme.BorderColor, isSelected ? 2f : 1f);
            graphics.FillRectangle(tileBrush, tile);
            graphics.DrawRectangle(tileBorder, tile);

            int iconSize = Math.Max(74, Math.Min(tile.Width - 28, tile.Height - 74));
            Rectangle iconRect = new(tile.X + (tile.Width - iconSize) / 2, tile.Y + 12, iconSize, iconSize);
            Image icon = GetIcon(game, new Size(iconSize, iconSize));
            graphics.DrawImage(icon, iconRect);

            RectangleF nameRect = new(tile.X + 10, iconRect.Bottom + 10, tile.Width - 20, tile.Bottom - iconRect.Bottom - 18);
            using StringFormat centered = new()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap,
            };

            graphics.DrawString(game.Name, labelFont, text, nameRect, centered);

            _hitTargets.Add(new HitTarget(HitTargetKind.Game, tile, game));
        }
    }

    private void DrawFooter(Graphics graphics, Rectangle bounds, Font font, Brush text, Pen border)
    {
        IReadOnlyList<GameEntry> filteredGames = GetFilteredGames();
        (int _, int pageCount) = GetPageMetrics(filteredGames);
        bool hasPreviousPage = _pageIndex > 0;
        bool hasNextPage = _pageIndex < pageCount - 1;
        Rectangle prev = new(bounds.X + 10, bounds.Y + 9, 128, bounds.Height - 18);
        Rectangle next = new(bounds.Right - 138, bounds.Y + 9, 128, bounds.Height - 18);
        Rectangle center = new(prev.Right + 12, bounds.Y + 9, bounds.Width - prev.Width - next.Width - 44, bounds.Height - 18);
        GameEntry? selectedGame = GetSelectedGame(filteredGames);

        using Brush prevBrush = new SolidBrush(hasPreviousPage ? CurrentTheme.PanelColor : Color.FromArgb(50, 56, 68));
        using Brush nextBrush = new SolidBrush(hasNextPage ? CurrentTheme.PanelColor : Color.FromArgb(50, 56, 68));
        using Brush disabledText = new SolidBrush(Color.FromArgb(118, 128, 146));

        graphics.FillRectangle(prevBrush, prev);
        graphics.FillRectangle(nextBrush, next);
        graphics.DrawRectangle(border, prev);
        graphics.DrawRectangle(border, next);

        using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString("◀", font, hasPreviousPage ? text : disabledText, prev, centered);
        graphics.DrawString("▶", font, hasNextPage ? text : disabledText, next, centered);
        graphics.FillRectangle(prevBrush, prev);
        graphics.FillRectangle(nextBrush, next);
        graphics.DrawRectangle(border, prev);
        graphics.DrawRectangle(border, next);
        graphics.DrawString("<", font, hasPreviousPage ? text : disabledText, prev, centered);
        graphics.DrawString(">", font, hasNextPage ? text : disabledText, next, centered);

        if (selectedGame is not null)
        {
            graphics.FillRectangle(new SolidBrush(CurrentTheme.AccentSoftColor), center);
            graphics.DrawRectangle(new Pen(CurrentTheme.AccentColor, 2f), center);
            graphics.DrawString($"Launch {selectedGame.Name}", font, text, center, centered);
            _hitTargets.Add(new HitTarget(HitTargetKind.LaunchSelected, center, selectedGame));
        }
        else
        {
            graphics.DrawString("Select a game to launch", font, text, center, centered);
        }

        if (hasPreviousPage)
        {
            _hitTargets.Add(new HitTarget(HitTargetKind.PreviousPage, prev, null));
        }

        if (hasNextPage)
        {
            _hitTargets.Add(new HitTarget(HitTargetKind.NextPage, next, null));
        }
    }

    private void DrawPagerButton(Graphics graphics, Rectangle bounds, string label, bool isEnabled, Brush text, Brush mutedText, Pen border)
    {
        using Brush fill = new SolidBrush(isEnabled ? CurrentTheme.PanelColor : Color.FromArgb(50, 56, 68));
        graphics.FillRectangle(fill, bounds);
        graphics.DrawRectangle(border, bounds);

        using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString(label, new Font("Segoe UI Semibold", 10f), isEnabled ? text : mutedText, bounds, centered);
    }

    private IReadOnlyList<GameEntry> GetFilteredGames()
    {
        return (_platformFilter is null
                ? _games
                : _games.Where(game => game.Platform == _platformFilter.Value).ToArray())
            .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private GameEntry? GetSelectedGame(IReadOnlyList<GameEntry> games)
    {
        return string.IsNullOrWhiteSpace(_selectedGameId)
            ? null
            : games.FirstOrDefault(game => string.Equals(game.Id, _selectedGameId, StringComparison.OrdinalIgnoreCase));
    }

    private void EnsureSelectedGameVisible()
    {
        if (GetSelectedGame(GetFilteredGames()) is null)
        {
            _selectedGameId = null;
        }
    }

    private (int ItemsPerPage, int PageCount) GetPageMetrics(IReadOnlyList<GameEntry> games)
    {
        int columns = Math.Max(1, WidgetSize.Width);
        int rows = Math.Max(1, WidgetSize.Height - 1);
        int itemsPerPage = Math.Max(1, columns * rows);
        int pageCount = Math.Max(1, (int)Math.Ceiling(games.Count / (double)itemsPerPage));
        return (itemsPerPage, pageCount);
    }

    private Image GetIcon(GameEntry game, Size targetSize)
    {
        string cacheKey = $"{game.Id}:{targetSize.Width}x{targetSize.Height}";
        if (_iconCache.TryGetValue(cacheKey, out Image image))
        {
            return image;
        }

        image = WidgetGameIconResolver.Load(game, targetSize);
        _iconCache[cacheKey] = image;
        return image;
    }

    private void DrawPlatformFilterIcon(Graphics graphics, Rectangle bounds, GamePlatform platform, Color fallbackColor)
    {
        Image? icon = GetLauncherIcon(platform, new Size(24, 24));
        if (icon is not null)
        {
            Rectangle iconRect = new(bounds.X + (bounds.Width - 24) / 2, bounds.Y + (bounds.Height - 24) / 2, 24, 24);
            graphics.DrawImage(icon, iconRect);
            return;
        }

        using Font fallbackFont = new("Segoe UI Semibold", 10f);
        using Brush fallbackBrush = new SolidBrush(fallbackColor);
        using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        string fallbackText = platform switch
        {
            GamePlatform.Steam => "Steam",
            GamePlatform.Epic => "Epic",
            GamePlatform.Ea => "EA",
            GamePlatform.Gog => "GOG",
            GamePlatform.BattleNet => "BNet",
            GamePlatform.Xbox => "Xbox",
            GamePlatform.Ubisoft => "Ubi",
            _ => "?",
        };
        graphics.DrawString(fallbackText, fallbackFont, fallbackBrush, bounds, centered);
    }

    private Image? GetLauncherIcon(GamePlatform platform, Size targetSize)
    {
        if (_launcherIconCache.TryGetValue(platform, out Image? image))
        {
            return image;
        }

        image = WidgetLauncherArtworkLoader.TryLoad(platform, targetSize);
        _launcherIconCache[platform] = image;
        return image;
    }

    private List<QuickActionKind> GetVisibleQuickActions()
    {
        return _quickActionSlots
            .Where(action => action != QuickActionKind.None)
            .Where(IsQuickActionAvailable)
            .Distinct()
            .ToList();
    }

    private bool IsQuickActionAvailable(QuickActionKind quickAction)
    {
        if (_quickActionAvailabilityCache.TryGetValue(quickAction, out bool isAvailable))
        {
            return isAvailable;
        }

        switch (quickAction)
        {
            case QuickActionKind.WeMod:
                isAvailable = WidgetSystemActions.TryGetWeModExecutablePath(out _);
                break;
            case QuickActionKind.SteamLauncher:
                isAvailable = WidgetSystemActions.TryGetLauncherExecutablePath(GamePlatform.Steam, out _);
                break;
            case QuickActionKind.EpicLauncher:
                isAvailable = WidgetSystemActions.TryGetLauncherExecutablePath(GamePlatform.Epic, out _);
                break;
            case QuickActionKind.EaLauncher:
                isAvailable = WidgetSystemActions.TryGetLauncherExecutablePath(GamePlatform.Ea, out _);
                break;
            case QuickActionKind.GogLauncher:
                isAvailable = WidgetSystemActions.TryGetLauncherExecutablePath(GamePlatform.Gog, out _);
                break;
            case QuickActionKind.BattleNetLauncher:
                isAvailable = WidgetSystemActions.TryGetLauncherExecutablePath(GamePlatform.BattleNet, out _);
                break;
            case QuickActionKind.XboxLauncher:
                isAvailable = WidgetSystemActions.TryGetLauncherExecutablePath(GamePlatform.Xbox, out _);
                break;
            case QuickActionKind.UbisoftLauncher:
                isAvailable = WidgetSystemActions.TryGetLauncherExecutablePath(GamePlatform.Ubisoft, out _);
                break;
            case QuickActionKind.Discord:
                isAvailable = WidgetSystemActions.TryGetDiscordExecutablePath(out _);
                break;
            case QuickActionKind.Browser:
                isAvailable = WidgetSystemActions.TryGetDefaultBrowserExecutablePath(out _);
                break;
            case QuickActionKind.MicMute:
            case QuickActionKind.Temps:
                isAvailable = true;
                break;
            case QuickActionKind.None:
            default:
                isAvailable = false;
                break;
        }

        _quickActionAvailabilityCache[quickAction] = isAvailable;
        return isAvailable;
    }

    private string GetQuickActionLabel(QuickActionKind quickAction)
    {
        switch (quickAction)
        {
            case QuickActionKind.WeMod:
                return "WeMod";
            case QuickActionKind.SteamLauncher:
                return "Steam";
            case QuickActionKind.EpicLauncher:
                return "Epic";
            case QuickActionKind.EaLauncher:
                return "EA";
            case QuickActionKind.GogLauncher:
                return "GOG";
            case QuickActionKind.BattleNetLauncher:
                return "Battle.net";
            case QuickActionKind.XboxLauncher:
                return "Xbox";
            case QuickActionKind.UbisoftLauncher:
                return "Ubisoft";
            case QuickActionKind.Discord:
                return "Discord";
            case QuickActionKind.Browser:
                return "Browser";
            case QuickActionKind.MicMute:
                return "Mic";
            case QuickActionKind.Temps:
                return "Temps";
            default:
                return "None";
        }
    }

    private string GetQuickActionHelperText(QuickActionKind quickAction)
    {
        switch (quickAction)
        {
            case QuickActionKind.None:
                return "Hidden";
            case QuickActionKind.WeMod:
                return WidgetSystemActions.TryGetWeModExecutablePath(out _) ? "Installed app" : "Not found";
            case QuickActionKind.SteamLauncher:
                return WidgetSystemActions.TryGetLauncherExecutablePath(GamePlatform.Steam, out _) ? "Installed app" : "Not found";
            case QuickActionKind.EpicLauncher:
                return WidgetSystemActions.TryGetLauncherExecutablePath(GamePlatform.Epic, out _) ? "Installed app" : "Not found";
            case QuickActionKind.EaLauncher:
                return WidgetSystemActions.TryGetLauncherExecutablePath(GamePlatform.Ea, out _) ? "Installed app" : "Not found";
            case QuickActionKind.GogLauncher:
                return WidgetSystemActions.TryGetLauncherExecutablePath(GamePlatform.Gog, out _) ? "Installed app" : "Not found";
            case QuickActionKind.BattleNetLauncher:
                return WidgetSystemActions.TryGetLauncherExecutablePath(GamePlatform.BattleNet, out _) ? "Installed app" : "Not found";
            case QuickActionKind.XboxLauncher:
                return WidgetSystemActions.TryGetLauncherExecutablePath(GamePlatform.Xbox, out _) ? "Installed app" : "Not found";
            case QuickActionKind.UbisoftLauncher:
                return WidgetSystemActions.TryGetLauncherExecutablePath(GamePlatform.Ubisoft, out _) ? "Installed app" : "Not found";
            case QuickActionKind.Discord:
                return WidgetSystemActions.TryGetDiscordExecutablePath(out _) ? "Installed app" : "Not found";
            case QuickActionKind.Browser:
                return WidgetSystemActions.TryGetDefaultBrowserExecutablePath(out _) ? "Windows default" : "Not found";
            case QuickActionKind.MicMute:
                return "Toggle mute";
            case QuickActionKind.Temps:
                return "Status tile";
            default:
                return string.Empty;
        }
    }

    private Image? GetQuickActionIcon(QuickActionKind quickAction, Size targetSize)
    {
        if (_quickActionIconCache.TryGetValue(quickAction, out Image? image))
        {
            return image;
        }

        image = quickAction switch
        {
            QuickActionKind.WeMod => WidgetWeModArtworkLoader.TryLoad(targetSize),
            QuickActionKind.SteamLauncher => GetLauncherIcon(GamePlatform.Steam, targetSize),
            QuickActionKind.EpicLauncher => GetLauncherIcon(GamePlatform.Epic, targetSize),
            QuickActionKind.EaLauncher => GetLauncherIcon(GamePlatform.Ea, targetSize),
            QuickActionKind.GogLauncher => GetLauncherIcon(GamePlatform.Gog, targetSize),
            QuickActionKind.BattleNetLauncher => GetLauncherIcon(GamePlatform.BattleNet, targetSize),
            QuickActionKind.XboxLauncher => GetLauncherIcon(GamePlatform.Xbox, targetSize),
            QuickActionKind.UbisoftLauncher => GetLauncherIcon(GamePlatform.Ubisoft, targetSize),
            QuickActionKind.Discord when WidgetSystemActions.TryGetDiscordExecutablePath(out string? discordPath) => WidgetLauncherArtworkLoader.TryLoad(discordPath!, targetSize),
            QuickActionKind.Browser when WidgetSystemActions.TryGetDefaultBrowserExecutablePath(out string? browserPath) => WidgetLauncherArtworkLoader.TryLoad(browserPath!, targetSize),
            _ => null,
        };

        _quickActionIconCache[quickAction] = image;
        return image;
    }

    private Image? GetStockMicIcon(bool isMuted, Size targetSize, Color color)
    {
        string cacheKey = $"{(isMuted ? "mic-mute" : "mic")}:{targetSize.Width}x{targetSize.Height}:{color.ToArgb()}";
        if (_stockIconCache.TryGetValue(cacheKey, out Image? image))
        {
            return image;
        }

        image = WidgetStockIconLoader.TryLoadDefaultIcon(isMuted ? "mic-mute.svg" : "mic.svg", targetSize, color);
        _stockIconCache[cacheKey] = image;
        return image;
    }

    private void DrawThemeButton(Graphics graphics, Rectangle bounds, Font font, Brush text, Brush mutedText, Pen border)
    {
        graphics.FillRectangle(new SolidBrush(CurrentTheme.PanelColor), bounds);
        graphics.DrawRectangle(border, bounds);

        using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        Rectangle titleRect = new(bounds.X + 6, bounds.Y + 4, bounds.Width - 12, 14);
        Rectangle valueRect = new(bounds.X + 6, bounds.Y + 18, bounds.Width - 12, 18);
        graphics.DrawString("Theme", font, mutedText, titleRect, centered);
        graphics.DrawString(CurrentTheme.Name, font, text, valueRect, centered);
    }

    private void DrawConfigButton(Graphics graphics, Rectangle bounds, Font font, Brush text, Brush mutedText, Pen border)
    {
        graphics.FillRectangle(new SolidBrush(CurrentTheme.PanelColor), bounds);
        graphics.DrawRectangle(border, bounds);

        using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        Rectangle iconRect = new(bounds.X + 6, bounds.Y + 3, bounds.Width - 12, 16);
        Rectangle labelRect = new(bounds.X + 6, bounds.Y + 18, bounds.Width - 12, 18);
        graphics.DrawString("\u2699", font, text, iconRect, centered);
        graphics.DrawString("Config", font, mutedText, labelRect, centered);
    }

    private ThemePalette CurrentTheme => ThemePalettes[_themeIndex];

    private void LoadThemeSelection()
    {
        try
        {
            string statePath = GetExistingThemeStatePath();
            if (!File.Exists(statePath))
            {
                return;
            }

            string contents = File.ReadAllText(statePath).Trim();
            if (!int.TryParse(contents, out int themeIndex))
            {
                return;
            }

            _themeIndex = Math.Max(0, Math.Min(themeIndex, ThemePalettes.Length - 1));
        }
        catch
        {
        }
    }

    private void SaveThemeSelection()
    {
        try
        {
            string statePath = GetThemeStatePath();
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            File.WriteAllText(statePath, _themeIndex.ToString());
        }
        catch
        {
        }
    }

    private string GetThemeStatePath()
    {
        string stateDirectory = GetPreferredWidgetStateDirectory();
        return Path.Combine(stateDirectory, $"{Guid:N}.theme.txt");
    }

    private void LoadWidgetConfiguration()
    {
        try
        {
            string configPath = GetExistingWidgetConfigPath();
            if (!File.Exists(configPath))
            {
                _enabledPlatforms = CreateDefaultEnabledPlatforms();
                _quickActionSlots = CreateDefaultQuickActionSlots();
                return;
            }

            foreach (string rawLine in File.ReadAllLines(configPath))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, separatorIndex).Trim();
                string value = line.Substring(separatorIndex + 1).Trim();

                if (string.Equals(key, "enabledPlatforms", StringComparison.OrdinalIgnoreCase))
                {
                    HashSet<GamePlatform> parsedPlatforms = new();
                    foreach (string part in value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (Enum.TryParse(part.Trim(), true, out GamePlatform platform) && _scannerFactories.ContainsKey(platform))
                        {
                            parsedPlatforms.Add(platform);
                        }
                    }

                    if (parsedPlatforms.Count > 0)
                    {
                        _enabledPlatforms = new HashSet<GamePlatform>(
                            parsedPlatforms
                                .Where(IsLauncherAvailable)
                                .OrderBy(platform => platform)
                                .Take(MaxEnabledPlatforms));
                    }
                }
                else if (string.Equals(key, "quickActionSlots", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    QuickActionKind[] loadedSlots = CreateDefaultQuickActionSlots();
                    for (int index = 0; index < Math.Min(parts.Length, QuickActionSlotCount); index++)
                    {
                        if (Enum.TryParse(parts[index].Trim(), true, out QuickActionKind quickAction))
                        {
                            loadedSlots[index] = quickAction;
                        }
                    }

                    _quickActionSlots = loadedSlots;
                }
                else if (string.Equals(key, "clockFormat", StringComparison.OrdinalIgnoreCase))
                {
                    _useTwelveHourClock = value.Equals("12h", StringComparison.OrdinalIgnoreCase) ||
                        value.Equals("ampm", StringComparison.OrdinalIgnoreCase) ||
                        value.Equals("12", StringComparison.OrdinalIgnoreCase);
                }
                else if (string.Equals(key, "quickAction", StringComparison.OrdinalIgnoreCase))
                {
                    if (Enum.TryParse(value, true, out QuickActionKind quickAction))
                    {
                        _quickActionSlots = CreateDefaultQuickActionSlots();
                        _quickActionSlots[0] = quickAction;
                    }
                }
                else if (string.Equals(key, "showWeModButton", StringComparison.OrdinalIgnoreCase))
                {
                    bool useWeMod = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    if (useWeMod)
                    {
                        _quickActionSlots = CreateDefaultQuickActionSlots();
                        _quickActionSlots[0] = QuickActionKind.WeMod;
                    }
                }
                else if (string.Equals(key, "localGame", StringComparison.OrdinalIgnoreCase) && File.Exists(value))
                {
                    string executablePath = value;
                    if (_manualGames.Any(game => string.Equals(game.ExecutablePath, executablePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    _manualGames.Add(new ManualGameEntry(
                        $"manual:{executablePath}",
                        Path.GetFileNameWithoutExtension(executablePath),
                        executablePath));
                }
            }
        }
        catch
        {
            _enabledPlatforms = CreateDefaultEnabledPlatforms();
            _quickActionSlots = CreateDefaultQuickActionSlots();
            _manualGames = [];
        }
    }

    private void SaveWidgetConfiguration()
    {
        try
        {
            string configPath = GetWidgetConfigPath();
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            string[] baseLines =
            [
                $"enabledPlatforms={string.Join(",", _enabledPlatforms.OrderBy(platform => platform).Select(platform => platform.ToString()))}",
                $"quickActionSlots={string.Join(",", _quickActionSlots.Select(action => action.ToString()))}",
                $"clockFormat={(_useTwelveHourClock ? "12h" : "24h")}",
            ];

            string[] lines = baseLines
                .Concat(_manualGames
                    .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(game => $"localGame={game.ExecutablePath}"))
                .ToArray();
            File.WriteAllLines(configPath, lines);
        }
        catch
        {
        }
    }

    private string GetWidgetConfigPath()
    {
        string stateDirectory = GetPreferredWidgetStateDirectory();
        return Path.Combine(stateDirectory, $"{Guid:N}.config.txt");
    }

    private string GetExistingWidgetConfigPath()
    {
        string preferredPath = GetWidgetConfigPath();
        if (File.Exists(preferredPath))
        {
            return preferredPath;
        }

        string legacyPath = Path.Combine(GetLegacyWidgetStateDirectory(), $"{Guid:N}.config.txt");
        return File.Exists(legacyPath) ? legacyPath : preferredPath;
    }

    private string GetExistingThemeStatePath()
    {
        string preferredPath = GetThemeStatePath();
        if (File.Exists(preferredPath))
        {
            return preferredPath;
        }

        string legacyPath = Path.Combine(GetLegacyWidgetStateDirectory(), $"{Guid:N}.theme.txt");
        return File.Exists(legacyPath) ? legacyPath : preferredPath;
    }

    private string GetPreferredWidgetStateDirectory()
    {
        string widgetsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "G.SKILL",
            "WigiDashManager",
            "Widgets");

        return Path.Combine(widgetsRoot, WidgetStateFolderName);
    }

    private string GetLegacyWidgetStateDirectory()
    {
        string widgetsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "G.SKILL",
            "WigiDashManager",
            "Widgets");

        return Path.Combine(widgetsRoot, LegacyWidgetStateFolderName);
    }

    private static void LaunchGame(GameEntry game)
    {
        try
        {
            string? workingDirectory = null;
            if (!string.IsNullOrWhiteSpace(game.InstallPath) && Directory.Exists(game.InstallPath))
            {
                workingDirectory = game.InstallPath;
            }
            else if (File.Exists(game.LaunchCommand))
            {
                workingDirectory = Path.GetDirectoryName(game.LaunchCommand);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = game.LaunchCommand,
                Arguments = game.LaunchArguments ?? string.Empty,
                UseShellExecute = true,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory,
            });
        }
        catch
        {
        }
    }

    private void UpdateUtilityState()
    {
        if (_isDisposed)
        {
            return;
        }

        _clockText = DateTime.Now.ToString(_useTwelveHourClock ? "hh:mm tt" : "HH:mm");
        if (_cpuSensor is null && _gpuSensor is null)
        {
            (string cpu, string gpu) = WidgetSystemActions.GetTemperatureSummary();
            _cpuTempText = cpu;
            _gpuTempText = gpu;
        }
    }

    private void InitializeSensorMonitoring()
    {
        IWidgetManager? manager = _widgetObject.WidgetManager;
        if (manager is null)
        {
            return;
        }

        try
        {
            IReadOnlyList<SensorItem> sensors = manager.GetSensorList();
            _cpuSensor = SelectBestSensor(sensors, isGpu: false);
            _gpuSensor = SelectBestSensor(sensors, isGpu: true);

            if (_cpuSensor is not null)
            {
                manager.AddMonitoringItem(_cpuSensor);
            }

            if (_gpuSensor is not null)
            {
                manager.AddMonitoringItem(_gpuSensor);
            }

            manager.SensorUpdated += OnSensorUpdated;
        }
        catch
        {
        }
    }

    private void TearDownSensorMonitoring()
    {
        IWidgetManager? manager = _widgetObject.WidgetManager;
        if (manager is null)
        {
            return;
        }

        try
        {
            manager.SensorUpdated -= OnSensorUpdated;

            if (_cpuSensor is not null)
            {
                manager.RemoveMonitoringItem(_cpuSensor);
            }

            if (_gpuSensor is not null)
            {
                manager.RemoveMonitoringItem(_gpuSensor);
            }
        }
        catch
        {
        }
    }

    private void OnSensorUpdated(SensorItem item, double value)
    {
        if (_isDisposed)
        {
            return;
        }

        bool changed = false;

        if (IsSameSensor(_cpuSensor, item))
        {
            _cpuTempText = FormatTemperature(value);
            changed = true;
        }

        if (IsSameSensor(_gpuSensor, item))
        {
            _gpuTempText = FormatTemperature(value);
            changed = true;
        }

        if (changed && !_isSleeping)
        {
            RaiseWidgetUpdated();
        }
    }

    private static SensorItem? SelectBestSensor(IReadOnlyList<SensorItem> sensors, bool isGpu)
    {
        return sensors
            .Where(sensor => LooksLikeTemperatureSensor(sensor))
            .Where(sensor => isGpu ? LooksLikeGpuSensor(sensor) : LooksLikeCpuSensor(sensor))
            .OrderByDescending(sensor => ScoreSensor(sensor, isGpu))
            .FirstOrDefault();
    }

    private static bool LooksLikeTemperatureSensor(SensorItem sensor)
    {
        return ContainsIgnoreCase(sensor.Unit, "C") ||
            ContainsIgnoreCase(sensor.Name, "temp") ||
            ContainsIgnoreCase(sensor.Source, "temp");
    }

    private static bool LooksLikeCpuSensor(SensorItem sensor)
    {
        string text = $"{sensor.Source} {sensor.Name}";
        return ContainsIgnoreCase(text, "cpu") ||
            ContainsIgnoreCase(text, "package") ||
            ContainsIgnoreCase(text, "tdie") ||
            ContainsIgnoreCase(text, "tctl");
    }

    private static bool LooksLikeGpuSensor(SensorItem sensor)
    {
        string text = $"{sensor.Source} {sensor.Name}";
        return ContainsIgnoreCase(text, "gpu") ||
            ContainsIgnoreCase(text, "graphics") ||
            ContainsIgnoreCase(text, "hot spot");
    }

    private static int ScoreSensor(SensorItem sensor, bool isGpu)
    {
        string text = $"{sensor.Source} {sensor.Name}";
        int score = 0;

        if (isGpu)
        {
            if (ContainsIgnoreCase(text, "gpu temperature"))
            {
                score += 50;
            }

            if (ContainsIgnoreCase(text, "hot spot"))
            {
                score += 30;
            }
        }
        else
        {
            if (ContainsIgnoreCase(text, "cpu package"))
            {
                score += 50;
            }

            if (ContainsIgnoreCase(text, "package"))
            {
                score += 25;
            }

            if (ContainsIgnoreCase(text, "tdie") ||
                ContainsIgnoreCase(text, "tctl"))
            {
                score += 20;
            }
        }

        if (ContainsIgnoreCase(sensor.Unit, "C"))
        {
            score += 10;
        }

        return score;
    }

    private static bool IsSameSensor(SensorItem? left, SensorItem right)
    {
        return left is not null &&
            left.Guid == right.Guid &&
            left.SensorId1 == right.SensorId1 &&
            left.SensorId2 == right.SensorId2;
    }

    private static string FormatTemperature(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) ? $"{Math.Round(value):0}C" : "--";
    }

    private static bool ContainsIgnoreCase(string value, string fragment)
    {
        return value?.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void LogMessage(string message)
    {
        try
        {
            string logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "G.SKILL",
                "WigiDashManager",
                "Logs");

            Directory.CreateDirectory(logDirectory);

            string logPath = Path.Combine(logDirectory, "gamehub-widget.log");
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}";
            File.AppendAllText(logPath, line);
        }
        catch
        {
        }
    }

    private sealed class HitTarget
    {
        public HitTarget(HitTargetKind kind, Rectangle bounds, GameEntry? game, ManualGameEntry? manualGame = null)
        {
            Kind = kind;
            Bounds = bounds;
            Game = game;
            ManualGame = manualGame;
        }

        public HitTargetKind Kind { get; }

        public Rectangle Bounds { get; }

        public GameEntry? Game { get; }

        public ManualGameEntry? ManualGame { get; }
    }

    private enum HitTargetKind
    {
        UtilityQuickActionSlot1,
        UtilityQuickActionSlot2,
        UtilityQuickActionSlot3,
        UtilityMicMute,
        ToggleClockFormat,
        OpenConfig,
        CloseConfig,
        ConfigLaunchers,
        ConfigQuickActions,
        ConfigLocalGames,
        ToggleSteam,
        ToggleEpic,
        ToggleEa,
        ToggleGog,
        ToggleBattleNet,
        ToggleXbox,
        ToggleUbisoft,
        PreviousQuickActionSlot1,
        NextQuickActionSlot1,
        PreviousQuickActionSlot2,
        NextQuickActionSlot2,
        PreviousQuickActionSlot3,
        NextQuickActionSlot3,
        AddLocalGame,
        RemoveLocalGame,
        PreviousLocalGamesPage,
        NextLocalGamesPage,
        PreviousPage,
        NextPage,
        LaunchSelected,
        FilterAll,
        FilterSteam,
        FilterEpic,
        FilterEa,
        FilterGog,
        FilterBattleNet,
        FilterXbox,
        FilterUbisoft,
        CycleTheme,
        Game,
    }

    private sealed class ThemePalette
    {
        public ThemePalette(
            string name,
            Color accentColor,
            Color accentSoftColor,
            Color panelColor,
            Color selectedPanelColor,
            Color backgroundColor,
            Color cardColor,
            Color sidebarColor,
            Color borderColor)
        {
            Name = name;
            AccentColor = accentColor;
            AccentSoftColor = accentSoftColor;
            PanelColor = panelColor;
            SelectedPanelColor = selectedPanelColor;
            BackgroundColor = backgroundColor;
            CardColor = cardColor;
            SidebarColor = sidebarColor;
            BorderColor = borderColor;
        }

        public string Name { get; }

        public Color AccentColor { get; }

        public Color AccentSoftColor { get; }

        public Color PanelColor { get; }

        public Color SelectedPanelColor { get; }

        public Color BackgroundColor { get; }

        public Color CardColor { get; }

        public Color SidebarColor { get; }

        public Color BorderColor { get; }
    }

    private enum ConfigPage
    {
        Launchers,
        QuickActions,
        LocalGames,
    }

    private enum QuickActionKind
    {
        None,
        WeMod,
        SteamLauncher,
        EpicLauncher,
        EaLauncher,
        GogLauncher,
        BattleNetLauncher,
        XboxLauncher,
        UbisoftLauncher,
        Discord,
        Browser,
        MicMute,
        Temps,
    }

    private sealed class ManualGameEntry
    {
        public ManualGameEntry(string id, string name, string executablePath)
        {
            Id = id;
            Name = name;
            ExecutablePath = executablePath;
        }

        public string Id { get; }

        public string Name { get; }

        public string ExecutablePath { get; }
    }

    private IEnumerable<GameEntry> CreateManualGameEntries()
    {
        foreach (ManualGameEntry manualGame in _manualGames.ToArray())
        {
            if (!File.Exists(manualGame.ExecutablePath))
            {
                continue;
            }

            yield return new GameEntry(
                Id: manualGame.Id,
                Name: manualGame.Name,
                Platform: GamePlatform.Standalone,
                LaunchCommand: manualGame.ExecutablePath,
                LaunchArguments: null,
                InstallPath: Path.GetDirectoryName(manualGame.ExecutablePath),
                IconPath: null,
                LastPlayedAt: null);
        }
    }
}
