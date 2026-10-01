using System;
using System.Collections.Generic;
using System.Drawing;
using WigiDashWidgetFramework;
using WigiDashWidgetFramework.WidgetUtility;

namespace HwinfoSensorPanel;

public sealed class HwinfoPanelFactory : IWidgetObject
{
    public Guid Guid => new("B4C9D6B1-3C75-4C27-8E8F-1D2DA1B8A4D3");
    public string Name => "HWiNFO Sensor Panel";
    public string Author => "by ReXx09";
    public string Website => "";
    public string Description => "Grafisches HW-Info Sensorpanel für das WigiDash.";
    public Version Version => new(0, 1, 0);
    public SdkVersion TargetSdk => WidgetUtility.CurrentSdkVersion;
    public List<WidgetSize> SupportedSizes => new()
    {
        new WidgetSize(1, 1), new WidgetSize(2, 1), new WidgetSize(3, 1), new WidgetSize(4, 1), new WidgetSize(5, 1),
        new WidgetSize(1, 2), new WidgetSize(2, 2), new WidgetSize(3, 2), new WidgetSize(4, 2), new WidgetSize(5, 2),
        new WidgetSize(1, 3), new WidgetSize(2, 3), new WidgetSize(3, 3), new WidgetSize(4, 3), new WidgetSize(5, 3),
        new WidgetSize(1, 4), new WidgetSize(2, 4), new WidgetSize(3, 4), new WidgetSize(4, 4), new WidgetSize(5, 4)
    };
    public Bitmap PreviewImage => HwinfoPanelWidget.CreatePreview(new WidgetSize(5, 4));
    public Bitmap WidgetThumbnail => HwinfoPanelWidget.CreatePreview(new WidgetSize(2, 2));
    public IWidgetManager WidgetManager { get; set; }
    public string LastErrorMessage { get; set; } = string.Empty;

    public Bitmap GetWidgetPreview(WidgetSize widgetSize) => HwinfoPanelWidget.CreatePreview(widgetSize);

    public IWidgetInstance CreateWidgetInstance(WidgetSize widgetSize, Guid instanceGuid)
    {
        ISensorSource source = WidgetManager == null ? new DemoSensorSource() : new ManagerSensorSource(WidgetManager);
        return new HwinfoPanelWidget(this, widgetSize, instanceGuid, source);
    }

    public bool RemoveWidgetInstance(Guid instanceGuid) => true;

    public WidgetError Load(string resourcePath) => WidgetError.NO_ERROR;

    public WidgetError Unload() => WidgetError.NO_ERROR;
}