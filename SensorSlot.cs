namespace HwinfoSensorPanel;

public enum PanelTarget
{
    Combined,
    Cpu,
    Gpu
}

public enum FiveByFourGaugeMode
{
    Load,
    Temperature,
    Combined
}

public enum HeaderTouchAction
{
    None,
    ToggleDisplay,
    Refresh,
    ExternalAction
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
    NetworkUpload,
    NetworkDownload
}