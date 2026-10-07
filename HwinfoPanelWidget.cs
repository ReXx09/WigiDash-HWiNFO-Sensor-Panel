using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Controls;
using WigiDashWidgetFramework;
using WigiDashWidgetFramework.WidgetUtility;

namespace HwinfoSensorPanel;

public sealed class HwinfoPanelWidget : IWidgetInstance
{
    private const float ReferenceWidth = 1016f;
    private const float ReferenceHeight = 592f;
    private static readonly Guid headerTouchTriggerId = Guid.Parse("B4C9D6B1-3C75-4C27-8E8F-1D2DA1B8A4D4");
    private static readonly object sharedGaugeSettingsLock = new();
    private static readonly HashSet<HwinfoPanelWidget> sharedGaugeWidgets = new();
    private static bool sharedGaugeSettingsLoaded;
    private static Color sharedGaugeLowColor = Color.FromArgb(55, 190, 105);
    private static Color sharedGaugeMediumColor = Color.FromArgb(235, 190, 45);
    private static Color sharedGaugeHighColor = Color.FromArgb(230, 35, 38);
    private static int sharedGaugeWarningThreshold = 60;
    private static int sharedGaugeCriticalThreshold = 85;
    private static Color sharedTemperatureLowColor = Color.FromArgb(55, 190, 105);
    private static Color sharedTemperatureMediumColor = Color.FromArgb(235, 190, 45);
    private static Color sharedTemperatureHighColor = Color.FromArgb(230, 35, 38);
    private static int sharedTemperatureWarningThreshold = 70;
    private static int sharedTemperatureCriticalThreshold = 85;
    private static readonly Bitmap logoBitmap = LoadLogoBitmap();
    private readonly HwinfoPanelFactory factory;
    private readonly ISensorSource sensorSource;
    private readonly object bitmapLock = new();
    private readonly object drawLock = new();
    private readonly AutoResetEvent stopEvent = new(false);
    private readonly AutoResetEvent discordStopEvent = new(false);
    private readonly Thread drawThread;
    private readonly Thread discordThread;
    private volatile bool running = true;
    private readonly int bitmapWidth;
    private readonly int bitmapHeight;
    private Bitmap bitmap;
    private Color accentColor = Color.FromArgb(230, 35, 38);
    private Color gaugeLowColor = Color.FromArgb(55, 190, 105);
    private Color gaugeMediumColor = Color.FromArgb(235, 190, 45);
    private Color gaugeHighColor = Color.FromArgb(230, 35, 38);
    private int gaugeWarningThreshold = 60;
    private int gaugeCriticalThreshold = 85;
    private Color temperatureLowColor = Color.FromArgb(55, 190, 105);
    private Color temperatureMediumColor = Color.FromArgb(235, 190, 45);
    private Color temperatureHighColor = Color.FromArgb(230, 35, 38);
    private int temperatureWarningThreshold = 70;
    private int temperatureCriticalThreshold = 85;
    private bool oneByOneShowsTemperature;
    private FiveByFourGaugeMode fiveByFourGaugeMode = FiveByFourGaugeMode.Load;
    private TwoByThreeLayoutMode twoByThreeLayoutMode = TwoByThreeLayoutMode.Balanced;
    private MemoryGaugeAlignment ramGaugeAlignment = MemoryGaugeAlignment.Right;
    private MemoryGaugeAlignment vramGaugeAlignment = MemoryGaugeAlignment.Right;
    private double uploadNetworkScaleMegabytesPerSecond = 3000;
    private double downloadNetworkScaleMegabytesPerSecond = 3000;
    private int updateIntervalMilliseconds = 250;
    private PanelTarget panelTarget = PanelTarget.Combined;
    private string timeZoneId = TimeZoneInfo.Local.Id;
    private HeaderTouchAction headerTouchAction;
    private Guid? headerExternalActionId;
    private PanelPage startPage = PanelPage.Hardware;
    private volatile PanelPage currentPage = PanelPage.Hardware;
    private volatile int homeView;
    private volatile DiscordStatus discordStatus = new();
    private int discordOnlineOffset;
    private int discordVoiceOffset;
    private string discordStatusUrl = "http://127.0.0.1:47900/status";
    private string discordLaunchUrl = "discord://-/";
    private string discordApiKey = string.Empty;
    private readonly HomeTileType[] homeTiles = { HomeTileType.Cpu, HomeTileType.Gpu, HomeTileType.Ram, HomeTileType.Network };
    private readonly Guid?[] homeTileActions = new Guid?[4];
    private readonly string[] homeTileLinks = new string[4];
    private readonly string[] homeTileLabels = new string[4];
    private readonly string[] homeTileBackgrounds = new string[4];
    private readonly HomeButtonTarget[] homeButtons = { HomeButtonTarget.Home, HomeButtonTarget.Hardware, HomeButtonTarget.MemoryNetwork, HomeButtonTarget.Actions, HomeButtonTarget.Info };
    private readonly string[] homeButtonLabels = { "HOME", "CPU / GPU", "RAM / NET", "AKTIONEN", "INFO" };
    // Wird pro Frame komplett neu aufgebaut und erst danach atomar ersetzt.
    private volatile List<HitTarget> hitTargets = new();
    private int timeFontSize = 15;
    private Color timeColor = Color.FromArgb(170, 178, 190);

    public HwinfoPanelWidget(HwinfoPanelFactory parent, WidgetSize widgetSize, Guid instanceGuid, ISensorSource source)
    {
        factory = parent;
        WidgetSize = widgetSize;
        Guid = instanceGuid;
        sensorSource = source;
        bitmapWidth = widgetSize.ToSize().Width;
        bitmapHeight = widgetSize.ToSize().Height;
        bitmap = new Bitmap(bitmapWidth, bitmapHeight);
        LoadSettings();
        currentPage = startPage;
        factory.WidgetManager?.RegisterTrigger(this, headerTouchTriggerId, "HWiNFO Sensor Panel: Header geklickt");
        lock (sharedGaugeSettingsLock)
            sharedGaugeWidgets.Add(this);
        LoadSensorBindings();
        drawThread = new Thread(DrawLoop) { IsBackground = true };
        drawThread.Start();
        discordThread = new Thread(DiscordLoop) { IsBackground = true };
        discordThread.Start();
    }

    public IWidgetObject WidgetObject => factory;
    public Guid Guid { get; }
    public WidgetSize WidgetSize { get; }
    public event WidgetUpdatedEventHandler WidgetUpdated;

    public void RequestUpdate() => PublishBitmap();

    public void ClickEvent(ClickType clickType, int x, int y)
    {
        if (!SupportsPages)
            return;

        float referenceX = x * LayoutReferenceWidth / bitmapWidth;
        float referenceY = y * LayoutReferenceHeight / bitmapHeight;
        if (clickType == ClickType.Single && TryHandlePageTouch((int)referenceX, (int)referenceY))
            return;

        if (headerTouchAction == HeaderTouchAction.None)
            return;

        if (referenceX < 8 || referenceX > LayoutReferenceWidth - 8 || referenceY < 8 || referenceY > 62)
            return;

        factory.WidgetManager?.OnTriggerOccurred(headerTouchTriggerId);
        if (headerTouchAction == HeaderTouchAction.Refresh)
            UpdateNow();
        else if (headerTouchAction == HeaderTouchAction.ToggleDisplay)
            ToggleDisplayMode();
        else if (headerTouchAction == HeaderTouchAction.ExternalAction && headerExternalActionId.HasValue)
            factory.WidgetManager?.OnTriggerOccurred(headerExternalActionId.Value);
    }

    public UserControl GetSettingsControl() => new HwinfoPanelSettings(this);

    // Seiten gibt es nur in den Rastern, die das vollständige Layout mit Header zeichnen und Touch auswerten.
    public bool SupportsPages => WidgetSize.Width >= 3 && WidgetSize.Height >= 2;
    public PanelPage StartPage => startPage;
    public HomeTileType GetHomeTileType(int index) => index >= 0 && index < homeTiles.Length ? homeTiles[index] : HomeTileType.Empty;
    public Guid? GetHomeTileActionId(int index) => index >= 0 && index < homeTileActions.Length ? homeTileActions[index] : null;
    public string GetHomeTileLink(int index) => index >= 0 && index < homeTileLinks.Length ? homeTileLinks[index] ?? string.Empty : string.Empty;
    public string GetHomeTileLabel(int index) => index >= 0 && index < homeTileLabels.Length ? homeTileLabels[index] ?? string.Empty : string.Empty;
    public string GetHomeTileBackground(int index) => index >= 0 && index < homeTileBackgrounds.Length ? homeTileBackgrounds[index] ?? string.Empty : string.Empty;
    public HomeButtonTarget GetHomeButtonTarget(int index) => index >= 0 && index < homeButtons.Length ? homeButtons[index] : HomeButtonTarget.Empty;
    public string GetHomeButtonLabel(int index) => index >= 0 && index < homeButtonLabels.Length ? homeButtonLabels[index] : string.Empty;

    private float LayoutReferenceHeight => WidgetSize.Height >= 4 ? ReferenceHeight : 447f;
    private float LayoutReferenceWidth => WidgetSize.Width >= 2 && WidgetSize.Height >= 2
        ? bitmapWidth * LayoutReferenceHeight / bitmapHeight
        : ReferenceWidth;

    private bool TryHandlePageTouch(int referenceX, int referenceY)
    {
        foreach (HitTarget target in hitTargets)
        {
            if (!target.Bounds.Contains(referenceX, referenceY))
                continue;

            target.OnTap();
            return true;
        }

        return false;
    }

    private void NavigateTo(PanelPage page)
    {
        if (!SupportsPages || currentPage == page)
            return;

        currentPage = page;
        UpdateNow();
    }

    public Color AccentColor => accentColor;
    public Color GaugeLowColor => sharedGaugeLowColor;
    public Color GaugeMediumColor => sharedGaugeMediumColor;
    public Color GaugeHighColor => sharedGaugeHighColor;
    public int GaugeWarningThreshold => sharedGaugeWarningThreshold;
    public int GaugeCriticalThreshold => sharedGaugeCriticalThreshold;
    public Color TemperatureLowColor => sharedTemperatureLowColor;
    public Color TemperatureMediumColor => sharedTemperatureMediumColor;
    public Color TemperatureHighColor => sharedTemperatureHighColor;
    public int TemperatureWarningThreshold => sharedTemperatureWarningThreshold;
    public int TemperatureCriticalThreshold => sharedTemperatureCriticalThreshold;
    public bool OneByOneShowsTemperature => oneByOneShowsTemperature;
    public FiveByFourGaugeMode FiveByFourGaugeMode => fiveByFourGaugeMode;
    public TwoByThreeLayoutMode TwoByThreeLayoutMode => twoByThreeLayoutMode;
    public MemoryGaugeAlignment RamGaugeAlignment => ramGaugeAlignment;
    public MemoryGaugeAlignment VramGaugeAlignment => vramGaugeAlignment;
    public double UploadNetworkScaleMegabytesPerSecond => uploadNetworkScaleMegabytesPerSecond;
    public double DownloadNetworkScaleMegabytesPerSecond => downloadNetworkScaleMegabytesPerSecond;
    public int UpdateIntervalMilliseconds => updateIntervalMilliseconds;
    public PanelTarget PanelTarget => panelTarget;
    public string TimeZoneId => timeZoneId;
    public string DiscordStatusUrl => discordStatusUrl;
    public string DiscordLaunchUrl => discordLaunchUrl;
    public string DiscordApiKey => discordApiKey;
    public HeaderTouchAction HeaderTouchAction => headerTouchAction;
    public Guid? HeaderExternalActionId => headerExternalActionId;
    public int TimeFontSize => timeFontSize;
    public Color TimeColor => timeColor;
    public IReadOnlyDictionary<Guid, string> AvailableExternalActions => factory.WidgetManager?.GetTriggerList() ?? new Dictionary<Guid, string>();
    public IReadOnlyList<SensorItem> AvailableSensors => (sensorSource as ManagerSensorSource)?.Sensors ?? Array.Empty<SensorItem>();

    public Guid? GetBoundSensor(SensorSlot slot)
    {
        return (sensorSource as ManagerSensorSource)?.GetBinding(slot);
    }

    public void BindSensor(SensorSlot slot, Guid sensorGuid)
    {
        if ((sensorSource as ManagerSensorSource)?.Bind(slot, sensorGuid) == true)
        {
            factory.WidgetManager?.StoreSetting(this, $"Sensor.{slot}", sensorGuid.ToString());
            RequestUpdate();
        }
    }

    public void SetAccentColor(Color color)
    {
        accentColor = color;
        factory.WidgetManager?.StoreSetting(this, "AccentColor", ColorTranslator.ToHtml(color));
        RequestUpdate();
    }

    public void SetGaugeColors(Color low, Color medium, Color high)
    {
        gaugeLowColor = low;
        gaugeMediumColor = medium;
        gaugeHighColor = high;
        sharedGaugeLowColor = low;
        sharedGaugeMediumColor = medium;
        sharedGaugeHighColor = high;
        StoreSharedSetting("GaugeLowColor", ColorTranslator.ToHtml(low));
        StoreSharedSetting("GaugeMediumColor", ColorTranslator.ToHtml(medium));
        StoreSharedSetting("GaugeHighColor", ColorTranslator.ToHtml(high));
        RequestUpdate();
    }

    public void SetGaugeThresholds(int warning, int critical)
    {
        gaugeWarningThreshold = Math.Max(1, Math.Min(98, warning));
        gaugeCriticalThreshold = Math.Max(gaugeWarningThreshold + 1, Math.Min(99, critical));
        sharedGaugeWarningThreshold = gaugeWarningThreshold;
        sharedGaugeCriticalThreshold = gaugeCriticalThreshold;
        StoreSharedSetting("GaugeWarningThreshold", gaugeWarningThreshold.ToString());
        StoreSharedSetting("GaugeCriticalThreshold", gaugeCriticalThreshold.ToString());
        RequestUpdate();
    }

    public void SetTemperatureColors(Color low, Color medium, Color high)
    {
        temperatureLowColor = low;
        temperatureMediumColor = medium;
        temperatureHighColor = high;
        sharedTemperatureLowColor = low;
        sharedTemperatureMediumColor = medium;
        sharedTemperatureHighColor = high;
        StoreSharedSetting("TemperatureLowColor", ColorTranslator.ToHtml(low));
        StoreSharedSetting("TemperatureMediumColor", ColorTranslator.ToHtml(medium));
        StoreSharedSetting("TemperatureHighColor", ColorTranslator.ToHtml(high));
        RequestUpdate();
    }

    public void SetTemperatureThresholds(int warning, int critical)
    {
        temperatureWarningThreshold = Math.Max(1, Math.Min(149, warning));
        temperatureCriticalThreshold = Math.Max(temperatureWarningThreshold + 1, Math.Min(150, critical));
        sharedTemperatureWarningThreshold = temperatureWarningThreshold;
        sharedTemperatureCriticalThreshold = temperatureCriticalThreshold;
        StoreSharedSetting("TemperatureWarningThreshold", temperatureWarningThreshold.ToString());
        StoreSharedSetting("TemperatureCriticalThreshold", temperatureCriticalThreshold.ToString());
        RequestUpdate();
    }

    public void SetOneByOneShowsTemperature(bool showTemperature)
    {
        oneByOneShowsTemperature = showTemperature;
        factory.WidgetManager?.StoreSetting(this, "OneByOneShowsTemperature", showTemperature.ToString());
        RequestUpdate();
    }

    public void SetFiveByFourGaugeMode(FiveByFourGaugeMode mode)
    {
        fiveByFourGaugeMode = mode;
        factory.WidgetManager?.StoreSetting(this, "FiveByFourGaugeMode", mode.ToString());
        RequestUpdate();
    }

    public void SetTwoByThreeLayoutMode(TwoByThreeLayoutMode mode)
    {
        twoByThreeLayoutMode = mode;
        factory.WidgetManager?.StoreSetting(this, "TwoByThreeLayoutMode", mode.ToString());
        RequestUpdate();
    }

    public void SetRamGaugeAlignment(MemoryGaugeAlignment alignment)
    {
        ramGaugeAlignment = alignment;
        factory.WidgetManager?.StoreSetting(this, "RamGaugeAlignment", alignment.ToString());
        RequestUpdate();
    }

    public void SetVramGaugeAlignment(MemoryGaugeAlignment alignment)
    {
        vramGaugeAlignment = alignment;
        factory.WidgetManager?.StoreSetting(this, "VramGaugeAlignment", alignment.ToString());
        RequestUpdate();
    }

    private void StoreSharedSetting(string key, string value)
    {
        HwinfoPanelWidget[] widgets;
        lock (sharedGaugeSettingsLock)
            widgets = new List<HwinfoPanelWidget>(sharedGaugeWidgets) { this }.ToArray();

        foreach (HwinfoPanelWidget widget in widgets)
            widget.factory.WidgetManager?.StoreSetting(widget, key, value);
    }

    public void SetUpdateInterval(int milliseconds)
    {
        updateIntervalMilliseconds = milliseconds;
        factory.WidgetManager?.StoreSetting(this, "UpdateInterval", milliseconds.ToString());
        RequestUpdate();
    }

    public void SetUploadNetworkScale(double megabytesPerSecond)
    {
        uploadNetworkScaleMegabytesPerSecond = Math.Max(100, Math.Min(100000, megabytesPerSecond));
        factory.WidgetManager?.StoreSetting(this, "UploadNetworkScaleMegabytesPerSecond", uploadNetworkScaleMegabytesPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture));
        RequestUpdate();
    }

    public void SetDownloadNetworkScale(double megabytesPerSecond)
    {
        downloadNetworkScaleMegabytesPerSecond = Math.Max(100, Math.Min(100000, megabytesPerSecond));
        factory.WidgetManager?.StoreSetting(this, "DownloadNetworkScaleMegabytesPerSecond", downloadNetworkScaleMegabytesPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture));
        RequestUpdate();
    }

    public void SetPanelTarget(PanelTarget target)
    {
        panelTarget = target;
        factory.WidgetManager?.StoreSetting(this, "PanelTarget", target.ToString());
        RequestUpdate();
    }

    public void SetTimeZone(string id)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(id);
            timeZoneId = id;
            factory.WidgetManager?.StoreSetting(this, "TimeZoneId", id);
            RequestUpdate();
        }
        catch (TimeZoneNotFoundException)
        {
        }
    }

    public void SetHeaderTouchAction(HeaderTouchAction action)
    {
        headerTouchAction = action;
        factory.WidgetManager?.StoreSetting(this, "HeaderTouchAction", action.ToString());
    }

    public void SetStartPage(PanelPage page)
    {
        startPage = page;
        currentPage = page;
        factory.WidgetManager?.StoreSetting(this, "StartPage", page.ToString());
        UpdateNow();
    }

    public void SetDiscordStatusUrl(string url)
    {
        discordStatusUrl = string.IsNullOrWhiteSpace(url) ? "http://127.0.0.1:47900/status" : url.Trim();
        factory.WidgetManager?.StoreSetting(this, "DiscordStatusUrl", discordStatusUrl);
        discordStopEvent.Set();
        RequestUpdate();
    }

    public void SetDiscordLaunchUrl(string url)
    {
        discordLaunchUrl = string.IsNullOrWhiteSpace(url) ? "discord://-/" : url.Trim();
        factory.WidgetManager?.StoreSetting(this, "DiscordLaunchUrl", discordLaunchUrl);
        RequestUpdate();
    }

    public void SetDiscordApiKey(string apiKey)
    {
        discordApiKey = apiKey?.Trim() ?? string.Empty;
        factory.WidgetManager?.StoreSetting(this, "DiscordApiKey", discordApiKey);
        discordStopEvent.Set();
        RequestUpdate();
    }

    public void SetHomeTileType(int index, HomeTileType tileType)
    {
        if (index < 0 || index >= homeTiles.Length)
            return;

        homeTiles[index] = tileType;
        factory.WidgetManager?.StoreSetting(this, $"HomeTile{index + 1}", tileType.ToString());
        RequestUpdate();
    }

    public void SetHomeTileAction(int index, Guid? actionId)
    {
        if (index < 0 || index >= homeTileActions.Length)
            return;

        homeTileActions[index] = actionId;
        factory.WidgetManager?.StoreSetting(this, $"HomeTileAction{index + 1}", actionId?.ToString() ?? string.Empty);
        RequestUpdate();
    }

    public void SetHomeTileLink(int index, string link)
    {
        if (index < 0 || index >= homeTileLinks.Length)
            return;

        homeTileLinks[index] = link?.Trim() ?? string.Empty;
        factory.WidgetManager?.StoreSetting(this, $"HomeTileLink{index + 1}", homeTileLinks[index]);
        RequestUpdate();
    }

    public void SetHomeTileLabel(int index, string label)
    {
        if (index < 0 || index >= homeTileLabels.Length)
            return;

        homeTileLabels[index] = label?.Trim() ?? string.Empty;
        factory.WidgetManager?.StoreSetting(this, $"HomeTileLabel{index + 1}", homeTileLabels[index]);
        RequestUpdate();
    }

    public void SetHomeTileBackground(int index, string path)
    {
        if (index < 0 || index >= homeTileBackgrounds.Length)
            return;

        homeTileBackgrounds[index] = path?.Trim() ?? string.Empty;
        factory.WidgetManager?.StoreSetting(this, $"HomeTileBackground{index + 1}", homeTileBackgrounds[index]);
        RequestUpdate();
    }

    public void SetHomeButtonTarget(int index, HomeButtonTarget target)
    {
        if (index < 0 || index >= homeButtons.Length)
            return;

        homeButtons[index] = target;
        factory.WidgetManager?.StoreSetting(this, $"HomeButton{index + 1}", target.ToString());
        RequestUpdate();
    }

    public void SetHomeButtonLabel(int index, string label)
    {
        if (index < 0 || index >= homeButtonLabels.Length)
            return;

        string normalizedLabel = string.IsNullOrWhiteSpace(label) ? GetDefaultHomeButtonLabel(homeButtons[index]) : label.Trim();
        homeButtonLabels[index] = normalizedLabel.Length > 18 ? normalizedLabel.Substring(0, 18) : normalizedLabel;
        factory.WidgetManager?.StoreSetting(this, $"HomeButtonLabel{index + 1}", homeButtonLabels[index]);
        RequestUpdate();
    }

    public void SetHeaderExternalAction(Guid? actionId)
    {
        headerExternalActionId = actionId;
        factory.WidgetManager?.StoreSetting(this, "HeaderExternalActionId", actionId?.ToString() ?? string.Empty);
    }

    public void SetTimeFontSize(int size)
    {
        timeFontSize = size < 10 ? 10 : size > 24 ? 24 : size;
        factory.WidgetManager?.StoreSetting(this, "TimeFontSize", timeFontSize.ToString());
        RequestUpdate();
    }

    public void SetTimeColor(Color color)
    {
        timeColor = color;
        factory.WidgetManager?.StoreSetting(this, "TimeColor", ColorTranslator.ToHtml(color));
        RequestUpdate();
    }

    private void ToggleDisplayMode()
    {
        if (WidgetSize.Width == 5 && WidgetSize.Height == 4)
            SetFiveByFourGaugeMode((FiveByFourGaugeMode)(((int)fiveByFourGaugeMode + 1) % 3));
        else
            SetPanelTarget((PanelTarget)(((int)panelTarget + 1) % (WidgetSize.Width == 2 && WidgetSize.Height == 2 ? 4 : 3)));
    }

    public void UpdateNow()
    {
        Draw(sensorSource.Read());
        PublishBitmap();
    }

    public void EnterSleep()
    {
    }

    public void ExitSleep() => PublishBitmap();

    public void Dispose()
    {
        running = false;
        stopEvent.Set();
        discordStopEvent.Set();
        factory.WidgetManager?.UnregisterTrigger(this, headerTouchTriggerId);
        if (drawThread.IsAlive) drawThread.Join();
        if (discordThread.IsAlive) discordThread.Join();
        sensorSource.Dispose();
        lock (bitmapLock)
        {
            bitmap.Dispose();
        }
        lock (sharedGaugeSettingsLock)
            sharedGaugeWidgets.Remove(this);
        stopEvent.Dispose();
        discordStopEvent.Dispose();
    }

    private void DrawLoop()
    {
        while (running)
        {
            try
            {
                Draw(sensorSource.Read());
                PublishBitmap();
            }
            catch (Exception)
            {
                if (!running)
                    break;
            }

            if (stopEvent.WaitOne(updateIntervalMilliseconds))
                break;
        }
    }

    private void DiscordLoop()
    {
        while (running)
        {
            UpdateDiscordStatus();
            discordStopEvent.WaitOne(2000);
        }
    }

    private void UpdateDiscordStatus()
    {
        try
        {
            using HttpClient client = new() { Timeout = TimeSpan.FromMilliseconds(750) };
            using HttpRequestMessage request = new(HttpMethod.Get, discordStatusUrl);
            if (!string.IsNullOrWhiteSpace(discordApiKey))
                request.Headers.Add("X-API-Key", discordApiKey);
            string json = client.SendAsync(request).GetAwaiter().GetResult().Content.ReadAsStringAsync().GetAwaiter().GetResult();
            DiscordStatus status = new JavaScriptSerializer().Deserialize<DiscordStatus>(json);
            if (status != null)
            {
                discordStatus = status;
                discordOnlineOffset = 0;
                discordVoiceOffset = 0;
                RequestUpdate();
            }
        }
        catch (Exception)
        {
            discordStatus = new DiscordStatus { Status = "offline" };
            discordOnlineOffset = 0;
            discordVoiceOffset = 0;
        }
    }

    private void LoadSettings()
    {
        if (factory.WidgetManager == null)
            return;

        if (factory.WidgetManager.LoadSetting(this, "AccentColor", out string savedColor))
            accentColor = ColorTranslator.FromHtml(savedColor);

        lock (sharedGaugeSettingsLock)
        {
            if (!sharedGaugeSettingsLoaded)
            {
                if (factory.WidgetManager.LoadSetting(this, "GaugeLowColor", out string savedGaugeLowColor))
                    gaugeLowColor = ColorTranslator.FromHtml(savedGaugeLowColor);

                if (factory.WidgetManager.LoadSetting(this, "GaugeMediumColor", out string savedGaugeMediumColor))
                    gaugeMediumColor = ColorTranslator.FromHtml(savedGaugeMediumColor);

                if (factory.WidgetManager.LoadSetting(this, "GaugeHighColor", out string savedGaugeHighColor))
                    gaugeHighColor = ColorTranslator.FromHtml(savedGaugeHighColor);

                if (factory.WidgetManager.LoadSetting(this, "GaugeWarningThreshold", out string savedGaugeWarningThreshold) &&
                    int.TryParse(savedGaugeWarningThreshold, out int warningThreshold))
                    gaugeWarningThreshold = Math.Max(1, Math.Min(98, warningThreshold));

                if (factory.WidgetManager.LoadSetting(this, "GaugeCriticalThreshold", out string savedGaugeCriticalThreshold) &&
                    int.TryParse(savedGaugeCriticalThreshold, out int criticalThreshold))
                    gaugeCriticalThreshold = Math.Max(gaugeWarningThreshold + 1, Math.Min(99, criticalThreshold));

                if (factory.WidgetManager.LoadSetting(this, "TemperatureLowColor", out string savedTemperatureLowColor))
                    temperatureLowColor = ColorTranslator.FromHtml(savedTemperatureLowColor);

                if (factory.WidgetManager.LoadSetting(this, "TemperatureMediumColor", out string savedTemperatureMediumColor))
                    temperatureMediumColor = ColorTranslator.FromHtml(savedTemperatureMediumColor);

                if (factory.WidgetManager.LoadSetting(this, "TemperatureHighColor", out string savedTemperatureHighColor))
                    temperatureHighColor = ColorTranslator.FromHtml(savedTemperatureHighColor);

                if (factory.WidgetManager.LoadSetting(this, "TemperatureWarningThreshold", out string savedTemperatureWarningThreshold) &&
                    int.TryParse(savedTemperatureWarningThreshold, out int temperatureWarning))
                    temperatureWarningThreshold = Math.Max(1, Math.Min(149, temperatureWarning));

                if (factory.WidgetManager.LoadSetting(this, "TemperatureCriticalThreshold", out string savedTemperatureCriticalThreshold) &&
                    int.TryParse(savedTemperatureCriticalThreshold, out int temperatureCritical))
                    temperatureCriticalThreshold = Math.Max(temperatureWarningThreshold + 1, Math.Min(150, temperatureCritical));

                sharedGaugeLowColor = gaugeLowColor;
                sharedGaugeMediumColor = gaugeMediumColor;
                sharedGaugeHighColor = gaugeHighColor;
                sharedGaugeWarningThreshold = gaugeWarningThreshold;
                sharedGaugeCriticalThreshold = gaugeCriticalThreshold;
                sharedTemperatureLowColor = temperatureLowColor;
                sharedTemperatureMediumColor = temperatureMediumColor;
                sharedTemperatureHighColor = temperatureHighColor;
                sharedTemperatureWarningThreshold = temperatureWarningThreshold;
                sharedTemperatureCriticalThreshold = temperatureCriticalThreshold;
                sharedGaugeSettingsLoaded = true;
            }
            else
            {
                gaugeLowColor = sharedGaugeLowColor;
                gaugeMediumColor = sharedGaugeMediumColor;
                gaugeHighColor = sharedGaugeHighColor;
                gaugeWarningThreshold = sharedGaugeWarningThreshold;
                gaugeCriticalThreshold = sharedGaugeCriticalThreshold;
                temperatureLowColor = sharedTemperatureLowColor;
                temperatureMediumColor = sharedTemperatureMediumColor;
                temperatureHighColor = sharedTemperatureHighColor;
                temperatureWarningThreshold = sharedTemperatureWarningThreshold;
                temperatureCriticalThreshold = sharedTemperatureCriticalThreshold;
            }
        }

        if (factory.WidgetManager.LoadSetting(this, "UpdateInterval", out string savedInterval) &&
            int.TryParse(savedInterval, out int interval))
            updateIntervalMilliseconds = interval < 100 ? 100 : interval > 2000 ? 2000 : interval;

        double legacyNetworkScale = 3000;
        double parsedLegacyNetworkScale = 3000;
        bool hasLegacyNetworkScale = factory.WidgetManager.LoadSetting(this, "NetworkScaleMegabytesPerSecond", out string savedNetworkScale) &&
            double.TryParse(savedNetworkScale, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsedLegacyNetworkScale);
        if (hasLegacyNetworkScale)
            legacyNetworkScale = parsedLegacyNetworkScale;
        if (factory.WidgetManager.LoadSetting(this, "UploadNetworkScaleMegabytesPerSecond", out string savedUploadScale) &&
            double.TryParse(savedUploadScale, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double uploadNetworkScale))
            uploadNetworkScaleMegabytesPerSecond = Math.Max(100, Math.Min(100000, uploadNetworkScale));
        else if (hasLegacyNetworkScale)
            uploadNetworkScaleMegabytesPerSecond = Math.Max(100, Math.Min(100000, legacyNetworkScale));
        if (factory.WidgetManager.LoadSetting(this, "DownloadNetworkScaleMegabytesPerSecond", out string savedDownloadScale) &&
            double.TryParse(savedDownloadScale, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double downloadNetworkScale))
            downloadNetworkScaleMegabytesPerSecond = Math.Max(100, Math.Min(100000, downloadNetworkScale));
        else if (hasLegacyNetworkScale)
            downloadNetworkScaleMegabytesPerSecond = Math.Max(100, Math.Min(100000, legacyNetworkScale));

        if (factory.WidgetManager.LoadSetting(this, "PanelTarget", out string savedTarget) &&
            Enum.TryParse(savedTarget, out PanelTarget target))
            panelTarget = target;

        if (factory.WidgetManager.LoadSetting(this, "HeaderTouchAction", out string savedHeaderTouchAction) &&
            Enum.TryParse(savedHeaderTouchAction, out HeaderTouchAction headerAction))
            headerTouchAction = headerAction;

        if (factory.WidgetManager.LoadSetting(this, "HeaderExternalActionId", out string savedHeaderExternalActionId) &&
            Guid.TryParse(savedHeaderExternalActionId, out Guid externalActionId))
            headerExternalActionId = externalActionId;

        if (factory.WidgetManager.LoadSetting(this, "StartPage", out string savedStartPage) &&
            Enum.TryParse(savedStartPage, out PanelPage savedPage))
            startPage = savedPage;

        if (factory.WidgetManager.LoadSetting(this, "DiscordStatusUrl", out string savedDiscordStatusUrl) &&
            !string.IsNullOrWhiteSpace(savedDiscordStatusUrl))
            discordStatusUrl = savedDiscordStatusUrl.Trim();

        if (factory.WidgetManager.LoadSetting(this, "DiscordLaunchUrl", out string savedDiscordLaunchUrl) &&
            !string.IsNullOrWhiteSpace(savedDiscordLaunchUrl))
            discordLaunchUrl = savedDiscordLaunchUrl.Trim();

        if (factory.WidgetManager.LoadSetting(this, "DiscordApiKey", out string savedDiscordApiKey))
            discordApiKey = savedDiscordApiKey.Trim();

        for (int index = 0; index < homeTiles.Length; index++)
        {
            if (factory.WidgetManager.LoadSetting(this, $"HomeTile{index + 1}", out string savedHomeTile) &&
                Enum.TryParse(savedHomeTile, out HomeTileType homeTile))
                homeTiles[index] = homeTile;

            if (factory.WidgetManager.LoadSetting(this, $"HomeTileAction{index + 1}", out string savedHomeTileAction) &&
                Guid.TryParse(savedHomeTileAction, out Guid homeTileAction))
                homeTileActions[index] = homeTileAction;

            if (factory.WidgetManager.LoadSetting(this, $"HomeTileLink{index + 1}", out string savedHomeTileLink))
                homeTileLinks[index] = savedHomeTileLink;

            if (factory.WidgetManager.LoadSetting(this, $"HomeTileLabel{index + 1}", out string savedHomeTileLabel))
                homeTileLabels[index] = savedHomeTileLabel;

            if (factory.WidgetManager.LoadSetting(this, $"HomeTileBackground{index + 1}", out string savedHomeTileBackground))
                homeTileBackgrounds[index] = savedHomeTileBackground;
        }

            for (int index = 0; index < homeButtons.Length; index++)
            {
                if (factory.WidgetManager.LoadSetting(this, $"HomeButton{index + 1}", out string savedHomeButton) &&
                Enum.TryParse(savedHomeButton, out HomeButtonTarget homeButton))
                homeButtons[index] = homeButton;

                if (factory.WidgetManager.LoadSetting(this, $"HomeButtonLabel{index + 1}", out string savedHomeButtonLabel) &&
                    !string.IsNullOrWhiteSpace(savedHomeButtonLabel))
                    homeButtonLabels[index] = savedHomeButtonLabel.Trim();
            }

        if (factory.WidgetManager.LoadSetting(this, "TimeFontSize", out string savedTimeFontSize) &&
            int.TryParse(savedTimeFontSize, out int fontSize))
            timeFontSize = fontSize < 10 ? 10 : fontSize > 24 ? 24 : fontSize;

        if (factory.WidgetManager.LoadSetting(this, "TimeColor", out string savedTimeColor))
        {
            try
            {
                timeColor = ColorTranslator.FromHtml(savedTimeColor);
            }
            catch (Exception)
            {
            }
        }

        if (factory.WidgetManager.LoadSetting(this, "TimeZoneId", out string savedTimeZoneId))
        {
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(savedTimeZoneId);
                timeZoneId = savedTimeZoneId;
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }

        if (factory.WidgetManager.LoadSetting(this, "OneByOneShowsTemperature", out string savedOneByOneShowsTemperature) &&
            bool.TryParse(savedOneByOneShowsTemperature, out bool showsTemperature))
            oneByOneShowsTemperature = showsTemperature;

        if (factory.WidgetManager.LoadSetting(this, "FiveByFourGaugeMode", out string savedFiveByFourGaugeMode) &&
            Enum.TryParse(savedFiveByFourGaugeMode, out FiveByFourGaugeMode gaugeMode))
            fiveByFourGaugeMode = gaugeMode;
        else if (factory.WidgetManager.LoadSetting(this, "FiveByFourShowsTemperature", out string savedFiveByFourShowsTemperature) &&
                 bool.TryParse(savedFiveByFourShowsTemperature, out bool fiveByFourShowsTemperatureValue))
            fiveByFourGaugeMode = fiveByFourShowsTemperatureValue ? FiveByFourGaugeMode.Temperature : FiveByFourGaugeMode.Load;

        if (factory.WidgetManager.LoadSetting(this, "TwoByThreeLayoutMode", out string savedTwoByThreeLayoutMode) &&
            Enum.TryParse(savedTwoByThreeLayoutMode, out TwoByThreeLayoutMode twoByThreeMode))
            twoByThreeLayoutMode = twoByThreeMode;

        if (factory.WidgetManager.LoadSetting(this, "RamGaugeAlignment", out string savedRamGaugeAlignment) &&
            Enum.TryParse(savedRamGaugeAlignment, out MemoryGaugeAlignment ramAlignment))
            ramGaugeAlignment = ramAlignment;

        if (factory.WidgetManager.LoadSetting(this, "VramGaugeAlignment", out string savedVramGaugeAlignment) &&
            Enum.TryParse(savedVramGaugeAlignment, out MemoryGaugeAlignment vramAlignment))
            vramGaugeAlignment = vramAlignment;
        else if (factory.WidgetManager.LoadSetting(this, "MemoryGaugeAlignment", out string savedMemoryGaugeAlignment) &&
                 Enum.TryParse(savedMemoryGaugeAlignment, out MemoryGaugeAlignment legacyAlignment))
        {
            ramGaugeAlignment = legacyAlignment;
            vramGaugeAlignment = legacyAlignment;
        }
    }

    private void LoadSensorBindings()
    {
        if (!(sensorSource is ManagerSensorSource managerSource) || factory.WidgetManager == null)
            return;

        foreach (SensorSlot slot in Enum.GetValues(typeof(SensorSlot)))
        {
            if (factory.WidgetManager.LoadSetting(this, $"Sensor.{slot}", out string savedGuid) &&
                Guid.TryParse(savedGuid, out Guid sensorGuid) &&
                managerSource.IsCompatible(slot, sensorGuid))
                managerSource.Bind(slot, sensorGuid);
        }
    }

    private void Draw(SensorSnapshot data)
    {
        lock (drawLock)
        {
            Bitmap next = new(bitmapWidth, bitmapHeight);
            using (Graphics graphics = Graphics.FromImage(next))
            using (Font titleFont = new("Segoe UI", 15, FontStyle.Bold))
            using (Font valueFont = new("Segoe UI", 22, FontStyle.Bold))
            using (Font detailFont = new("Segoe UI", 11))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                bool compact = WidgetSize.Width <= 2 && WidgetSize.Height <= 2;
                bool singleRow = WidgetSize.Width >= 3 && WidgetSize.Height == 1;
                graphics.Clear(Color.FromArgb(12, 14, 18));

                if (singleRow)
                    DrawFiveByOnePanel(graphics, next.Width, next.Height, data, WidgetSize.Width != 3);
                else if (compact)
                    DrawCompactPanel(graphics, next.Width, next.Height, data, titleFont, valueFont, detailFont);
                else if (WidgetSize.Width == 2 && WidgetSize.Height == 3)
                    DrawTwoByThreePanel(graphics, next.Width, next.Height, data);
                else
                {
                    float referenceHeight = LayoutReferenceHeight;
                    graphics.ScaleTransform(next.Width / LayoutReferenceWidth, next.Height / referenceHeight);
                    PanelPage page = SupportsPages ? currentPage : PanelPage.Hardware;
                    List<HitTarget> targets = new();
                    bool compactThreeByThree = WidgetSize.Width == 3 && WidgetSize.Height == 3;
                    if (!compactThreeByThree)
                        DrawHeader(graphics, titleFont, detailFont, data, accentColor, SupportsPages && page != PanelPage.Home, targets);

                    switch (page)
                    {
                        case PanelPage.Home:
                            DrawHomePage(graphics, referenceHeight, data, titleFont, detailFont, targets);
                            break;
                        default:
                            DrawHardwarePage(graphics, data, titleFont, valueFont, detailFont);
                            break;
                    }

                    hitTargets = targets;
                }
            }

            lock (bitmapLock)
            {
                Bitmap old = bitmap;
                bitmap = next;
                old.Dispose();
            }
        }
    }

    private void DrawHardwarePage(Graphics graphics, SensorSnapshot data, Font titleFont, Font valueFont, Font detailFont)
    {
        int margin = 12;
        int gap = 12;
        bool isThreeByThree = WidgetSize.Width == 3 && WidgetSize.Height == 3;
        bool isFourByThree = WidgetSize.Width == 4 && WidgetSize.Height == 3;
        int top = isThreeByThree ? 12 : 72;
        int largeWidth = ((int)LayoutReferenceWidth - margin * 2 - gap) / 2;
        int largeHeight = isThreeByThree ? 220 : 220;
        if (isThreeByThree && panelTarget == PanelTarget.Cpu)
        {
            DrawCoreCard(graphics, new Rectangle(margin, top, (int)LayoutReferenceWidth - margin * 2, largeHeight), "CPU", data.CpuName, data.CpuLoadPercent, data.CpuTemperatureCelsius, data.CpuClockMhz, data.CpuPowerWatts, data.CpuFanRpm, accentColor, titleFont, valueFont, detailFont);
        }
        else if (isThreeByThree && panelTarget == PanelTarget.Gpu)
        {
            DrawCoreCard(graphics, new Rectangle(margin, top, (int)LayoutReferenceWidth - margin * 2, largeHeight), "GPU", data.GpuName, data.GpuLoadPercent, data.GpuTemperatureCelsius, data.GpuClockMhz, data.GpuPowerWatts, data.GpuFanRpm, accentColor, titleFont, valueFont, detailFont);
        }
        else
        {
            DrawCoreCard(graphics, new Rectangle(margin, top, largeWidth, largeHeight), "CPU", data.CpuName, data.CpuLoadPercent, data.CpuTemperatureCelsius, data.CpuClockMhz, data.CpuPowerWatts, data.CpuFanRpm, accentColor, titleFont, valueFont, detailFont);
            DrawCoreCard(graphics, new Rectangle(margin + largeWidth + gap, top, largeWidth, largeHeight), "GPU", data.GpuName, data.GpuLoadPercent, data.GpuTemperatureCelsius, data.GpuClockMhz, data.GpuPowerWatts, data.GpuFanRpm, accentColor, titleFont, valueFont, detailFont);
        }

        int bottomTop = top + largeHeight + gap;
        int smallWidth = ((int)LayoutReferenceWidth - margin * 2 - gap * 2) / 3;
        int smallHeight = isThreeByThree ? 190 : 135;
        int ramWidth = isThreeByThree ? smallWidth - 20 : smallWidth;
        int networkWidth = isThreeByThree ? smallWidth + 40 : smallWidth;
        int vramWidth = isThreeByThree ? smallWidth - 20 : smallWidth;
        int networkX = margin + ramWidth + gap;
        int vramX = networkX + networkWidth + gap;
        if (WidgetSize.Width == 5 && WidgetSize.Height >= 4)
        {
            int infoTop = bottomTop + smallHeight + gap;
            int storageHeight = smallHeight * 2 + gap;
            int storageX = margin + smallWidth + gap;
            int rightX = margin + (smallWidth + gap) * 2;
            DrawMemoryCard(graphics, new Rectangle(margin, bottomTop, smallWidth, smallHeight), data, accentColor, titleFont, valueFont, detailFont, ramGaugeAlignment, false, false);
            DrawDiskCard(graphics, new Rectangle(storageX, bottomTop, smallWidth, storageHeight), data, accentColor, titleFont, detailFont);
            DrawVramCard(graphics, new Rectangle(rightX, bottomTop, smallWidth, smallHeight), data, accentColor, titleFont, valueFont, detailFont, vramGaugeAlignment, false, false);
            DrawNetworkCard(graphics, new Rectangle(margin, infoTop, smallWidth, smallHeight), data, accentColor, titleFont, detailFont, uploadNetworkScaleMegabytesPerSecond, downloadNetworkScaleMegabytesPerSecond, false);
            DrawFanCard(graphics, new Rectangle(rightX, infoTop, smallWidth, smallHeight), data, accentColor, titleFont, detailFont);
        }
        else if (WidgetSize.Width >= 5 && WidgetSize.Height >= 4)
        {
            int infoTop = bottomTop + smallHeight + gap;
            DrawDiskCard(graphics, new Rectangle(margin, infoTop, smallWidth, 120), data, accentColor, titleFont, detailFont);
            DrawFanCard(graphics, new Rectangle(margin + smallWidth + gap, infoTop, smallWidth, 120), data, accentColor, titleFont, detailFont);
            DrawFpsCard(graphics, new Rectangle(margin + (smallWidth + gap) * 2, infoTop, smallWidth, 120), data, accentColor, titleFont, detailFont);
        }
        else
        {
            DrawMemoryCard(graphics, new Rectangle(margin, bottomTop, ramWidth, smallHeight), data, accentColor, titleFont, valueFont, detailFont, ramGaugeAlignment, isThreeByThree, isFourByThree);
            DrawNetworkCard(graphics, new Rectangle(networkX, bottomTop, networkWidth, smallHeight), data, accentColor, titleFont, detailFont, uploadNetworkScaleMegabytesPerSecond, downloadNetworkScaleMegabytesPerSecond, isFourByThree);
            DrawVramCard(graphics, new Rectangle(vramX, bottomTop, vramWidth, smallHeight), data, accentColor, titleFont, valueFont, detailFont, vramGaugeAlignment, isThreeByThree, isFourByThree);
        }
    }

    private void DrawHomePage(Graphics graphics, float referenceHeight, SensorSnapshot data, Font titleFont, Font detailFont, List<HitTarget> targets)
    {
        DrawHomeDashboard(graphics, referenceHeight, data, titleFont, detailFont, targets);
    }

    private void DrawHomeDashboard(Graphics graphics, float referenceHeight, SensorSnapshot data, Font titleFont, Font detailFont, List<HitTarget> targets)
    {
        if (WidgetSize.Width >= 4 && WidgetSize.Height >= 3)
        {
            DrawLargeHomeDashboard(graphics, referenceHeight, data, titleFont, detailFont, targets);
            return;
        }

        const int margin = 12;
        const int gap = 12;
        int top = WidgetSize.Width == 3 && WidgetSize.Height == 3 ? 12 : 72;
        int tileWidth = ((int)LayoutReferenceWidth - margin * 2 - gap) / 2;
        int tileHeight = ((int)referenceHeight - top - margin - gap) / 2;

        for (int index = 0; index < homeTiles.Length; index++)
        {
            int column = index % 2;
            int row = index / 2;
            Rectangle bounds = new(margin + column * (tileWidth + gap), top + row * (tileHeight + gap), tileWidth, tileHeight);
            DrawHomeTile(graphics, bounds, homeTiles[index], index, data, titleFont, detailFont);
            if (homeTiles[index] == HomeTileType.CustomAction && homeTileActions[index].HasValue)
            {
                Guid actionId = homeTileActions[index].Value;
                targets.Add(new HitTarget(bounds, () => factory.WidgetManager?.OnTriggerOccurred(actionId)));
            }
            else if (homeTiles[index] == HomeTileType.WebLink && !string.IsNullOrWhiteSpace(homeTileLinks[index]))
            {
                string link = homeTileLinks[index];
                targets.Add(new HitTarget(bounds, () => OpenWebLink(link)));
            }
        }
    }

    private void DrawLargeHomeDashboard(Graphics graphics, float referenceHeight, SensorSnapshot data, Font titleFont, Font detailFont, List<HitTarget> targets)
    {
        const int margin = 12;
        const int gap = 12;
        const int sidebarWidth = 170;
        int top = 72;
        int sidebarX = (int)LayoutReferenceWidth - margin - sidebarWidth;
        int windowWidth = sidebarX - gap - margin;
        int windowHeight = (int)referenceHeight - top - margin;
        Rectangle window = new(margin, top, windowWidth, windowHeight);

        DrawCardFrame(graphics, window, accentColor);
        DrawHomeWindow(graphics, window, data, titleFont, detailFont, targets);

        for (int index = 0; index < homeButtons.Length; index++)
        {
            Rectangle button = new(sidebarX, top + index * 58, sidebarWidth, 46);
            HomeButtonTarget target = homeButtons[index];
            bool selected = target != HomeButtonTarget.Hardware && homeView == (int)target;
            using Brush background = new SolidBrush(selected ? Color.FromArgb(32, 38, 48) : Color.FromArgb(18, 21, 27));
            using Pen border = new(selected ? accentColor : Color.FromArgb(75, 82, 94), selected ? 2 : 1);
            using Font buttonFont = new("Segoe UI", 11, FontStyle.Bold);
            using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.FillRectangle(background, button);
            graphics.DrawRectangle(border, button);
            graphics.DrawString(GetHomeButtonLabel(index, target), buttonFont, Brushes.White, button, centered);

            Rectangle touchArea = button;
            touchArea.Inflate(6, 6);
            HomeButtonTarget selectedTarget = target;
            targets.Add(new HitTarget(touchArea, () =>
            {
                if (selectedTarget == HomeButtonTarget.Hardware)
                    NavigateTo(PanelPage.Hardware);
                else if (selectedTarget != HomeButtonTarget.Empty)
                    SetHomeView((int)selectedTarget);
            }));
        }
    }

    private string GetHomeButtonLabel(int index, HomeButtonTarget target)
    {
        string label = index >= 0 && index < homeButtonLabels.Length ? homeButtonLabels[index] : string.Empty;
        return string.IsNullOrWhiteSpace(label) ? GetDefaultHomeButtonLabel(target) : label;
    }

    private static string GetDefaultHomeButtonLabel(HomeButtonTarget target)
    {
        return target switch
        {
            HomeButtonTarget.Home => "HOME",
            HomeButtonTarget.Hardware => "CPU / GPU",
            HomeButtonTarget.MemoryNetwork => "RAM / NET",
            HomeButtonTarget.Actions => "AKTIONEN",
            HomeButtonTarget.Info => "INFO",
            HomeButtonTarget.Discord => "DISCORD",
            _ => "LEER"
        };
    }

    private void SetHomeView(int view)
    {
        homeView = view < 0 ? 0 : view > 5 ? 5 : view;
        RequestUpdate();
    }

    private void DrawHomeWindow(Graphics graphics, Rectangle window, SensorSnapshot data, Font titleFont, Font detailFont, List<HitTarget> targets)
    {
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        using Brush accentBrush = new SolidBrush(accentColor);
        using Font windowTitleFont = new("Segoe UI", 17, FontStyle.Bold);
        graphics.DrawString(homeView switch
        {
            1 => "CPU / GPU",
            2 => "RAM / NET",
            3 => "AKTIONEN",
            4 => "INFO",
            5 => "DISCORD",
            _ => "HOME"
        }, windowTitleFont, white, window.X + 20, window.Y + 16);

        Rectangle content = new(window.X + 18, window.Y + 54, window.Width - 36, window.Height - 72);
        if (homeView == 1)
        {
            int cardWidth = (content.Width - 12) / 2;
            DrawCoreCard(graphics, new Rectangle(content.X, content.Y, cardWidth, 220), "CPU", data.CpuName, data.CpuLoadPercent, data.CpuTemperatureCelsius, data.CpuClockMhz, data.CpuPowerWatts, data.CpuFanRpm, accentColor, titleFont, new Font("Segoe UI", 22, FontStyle.Bold), detailFont);
            DrawCoreCard(graphics, new Rectangle(content.X + cardWidth + 12, content.Y, cardWidth, 220), "GPU", data.GpuName, data.GpuLoadPercent, data.GpuTemperatureCelsius, data.GpuClockMhz, data.GpuPowerWatts, data.GpuFanRpm, accentColor, titleFont, new Font("Segoe UI", 22, FontStyle.Bold), detailFont);
        }
        else if (homeView == 2)
        {
            int cardWidth = (content.Width - 24) / 3;
            DrawMemoryCard(graphics, new Rectangle(content.X, content.Y, cardWidth, content.Height), data, accentColor, titleFont, new Font("Segoe UI", 22, FontStyle.Bold), detailFont, ramGaugeAlignment, false, false);
            DrawNetworkCard(graphics, new Rectangle(content.X + cardWidth + 12, content.Y, cardWidth, content.Height), data, accentColor, titleFont, detailFont, uploadNetworkScaleMegabytesPerSecond, downloadNetworkScaleMegabytesPerSecond, false);
            DrawVramCard(graphics, new Rectangle(content.X + (cardWidth + 12) * 2, content.Y, cardWidth, content.Height), data, accentColor, titleFont, new Font("Segoe UI", 22, FontStyle.Bold), detailFont, vramGaugeAlignment, false, false);
        }
        else if (homeView == 3)
        {
            int tileWidth = (content.Width - 12) / 2;
            int tileHeight = (content.Height - 12) / 2;
            for (int index = 0; index < homeTiles.Length; index++)
            {
                int column = index % 2;
                int row = index / 2;
                Rectangle tile = new(content.X + column * (tileWidth + 12), content.Y + row * (tileHeight + 12), tileWidth, tileHeight);
                DrawHomeTile(graphics, tile, homeTiles[index], index, data, titleFont, detailFont);
                if (homeTiles[index] == HomeTileType.CustomAction && homeTileActions[index].HasValue)
                {
                    Guid actionId = homeTileActions[index].Value;
                    targets.Add(new HitTarget(tile, () => factory.WidgetManager?.OnTriggerOccurred(actionId)));
                }
                else if (homeTiles[index] == HomeTileType.WebLink && !string.IsNullOrWhiteSpace(homeTileLinks[index]))
                {
                    string link = homeTileLinks[index];
                    targets.Add(new HitTarget(tile, () => OpenWebLink(link)));
                }
            }
        }
        else if (homeView == 4)
        {
            graphics.DrawString("HWiNFO SENSOR PANEL", titleFont, white, content.X, content.Y + 20);
            graphics.DrawString("HOME", detailFont, accentBrush, content.X, content.Y + 58);
            graphics.DrawString("CPU, GPU, RAM, VRAM, Netzwerk und externe Aktionen", detailFont, muted, content.X, content.Y + 88);
            graphics.DrawString("Version 1.0.0 · by ReXx09", detailFont, muted, content.X, content.Y + 116);
        }
        else if (homeView == 5)
        {
            DrawDiscordPanel(graphics, content, titleFont, detailFont, targets);
        }
        else
        {
            int tileWidth = (content.Width - 12) / 2;
            int tileHeight = (content.Height - 12) / 2;
            for (int index = 0; index < homeTiles.Length; index++)
            {
                int column = index % 2;
                int row = index / 2;
                Rectangle tile = new(content.X + column * (tileWidth + 12), content.Y + row * (tileHeight + 12), tileWidth, tileHeight);
                DrawHomeTile(graphics, tile, homeTiles[index], index, data, titleFont, detailFont);
            }
        }
    }

    private void DrawDiscordPanel(Graphics graphics, Rectangle content, Font titleFont, Font detailFont, List<HitTarget> targets)
    {
        using Brush white = new SolidBrush(Color.White);
        using Brush statusBrush = new SolidBrush(GetDiscordStatusColor(discordStatus.Status));
        using Brush accentBrush = new SolidBrush(accentColor);
        Rectangle statusCard = new(content.X, content.Y, content.Width, 72);
        DrawCardFrame(graphics, statusCard, accentColor);
        graphics.FillEllipse(statusBrush, statusCard.X + 16, statusCard.Y + 17, 16, 16);
        graphics.DrawString(string.IsNullOrWhiteSpace(discordStatus.Guild) ? "DISCORD" : NormalizeDiscordText(discordStatus.Guild), titleFont, white, statusCard.X + 44, statusCard.Y + 10);
        graphics.DrawString($"{GetDiscordParticipants().Count} ONLINE", detailFont, accentBrush, statusCard.X + 44, statusCard.Y + 39);

        int listTop = statusCard.Bottom + 12;
        int listHeight = content.Bottom - listTop;
        int listWidth = (content.Width - 12) / 2;
        Rectangle onlineBounds = new(content.X, listTop, listWidth, listHeight);
        Rectangle voiceBounds = new(content.X + listWidth + 12, listTop, listWidth, listHeight);
        DrawDiscordParticipantList(graphics, onlineBounds, "ONLINE", GetDiscordParticipants(), targets, false, titleFont, detailFont);
        DrawDiscordParticipantList(graphics, voiceBounds, "VOICE", GetDiscordVoiceParticipants(), targets, true, titleFont, detailFont);
    }

    private List<DiscordParticipant> GetDiscordParticipants()
    {
        List<DiscordParticipant> participants = GetDiscordRawParticipants();
        return participants.FindAll(participant => !string.Equals(participant.Status, "offline", StringComparison.OrdinalIgnoreCase));
    }

    private List<DiscordParticipant> GetDiscordRawParticipants()
    {
        if (discordStatus.Participants != null && discordStatus.Participants.Count > 0)
            return discordStatus.Participants;

        if (string.IsNullOrWhiteSpace(discordStatus.Username))
            return new List<DiscordParticipant>();

        return new List<DiscordParticipant>
        {
            new()
            {
                Username = discordStatus.Username,
                Status = discordStatus.Status,
                Activity = discordStatus.Activity,
                VoiceChannel = discordStatus.VoiceChannel
            }
        };
    }

    private List<DiscordParticipant> GetDiscordVoiceParticipants()
    {
        List<DiscordParticipant> participants = GetDiscordRawParticipants();
        return participants.FindAll(participant => !string.IsNullOrWhiteSpace(participant.VoiceChannel));
    }

    private void DrawDiscordParticipantList(Graphics graphics, Rectangle bounds, string title, List<DiscordParticipant> participants, List<HitTarget> targets, bool voiceOnly, Font titleFont, Font detailFont)
    {
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        DrawCardFrame(graphics, bounds, Color.FromArgb(75, 82, 94));
        graphics.DrawString(title, detailFont, muted, bounds.X + 12, bounds.Y + 10);
        int rowHeight = 42;
        int visibleRows = Math.Max(1, (bounds.Height - 58) / rowHeight);
        int offset = voiceOnly ? discordVoiceOffset : discordOnlineOffset;
        int maxOffset = Math.Max(0, participants.Count - visibleRows);
        offset = Math.Min(offset, maxOffset);
        int rowTop = bounds.Y + 34;
        for (int index = 0; index < visibleRows && offset + index < participants.Count; index++)
        {
            DiscordParticipant participant = participants[offset + index];
            Rectangle row = new(bounds.X + 8, rowTop + index * rowHeight, bounds.Width - 16, rowHeight - 4);
            using Brush statusBrush = new SolidBrush(GetDiscordStatusColor(participant.Status));
            graphics.FillEllipse(statusBrush, row.X + 4, row.Y + 10, 12, 12);
            string username = NormalizeDiscordText(string.IsNullOrWhiteSpace(participant.Username) ? "Unbekannt" : participant.Username);
            graphics.DrawString(username, detailFont, Brushes.White, row.X + 24, row.Y + 4);
            string detail = voiceOnly
                ? (participant.Muted ? "MIC AUS" : participant.Deafened ? "TAUB" : "VOICE")
                : NormalizeDiscordText(string.IsNullOrWhiteSpace(participant.Activity) ? participant.Status : participant.Activity);
            using Font rowDetailFont = new("Segoe UI", 9);
            graphics.DrawString(detail, rowDetailFont, Brushes.Gray, row.X + 24, row.Y + 22);
        }

        if (participants.Count == 0)
            graphics.DrawString(voiceOnly ? "Keine Voice-Teilnehmer" : "Keine Online-Teilnehmer", detailFont, Brushes.Gray, bounds.X + 12, rowTop + 12);

        int buttonTop = bounds.Bottom - 28;
        int buttonWidth = (bounds.Width - 24) / 2;
        Rectangle upButton = new(bounds.X + 8, buttonTop, buttonWidth, 22);
        Rectangle downButton = new(bounds.X + 16 + buttonWidth, buttonTop, buttonWidth, 22);
        DrawDiscordButton(graphics, upButton, "^", detailFont);
        DrawDiscordButton(graphics, downButton, "v", detailFont);
        targets.Add(new HitTarget(upButton, () =>
        {
            if (voiceOnly)
                discordVoiceOffset = Math.Max(0, discordVoiceOffset - 1);
            else
                discordOnlineOffset = Math.Max(0, discordOnlineOffset - 1);
            RequestUpdate();
        }));
        targets.Add(new HitTarget(downButton, () =>
        {
            if (voiceOnly)
                discordVoiceOffset = Math.Min(maxOffset, discordVoiceOffset + 1);
            else
                discordOnlineOffset = Math.Min(maxOffset, discordOnlineOffset + 1);
            RequestUpdate();
        }));
    }

    private void DrawDiscordInfoCard(Graphics graphics, Rectangle bounds, string label, string value, Font titleFont, Font detailFont)
    {
        using Brush labelBrush = new SolidBrush(Color.FromArgb(160, 170, 182));
        DrawCardFrame(graphics, bounds, Color.FromArgb(75, 82, 94));
        graphics.DrawString(label, detailFont, labelBrush, bounds.X + 12, bounds.Y + 10);
        graphics.DrawString(value, titleFont, Brushes.White, bounds.X + 12, bounds.Y + 36);
    }

    private static string NormalizeDiscordText(string value)
    {
        string normalized = value.Normalize(System.Text.NormalizationForm.FormKD);
        System.Text.StringBuilder result = new();
        foreach (char character in normalized)
        {
            if (char.IsSurrogate(character) || char.IsControl(character))
                continue;

            result.Append(character switch
            {
                '》' => '>',
                '《' => '<',
                _ => character
            });
        }

        return result.ToString().Trim();
    }

    private void DrawDiscordButton(Graphics graphics, Rectangle bounds, string label, Font titleFont)
    {
        using Brush background = new SolidBrush(Color.FromArgb(24, 29, 38));
        using Pen border = new(accentColor, 1);
        using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.FillRectangle(background, bounds);
        graphics.DrawRectangle(border, bounds);
        graphics.DrawString(label, titleFont, Brushes.White, bounds, centered);
    }

    private static Color GetDiscordStatusColor(string status)
    {
        return status?.ToLowerInvariant() switch
        {
            "online" => Color.FromArgb(60, 200, 110),
            "idle" => Color.FromArgb(235, 190, 45),
            "dnd" => Color.FromArgb(230, 65, 70),
            _ => Color.FromArgb(125, 133, 145)
        };
    }

    private void DrawHomeTile(Graphics graphics, Rectangle bounds, HomeTileType tileType, int tileIndex, SensorSnapshot data, Font titleFont, Font detailFont)
    {
        DrawHomeTileBackground(graphics, bounds, homeTileBackgrounds[tileIndex]);
        DrawCardFrame(graphics, bounds, accentColor);
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        string title;
        string value;
        string detail;

        switch (tileType)
        {
            case HomeTileType.Cpu:
                title = "CPU";
                value = $"{data.CpuLoadPercent:0}%";
                detail = $"{data.CpuTemperatureCelsius:0} °C   {data.CpuClockMhz:0} MHz";
                break;
            case HomeTileType.Gpu:
                title = "GPU";
                value = $"{data.GpuLoadPercent:0}%";
                detail = $"{data.GpuTemperatureCelsius:0} °C   {data.GpuClockMhz:0} MHz";
                break;
            case HomeTileType.Ram:
                title = "RAM";
                value = $"{data.MemoryLoadPercent:0}%";
                detail = $"{data.MemoryUsedGigabytes:0.0} / {data.MemoryTotalGigabytes:0} GB";
                break;
            case HomeTileType.Vram:
                double totalVram = data.GpuMemoryTotalMegabytes > 0 ? data.GpuMemoryTotalMegabytes : 24576;
                title = "VRAM";
                value = $"{data.GpuMemoryMegabytes / totalVram * 100:0}%";
                detail = $"{data.GpuMemoryMegabytes / 1024:0.0} / {totalVram / 1024:0} GB";
                break;
            case HomeTileType.Network:
                title = "NETWORK";
                value = $"{data.NetworkDownloadMegabytesPerSecond:0.0} MB/s";
                detail = $"Up {data.NetworkUploadMegabytesPerSecond:0.0} MB/s";
                break;
            case HomeTileType.Fans:
                title = "FANS";
                value = $"{data.CpuFanRpm:0} RPM";
                detail = $"GPU {data.GpuFanRpm:0} RPM";
                break;
            case HomeTileType.Fps:
                title = "FPS";
                value = $"{data.Fps:0}";
                detail = "Frame rate";
                break;
            case HomeTileType.CustomAction:
                title = "AKTION";
                value = "START";
                detail = GetHomeActionLabel(tileIndex);
                break;
            case HomeTileType.WebLink:
                title = "WEBLINK";
                value = "OPEN";
                detail = ShortenHomeLink(homeTileLinks[tileIndex]);
                break;
            default:
                title = "EMPTY";
                value = "-";
                detail = "Home slot";
                break;
        }

        string customLabel = homeTileLabels[tileIndex];
        if (!string.IsNullOrWhiteSpace(customLabel))
            title = customLabel;
        graphics.DrawString(title, titleFont, white, bounds.X + 18, bounds.Y + 16);
        using Font valueFont = new("Segoe UI", 30, FontStyle.Bold);
        graphics.DrawString(value, valueFont, white, bounds.X + 18, bounds.Y + 62);
        graphics.DrawString(detail, detailFont, muted, bounds.X + 18, bounds.Bottom - 32);
    }

    private static void DrawHomeTileBackground(Graphics graphics, Rectangle bounds, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            using Bitmap background = new(path);
            Rectangle target = new(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4);
            graphics.DrawImage(background, target);
        }
        catch (ArgumentException)
        {
        }
        catch (ExternalException)
        {
        }
    }

    private static string ShortenHomeLink(string link)
    {
        if (string.IsNullOrWhiteSpace(link))
            return "URL nicht konfiguriert";

        return link.Length > 32 ? link.Substring(0, 29) + "..." : link;
    }

    private static void OpenWebLink(string link)
    {
        string normalizedLink = link.Trim();
        if (!normalizedLink.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalizedLink.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            normalizedLink = "http://" + normalizedLink;

        if (!Uri.TryCreate(normalizedLink, UriKind.Absolute, out Uri uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;

        OpenExternalLink(normalizedLink);
    }

    private static void OpenExternalLink(string link)
    {
        if (!Uri.TryCreate(link.Trim(), UriKind.Absolute, out Uri uri))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (InvalidOperationException)
        {
            OpenWebLinkWithExplorer(uri.AbsoluteUri);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            OpenWebLinkWithExplorer(uri.AbsoluteUri);
        }
    }

    private static void OpenWebLinkWithExplorer(string link)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = link,
                UseShellExecute = false
            });
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private string GetHomeActionLabel(int tileIndex)
    {
        if (tileIndex < 0 || tileIndex >= homeTileActions.Length || homeTiles[tileIndex] != HomeTileType.CustomAction)
            return "Home slot";

        if (homeTileActions[tileIndex].HasValue &&
            AvailableExternalActions.TryGetValue(homeTileActions[tileIndex].Value, out string label))
            return label;

        return "Externe Aktion";
    }

    private void DrawHardwareTile(Graphics graphics, Rectangle bounds, SensorSnapshot data, Font titleFont, Font detailFont)
    {
        DrawCardFrame(graphics, bounds, accentColor);
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        int x = bounds.X + 20;
        graphics.DrawString("HARDWARE", titleFont, white, x, bounds.Y + 16);
        graphics.DrawString("CPU \u00b7 GPU \u00b7 RAM \u00b7 VRAM \u00b7 Network", detailFont, muted, x, bounds.Y + 46);

        string[] labels = { "CPU", "GPU", "RAM" };
        string[] values =
        {
            $"{data.CpuLoadPercent:0} %    {data.CpuTemperatureCelsius:0} \u00b0C",
            $"{data.GpuLoadPercent:0} %    {data.GpuTemperatureCelsius:0} \u00b0C",
            $"{data.MemoryLoadPercent:0} %    {data.MemoryUsedGigabytes:0.0} / {data.MemoryTotalGigabytes:0} GB"
        };
        for (int index = 0; index < labels.Length; index++)
        {
            int y = bounds.Y + 86 + index * 26;
            graphics.DrawString(labels[index], detailFont, muted, x, y);
            graphics.DrawString(values[index], detailFont, white, x + 60, y);
        }

        using StringFormat bottomRight = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Far };
        graphics.DrawString("Tap to open  >", detailFont, muted, new RectangleF(bounds.X + 12, bounds.Y + 12, bounds.Width - 24, bounds.Height - 24), bottomRight);
    }

    private void PublishBitmap()
    {
        Bitmap copy;
        lock (bitmapLock)
        {
            copy = new Bitmap(bitmap);
        }

        WidgetUpdated?.Invoke(this, new WidgetUpdatedEventArgs { WidgetBitmap = copy, WaitMax = 250 });
        copy.Dispose();
    }

    private sealed class HitTarget
    {
        public HitTarget(Rectangle bounds, Action onTap)
        {
            Bounds = bounds;
            OnTap = onTap;
        }

        public Rectangle Bounds { get; }
        public Action OnTap { get; }
    }

    private void DrawHeader(Graphics graphics, Font titleFont, Font detailFont, SensorSnapshot data, Color accent, bool showHomeButton, List<HitTarget> targets)
    {
        float referenceWidth = LayoutReferenceWidth;
        using Brush white = new SolidBrush(Color.White);
        using Font timeFont = new("Segoe UI", timeFontSize, FontStyle.Bold);
        using Brush timeBrush = new SolidBrush(timeColor);
        using Pen border = new(accent, 2);
        graphics.DrawRectangle(border, 8, 8, (int)referenceWidth - 16, 54);
        graphics.DrawString("HWiNFO SENSOR PANEL", titleFont, white, 18, 25);
        using Brush headerMuted = new SolidBrush(Color.FromArgb(160, 170, 182));
        graphics.DrawString("v1.0.0", detailFont, headerMuted, 210, 29);
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        string currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).ToString("HH:mm:ss");
        SizeF timeSize = graphics.MeasureString(currentTime, timeFont);
        graphics.DrawString(currentTime, timeFont, timeBrush, (referenceWidth - timeSize.Width) / 2, 25);
        const float rightPadding = 18;
        const float logoSize = 24;
        const float logoTextGap = 6;
        SizeF authorSize = graphics.MeasureString("by ReXx09", detailFont);
        float authorX = referenceWidth - rightPadding - authorSize.Width;
        float logoX = authorX - logoTextGap - logoSize;
        if (logoBitmap != null)
        {
            using Brush logoBackground = new SolidBrush(Color.White);
            graphics.FillEllipse(logoBackground, logoX, 22, logoSize, logoSize);
            graphics.DrawImage(logoBitmap, logoX, 22, logoSize, logoSize);
        }
        using Brush authorBrush = new SolidBrush(accent);
        graphics.DrawString("by ReXx09", detailFont, authorBrush, authorX, 28);

        if (showHomeButton || currentPage == PanelPage.Home)
        {
            Rectangle homeButton = new((int)logoX - 18 - 96, 18, 96, 34);
            using Brush homeBackground = new SolidBrush(Color.FromArgb(22, 25, 31));
            using Font homeFont = new("Segoe UI", 11, FontStyle.Bold);
            using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.FillRectangle(homeBackground, homeButton);
            graphics.DrawRectangle(border, homeButton);
            graphics.DrawString("HOME", homeFont, white, homeButton, centered);

            if (showHomeButton)
            {
                Rectangle homeTouchArea = homeButton;
                homeTouchArea.Inflate(8, 8);
                targets.Add(new HitTarget(homeTouchArea, () => NavigateTo(PanelPage.Home)));
            }
        }
    }

    private static Bitmap LoadLogoBitmap()
    {
        string assemblyDirectory = Path.GetDirectoryName(typeof(HwinfoPanelWidget).Assembly.Location) ?? string.Empty;
        string logoPath = Path.Combine(assemblyDirectory, "IMG_0382.ico");
        if (!File.Exists(logoPath))
            logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IMG_0382.ico");
        if (!File.Exists(logoPath))
            return null;

        using Icon icon = new(logoPath);
        return icon.ToBitmap();
    }

    private void DrawCoreCard(Graphics graphics, Rectangle bounds, string label, string model, double load, double temperature, double clock, double power, double fanRpm, Color accent, Font titleFont, Font valueFont, Font detailFont)
    {
        DrawCardFrame(graphics, bounds, accent);
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        using Brush accentBrush = new SolidBrush(accent);
        model = ShortenFiveByOneModel(label, model);
        bool isFourByThree = WidgetSize.Width == 4 && WidgetSize.Height == 3;
        SizeF labelSize = graphics.MeasureString(label, titleFont);
        SizeF modelSize = graphics.MeasureString(model, detailFont);
        float headerStart = bounds.X + (bounds.Width - labelSize.Width - 8 - modelSize.Width) / 2f;
        graphics.DrawString(label, titleFont, white, headerStart, bounds.Y + (isFourByThree ? 9 : 14));
        graphics.DrawString(model, detailFont, muted, headerStart + labelSize.Width + 8, bounds.Y + (isFourByThree ? 13 : 18));
        bool isFiveByFour = WidgetSize.Width == 5 && WidgetSize.Height == 4;
        bool isThreeByThree = WidgetSize.Width == 3 && WidgetSize.Height == 3;
        int gaugeRadius = isFiveByFour ? 60 : isThreeByThree ? (panelTarget == PanelTarget.Combined ? 45 : 60) : 58;
        int gaugeWidth = isFiveByFour ? 22 : isThreeByThree || isFourByThree ? 20 : 10;
        int metricBarAdjustment = isThreeByThree ? -10 : 0;
        int metricsX = 15;
        if (isFiveByFour && fiveByFourGaugeMode == FiveByFourGaugeMode.Combined)
        {
            Point loadCenter = new(bounds.X + 75, bounds.Y + 115);
            Point temperatureCenter = new(bounds.X + 415, bounds.Y + 115);
            DrawGauge(graphics, loadCenter, gaugeRadius, load, GetGaugeColor(load), gaugeWidth);
            DrawGauge(graphics, temperatureCenter, gaugeRadius, temperature, GetTemperatureGaugeColor(temperature), gaugeWidth);
            DrawCenteredText(graphics, $"{load:0}%", valueFont, white, loadCenter.X, bounds.Y + 92);
            DrawCenteredText(graphics, "Load", detailFont, muted, loadCenter.X, bounds.Y + 161);
            DrawCenteredText(graphics, $"{temperature:0} °C", valueFont, white, temperatureCenter.X, bounds.Y + 92);
            DrawCenteredText(graphics, "Temperature", detailFont, muted, temperatureCenter.X, bounds.Y + 161);
            DrawMetric(graphics, bounds.X + 145 + metricsX, bounds.Y + 72, "Clock", $"{clock:0} MHz", clock / 6000, accentBrush, detailFont, white, 163, 10);
            DrawMetric(graphics, bounds.X + 145 + metricsX, bounds.Y + 111, "Power", $"{power:0} W", power / 300, accentBrush, detailFont, white, 163, 10);
            DrawMetric(graphics, bounds.X + 145 + metricsX, bounds.Y + 150, "Fan", $"{fanRpm:0} RPM", fanRpm / (label == "CPU" ? 5000 : 3000), accentBrush, detailFont, white, 163, 10);
            return;
        }

        if (isThreeByThree)
        {
            bool singleTarget = panelTarget != PanelTarget.Combined;
            Point compactCenter = new(bounds.X + (singleTarget ? 80 : 68), bounds.Y + 115);
            DrawGauge(graphics, compactCenter, gaugeRadius, load, GetGaugeColor(load), gaugeWidth);
            DrawCenteredText(graphics, $"{load:0}%", valueFont, white, compactCenter.X, bounds.Y + 92);
            DrawCenteredText(graphics, "Load", detailFont, muted, compactCenter.X, bounds.Y + 161);
            if (singleTarget)
            {
                Point temperatureCenter = new(bounds.Right - 80, bounds.Y + 115);
                DrawGauge(graphics, temperatureCenter, gaugeRadius, temperature, GetTemperatureGaugeColor(temperature), gaugeWidth);
                DrawCenteredText(graphics, $"{temperature:0} °C", valueFont, white, temperatureCenter.X, bounds.Y + 92);
                DrawCenteredText(graphics, "Temp", detailFont, muted, temperatureCenter.X, bounds.Y + 161);
            }
            int compactMetricX = bounds.X + (singleTarget ? 180 : 125);
            int compactMetricWidth = bounds.Width - (singleTarget ? 360 : 145);
            if (singleTarget)
            {
                DrawMetric(graphics, compactMetricX, bounds.Y + 65, "Clock", $"{clock:0} MHz", clock / 6000, accentBrush, detailFont, white, compactMetricWidth, 10, -20);
                DrawMetric(graphics, compactMetricX, bounds.Y + 100, "Power", $"{power:0} W", power / 300, accentBrush, detailFont, white, compactMetricWidth, 10, -20);
                DrawMetric(graphics, compactMetricX, bounds.Y + 135, "Fan", $"{fanRpm:0} RPM", fanRpm / (label == "CPU" ? 5000 : 3000), accentBrush, detailFont, white, compactMetricWidth, 10, -20);
            }
            else
            {
                DrawMetric(graphics, compactMetricX, bounds.Y + 45, "Temp", $"{temperature:0} °C", temperature / 100, accentBrush, detailFont, white, compactMetricWidth, 10, -20);
                DrawMetric(graphics, compactMetricX, bounds.Y + 80, "Clock", $"{clock:0} MHz", clock / 6000, accentBrush, detailFont, white, compactMetricWidth, 10, -20);
                DrawMetric(graphics, compactMetricX, bounds.Y + 115, "Power", $"{power:0} W", power / 300, accentBrush, detailFont, white, compactMetricWidth, 10, -20);
                DrawMetric(graphics, compactMetricX, bounds.Y + 150, "Fan", $"{fanRpm:0} RPM", fanRpm / (label == "CPU" ? 5000 : 3000), accentBrush, detailFont, white, compactMetricWidth, 10, -20);
            }
            return;
        }

        bool showTemperatureGauge = isFiveByFour && fiveByFourGaugeMode == FiveByFourGaugeMode.Temperature;
        double gaugeValue = showTemperatureGauge ? temperature : load;
        Color gaugeColor = showTemperatureGauge ? GetTemperatureGaugeColor(temperature) : GetGaugeColor(load);
        string gaugeText = showTemperatureGauge ? $"{temperature:0} °C" : $"{load:0}%";
        string gaugeLabel = showTemperatureGauge ? "Temperature" : "Load";
        Point gaugeCenter = new(bounds.X + 100, bounds.Y + 115);
        DrawGauge(graphics, gaugeCenter, gaugeRadius, gaugeValue, gaugeColor, gaugeWidth);
        DrawCenteredText(graphics, gaugeText, valueFont, white, gaugeCenter.X, bounds.Y + 92);
        DrawCenteredText(graphics, gaugeLabel, detailFont, muted, gaugeCenter.X, bounds.Y + 161);
        int generalMetricX = bounds.X + (isFourByThree ? 180 : 200 + metricsX);
        int generalMetricWidth = isFourByThree ? bounds.Width - 195 : 248 + metricBarAdjustment;
        DrawMetric(graphics, generalMetricX, bounds.Y + (isFourByThree ? 50 : 65), showTemperatureGauge ? "Load" : "Temperature", showTemperatureGauge ? $"{load:0}%" : $"{temperature:0} °C", showTemperatureGauge ? load / 100 : temperature / 100, accentBrush, detailFont, white, generalMetricWidth, isFourByThree || isFiveByFour ? 10 : 4, 0, 22);
        DrawMetric(graphics, generalMetricX, bounds.Y + (isFiveByFour ? 100 : isFourByThree ? 85 : 90), "Clock", $"{clock:0} MHz", clock / 6000, accentBrush, detailFont, white, generalMetricWidth, 10, 0, 22);
        DrawMetric(graphics, generalMetricX, bounds.Y + (isFiveByFour ? 135 : isFourByThree ? 120 : 125), "Power", $"{power:0} W", power / 300, accentBrush, detailFont, white, generalMetricWidth, 10, 0, 22);
        DrawMetric(graphics, generalMetricX, bounds.Y + (isFiveByFour ? 170 : isFourByThree ? 155 : 160), "Fan", $"{fanRpm:0} RPM", fanRpm / (label == "CPU" ? 5000 : 3000), accentBrush, detailFont, white, generalMetricWidth, 10, 0, 22);
    }

    private static void DrawMemoryCard(Graphics graphics, Rectangle bounds, SensorSnapshot data, Color accent, Font titleFont, Font valueFont, Font detailFont, MemoryGaugeAlignment alignment, bool compactThreeByThree, bool hideLastInfo)
    {
        DrawCardFrame(graphics, bounds, accent);
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        if (compactThreeByThree)
        {
            graphics.DrawString("RAM", titleFont, white, bounds.X + 10, bounds.Y + 10);
            Point centeredGauge = new(bounds.X + bounds.Width / 2, bounds.Y + 88);
            DrawGauge(graphics, centeredGauge, 42, data.MemoryLoadPercent, accent, 16);
            DrawCenteredText(graphics, $"{data.MemoryLoadPercent:0}%", valueFont, white, centeredGauge.X, centeredGauge.Y - valueFont.Height / 2f);
            DrawCenteredText(graphics, "Load", detailFont, muted, centeredGauge.X, centeredGauge.Y + 27);
            DrawCenteredText(graphics, $"Clock  {data.MemoryClockMhz:0} MHz", detailFont, muted, bounds.X + bounds.Width / 2f, bounds.Y + 137);
            DrawCenteredText(graphics, $"Used  {data.MemoryUsedGigabytes:0.0} GB / {data.MemoryTotalGigabytes:0} GB", detailFont, muted, bounds.X + bounds.Width / 2f, bounds.Y + 163);
            return;
        }

        bool gaugeOnLeft = alignment == MemoryGaugeAlignment.Left;
        int contentX = gaugeOnLeft ? bounds.X + 118 : bounds.X + 14;
        graphics.DrawString("RAM", titleFont, white, contentX, bounds.Y + (hideLastInfo ? 7 : 12));
        graphics.DrawString($"Clock  {data.MemoryClockMhz:0} MHz", detailFont, muted, contentX, bounds.Y + (hideLastInfo ? 75 : 50));
        graphics.DrawString($"Used  {data.MemoryUsedGigabytes:0.0} GB / {data.MemoryTotalGigabytes:0} GB", detailFont, muted, contentX, bounds.Y + (hideLastInfo ? 101 : 76));
        if (!hideLastInfo)
            graphics.DrawString("38-38-38-77 CR2", detailFont, muted, contentX, bounds.Y + 102);
        Point gaugeCenter = new(gaugeOnLeft ? bounds.X + 62 : bounds.Right - 62, bounds.Y + 68);
        DrawGauge(graphics, gaugeCenter, 42, data.MemoryLoadPercent, accent, 16);
        DrawCenteredText(graphics, $"{data.MemoryLoadPercent:0}%", valueFont, white, gaugeCenter.X, gaugeCenter.Y - valueFont.Height / 2f);
        DrawCenteredText(graphics, "Load", detailFont, muted, gaugeCenter.X, gaugeCenter.Y + (hideLastInfo ? 22 : 42));
    }

    private static void DrawFpsCard(Graphics graphics, Rectangle bounds, SensorSnapshot data, Color accent, Font titleFont, Font detailFont)
    {
        DrawCardFrame(graphics, bounds, accent);
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        graphics.DrawString("FPS", titleFont, white, bounds.X + 14, bounds.Y + 12);
        using Font fpsFont = new("Segoe UI", 42, FontStyle.Bold);
        graphics.DrawString($"{data.Fps:0}", fpsFont, white, bounds.X + 14, bounds.Y + 45);
        graphics.DrawString("Frame rate", detailFont, muted, bounds.X + 18, bounds.Y + 100);
    }

    private void DrawDiskCard(Graphics graphics, Rectangle bounds, SensorSnapshot data, Color accent, Font titleFont, Font detailFont)
    {
        DrawCardFrame(graphics, bounds, accent);
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        graphics.DrawString("STORAGE", titleFont, white, bounds.X + 16, bounds.Y + 10);

        int rowHeight = 31;
        int rowTop = bounds.Y + 34;
        int maxDrives = Math.Max(1, Math.Min(8, (bounds.Height - 34) / rowHeight));
        int shownDrives = 0;
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            if (shownDrives >= maxDrives || drive.DriveType != DriveType.Fixed || !drive.IsReady)
                continue;

            try
            {
                double usedBytes = drive.TotalSize - drive.AvailableFreeSpace;
                double usedPercent = drive.TotalSize > 0 ? usedBytes / drive.TotalSize * 100 : 0;
                string driveLabel = drive.Name.TrimEnd('\\');
                int temperatureIndex = char.ToUpperInvariant(driveLabel[0]) - 'C';
                string temperature = data.DriveTemperatures != null && temperatureIndex >= 0 && temperatureIndex < data.DriveTemperatures.Length && data.DriveTemperatures[temperatureIndex].HasValue
                    ? $"{data.DriveTemperatures[temperatureIndex].Value:0} °C"
                    : "-- °C";
                string detail = $"{drive.AvailableFreeSpace / 1073741824d:0.0} GB frei";
                string usageLabel = $"{usedPercent:0}%";
                string temperatureLabel = temperature.Replace(" ", string.Empty);
                int rowY = rowTop + shownDrives * rowHeight;
                graphics.DrawString(driveLabel, detailFont, white, bounds.X + 16, rowY + 10);
                using Font smallFont = new("Segoe UI", 8);

                int barY = rowY + 11;
                int availableBarWidth = bounds.Width - 68;
                int usageBarWidth = Math.Max(90, (int)(availableBarWidth * 0.64f));
                int temperatureBarX = bounds.X + 52 + usageBarWidth + 6;
                int temperatureBarWidth = Math.Max(55, bounds.Right - 16 - temperatureBarX);
                Rectangle usageBar = new(bounds.X + 52, barY, usageBarWidth, 20);
                Rectangle temperatureBar = new(temperatureBarX, barY, temperatureBarWidth, 20);
                using Brush barBackground = new SolidBrush(Color.FromArgb(65, 72, 84));
                using Brush barFill = new SolidBrush(accent);
                graphics.FillRectangle(barBackground, usageBar);
                graphics.FillRectangle(barFill, new Rectangle(usageBar.X, usageBar.Y, (int)(usageBar.Width * Math.Min(100, Math.Max(0, usedPercent)) / 100), usageBar.Height));
                using Brush temperatureBrush = new SolidBrush(data.DriveTemperatures != null && temperatureIndex >= 0 && temperatureIndex < data.DriveTemperatures.Length && data.DriveTemperatures[temperatureIndex].HasValue
                    ? GetTemperatureColor(data.DriveTemperatures[temperatureIndex].Value)
                    : Color.FromArgb(65, 72, 84));
                graphics.FillRectangle(temperatureBrush, temperatureBar);
                using StringFormat barLabelFormat = new() { LineAlignment = StringAlignment.Center };
                using StringFormat rightLabelFormat = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                graphics.DrawString(usageLabel, smallFont, white, new RectangleF(usageBar.X + 5, usageBar.Y, usageBar.Width - 10, usageBar.Height), barLabelFormat);
                graphics.DrawString(detail, smallFont, white, new RectangleF(usageBar.X + 5, usageBar.Y, usageBar.Width - 10, usageBar.Height), rightLabelFormat);
                graphics.DrawString(temperatureLabel, smallFont, white, new RectangleF(temperatureBar.X + 4, temperatureBar.Y, temperatureBar.Width - 8, temperatureBar.Height), barLabelFormat);
                shownDrives++;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        if (shownDrives == 0)
            graphics.DrawString("Keine Laufwerke", detailFont, muted, bounds.X + 16, rowTop);
    }

    private static Color GetTemperatureColor(double temperature)
    {
        if (temperature >= 70)
            return Color.FromArgb(220, 55, 55);
        if (temperature >= 55)
            return Color.FromArgb(225, 145, 35);
        if (temperature >= 45)
            return Color.FromArgb(205, 185, 45);
        return Color.FromArgb(55, 165, 90);
    }

    private static void DrawInfoCard(Graphics graphics, Rectangle bounds, string label, string value, Color accent, Font titleFont, Font detailFont)
    {
        DrawCardFrame(graphics, bounds, accent);
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        graphics.DrawString(label, titleFont, white, bounds.X + 14, bounds.Y + 14);
        graphics.DrawString(value, detailFont, muted, bounds.X + 14, bounds.Y + 54);
        graphics.FillRectangle(new SolidBrush(accent), bounds.X + 14, bounds.Y + 86, bounds.Width - 28, 4);
    }

    private static void DrawVramCard(Graphics graphics, Rectangle bounds, SensorSnapshot data, Color accent, Font titleFont, Font valueFont, Font detailFont, MemoryGaugeAlignment alignment, bool compactThreeByThree, bool hideLastInfo)
    {
        DrawCardFrame(graphics, bounds, accent);
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        double totalMegabytes = data.GpuMemoryTotalMegabytes > 0 ? data.GpuMemoryTotalMegabytes : 24576;
        double usedGigabytes = data.GpuMemoryMegabytes / 1024;
        double totalGigabytes = totalMegabytes / 1024;
        double load = data.GpuMemoryMegabytes / totalMegabytes * 100;
        if (compactThreeByThree)
        {
            graphics.DrawString("VRAM", titleFont, white, bounds.X + 10, bounds.Y + 10);
            Point centeredGauge = new(bounds.X + bounds.Width / 2, bounds.Y + 88);
            DrawGauge(graphics, centeredGauge, 42, load, accent, 16);
            DrawCenteredText(graphics, $"{load:0}%", valueFont, white, centeredGauge.X, centeredGauge.Y - valueFont.Height / 2f);
            DrawCenteredText(graphics, "Load", detailFont, muted, centeredGauge.X, centeredGauge.Y + 27);
            DrawCenteredText(graphics, $"Clock  {data.GpuClockMhz:0} MHz", detailFont, muted, bounds.X + bounds.Width / 2f, bounds.Y + 137);
            DrawCenteredText(graphics, $"Used  {usedGigabytes:0.0} GB / {totalGigabytes:0} GB", detailFont, muted, bounds.X + bounds.Width / 2f, bounds.Y + 163);
            return;
        }

        bool gaugeOnLeft = alignment == MemoryGaugeAlignment.Left;
        int contentX = gaugeOnLeft ? bounds.X + 118 : bounds.X + 14;
        graphics.DrawString("VRAM", titleFont, white, contentX, bounds.Y + (hideLastInfo ? 7 : 12));
        graphics.DrawString($"Clock  {data.GpuClockMhz:0} MHz", detailFont, muted, contentX, bounds.Y + (hideLastInfo ? 75 : 50));
        graphics.DrawString($"Used  {usedGigabytes:0.0} GB / {totalGigabytes:0} GB", detailFont, muted, contentX, bounds.Y + (hideLastInfo ? 101 : 76));
        if (!hideLastInfo)
            graphics.DrawString("GPU Memory", detailFont, muted, contentX, bounds.Y + 102);
        Point gaugeCenter = new(gaugeOnLeft ? bounds.X + 62 : bounds.Right - 62, bounds.Y + 68);
        DrawGauge(graphics, gaugeCenter, 42, load, accent, 16);
        DrawCenteredText(graphics, $"{load:0}%", valueFont, white, gaugeCenter.X, gaugeCenter.Y - valueFont.Height / 2f);
        DrawCenteredText(graphics, "Load", detailFont, muted, gaugeCenter.X, gaugeCenter.Y + (hideLastInfo ? 22 : 42));
    }

    private static void DrawFanCard(Graphics graphics, Rectangle bounds, SensorSnapshot data, Color accent, Font titleFont, Font detailFont)
    {
        DrawCardFrame(graphics, bounds, accent);
        using Brush white = new SolidBrush(Color.White);
        using Brush barBackground = new SolidBrush(Color.FromArgb(65, 73, 83));
        using Brush barFill = new SolidBrush(accent);
        DrawCenteredText(graphics, "GPU / CPU FAN", titleFont, white, bounds.X + bounds.Width / 2f, bounds.Y + 12);

        const int barHeight = 30;
        int labelX = bounds.X + 14;
        float labelWidth = Math.Max(
            graphics.MeasureString("GPU", detailFont).Width,
            graphics.MeasureString("CPU", detailFont).Width);
        int barX = labelX + (int)Math.Ceiling(labelWidth) + 10;
        int barWidth = bounds.Right - 14 - barX;
        int gpuBarY = bounds.Y + 42;
        int cpuBarY = bounds.Y + 82;
        float gpuProgress = (float)Math.Min(1, Math.Max(0, data.GpuFanRpm / 3000f));
        float cpuProgress = (float)Math.Min(1, Math.Max(0, data.CpuFanRpm / 5000f));

        graphics.DrawString("GPU", detailFont, white, labelX, gpuBarY + (barHeight - detailFont.Height) / 2f);
        graphics.FillRectangle(barBackground, barX, gpuBarY, barWidth, barHeight);
        graphics.FillRectangle(barFill, barX, gpuBarY, barWidth * gpuProgress, barHeight);
        graphics.DrawString("CPU", detailFont, white, labelX, cpuBarY + (barHeight - detailFont.Height) / 2f);
        graphics.FillRectangle(barBackground, barX, cpuBarY, barWidth, barHeight);
        graphics.FillRectangle(barFill, barX, cpuBarY, barWidth * cpuProgress, barHeight);

        DrawCenteredText(graphics, $"{data.GpuFanRpm:0} RPM", detailFont, white, barX + barWidth / 2f, gpuBarY + (barHeight - detailFont.Height) / 2f);
        DrawCenteredText(graphics, $"{data.CpuFanRpm:0} RPM", detailFont, white, barX + barWidth / 2f, cpuBarY + (barHeight - detailFont.Height) / 2f);
    }

    private static void DrawNetworkCard(Graphics graphics, Rectangle bounds, SensorSnapshot data, Color accent, Font titleFont, Font detailFont, double uploadNetworkScaleMegabytesPerSecond, double downloadNetworkScaleMegabytesPerSecond, bool isFourByThree)
    {
        DrawCardFrame(graphics, bounds, accent);
        using Brush white = new SolidBrush(Color.White);
        using Brush barBackground = new SolidBrush(Color.FromArgb(65, 73, 83));
        using Brush barFill = new SolidBrush(accent);
        DrawCenteredText(graphics, "NETWORK", titleFont, white, bounds.X + bounds.Width / 2f, bounds.Y + (isFourByThree ? 7 : 12));

        const int barHeight = 30;
        int labelX = bounds.X + 14;
        float labelWidth = Math.Max(
            graphics.MeasureString("Upload", detailFont).Width,
            graphics.MeasureString("Download", detailFont).Width);
        int barX = labelX + (int)Math.Ceiling(labelWidth) + 10;
        int barWidth = bounds.Right - 14 - barX;
        int uploadBarY = bounds.Y + 42;
        int downloadBarY = bounds.Y + 82;
        float uploadProgress = (float)Math.Min(1, Math.Max(0, data.NetworkUploadMegabytesPerSecond / uploadNetworkScaleMegabytesPerSecond));
        float downloadProgress = (float)Math.Min(1, Math.Max(0, data.NetworkDownloadMegabytesPerSecond / downloadNetworkScaleMegabytesPerSecond));

        graphics.DrawString("Upload", detailFont, white, labelX, uploadBarY + (barHeight - detailFont.Height) / 2f);
        graphics.FillRectangle(barBackground, barX, uploadBarY, barWidth, barHeight);
        graphics.FillRectangle(barFill, barX, uploadBarY, barWidth * uploadProgress, barHeight);
        graphics.DrawString("Download", detailFont, white, labelX, downloadBarY + (barHeight - detailFont.Height) / 2f);
        graphics.FillRectangle(barBackground, barX, downloadBarY, barWidth, barHeight);
        graphics.FillRectangle(barFill, barX, downloadBarY, barWidth * downloadProgress, barHeight);

        DrawCenteredText(graphics, $"{data.NetworkUploadMegabytesPerSecond:0.0} MB/s", detailFont, white, barX + barWidth / 2f, uploadBarY + (barHeight - detailFont.Height) / 2f);
        DrawCenteredText(graphics, $"{data.NetworkDownloadMegabytesPerSecond:0.0} MB/s", detailFont, white, barX + barWidth / 2f, downloadBarY + (barHeight - detailFont.Height) / 2f);
    }

    private static void DrawMetric(Graphics graphics, int x, int y, string label, string value, double progress, Brush accent, Font detailFont, Brush white, int width = 220, int barHeight = 4, int valueOffset = 0, int barOffset = 22)
    {
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        graphics.DrawString(label, detailFont, muted, x, y);
        graphics.DrawString(value, detailFont, white, x + 108 + valueOffset, y);
        double clampedProgress = progress < 0 ? 0 : progress > 1 ? 1 : progress;
        graphics.FillRectangle(new SolidBrush(Color.FromArgb(65, 73, 83)), x, y + barOffset, width, barHeight);
        graphics.FillRectangle(accent, x, y + barOffset, (float)(width * clampedProgress), barHeight);
    }

    private static void DrawCompactMetricBar(Graphics graphics, int x, int y, int width, string label, string value, double progress, Brush accent, Font detailFont, Brush white)
    {
        const int barHeight = 16;
        using Brush background = new SolidBrush(Color.FromArgb(65, 73, 83));
        double clampedProgress = progress < 0 ? 0 : progress > 1 ? 1 : progress;
        graphics.FillRectangle(background, x, y, width, barHeight);
        graphics.FillRectangle(accent, x, y, (float)(width * clampedProgress), barHeight);
        graphics.DrawString(label, detailFont, white, x + 6, y + 1);
        graphics.DrawString(value, detailFont, white, x + 72, y + 1);
    }

    private void DrawCompactPanel(Graphics graphics, int width, int height, SensorSnapshot data, Font titleFont, Font valueFont, Font detailFont)
    {
        if (WidgetSize.Width == 2 && WidgetSize.Height == 2 && panelTarget == PanelTarget.Storage)
        {
            DrawDiskCard(graphics, new Rectangle(2, 2, width - 5, height - 5), data, accentColor, titleFont, detailFont);
            return;
        }

        bool gpu = panelTarget == PanelTarget.Gpu;
        string label = gpu ? "GPU" : "CPU";
        double load = gpu ? data.GpuLoadPercent : data.CpuLoadPercent;
        double temperature = gpu ? data.GpuTemperatureCelsius : data.CpuTemperatureCelsius;
        double clock = gpu ? data.GpuClockMhz : data.CpuClockMhz;
        double power = gpu ? data.GpuPowerWatts : data.CpuPowerWatts;
        string model = gpu ? data.GpuName : data.CpuName;
        model = ShortenFiveByOneModel(label, model);
        int centerY = height / 2;
        if (WidgetSize.Height == 1 && WidgetSize.Width <= 2)
            DrawCardFrame(graphics, new Rectangle(2, 2, width - 5, height - 5), accentColor);

        if (width < 300)
        {
            bool isOneByOne = WidgetSize.Width == 1 && WidgetSize.Height == 1;
            bool isTwoByOne = WidgetSize.Width == 2 && WidgetSize.Height == 1;
            bool isTwoByTwo = WidgetSize.Width == 2 && WidgetSize.Height == 2;
            int radius = isOneByOne || isTwoByOne ? 49 : isTwoByTwo ? 60 : Math.Max(24, Math.Min(width, height) / 4);
            int gaugeWidth = isOneByOne || isTwoByOne || isTwoByTwo ? 20 : 10;
            Point gaugeCenter = new(width / 2, centerY + 12);
            double gaugeValue = isOneByOne && oneByOneShowsTemperature ? temperature : load;
            Color gaugeColor = isOneByOne && oneByOneShowsTemperature ? GetTemperatureGaugeColor(temperature) : GetGaugeColor(load);
            DrawGauge(graphics, gaugeCenter, radius, gaugeValue, gaugeColor, gaugeWidth);
            using Brush compactWhite = new SolidBrush(Color.White);
            string gaugeText = isOneByOne && oneByOneShowsTemperature ? $"{temperature:0} °C" : $"{load:0}%";
            string secondaryText = isOneByOne && oneByOneShowsTemperature ? $"{load:0}%" : $"{temperature:0} °C";
            DrawCenteredText(graphics, gaugeText, valueFont, compactWhite, gaugeCenter.X, gaugeCenter.Y - valueFont.Height / 2f);
            graphics.DrawString(label, titleFont, compactWhite, 12, 10);
            DrawCenteredText(graphics, secondaryText, detailFont, compactWhite, gaugeCenter.X, height - 24);
            return;
        }

        bool isTwoByOneLayout = WidgetSize.Width == 2 && WidgetSize.Height == 1;
        bool isTwoByTwoLayout = WidgetSize.Width == 2 && WidgetSize.Height == 2;
        int radiusDual = isTwoByOneLayout ? 49 : isTwoByTwoLayout ? 60 : Math.Max(30, Math.Min(58, Math.Min(width / 5, height / 2 - 18)));
        int gaugeWidthDual = isTwoByOneLayout || isTwoByTwoLayout ? 20 : 10;
        Point loadCenter = new(width / 4, centerY + 8);
        Point temperatureCenter = new(width * 3 / 4, centerY + 8);
        DrawGauge(graphics, loadCenter, radiusDual, load, GetGaugeColor(load), gaugeWidthDual);
        DrawGauge(graphics, temperatureCenter, radiusDual, temperature, GetTemperatureGaugeColor(temperature), gaugeWidthDual);
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(165, 175, 188));
        if (isTwoByOneLayout)
            DrawCenteredText(graphics, label, titleFont, white, width / 2f, 10);
        else
            graphics.DrawString(label, titleFont, white, 14, 10);
        graphics.DrawString(model, detailFont, muted, 14, 34);
        DrawCenteredText(graphics, $"{load:0}%", valueFont, white, loadCenter.X, loadCenter.Y - valueFont.Height / 2f);
        DrawCenteredText(graphics, $"{temperature:0} °C", valueFont, white, temperatureCenter.X, temperatureCenter.Y - valueFont.Height / 2f);
        if (isTwoByTwoLayout)
        {
            using Brush accentBrush = new SolidBrush(accentColor);
            DrawCompactMetricBar(graphics, 14, height - 49, width - 28, "Clock", $"{clock:0} MHz", clock / 6000, accentBrush, detailFont, white);
            DrawCompactMetricBar(graphics, 14, height - 26, width - 28, "Power", $"{power:0} W", power / 300, accentBrush, detailFont, white);
        }
        else
        {
            graphics.DrawString($"Clock {clock:0} MHz", detailFont, white, width / 2 - 64, height - 42);
            graphics.DrawString($"Power {power:0} W", detailFont, white, width / 2 - 58, height - 22);
        }
    }

    private void DrawTwoByThreePanel(Graphics graphics, int width, int height, SensorSnapshot data)
    {
        int margin = Math.Max(6, width / 32);
        int gap = Math.Max(6, width / 42);
        int headerHeight = Math.Max(32, height / 10);
        int contentTop = margin + headerHeight + gap;
        int contentHeight = height - contentTop - margin;
        int columnWidth = (width - margin * 2 - gap) / 2;
        int topHeight = twoByThreeLayoutMode == TwoByThreeLayoutMode.Minimal
            ? (contentHeight - gap * 2) / 3
            : (int)(contentHeight * 0.58f);
        int bottomTop = contentTop + topHeight + gap;
        int bottomHeight = contentHeight - topHeight - gap;

        DrawTwoByThreeHeader(graphics, width, headerHeight, margin);
        bool combined = panelTarget == PanelTarget.Combined;
        if (twoByThreeLayoutMode == TwoByThreeLayoutMode.Minimal)
        {
            int rowHeight = (contentHeight - gap * 2) / 3;
            if (combined)
            {
                DrawTwoByThreeValueTile(graphics, new Rectangle(margin, contentTop, columnWidth, rowHeight), "CPU", $"{data.CpuLoadPercent:0}%", $"{data.CpuTemperatureCelsius:0} °C");
                DrawTwoByThreeValueTile(graphics, new Rectangle(margin + columnWidth + gap, contentTop, columnWidth, rowHeight), "GPU", $"{data.GpuLoadPercent:0}%", $"{data.GpuTemperatureCelsius:0} °C");
            }
            else if (panelTarget == PanelTarget.Cpu)
            {
                DrawTwoByThreeValueTile(graphics, new Rectangle(margin, contentTop, width - margin * 2, rowHeight), "CPU", $"{data.CpuLoadPercent:0}%", $"{data.CpuTemperatureCelsius:0} °C");
            }
            else
            {
                DrawTwoByThreeValueTile(graphics, new Rectangle(margin, contentTop, width - margin * 2, rowHeight), "GPU", $"{data.GpuLoadPercent:0}%", $"{data.GpuTemperatureCelsius:0} °C");
            }
            DrawTwoByThreeValueTile(graphics, new Rectangle(margin, contentTop + rowHeight + gap, columnWidth, rowHeight), "RAM", $"{data.MemoryLoadPercent:0}%", $"{data.MemoryUsedGigabytes:0.0} / {data.MemoryTotalGigabytes:0} GB");
            DrawTwoByThreeValueTile(graphics, new Rectangle(margin + columnWidth + gap, contentTop + rowHeight + gap, columnWidth, rowHeight), "VRAM", $"{GetVramLoad(data):0}%", $"{data.GpuMemoryMegabytes / 1024:0.0} GB used");
            DrawTwoByThreeValueTile(graphics, new Rectangle(margin, contentTop + (rowHeight + gap) * 2, columnWidth, rowHeight), "NETWORK", $"{data.NetworkDownloadMegabytesPerSecond:0.0}", "MB/s download");
            DrawTwoByThreeValueTile(graphics, new Rectangle(margin + columnWidth + gap, contentTop + (rowHeight + gap) * 2, columnWidth, rowHeight), "FPS", $"{data.Fps:0}", "Frame rate");
            return;
        }

        if (combined)
        {
            DrawTwoByThreeCoreTile(graphics, new Rectangle(margin, contentTop, columnWidth, topHeight), "CPU", data.CpuLoadPercent, data.CpuTemperatureCelsius, data.CpuClockMhz);
            DrawTwoByThreeCoreTile(graphics, new Rectangle(margin + columnWidth + gap, contentTop, columnWidth, topHeight), "GPU", data.GpuLoadPercent, data.GpuTemperatureCelsius, data.GpuClockMhz);
        }
        else if (panelTarget == PanelTarget.Cpu)
        {
            DrawTwoByThreeCoreTile(graphics, new Rectangle(margin, contentTop, width - margin * 2, topHeight), "CPU", data.CpuLoadPercent, data.CpuTemperatureCelsius, data.CpuClockMhz);
        }
        else
        {
            DrawTwoByThreeCoreTile(graphics, new Rectangle(margin, contentTop, width - margin * 2, topHeight), "GPU", data.GpuLoadPercent, data.GpuTemperatureCelsius, data.GpuClockMhz);
        }

        if (twoByThreeLayoutMode == TwoByThreeLayoutMode.Gauges)
        {
            DrawTwoByThreeValueTile(graphics, new Rectangle(margin, bottomTop, columnWidth, bottomHeight), "RAM", $"{data.MemoryLoadPercent:0}%", $"{data.MemoryUsedGigabytes:0.0} / {data.MemoryTotalGigabytes:0} GB");
            DrawTwoByThreeValueTile(graphics, new Rectangle(margin + columnWidth + gap, bottomTop, columnWidth, bottomHeight), "VRAM", $"{GetVramLoad(data):0}%", $"{data.GpuMemoryMegabytes / 1024:0.0} / {data.GpuMemoryTotalMegabytes / 1024:0} GB");
            return;
        }

        int bottomColumnWidth = (width - margin * 2 - gap * 2) / 3;
        DrawTwoByThreeValueTile(graphics, new Rectangle(margin, bottomTop, bottomColumnWidth, bottomHeight), "RAM", $"{data.MemoryLoadPercent:0}%", $"{data.MemoryUsedGigabytes:0.0} GB");
        DrawTwoByThreeValueTile(graphics, new Rectangle(margin + bottomColumnWidth + gap, bottomTop, bottomColumnWidth, bottomHeight), "NETWORK", $"{data.NetworkDownloadMegabytesPerSecond:0.0}", "MB/s down");
        DrawTwoByThreeValueTile(graphics, new Rectangle(margin + (bottomColumnWidth + gap) * 2, bottomTop, bottomColumnWidth, bottomHeight), "VRAM", $"{GetVramLoad(data):0}%", "GPU memory");
    }

    private void DrawTwoByThreeHeader(Graphics graphics, int width, int headerHeight, int margin)
    {
        using Pen border = new(accentColor, 2);
        using Brush white = new SolidBrush(Color.White);
        using Brush timeBrush = new SolidBrush(timeColor);
        using Font titleFont = new("Segoe UI", Math.Max(9, headerHeight / 3), FontStyle.Bold);
        using Font timeFont = new("Segoe UI", Math.Max(10, headerHeight / 3), FontStyle.Bold);
        graphics.DrawRectangle(border, margin, margin, width - margin * 2, headerHeight);
        graphics.DrawString("HWiNFO", titleFont, white, margin + 8, margin + 5);
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        string currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).ToString("HH:mm:ss");
        SizeF timeSize = graphics.MeasureString(currentTime, timeFont);
        graphics.DrawString(currentTime, timeFont, timeBrush, (width - timeSize.Width) / 2, margin + 5);
    }

    private void DrawTwoByThreeCoreTile(Graphics graphics, Rectangle bounds, string label, double load, double temperature, double clock)
    {
        DrawCardFrame(graphics, bounds, accentColor);
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        int radius = Math.Max(40, Math.Min(bounds.Height / 4, bounds.Width / 6));
        using Font labelFont = new("Segoe UI", Math.Max(9, bounds.Height / 13), FontStyle.Bold);
        using Font valueFont = new("Segoe UI", Math.Max(14, radius * 0.48f), FontStyle.Bold);
        using Font detailFont = new("Segoe UI", Math.Max(8, bounds.Height / 16));
        DrawCenteredText(graphics, label, labelFont, white, bounds.X + bounds.Width / 2f, bounds.Y + 8);
        bool singleTarget = panelTarget != PanelTarget.Combined;
        int gaugeCenterOffsetY = singleTarget ? 33 : 48;
        Point loadCenter = new(singleTarget ? bounds.X + radius + 54 : bounds.Right - radius - 54, bounds.Y + radius + gaugeCenterOffsetY);
        DrawGauge(graphics, loadCenter, radius, load, GetGaugeColor(load), 18);
        DrawCenteredText(graphics, $"{load:0}%", valueFont, white, loadCenter.X, loadCenter.Y - valueFont.Height / 2f);
        DrawCenteredText(graphics, "Load", detailFont, muted, loadCenter.X, loadCenter.Y + radius - 18);
        if (singleTarget)
        {
            Point temperatureCenter = new(bounds.Right - radius - 54, bounds.Y + radius + gaugeCenterOffsetY);
            DrawGauge(graphics, temperatureCenter, radius, temperature, GetTemperatureGaugeColor(temperature), 18);
            DrawCenteredText(graphics, $"{temperature:0} °C", valueFont, white, temperatureCenter.X, temperatureCenter.Y - valueFont.Height / 2f);
            DrawCenteredText(graphics, "Temp", detailFont, muted, temperatureCenter.X, temperatureCenter.Y + radius - 18);
        }
        int metricX = bounds.X + 10;
        int metricWidth = bounds.Width - 20;
        int metricStep = detailFont.Height + 8;
        int metricsTop = bounds.Bottom - metricStep * 2 - 10;
        DrawSmallMetric(graphics, metricX, metricsTop, metricWidth, "Temp", $"{temperature:0} °C", temperature / 100, GetTemperatureGaugeColor(temperature), detailFont, white, muted);
        DrawSmallMetric(graphics, metricX, metricsTop + metricStep, metricWidth, "Clock", $"{clock:0} MHz", clock / 6000, accentColor, detailFont, white, muted);
    }

    private void DrawTwoByThreeValueTile(Graphics graphics, Rectangle bounds, string title, string value, string detail)
    {
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        using Font titleFont = new("Segoe UI", Math.Max(9, bounds.Height / 10), FontStyle.Bold);
        using Font valueFont = new("Segoe UI", Math.Max(15, bounds.Height / 4), FontStyle.Bold);
        using Font detailFont = new("Segoe UI", Math.Max(8, bounds.Height / 13));
        DrawCardFrame(graphics, bounds, accentColor);
        graphics.DrawString(title, titleFont, white, bounds.X + 8, bounds.Y + 7);
        graphics.DrawString(value, valueFont, white, bounds.X + 8, bounds.Y + bounds.Height / 3);
        graphics.DrawString(detail, detailFont, muted, new RectangleF(bounds.X + 8, bounds.Bottom - detailFont.Height - 8, bounds.Width - 16, detailFont.Height), new StringFormat { Trimming = StringTrimming.EllipsisCharacter });
    }

    private static double GetVramLoad(SensorSnapshot data)
    {
        double total = data.GpuMemoryTotalMegabytes > 0 ? data.GpuMemoryTotalMegabytes : 24576;
        return data.GpuMemoryMegabytes / total * 100;
    }

    private static void DrawSmallMetric(Graphics graphics, int x, int y, int width, string label, string value, double progress, Color progressColor, Font font, Brush white, Brush muted)
    {
        if (width <= 8)
            return;

        graphics.DrawString(label, font, muted, x, y);
        SizeF valueSize = graphics.MeasureString(value, font);
        graphics.DrawString(value, font, white, x + width - valueSize.Width, y);
        int barY = y + font.Height + 2;
        graphics.FillRectangle(new SolidBrush(Color.FromArgb(65, 73, 83)), x, barY, width, 4);
        float clamped = (float)Math.Max(0, Math.Min(1, progress));
        graphics.FillRectangle(new SolidBrush(progressColor), x, barY, width * clamped, 4);
    }

    private void DrawFiveByOnePanel(Graphics graphics, int width, int height, SensorSnapshot data, bool showTemperatureGauge)
    {
        using Font titleFont = new("Segoe UI", 12, FontStyle.Bold);
        using Font detailFont = new("Segoe UI", 9);
        using Font valueFont = new("Segoe UI", 16, FontStyle.Bold);
        int gap = 10;
        int margin = 8;
        int cardWidth = (width - margin * 2 - gap) / 2;
        Rectangle cpuBounds = new(margin, margin, cardWidth, height - margin * 2);
        Rectangle gpuBounds = new(margin + cardWidth + gap, margin, cardWidth, height - margin * 2);
        DrawFiveByOneCard(graphics, cpuBounds, "CPU", data.CpuName, data.CpuLoadPercent, data.CpuTemperatureCelsius, data.CpuClockMhz, data.CpuPowerWatts, showTemperatureGauge, titleFont, detailFont, valueFont);
        DrawFiveByOneCard(graphics, gpuBounds, "GPU", data.GpuName, data.GpuLoadPercent, data.GpuTemperatureCelsius, data.GpuClockMhz, data.GpuPowerWatts, showTemperatureGauge, titleFont, detailFont, valueFont);
    }

    private void DrawFiveByOneCard(Graphics graphics, Rectangle bounds, string label, string model, double load, double temperature, double clock, double power, bool showTemperatureGauge, Font titleFont, Font detailFont, Font valueFont)
    {
        DrawCardFrame(graphics, bounds, accentColor);
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        SizeF labelSize = graphics.MeasureString(label, titleFont);
        graphics.DrawString(label, titleFont, white, bounds.X + (bounds.Width - labelSize.Width) / 2, bounds.Y + 7);
        model = ShortenFiveByOneModel(label, model);
        using StringFormat centeredModel = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        bool modelBelowPower = WidgetSize.Width == 3 && WidgetSize.Height == 1;
        if (!modelBelowPower)
            graphics.DrawString(model, detailFont, muted, new RectangleF(bounds.X + 10, bounds.Y + 27, bounds.Width - 20, 14), centeredModel);
        int gaugeRadius = Math.Min(49, Math.Max(22, (int)Math.Round(bounds.Height / 3.0 * 1.44)));
        int gaugeWidth = Math.Min(20, Math.Max(10, (int)Math.Round(gaugeRadius * 20.0 / 49)));
        int gaugeCenterY = bounds.Y + bounds.Height / 2 + 2;
        Point loadCenter = new(bounds.X + gaugeRadius + 28, gaugeCenterY);
        Point temperatureCenter = new(bounds.Right - gaugeRadius - 20, gaugeCenterY);
        DrawGauge(graphics, loadCenter, gaugeRadius, load, GetGaugeColor(load), gaugeWidth);
        DrawCenteredText(graphics, $"{load:0}%", valueFont, white, loadCenter.X, loadCenter.Y - valueFont.Height / 2f);
        DrawCenteredText(graphics, "LOAD", detailFont, muted, loadCenter.X, loadCenter.Y + gaugeRadius - 7);
        int metricWidth = showTemperatureGauge ? 155 : 115;
        int metricX = showTemperatureGauge ? bounds.X + (bounds.Width - metricWidth) / 2 : bounds.Right - metricWidth - 6;
        if (showTemperatureGauge)
        {
            DrawGauge(graphics, temperatureCenter, gaugeRadius, temperature, GetTemperatureGaugeColor(temperature), gaugeWidth);
            DrawCenteredText(graphics, $"{temperature:0} °C", valueFont, white, temperatureCenter.X, temperatureCenter.Y - valueFont.Height / 2f);
            DrawCenteredText(graphics, "TEMP", detailFont, muted, temperatureCenter.X, temperatureCenter.Y + gaugeRadius - 7);
        }
        else
            DrawCompactMetric(graphics, metricX, bounds.Y + 25, "TEMP", $"{temperature:0} °C", temperature / 100, metricWidth);
        DrawCompactMetric(graphics, metricX, bounds.Y + 51, "CLOCK", $"{clock:0} MHz", clock / 6000, metricWidth);
        DrawCompactMetric(graphics, metricX, bounds.Y + 77, "POWER", $"{power:0} W", power / 300, metricWidth);
        if (modelBelowPower)
        {
            using Font modelFont = new("Segoe UI", 8);
            graphics.DrawString(model, modelFont, muted, new RectangleF(bounds.X + bounds.Width / 2f - 4, bounds.Bottom - 17, bounds.Width / 2f - 6, 14), centeredModel);
        }
    }

    private static string ShortenFiveByOneModel(string label, string model)
    {
        if (!string.Equals(label, "GPU", StringComparison.OrdinalIgnoreCase))
            return model;

        Match match = Regex.Match(model ?? string.Empty, @"NVIDIA\s+GeForce\s+RTX\s*(?<model>\d{3,4})", RegexOptions.IgnoreCase);
        return match.Success ? $"NVIDIA GeForce RTX {match.Groups["model"].Value}" : model;
    }

    private static void DrawCenteredText(Graphics graphics, string text, Font font, Brush brush, float centerX, float y)
    {
        SizeF size = graphics.MeasureString(text, font);
        graphics.DrawString(text, font, brush, centerX - size.Width / 2, y);
    }

    private void DrawCompactMetric(Graphics graphics, int x, int y, string label, string value, double progress, int width)
    {
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        graphics.DrawString(label, new Font("Segoe UI", 8), muted, x, y);
        graphics.DrawString(value, new Font("Segoe UI", 8), white, x + 42, y);
        double clamped = progress < 0 ? 0 : progress > 1 ? 1 : progress;
        graphics.FillRectangle(new SolidBrush(Color.FromArgb(65, 73, 83)), x, y + 14, width, 4);
        graphics.FillRectangle(new SolidBrush(accentColor), x, y + 14, (float)(width * clamped), 4);
    }

    private Color GetGaugeColor(double value)
    {
        if (value >= sharedGaugeCriticalThreshold)
            return sharedGaugeHighColor;
        if (value >= sharedGaugeWarningThreshold)
            return sharedGaugeMediumColor;
        return sharedGaugeLowColor;
    }

    private Color GetTemperatureGaugeColor(double value)
    {
        if (value >= sharedTemperatureCriticalThreshold)
            return sharedTemperatureHighColor;
        if (value >= sharedTemperatureWarningThreshold)
            return sharedTemperatureMediumColor;
        return sharedTemperatureLowColor;
    }

    private static void DrawGauge(Graphics graphics, Point center, int radius, double value, Color accent, int strokeWidth = 10)
    {
        float[] transform = graphics.Transform.Elements;
        float horizontalRadius = radius;
        if (Math.Abs(transform[0]) > 0.001f && Math.Abs(transform[3] - transform[0]) > 0.001f)
            horizontalRadius = radius * transform[3] / transform[0];

        using Pen backgroundPen = new(Color.FromArgb(70, 78, 88), strokeWidth);
        using Pen valuePen = new(accent, strokeWidth);
        graphics.DrawArc(backgroundPen, center.X - horizontalRadius, center.Y - radius, horizontalRadius * 2, radius * 2, 135, 270);
        double clampedValue = value < 0 ? 0 : value > 100 ? 100 : value;
        graphics.DrawArc(valuePen, center.X - horizontalRadius, center.Y - radius, horizontalRadius * 2, radius * 2, 135, (float)(270 * clampedValue / 100));
    }

    private static void DrawCardFrame(Graphics graphics, Rectangle bounds, Color accent)
    {
        using Pen border = new(accent, 2);
        using Brush background = new SolidBrush(Color.FromArgb(22, 25, 31));
        graphics.FillRectangle(background, bounds);
        graphics.DrawRectangle(border, bounds);
    }

    public static Bitmap CreatePreview(WidgetSize widgetSize)
    {
        using DemoSensorSource source = new();
        HwinfoPanelFactory factory = new();
        using HwinfoPanelWidget widget = new(factory, widgetSize, Guid.NewGuid(), source);
        Thread.Sleep(20);
        Bitmap preview;
        lock (widget.bitmapLock)
        {
            preview = new Bitmap(widget.bitmap);
        }
        return preview;
    }
}