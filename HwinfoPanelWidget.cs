using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Controls;
using WigiDashWidgetFramework;
using WigiDashWidgetFramework.WidgetUtility;

namespace HwinfoSensorPanel;

public sealed class HwinfoPanelWidget : IWidgetInstance
{
    private const float ReferenceWidth = 1016f;
    private const float ReferenceHeight = 592f;
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
    private readonly HwinfoPanelFactory factory;
    private readonly ISensorSource sensorSource;
    private readonly object bitmapLock = new();
    private readonly object drawLock = new();
    private readonly AutoResetEvent stopEvent = new(false);
    private readonly Thread drawThread;
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
    private int updateIntervalMilliseconds = 250;
    private PanelTarget panelTarget = PanelTarget.Combined;

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
        lock (sharedGaugeSettingsLock)
            sharedGaugeWidgets.Add(this);
        LoadSensorBindings();
        drawThread = new Thread(DrawLoop) { IsBackground = true };
        drawThread.Start();
    }

    public IWidgetObject WidgetObject => factory;
    public Guid Guid { get; }
    public WidgetSize WidgetSize { get; }
    public event WidgetUpdatedEventHandler WidgetUpdated;

    public void RequestUpdate() => PublishBitmap();

    public void ClickEvent(ClickType clickType, int x, int y)
    {
    }

    public UserControl GetSettingsControl() => new HwinfoPanelSettings(this);

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
    public int UpdateIntervalMilliseconds => updateIntervalMilliseconds;
    public PanelTarget PanelTarget => panelTarget;
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

    public void SetPanelTarget(PanelTarget target)
    {
        panelTarget = target;
        factory.WidgetManager?.StoreSetting(this, "PanelTarget", target.ToString());
        RequestUpdate();
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
        if (drawThread.IsAlive) drawThread.Join();
        sensorSource.Dispose();
        lock (bitmapLock)
        {
            bitmap.Dispose();
        }
        lock (sharedGaugeSettingsLock)
            sharedGaugeWidgets.Remove(this);
        stopEvent.Dispose();
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

        if (factory.WidgetManager.LoadSetting(this, "PanelTarget", out string savedTarget) &&
            Enum.TryParse(savedTarget, out PanelTarget target))
            panelTarget = target;

        if (factory.WidgetManager.LoadSetting(this, "OneByOneShowsTemperature", out string savedOneByOneShowsTemperature) &&
            bool.TryParse(savedOneByOneShowsTemperature, out bool showsTemperature))
            oneByOneShowsTemperature = showsTemperature;

        if (factory.WidgetManager.LoadSetting(this, "FiveByFourGaugeMode", out string savedFiveByFourGaugeMode) &&
            Enum.TryParse(savedFiveByFourGaugeMode, out FiveByFourGaugeMode gaugeMode))
            fiveByFourGaugeMode = gaugeMode;
        else if (factory.WidgetManager.LoadSetting(this, "FiveByFourShowsTemperature", out string savedFiveByFourShowsTemperature) &&
                 bool.TryParse(savedFiveByFourShowsTemperature, out bool fiveByFourShowsTemperatureValue))
            fiveByFourGaugeMode = fiveByFourShowsTemperatureValue ? FiveByFourGaugeMode.Temperature : FiveByFourGaugeMode.Load;
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
                else
                {
                    float referenceHeight = WidgetSize.Height >= 4 ? ReferenceHeight : 447f;
                    graphics.ScaleTransform(next.Width / ReferenceWidth, next.Height / referenceHeight);
                    DrawHeader(graphics, titleFont, detailFont, data, accentColor);

                    int margin = 12;
                    int gap = 12;
                    int top = 72;
                    int largeWidth = ((int)ReferenceWidth - margin * 2 - gap) / 2;
                    int largeHeight = 220;
                    DrawCoreCard(graphics, new Rectangle(margin, top, largeWidth, largeHeight), "CPU", data.CpuName, data.CpuLoadPercent, data.CpuTemperatureCelsius, data.CpuClockMhz, data.CpuPowerWatts, accentColor, titleFont, valueFont, detailFont);
                    DrawCoreCard(graphics, new Rectangle(margin + largeWidth + gap, top, largeWidth, largeHeight), "GPU", data.GpuName, data.GpuLoadPercent, data.GpuTemperatureCelsius, data.GpuClockMhz, data.GpuPowerWatts, accentColor, titleFont, valueFont, detailFont);

                    int bottomTop = top + largeHeight + gap;
                    int smallWidth = ((int)ReferenceWidth - margin * 2 - gap * 2) / 3;
                    DrawMemoryCard(graphics, new Rectangle(margin, bottomTop, smallWidth, 135), data, accentColor, titleFont, detailFont);
                    DrawFpsCard(graphics, new Rectangle(margin + smallWidth + gap, bottomTop, smallWidth, 135), data, accentColor, titleFont, detailFont);
                    DrawLogoCard(graphics, new Rectangle(margin + (smallWidth + gap) * 2, bottomTop, smallWidth, 135), accentColor, titleFont, detailFont);

                    if (WidgetSize.Width >= 5 && WidgetSize.Height >= 4)
                    {
                        int infoTop = bottomTop + 135 + gap;
                        DrawInfoCard(graphics, new Rectangle(margin, infoTop, smallWidth, 120), "CPU FAN", $"{data.CpuFanRpm:0} RPM", accentColor, titleFont, detailFont);
                        DrawInfoCard(graphics, new Rectangle(margin + smallWidth + gap, infoTop, smallWidth, 120), "GPU FAN", $"{data.GpuFanRpm:0} RPM", accentColor, titleFont, detailFont);
                        DrawInfoCard(graphics, new Rectangle(margin + (smallWidth + gap) * 2, infoTop, smallWidth, 120), "RAM USED", $"{data.MemoryUsedGigabytes:0.0} / {data.MemoryTotalGigabytes:0} GB", accentColor, titleFont, detailFont);
                    }
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

    private static void DrawHeader(Graphics graphics, Font titleFont, Font detailFont, SensorSnapshot data, Color accent)
    {
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(170, 178, 190));
        graphics.DrawString($"CPU   {data.CpuFanRpm} RPM     GPU   {data.GpuFanRpm} RPM", detailFont, muted, 18, 16);
        graphics.DrawString("HWiNFO SENSOR PANEL", titleFont, white, 18, 37);
        graphics.DrawString(DateTime.Now.ToString("HH:mm:ss"), detailFont, muted, 760, 22);
        graphics.DrawString("LIVE", detailFont, new SolidBrush(accent), 760, 46);
    }

    private void DrawCoreCard(Graphics graphics, Rectangle bounds, string label, string model, double load, double temperature, double clock, double power, Color accent, Font titleFont, Font valueFont, Font detailFont)
    {
        DrawCardFrame(graphics, bounds, accent);
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        using Brush accentBrush = new SolidBrush(accent);
        graphics.DrawString(label, titleFont, white, bounds.X + 18, bounds.Y + 14);
        model = ShortenFiveByOneModel(label, model);
        graphics.DrawString(model, detailFont, muted, bounds.X + 18, bounds.Y + 43);
        bool isFiveByFour = WidgetSize.Width == 5 && WidgetSize.Height == 4;
        int gaugeRadius = isFiveByFour ? 60 : 58;
        int gaugeWidth = isFiveByFour ? 22 : 10;
        if (isFiveByFour && fiveByFourGaugeMode == FiveByFourGaugeMode.Combined)
        {
            Point loadCenter = new(bounds.X + 75, bounds.Y + 135);
            Point temperatureCenter = new(bounds.X + 365, bounds.Y + 135);
            DrawGauge(graphics, loadCenter, gaugeRadius, load, GetGaugeColor(load), gaugeWidth);
            DrawGauge(graphics, temperatureCenter, gaugeRadius, temperature, GetTemperatureGaugeColor(temperature), gaugeWidth);
            DrawCenteredText(graphics, $"{load:0}%", valueFont, white, loadCenter.X, bounds.Y + 112);
            DrawCenteredText(graphics, "Load", detailFont, muted, loadCenter.X, bounds.Y + 148);
            DrawCenteredText(graphics, $"{temperature:0} °C", valueFont, white, temperatureCenter.X, bounds.Y + 112);
            DrawCenteredText(graphics, "Temperature", detailFont, muted, temperatureCenter.X, bounds.Y + 148);
            DrawMetric(graphics, bounds.X + 155, bounds.Y + 102, "Clock", $"{clock:0} MHz", clock / 6000, accentBrush, detailFont, white, 145);
            DrawMetric(graphics, bounds.X + 155, bounds.Y + 151, "Power", $"{power:0} W", power / 300, accentBrush, detailFont, white, 145);
            return;
        }

        bool showTemperatureGauge = isFiveByFour && fiveByFourGaugeMode == FiveByFourGaugeMode.Temperature;
        double gaugeValue = showTemperatureGauge ? temperature : load;
        Color gaugeColor = showTemperatureGauge ? GetTemperatureGaugeColor(temperature) : GetGaugeColor(load);
        string gaugeText = showTemperatureGauge ? $"{temperature:0} °C" : $"{load:0}%";
        string gaugeLabel = showTemperatureGauge ? "Temperature" : "Load";
        Point gaugeCenter = new(bounds.X + 100, bounds.Y + 135);
        DrawGauge(graphics, gaugeCenter, gaugeRadius, gaugeValue, gaugeColor, gaugeWidth);
        DrawCenteredText(graphics, gaugeText, valueFont, white, gaugeCenter.X, bounds.Y + 112);
        DrawCenteredText(graphics, gaugeLabel, detailFont, muted, gaugeCenter.X, bounds.Y + 148);
        DrawMetric(graphics, bounds.X + 200, bounds.Y + 82, "Temperature", $"{temperature:0} °C", temperature / 100, accentBrush, detailFont, white);
        DrawMetric(graphics, bounds.X + 200, bounds.Y + 121, "Clock", $"{clock:0} MHz", clock / 6000, accentBrush, detailFont, white);
        DrawMetric(graphics, bounds.X + 200, bounds.Y + 160, "Power", $"{power:0} W", power / 300, accentBrush, detailFont, white);
    }

    private static void DrawMemoryCard(Graphics graphics, Rectangle bounds, SensorSnapshot data, Color accent, Font titleFont, Font detailFont)
    {
        DrawCardFrame(graphics, bounds, accent);
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        graphics.DrawString(data.MemoryName, titleFont, white, bounds.X + 14, bounds.Y + 12);
        graphics.DrawString($"Load                         {data.MemoryLoadPercent:0}%", detailFont, muted, bounds.X + 14, bounds.Y + 50);
        graphics.DrawString($"Used  {data.MemoryUsedGigabytes:0.0} GB / {data.MemoryTotalGigabytes:0} GB", detailFont, muted, bounds.X + 14, bounds.Y + 76);
        graphics.DrawString("38-38-38-77 CR2", detailFont, muted, bounds.X + 14, bounds.Y + 102);
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

    private static void DrawLogoCard(Graphics graphics, Rectangle bounds, Color accent, Font titleFont, Font detailFont)
    {
        DrawCardFrame(graphics, bounds, accent);
        using Brush white = new SolidBrush(Color.White);
        using Brush red = new SolidBrush(accent);
        graphics.DrawString("WIGIDASH", titleFont, white, bounds.X + 20, bounds.Y + 24);
        graphics.DrawString("HWiNFO", detailFont, red, bounds.X + 20, bounds.Y + 62);
        graphics.DrawString("CUSTOM PANEL", detailFont, white, bounds.X + 20, bounds.Y + 90);
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

    private static void DrawMetric(Graphics graphics, int x, int y, string label, string value, double progress, Brush accent, Font detailFont, Brush white, int width = 220)
    {
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        graphics.DrawString(label, detailFont, muted, x, y);
        graphics.DrawString(value, detailFont, white, x + 108, y);
        double clampedProgress = progress < 0 ? 0 : progress > 1 ? 1 : progress;
        graphics.FillRectangle(new SolidBrush(Color.FromArgb(65, 73, 83)), x, y + 22, width, 4);
        graphics.FillRectangle(accent, x, y + 22, (float)(width * clampedProgress), 4);
    }

    private void DrawCompactPanel(Graphics graphics, int width, int height, SensorSnapshot data, Font titleFont, Font valueFont, Font detailFont)
    {
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
            int radius = isOneByOne || isTwoByOne ? 49 : Math.Max(24, Math.Min(width, height) / 4);
            int gaugeWidth = isOneByOne || isTwoByOne ? 20 : 10;
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
        int radiusDual = isTwoByOneLayout ? 49 : Math.Max(30, Math.Min(58, Math.Min(width / 5, height / 2 - 18)));
        int gaugeWidthDual = isTwoByOneLayout ? 20 : 10;
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
        graphics.DrawString($"Clock {clock:0} MHz", detailFont, white, width / 2 - 64, height - 42);
        graphics.DrawString($"Power {power:0} W", detailFont, white, width / 2 - 58, height - 22);
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
        using Pen backgroundPen = new(Color.FromArgb(70, 78, 88), strokeWidth);
        using Pen valuePen = new(accent, strokeWidth);
        graphics.DrawArc(backgroundPen, center.X - radius, center.Y - radius, radius * 2, radius * 2, 135, 270);
        double clampedValue = value < 0 ? 0 : value > 100 ? 100 : value;
        graphics.DrawArc(valuePen, center.X - radius, center.Y - radius, radius * 2, radius * 2, 135, (float)(270 * clampedValue / 100));
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