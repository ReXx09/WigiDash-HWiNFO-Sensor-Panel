namespace HwinfoSensorPanel;

public sealed class SensorSnapshot
{
    public double CpuLoadPercent { get; set; }
    public double CpuTemperatureCelsius { get; set; }
    public double CpuClockMhz { get; set; }
    public double CpuPowerWatts { get; set; }
    public double GpuLoadPercent { get; set; }
    public double GpuTemperatureCelsius { get; set; }
    public double GpuClockMhz { get; set; }
    public double GpuPowerWatts { get; set; }
    public double GpuMemoryMegabytes { get; set; }
    public double MemoryLoadPercent { get; set; }
    public double MemoryUsedGigabytes { get; set; }
    public double MemoryTotalGigabytes { get; set; }
    public double Fps { get; set; }
    public int CpuFanRpm { get; set; }
    public int GpuFanRpm { get; set; }
}