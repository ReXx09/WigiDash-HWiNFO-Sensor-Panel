using System;

namespace HwinfoSensorPanel;

public sealed class DemoSensorSource : ISensorSource
{
    private readonly DateTime started = DateTime.UtcNow;

    public SensorSnapshot Read()
    {
        double seconds = (DateTime.UtcNow - started).TotalSeconds;
        double wave = (Math.Sin(seconds * 0.9) + 1) / 2;

        return new SensorSnapshot
        {
            CpuLoadPercent = 6 + wave * 18,
            CpuTemperatureCelsius = 38 + wave * 6,
            CpuClockMhz = 4700 + wave * 300,
            CpuPowerWatts = 73 + wave * 20,
            GpuLoadPercent = 35 + wave * 42,
            GpuTemperatureCelsius = 48 + wave * 8,
            GpuClockMhz = 1920 + wave * 90,
            GpuPowerWatts = 238 + wave * 35,
            GpuMemoryMegabytes = 3307 + wave * 460,
            MemoryLoadPercent = 41 + wave * 4,
            MemoryUsedGigabytes = 13.4 + wave * 0.5,
            MemoryTotalGigabytes = 32,
            Fps = 118 + wave * 16,
            CpuFanRpm = 2036 + (int)(wave * 180),
            GpuFanRpm = 980 + (int)(wave * 240)
        };
    }

    public void Dispose()
    {
    }
}