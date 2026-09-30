using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading;
using System.Windows.Controls;
using WigiDashWidgetFramework;
using WigiDashWidgetFramework.WidgetUtility;

namespace HwinfoSensorPanel;

public sealed class HwinfoPanelWidget : IWidgetInstance
{
    private const float ReferenceWidth = 1016f;
    private const float ReferenceHeight = 592f;
    private readonly HwinfoPanelFactory factory;
    private readonly ISensorSource sensorSource;
    private readonly object bitmapLock = new();
    private readonly object drawLock = new();
    private readonly Thread drawThread;
    private volatile bool running = true;
    private readonly int bitmapWidth;
    private readonly int bitmapHeight;
    private Bitmap bitmap;
    private Color accentColor = Color.FromArgb(230, 35, 38);
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
        if (drawThread.IsAlive) drawThread.Join(500);
        sensorSource.Dispose();
        lock (bitmapLock)
        {
            bitmap.Dispose();
        }
    }

    private void DrawLoop()
    {
        while (running)
        {
            Draw(sensorSource.Read());
            PublishBitmap();
            Thread.Sleep(updateIntervalMilliseconds);
        }
    }

    private void LoadSettings()
    {
        if (factory.WidgetManager == null)
            return;

        if (factory.WidgetManager.LoadSetting(this, "AccentColor", out string savedColor))
            accentColor = ColorTranslator.FromHtml(savedColor);

        if (factory.WidgetManager.LoadSetting(this, "UpdateInterval", out string savedInterval) &&
            int.TryParse(savedInterval, out int interval))
            updateIntervalMilliseconds = interval < 100 ? 100 : interval > 2000 ? 2000 : interval;

        if (factory.WidgetManager.LoadSetting(this, "PanelTarget", out string savedTarget) &&
            Enum.TryParse(savedTarget, out PanelTarget target))
            panelTarget = target;
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
                graphics.ScaleTransform(next.Width / ReferenceWidth, next.Height / ReferenceHeight);
                graphics.Clear(Color.FromArgb(12, 14, 18));
                DrawHeader(graphics, titleFont, detailFont, data, accentColor);

                if (WidgetSize.Width <= 2 && WidgetSize.Height <= 2)
                    DrawCompactPanel(graphics, next.Width, next.Height, data, titleFont, valueFont, detailFont);
                else
                {
                    int margin = 12;
                    int gap = 12;
                    int top = 72;
                    int largeWidth = (next.Width - margin * 2 - gap) / 2;
                    int largeHeight = 220;
                    DrawCoreCard(graphics, new Rectangle(margin, top, largeWidth, largeHeight), "CPU", data.CpuName, data.CpuLoadPercent, data.CpuTemperatureCelsius, data.CpuClockMhz, data.CpuPowerWatts, accentColor, titleFont, valueFont, detailFont);
                    DrawCoreCard(graphics, new Rectangle(margin + largeWidth + gap, top, largeWidth, largeHeight), "GPU", data.GpuName, data.GpuLoadPercent, data.GpuTemperatureCelsius, data.GpuClockMhz, data.GpuPowerWatts, accentColor, titleFont, valueFont, detailFont);

                    int bottomTop = top + largeHeight + gap;
                    int smallWidth = (next.Width - margin * 2 - gap * 2) / 3;
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

    private static void DrawCoreCard(Graphics graphics, Rectangle bounds, string label, string model, double load, double temperature, double clock, double power, Color accent, Font titleFont, Font valueFont, Font detailFont)
    {
        DrawCardFrame(graphics, bounds, accent);
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        using Brush accentBrush = new SolidBrush(accent);
        graphics.DrawString(label, titleFont, white, bounds.X + 18, bounds.Y + 14);
        graphics.DrawString(model, detailFont, muted, bounds.X + 18, bounds.Y + 43);
        DrawGauge(graphics, new Point(bounds.X + 100, bounds.Y + 135), 58, load, accent);
        graphics.DrawString($"{load:0}%", valueFont, white, bounds.X + 72, bounds.Y + 112);
        graphics.DrawString("Load", detailFont, muted, bounds.X + 87, bounds.Y + 148);
        DrawMetric(graphics, bounds.X + 200, bounds.Y + 82, "Temperature", $"{temperature:0} °C", accentBrush, detailFont, white);
        DrawMetric(graphics, bounds.X + 200, bounds.Y + 121, "Clock", $"{clock:0} MHz", accentBrush, detailFont, white);
        DrawMetric(graphics, bounds.X + 200, bounds.Y + 160, "Power", $"{power:0} W", accentBrush, detailFont, white);
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

    private static void DrawMetric(Graphics graphics, int x, int y, string label, string value, Brush accent, Font detailFont, Brush white)
    {
        using Brush muted = new SolidBrush(Color.FromArgb(160, 170, 182));
        graphics.DrawString(label, detailFont, muted, x, y);
        graphics.DrawString(value, detailFont, white, x + 108, y);
        graphics.FillRectangle(accent, x, y + 22, 220, 4);
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
        int centerY = height / 2;
        DrawGauge(graphics, new Point(105, centerY), 62, load, accentColor);
        DrawGauge(graphics, new Point(width - 105, centerY), 62, temperature / 100 * 100, accentColor);
        using Brush white = new SolidBrush(Color.White);
        using Brush muted = new SolidBrush(Color.FromArgb(165, 175, 188));
        graphics.DrawString(label, titleFont, white, 20, 18);
        graphics.DrawString(model, detailFont, muted, 20, 46);
        graphics.DrawString($"{load:0}%", valueFont, white, 72, centerY - 18);
        graphics.DrawString("LOAD", detailFont, muted, 78, centerY + 24);
        graphics.DrawString($"{temperature:0} °C", valueFont, white, width - 154, centerY - 18);
        graphics.DrawString("TEMP", detailFont, muted, width - 145, centerY + 24);
        graphics.DrawString($"Clock  {clock:0} MHz", detailFont, white, width / 2 - 90, centerY - 18);
        graphics.DrawString($"Power  {power:0} W", detailFont, white, width / 2 - 90, centerY + 12);
    }

    private static void DrawGauge(Graphics graphics, Point center, int radius, double value, Color accent)
    {
        using Pen backgroundPen = new(Color.FromArgb(70, 78, 88), 10);
        using Pen valuePen = new(accent, 10);
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