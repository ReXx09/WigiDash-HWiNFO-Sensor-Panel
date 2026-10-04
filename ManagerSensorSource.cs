using System;
using System.Collections.Generic;
using System.Linq;
using WigiDashWidgetFramework;

namespace HwinfoSensorPanel;

public sealed class ManagerSensorSource : ISensorSource
{
    private readonly IWidgetManager manager;
    private readonly object stateLock = new();
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

    public IReadOnlyList<SensorItem> Sensors
    {
        get
        {
            lock (stateLock)
                return sensors.ToArray();
        }
    }

    public Guid? GetBinding(SensorSlot slot)
    {
        lock (stateLock)
            return bindings.TryGetValue(slot, out Guid sensorGuid) ? sensorGuid : (Guid?)null;
    }

    public void RefreshSensors()
    {
        List<SensorItem> refreshed = manager.GetSensorList().ToList();
        lock (stateLock)
        {
            sensors.Clear();
            sensors.AddRange(refreshed);
        }
    }

    public bool Bind(SensorSlot slot, Guid sensorGuid)
    {
        SensorItem sensor;
        lock (stateLock)
            sensor = sensors.FirstOrDefault(item => item.Guid == sensorGuid);
        if (sensor == null)
            return false;

        bool added = manager.AddMonitoringItem(sensor);
        if (added)
        {
            lock (stateLock)
                bindings[slot] = sensorGuid;
        }

        return added;
    }

    public bool IsCompatible(SensorSlot slot, Guid sensorGuid)
    {
        lock (stateLock)
        {
            SensorItem sensor = sensors.FirstOrDefault(item => item.Guid == sensorGuid);
            return sensor != null && ScoreSensor(sensor, slot) > 0;
        }
    }

    public SensorSnapshot Read()
    {
        lock (stateLock)
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
                GpuMemoryTotalMegabytes = 24576,
                MemoryLoadPercent = ReadValue(SensorSlot.MemoryLoad),
                MemoryUsedGigabytes = ReadMemoryUsedGigabytes(),
                CpuFanRpm = (int)ReadValue(SensorSlot.CpuFan),
                GpuFanRpm = (int)ReadValue(SensorSlot.GpuFan),
                MemoryTotalGigabytes = 32,
                MemoryClockMhz = ReadValue(SensorSlot.MemoryClock),
                NetworkUploadMegabytesPerSecond = ReadValue(SensorSlot.NetworkUpload),
                NetworkDownloadMegabytesPerSecond = ReadValue(SensorSlot.NetworkDownload)
            };
        }
    }

    public void Dispose()
    {
        manager.SensorUpdated -= Manager_SensorUpdated;
    }

    private void Manager_SensorUpdated(SensorItem item, double value)
    {
        lock (stateLock)
            values[item.Guid] = value;
    }

    private double ReadValue(SensorSlot slot)
    {
        return bindings.TryGetValue(slot, out Guid sensorGuid) && values.TryGetValue(sensorGuid, out double value) ? value : 0;
    }

    private void BindDefaults()
    {
        foreach (SensorSlot slot in Enum.GetValues(typeof(SensorSlot)))
        {
            SensorItem sensor = FindBestSensor(slot);
            if (sensor != null)
                Bind(slot, sensor.Guid);
        }
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

    private SensorItem FindBestSensor(SensorSlot slot)
    {
        return sensors
            .Select(sensor => new { Sensor = sensor, Score = ScoreSensor(sensor, slot) })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .Select(candidate => candidate.Sensor)
            .FirstOrDefault();
    }

    private static int ScoreSensor(SensorItem sensor, SensorSlot slot)
    {
        string source = sensor.Source ?? string.Empty;
        string name = sensor.Name ?? string.Empty;
        string unit = sensor.Unit ?? string.Empty;
        string text = $"{source} {name}";
        bool cpu = text.IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Processor", StringComparison.OrdinalIgnoreCase) >= 0;
        bool gpu = text.IndexOf("GPU", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("GeForce", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0;
        bool memory = text.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("RAM", StringComparison.OrdinalIgnoreCase) >= 0;
        Func<string, bool> has = value => text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        Func<string, bool> unitIs = value => string.Equals(unit, value, StringComparison.OrdinalIgnoreCase);

        int score = 0;
        if (slot == SensorSlot.CpuFan)
        {
            if (!gpu && (has("Fan") || has("Pump") || has("CPU OPT"))) score += 35;
            if (unitIs("RPM")) score += 30;
            if (has("CPU")) score += 20;
            return score;
        }

        if (slot.ToString().StartsWith("Cpu", StringComparison.OrdinalIgnoreCase) && cpu && !gpu) score += 20;
        if (slot.ToString().StartsWith("Gpu", StringComparison.OrdinalIgnoreCase) && gpu) score += 20;
        if (slot.ToString().StartsWith("Memory", StringComparison.OrdinalIgnoreCase) && memory && !cpu && !gpu) score += 20;

        switch (slot)
        {
            case SensorSlot.CpuLoad:
            case SensorSlot.GpuLoad:
            case SensorSlot.MemoryLoad:
                if (unitIs("%")) score += 30;
                if (has("Load") || has("Usage") || has("Utilization")) score += 20;
                if (has("Clock") || has("Temperature")) score -= 40;
                break;
            case SensorSlot.CpuTemperature:
            case SensorSlot.GpuTemperature:
                if (unit.IndexOf("C", StringComparison.OrdinalIgnoreCase) >= 0) score += 30;
                if (has("Temperature") || has("Package") || has("Tdie")) score += 20;
                break;
            case SensorSlot.CpuClock:
            case SensorSlot.GpuClock:
            case SensorSlot.MemoryClock:
                if (unitIs("MHz")) score += 30;
                if (has("Clock") || has("Frequency")) score += 20;
                if (has("Memory")) score -= 15;
                break;
            case SensorSlot.CpuPower:
            case SensorSlot.GpuPower:
                if (unitIs("W")) score += 30;
                if (has("Power") || has("Package")) score += 20;
                break;
            case SensorSlot.GpuMemory:
                if (unitIs("MB") || unitIs("GB")) score += 30;
                if (has("Memory") || has("VRAM")) score += 20;
                break;
            case SensorSlot.MemoryUsed:
                if (unitIs("MB") || unitIs("GB")) score += 30;
                if (has("Used") || has("Usage")) score += 20;
                if (has("Clock") || has("Timing")) score -= 50;
                break;
            case SensorSlot.GpuFan:
                if (unitIs("RPM")) score += 30;
                if (has("Fan") || has("Pump")) score += 20;
                break;
            case SensorSlot.NetworkUpload:
                if (has("Upload") || has("Sent") || has("Transmit") || has("Tx")) score += 40;
                if (has("Network") || has("Ethernet") || has("Wi-Fi") || has("WiFi")) score += 20;
                break;
            case SensorSlot.NetworkDownload:
                if (has("Download") || has("Received") || has("Receive") || has("Rx")) score += 40;
                if (has("Network") || has("Ethernet") || has("Wi-Fi") || has("WiFi")) score += 20;
                break;
        }

        return score;
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