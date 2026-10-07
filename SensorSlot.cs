namespace HwinfoSensorPanel;

public enum PanelTarget
{
    Combined,
    Cpu,
    Gpu,
    Storage
}

public enum FiveByFourGaugeMode
{
    Load,
    Temperature,
    Combined
}

public enum TwoByThreeLayoutMode
{
    Balanced,
    Gauges,
    Minimal
}

public enum MemoryGaugeAlignment
{
    Right,
    Left
}

public enum HeaderTouchAction
{
    None,
    ToggleDisplay,
    Refresh,
    ExternalAction
}

public enum PanelPage
{
    Hardware,
    Home
}

public enum HomeTileType
{
    Cpu,
    Gpu,
    Ram,
    Vram,
    Network,
    Fans,
    Fps,
    Empty,
    CustomAction,
    WebLink
}

public enum HomeButtonTarget
{
    Home,
    Hardware,
    MemoryNetwork,
    Actions,
    Info,
    Discord,
    Empty
}

public enum SensorSlot
{
    CpuLoad,
    CpuTemperature,
    CpuClock,
    CpuPower,
    GpuLoad,
    GpuTemperature,
    GpuClock,
    GpuPower,
    GpuMemory,
    CpuFan,
    GpuFan,
    MemoryLoad,
    MemoryUsed,
    MemoryClock,
    NetworkUpload,
    NetworkDownload,
    DriveCTemperature,
    DriveDTemperature,
    DriveETemperature,
    DriveFTemperature,
    DriveGTemperature,
    DriveHTemperature,
    DriveITemperature,
    DriveJTemperature
}