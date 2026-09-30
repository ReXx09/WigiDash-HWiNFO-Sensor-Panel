using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;
using WigiDashWidgetFramework;
using DrawingColor = System.Drawing.Color;

namespace HwinfoSensorPanel;

public sealed class HwinfoPanelSettings : UserControl
{
    private readonly HwinfoPanelWidget widget;
    private readonly ComboBox colorSelector;
    private readonly Slider intervalSlider;
    private readonly TextBlock intervalValue;

    public HwinfoPanelSettings(HwinfoPanelWidget parent)
    {
        widget = parent;
        StackPanel panel = new() { Margin = new Thickness(12) };
        panel.Children.Add(new TextBlock { Text = "HWiNFO Sensor Panel", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 14) });

        panel.Children.Add(new TextBlock { Text = "Akzentfarbe", Margin = new Thickness(0, 0, 0, 4) });
        colorSelector = new ComboBox { Width = 180 };
        colorSelector.Items.Add("Rot");
        colorSelector.Items.Add("Blau");
        colorSelector.Items.Add("Grün");
        colorSelector.SelectedIndex = ColorIndex(widget.AccentColor);
        colorSelector.SelectionChanged += ColorSelector_SelectionChanged;
        panel.Children.Add(colorSelector);

        panel.Children.Add(new TextBlock { Text = "Aktualisierungsintervall", Margin = new Thickness(0, 16, 0, 4) });
        intervalSlider = new Slider { Minimum = 100, Maximum = 2000, TickFrequency = 100, IsSnapToTickEnabled = true, Value = widget.UpdateIntervalMilliseconds };
        intervalSlider.ValueChanged += IntervalSlider_ValueChanged;
        panel.Children.Add(intervalSlider);
        intervalValue = new TextBlock { Margin = new Thickness(0, 4, 0, 0) };
        panel.Children.Add(intervalValue);

        panel.Children.Add(new TextBlock { Text = "HWiNFO-Sensoren", Margin = new Thickness(0, 18, 0, 8), FontWeight = FontWeights.Bold });
        AddSensorSelector(panel, "CPU-Last", SensorSlot.CpuLoad);
        AddSensorSelector(panel, "CPU-Temperatur", SensorSlot.CpuTemperature);
        AddSensorSelector(panel, "CPU-Clock", SensorSlot.CpuClock);
        AddSensorSelector(panel, "CPU-Power", SensorSlot.CpuPower);
        AddSensorSelector(panel, "CPU-Lüfter", SensorSlot.CpuFan);
        AddSensorSelector(panel, "GPU-Last", SensorSlot.GpuLoad);
        AddSensorSelector(panel, "GPU-Temperatur", SensorSlot.GpuTemperature);
        AddSensorSelector(panel, "GPU-Clock", SensorSlot.GpuClock);
        AddSensorSelector(panel, "GPU-Power", SensorSlot.GpuPower);
        AddSensorSelector(panel, "GPU-VRAM", SensorSlot.GpuMemory);
        AddSensorSelector(panel, "GPU-Lüfter", SensorSlot.GpuFan);
        AddSensorSelector(panel, "RAM-Last", SensorSlot.MemoryLoad);
        AddSensorSelector(panel, "RAM-Used", SensorSlot.MemoryUsed);

        Button updateButton = new() { Content = "Jetzt aktualisieren", Margin = new Thickness(0, 18, 0, 0), Padding = new Thickness(10, 5, 10, 5) };
        updateButton.Click += UpdateButton_Click;
        panel.Children.Add(updateButton);

        Content = panel;
        UpdateIntervalText();
    }

    private void ColorSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DrawingColor color = colorSelector.SelectedIndex switch
        {
            1 => DrawingColor.FromArgb(45, 145, 230),
            2 => DrawingColor.FromArgb(55, 190, 105),
            _ => DrawingColor.FromArgb(230, 35, 38)
        };
        widget.SetAccentColor(color);
    }

    private void IntervalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (intervalValue == null)
            return;

        widget.SetUpdateInterval((int)e.NewValue);
        UpdateIntervalText();
    }

    private void UpdateIntervalText()
    {
        intervalValue.Text = $"{(int)intervalSlider.Value} ms";
    }

    private void AddSensorSelector(StackPanel panel, string label, SensorSlot slot)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 6, 0, 2) });
        ComboBox selector = new() { Width = 280, Tag = slot, DisplayMemberPath = "Label" };
        List<SensorOption> options = widget.AvailableSensors
            .Where(sensor => MatchesSlot(sensor, slot))
            .Select(sensor => new SensorOption(sensor))
            .OrderBy(option => option.Label)
            .ToList();
        if (options.Count == 0)
        {
            options = widget.AvailableSensors
                .Select(sensor => new SensorOption(sensor))
                .OrderBy(option => option.Label)
                .ToList();
        }
        selector.ItemsSource = options;
        Guid? selectedGuid = widget.GetBoundSensor(slot);
        selector.SelectedItem = selectedGuid.HasValue
            ? options.FirstOrDefault(option => option.Sensor.Guid == selectedGuid.Value)
            : null;
        selector.SelectionChanged += SensorSelector_SelectionChanged;
        panel.Children.Add(selector);
    }

    private static bool MatchesSlot(SensorItem sensor, SensorSlot slot)
    {
        string source = sensor.Source ?? string.Empty;
        string name = sensor.Name ?? string.Empty;
        string text = $"{source} {name}";
        bool isCpu = text.IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     text.IndexOf("Processor", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isGpu = text.IndexOf("GPU", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     text.IndexOf("GeForce", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     text.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isMemory = text.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        text.IndexOf("RAM", StringComparison.OrdinalIgnoreCase) >= 0;

        if (slot == SensorSlot.CpuLoad || slot == SensorSlot.CpuTemperature || slot == SensorSlot.CpuClock ||
            slot == SensorSlot.CpuPower || slot == SensorSlot.CpuFan)
            return isCpu && !isGpu;

        if (slot == SensorSlot.GpuLoad || slot == SensorSlot.GpuTemperature || slot == SensorSlot.GpuClock ||
            slot == SensorSlot.GpuPower || slot == SensorSlot.GpuMemory || slot == SensorSlot.GpuFan)
            return isGpu;

        if (slot == SensorSlot.MemoryLoad || slot == SensorSlot.MemoryUsed)
            return isMemory && !isCpu && !isGpu;

        return true;
    }

    private void SensorSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox selector && selector.SelectedItem is SensorOption option && selector.Tag is SensorSlot slot)
            widget.BindSensor(slot, option.Sensor.Guid);
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        widget.UpdateNow();
    }

    private static int ColorIndex(DrawingColor color)
    {
        if (color.B > color.R && color.B > color.G)
            return 1;
        if (color.G > color.R && color.G > color.B)
            return 2;
        return 0;
    }

    private sealed class SensorOption
    {
        public SensorOption(SensorItem sensor)
        {
            Sensor = sensor;
            Label = $"{sensor.Source} / {sensor.Name} ({sensor.Unit})";
        }

        public SensorItem Sensor { get; }
        public string Label { get; }
    }
}