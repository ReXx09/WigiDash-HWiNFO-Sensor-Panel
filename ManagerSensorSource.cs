using System;
using System.Collections.Generic;
using System.Linq;
using WigiDashWidgetFramework;

namespace HwinfoSensorPanel;

public sealed class ManagerSensorSource : ISensorSource
{
    private readonly IWidgetManager manager;
    private readonly Dictionary<SensorSlot, Guid> bindings = new();
    private readonly Dictionary<Guid, double> values = new();
    private readonly List<SensorItem> sensors = new();

    public ManagerSensorSource(IWidgetManager widgetManager)
    {
        manager = widgetManager;
        RefreshSensors();
        manager.SensorUpdated += Manager_SensorUpdated;
        BindDefaults();
    }

    public IReadOnlyList<SensorItem> Sensors => sensors;

    public Guid? GetBinding(SensorSlot slot)
    {
        return bindings.TryGetValue(slot, out Guid sensorGuid) ? sensorGuid : (Guid?)null;
    }

    public void RefreshSensors()
    {
        sensors.Clear();
        sensors.AddRange(manager.GetSensorList());
    }

    public bool Bind(SensorSlot slot, Guid sensorGuid)
    {
        SensorItem sensor = sensors.FirstOrDefault(item => item.Guid == sensorGuid);
        if (sensor == null)
            return false;

        bindings[slot] = sensorGuid;
        return manager.AddMonitoringItem(sensor);
    }

    public SensorSnapshot Read()
    {
        return new SensorSnapshot
        {
            CpuName = HardwareName("CPU", "CPU"),
            GpuName = HardwareName("GPU", "GPU"),
            MemoryName = HardwareName("Memory", "RAM"),
            CpuLoadPercent = ReadValue(SensorSlot.CpuLoad),
            CpuTemperatureCelsius = ReadValue(SensorSlot.CpuTemperature),
            CpuClockMhz = ReadValue(SensorSlot.CpuClock),
            CpuPowerWatts = ReadValue(SensorSlot.CpuPower),
            GpuLoadPercent = ReadValue(SensorSlot.GpuLoad),
            GpuTemperatureCelsius = ReadValue(SensorSlot.GpuTemperature),
            GpuClockMhz = ReadValue(SensorSlot.GpuClock),
            GpuPowerWatts = ReadValue(SensorSlot.GpuPower),
            GpuMemoryMegabytes = ReadValue(SensorSlot.GpuMemory),
            MemoryLoadPercent = ReadValue(SensorSlot.MemoryLoad),
            MemoryUsedGigabytes = ReadMemoryUsedGigabytes(),
            CpuFanRpm = (int)ReadValue(SensorSlot.CpuFan),
            GpuFanRpm = (int)ReadValue(SensorSlot.GpuFan),
            MemoryTotalGigabytes = 32
        };
    }

    public void Dispose()
    {
        manager.SensorUpdated -= Manager_SensorUpdated;
    }

    private void Manager_SensorUpdated(SensorItem item, double value)
    {
        values[item.Guid] = value;
    }

    private double ReadValue(SensorSlot slot)
    {
        return bindings.TryGetValue(slot, out Guid sensorGuid) && values.TryGetValue(sensorGuid, out double value) ? value : 0;
    }

    private void BindDefaults()
    {
        BindIfFound(SensorSlot.CpuLoad, "CPU", "Total", "%");
        BindIfFound(SensorSlot.CpuTemperature, "CPU", "Package", "°C");
        BindIfFound(SensorSlot.CpuClock, "CPU", "Core Clock", "MHz");
        BindIfFound(SensorSlot.CpuPower, "CPU", "Package Power", "W");
        BindIfFound(SensorSlot.GpuLoad, "GPU", "Utilization", "%");
        BindIfFound(SensorSlot.GpuTemperature, "GPU", "Temperature", "°C");
        BindIfFound(SensorSlot.GpuClock, "GPU", "Clock", "MHz");
        BindIfFound(SensorSlot.GpuPower, "GPU", "Power", "W");
        BindIfFound(SensorSlot.GpuMemory, "GPU", "Memory Usage", "MB");
        BindIfFound(SensorSlot.MemoryLoad, "Memory", "Load", "%");
        BindIfFound(SensorSlot.MemoryUsed, "Memory", "Used");
        BindIfFound(SensorSlot.CpuFan, "CPU", "Fan");
        BindIfFound(SensorSlot.GpuFan, "GPU", "Fan");
    }

    private void BindIfFound(SensorSlot slot, params string[] terms)
    {
        SensorItem sensor = sensors.FirstOrDefault(item =>
            terms.All(term => (item.Name ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                              (item.Source ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                              (item.Unit ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
        if (sensor != null)
            Bind(slot, sensor.Guid);
    }

    private double ReadMemoryUsedGigabytes()
    {
        double value = ReadValue(SensorSlot.MemoryUsed);
        SensorItem sensor = GetBoundSensor(SensorSlot.MemoryUsed);
        return sensor != null && string.Equals(sensor.Unit, "GB", StringComparison.OrdinalIgnoreCase) ? value : value / 1024.0;
    }

    private SensorItem GetBoundSensor(SensorSlot slot)
    {
        return bindings.TryGetValue(slot, out Guid sensorGuid)
            ? sensors.FirstOrDefault(sensor => sensor.Guid == sensorGuid)
            : null;
    }

    private string HardwareName(string sourceTerm, string fallback)
    {
        SensorItem sensor = sensors.FirstOrDefault(item =>
            (item.Source ?? string.Empty).IndexOf(sourceTerm, StringComparison.OrdinalIgnoreCase) >= 0);
        if (sensor == null || string.IsNullOrWhiteSpace(sensor.Source))
            return fallback;

        int separator = sensor.Source.IndexOf(':');
        return separator >= 0 ? sensor.Source.Substring(separator + 1).Trim() : sensor.Source.Trim();
    }
}