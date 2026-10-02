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
    private readonly ComboBox lowGaugeColorSelector;
    private readonly ComboBox mediumGaugeColorSelector;
    private readonly ComboBox highGaugeColorSelector;
    private readonly ComboBox lowTemperatureColorSelector;
    private readonly ComboBox mediumTemperatureColorSelector;
    private readonly ComboBox highTemperatureColorSelector;
    private readonly Slider intervalSlider;
    private readonly TextBox warningThresholdInput;
    private readonly TextBox criticalThresholdInput;
    private readonly TextBox temperatureWarningInput;
    private readonly TextBox temperatureCriticalInput;
    private readonly TextBlock intervalValue;

    public HwinfoPanelSettings(HwinfoPanelWidget parent)
    {
        widget = parent;
        StackPanel panel = new() { Margin = new Thickness(12) };
        panel.Children.Add(new TextBlock { Text = "HWiNFO Sensor Panel", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 14) });

        StackPanel mainOptions = new() { Orientation = Orientation.Horizontal };
        StackPanel targetColumn = new() { Width = 140, Margin = new Thickness(0, 0, 8, 0) };
        targetColumn.Children.Add(new TextBlock { Text = "Panelbereich", Margin = new Thickness(0, 0, 0, 4) });
        ComboBox targetSelector = new() { Width = 126, HorizontalAlignment = HorizontalAlignment.Left, HorizontalContentAlignment = HorizontalAlignment.Left };
        bool isFiveByFour = widget.WidgetSize.Width == 5 && widget.WidgetSize.Height == 4;
        if (isFiveByFour)
        {
            targetSelector.Items.Add("Last");
            targetSelector.Items.Add("Temperatur");
            targetSelector.Items.Add("Kombiniert");
            targetSelector.SelectedIndex = (int)widget.FiveByFourGaugeMode;
        }
        else
        {
            targetSelector.Items.Add("Kombiniert");
            targetSelector.Items.Add("CPU");
            targetSelector.Items.Add("GPU");
            targetSelector.SelectedIndex = (int)widget.PanelTarget;
        }
        StackPanel sensorPanel = new();
        targetSelector.SelectionChanged += (_, _) =>
        {
            if (isFiveByFour)
                widget.SetFiveByFourGaugeMode((FiveByFourGaugeMode)targetSelector.SelectedIndex);
            else
                widget.SetPanelTarget((PanelTarget)targetSelector.SelectedIndex);
            RebuildRelevantSensorSelectors(sensorPanel);
        };
        targetColumn.Children.Add(targetSelector);
        mainOptions.Children.Add(targetColumn);

        if (widget.WidgetSize.Width == 1 && widget.WidgetSize.Height == 1)
        {
            CheckBox oneByOneTemperature = new()
            {
                Content = "1x1-Gauge zeigt Temperatur",
                IsChecked = widget.OneByOneShowsTemperature,
                Margin = new Thickness(0, 10, 0, 4)
            };
            oneByOneTemperature.Checked += (_, _) => widget.SetOneByOneShowsTemperature(true);
            oneByOneTemperature.Unchecked += (_, _) => widget.SetOneByOneShowsTemperature(false);
            panel.Children.Add(oneByOneTemperature);
        }

        StackPanel accentColumn = new() { Width = 140 };
        accentColumn.Children.Add(new TextBlock { Text = "Akzentfarbe", Margin = new Thickness(0, 0, 0, 4) });
        colorSelector = new ComboBox { Width = 126, HorizontalAlignment = HorizontalAlignment.Left, HorizontalContentAlignment = HorizontalAlignment.Left };
        colorSelector.Items.Add("Rot");
        colorSelector.Items.Add("Blau");
        colorSelector.Items.Add("Grün");
        colorSelector.SelectedIndex = ColorIndex(widget.AccentColor);
        colorSelector.SelectionChanged += ColorSelector_SelectionChanged;
        accentColumn.Children.Add(colorSelector);
        mainOptions.Children.Add(accentColumn);
        panel.Children.Add(mainOptions);

        Grid gaugeOptions = new() { Margin = new Thickness(0, 12, 0, 0) };
        gaugeOptions.ColumnDefinitions.Add(new ColumnDefinition());
        gaugeOptions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        gaugeOptions.ColumnDefinitions.Add(new ColumnDefinition());
        gaugeOptions.RowDefinitions.Add(new RowDefinition());
        gaugeOptions.RowDefinitions.Add(new RowDefinition());
        gaugeOptions.RowDefinitions.Add(new RowDefinition());
        gaugeOptions.RowDefinitions.Add(new RowDefinition());

        TextBlock loadHeading = new() { Text = "Gaugefarbe nach Auslastung", FontWeight = FontWeights.Bold };
        Grid.SetColumn(loadHeading, 0);
        Grid.SetRow(loadHeading, 0);
        gaugeOptions.Children.Add(loadHeading);
        TextBlock temperatureHeading = new() { Text = "Gaugefarbe nach Temperatur", FontWeight = FontWeights.Bold };
        Grid.SetColumn(temperatureHeading, 2);
        Grid.SetRow(temperatureHeading, 0);
        gaugeOptions.Children.Add(temperatureHeading);

        StackPanel lowGaugeColumn = new() { Margin = new Thickness(0, 8, 0, 0) };
        lowGaugeColumn.Children.Add(new TextBlock { Text = "Niedrig", Margin = new Thickness(0, 0, 0, 4) });
        lowGaugeColorSelector = CreateGaugeColorSelector(widget.GaugeLowColor);
        lowGaugeColorSelector.SelectionChanged += GaugeColorSelector_SelectionChanged;
        lowGaugeColumn.Children.Add(lowGaugeColorSelector);
        Grid.SetColumn(lowGaugeColumn, 0);
        Grid.SetRow(lowGaugeColumn, 1);
        gaugeOptions.Children.Add(lowGaugeColumn);

        StackPanel lowTemperatureColumn = new() { Margin = new Thickness(0, 8, 0, 0) };
        lowTemperatureColumn.Children.Add(new TextBlock { Text = "Niedrig", Margin = new Thickness(0, 0, 0, 4) });
        lowTemperatureColorSelector = CreateGaugeColorSelector(widget.TemperatureLowColor);
        lowTemperatureColorSelector.SelectionChanged += TemperatureColorSelector_SelectionChanged;
        lowTemperatureColumn.Children.Add(lowTemperatureColorSelector);
        Grid.SetColumn(lowTemperatureColumn, 2);
        Grid.SetRow(lowTemperatureColumn, 1);
        gaugeOptions.Children.Add(lowTemperatureColumn);

        StackPanel mediumGaugeColumn = new() { Margin = new Thickness(0, 10, 0, 0) };
        mediumGaugeColumn.Children.Add(new TextBlock { Text = "Mittel", Margin = new Thickness(0, 0, 0, 4) });
        mediumGaugeColorSelector = CreateGaugeColorSelector(widget.GaugeMediumColor);
        mediumGaugeColorSelector.SelectionChanged += GaugeColorSelector_SelectionChanged;
        warningThresholdInput = CreateThresholdInput(widget.GaugeWarningThreshold);
        warningThresholdInput.LostFocus += ThresholdInput_LostFocus;
        StackPanel mediumGaugeRow = new() { Orientation = Orientation.Horizontal };
        mediumGaugeRow.Children.Add(mediumGaugeColorSelector);
        mediumGaugeRow.Children.Add(warningThresholdInput);
        mediumGaugeColumn.Children.Add(mediumGaugeRow);
        Grid.SetColumn(mediumGaugeColumn, 0);
        Grid.SetRow(mediumGaugeColumn, 2);
        gaugeOptions.Children.Add(mediumGaugeColumn);

        StackPanel mediumTemperatureColumn = new() { Margin = new Thickness(0, 10, 0, 0) };
        mediumTemperatureColumn.Children.Add(new TextBlock { Text = "Mittel", Margin = new Thickness(0, 0, 0, 4) });
        mediumTemperatureColorSelector = CreateGaugeColorSelector(widget.TemperatureMediumColor);
        mediumTemperatureColorSelector.SelectionChanged += TemperatureColorSelector_SelectionChanged;
        temperatureWarningInput = CreateThresholdInput(widget.TemperatureWarningThreshold);
        temperatureWarningInput.LostFocus += TemperatureThresholdInput_LostFocus;
        StackPanel mediumTemperatureRow = new() { Orientation = Orientation.Horizontal };
        mediumTemperatureRow.Children.Add(mediumTemperatureColorSelector);
        mediumTemperatureRow.Children.Add(temperatureWarningInput);
        mediumTemperatureColumn.Children.Add(mediumTemperatureRow);
        Grid.SetColumn(mediumTemperatureColumn, 2);
        Grid.SetRow(mediumTemperatureColumn, 2);
        gaugeOptions.Children.Add(mediumTemperatureColumn);

        StackPanel highGaugeColumn = new() { Margin = new Thickness(0, 10, 0, 0) };
        highGaugeColumn.Children.Add(new TextBlock { Text = "Hoch", Margin = new Thickness(0, 0, 0, 4) });
        highGaugeColorSelector = CreateGaugeColorSelector(widget.GaugeHighColor);
        highGaugeColorSelector.SelectionChanged += GaugeColorSelector_SelectionChanged;
        criticalThresholdInput = CreateThresholdInput(widget.GaugeCriticalThreshold);
        criticalThresholdInput.LostFocus += ThresholdInput_LostFocus;
        StackPanel highGaugeRow = new() { Orientation = Orientation.Horizontal };
        highGaugeRow.Children.Add(highGaugeColorSelector);
        highGaugeRow.Children.Add(criticalThresholdInput);
        highGaugeColumn.Children.Add(highGaugeRow);
        Grid.SetColumn(highGaugeColumn, 0);
        Grid.SetRow(highGaugeColumn, 3);
        gaugeOptions.Children.Add(highGaugeColumn);

        StackPanel highTemperatureColumn = new() { Margin = new Thickness(0, 10, 0, 0) };
        highTemperatureColumn.Children.Add(new TextBlock { Text = "Hoch", Margin = new Thickness(0, 0, 0, 4) });
        highTemperatureColorSelector = CreateGaugeColorSelector(widget.TemperatureHighColor);
        highTemperatureColorSelector.SelectionChanged += TemperatureColorSelector_SelectionChanged;
        temperatureCriticalInput = CreateThresholdInput(widget.TemperatureCriticalThreshold);
        temperatureCriticalInput.LostFocus += TemperatureThresholdInput_LostFocus;
        StackPanel highTemperatureRow = new() { Orientation = Orientation.Horizontal };
        highTemperatureRow.Children.Add(highTemperatureColorSelector);
        highTemperatureRow.Children.Add(temperatureCriticalInput);
        highTemperatureColumn.Children.Add(highTemperatureRow);
        Grid.SetColumn(highTemperatureColumn, 2);
        Grid.SetRow(highTemperatureColumn, 3);
        gaugeOptions.Children.Add(highTemperatureColumn);
        panel.Children.Add(gaugeOptions);

        panel.Children.Add(new TextBlock { Text = "Aktualisierungsintervall", Margin = new Thickness(0, 16, 0, 4) });
        intervalSlider = new Slider { Minimum = 100, Maximum = 2000, TickFrequency = 100, IsSnapToTickEnabled = true, Value = widget.UpdateIntervalMilliseconds };
        intervalSlider.ValueChanged += IntervalSlider_ValueChanged;
        panel.Children.Add(intervalSlider);
        intervalValue = new TextBlock { Margin = new Thickness(0, 4, 0, 0) };
        panel.Children.Add(intervalValue);

        panel.Children.Add(new TextBlock { Text = "Zeitzone", Margin = new Thickness(0, 14, 0, 4) });
        Dictionary<string, string> timeZoneOptions = new()
        {
            ["Lokale Zeit"] = TimeZoneInfo.Local.Id,
            ["UTC"] = "UTC",
            ["Deutschland / Berlin"] = "W. Europe Standard Time",
            ["Großbritannien / London"] = "GMT Standard Time",
            ["USA / New York"] = "Eastern Standard Time",
            ["USA / Chicago"] = "Central Standard Time",
            ["USA / Denver"] = "Mountain Standard Time",
            ["USA / Los Angeles"] = "Pacific Standard Time",
            ["Japan / Tokio"] = "Tokyo Standard Time",
            ["Singapur"] = "Singapore Standard Time",
            ["Australien / Sydney"] = "AUS Eastern Standard Time"
        };
        ComboBox timeZoneSelector = new()
        {
            Width = 240,
            ItemsSource = timeZoneOptions,
            DisplayMemberPath = "Key",
            SelectedValuePath = "Value",
            SelectedValue = widget.TimeZoneId,
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        timeZoneSelector.SelectionChanged += (_, _) =>
        {
            if (timeZoneSelector.SelectedValue is string selectedTimeZone)
                widget.SetTimeZone(selectedTimeZone);
        };
        panel.Children.Add(timeZoneSelector);

        panel.Children.Add(new TextBlock { Text = "Header-Touchaktion", Margin = new Thickness(0, 12, 0, 4) });
        ComboBox headerActionSelector = new()
        {
            Width = 180,
            ItemsSource = new[] { "Keine Aktion", "Anzeige umschalten", "Jetzt aktualisieren" },
            SelectedIndex = (int)widget.HeaderTouchAction,
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        headerActionSelector.SelectionChanged += (_, _) =>
        {
            if (headerActionSelector.SelectedIndex >= 0)
                widget.SetHeaderTouchAction((HeaderTouchAction)headerActionSelector.SelectedIndex);
        };
        panel.Children.Add(headerActionSelector);

        panel.Children.Add(new TextBlock { Text = "HWiNFO-Sensoren", Margin = new Thickness(0, 18, 0, 8), FontWeight = FontWeights.Bold });
        panel.Children.Add(sensorPanel);
        RebuildRelevantSensorSelectors(sensorPanel);

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

    private void GaugeColorSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        widget.SetGaugeColors(
            GaugeColor(lowGaugeColorSelector.SelectedIndex),
            GaugeColor(mediumGaugeColorSelector.SelectedIndex),
            GaugeColor(highGaugeColorSelector.SelectedIndex));
    }

    private void TemperatureColorSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        widget.SetTemperatureColors(
            GaugeColor(lowTemperatureColorSelector.SelectedIndex),
            GaugeColor(mediumTemperatureColorSelector.SelectedIndex),
            GaugeColor(highTemperatureColorSelector.SelectedIndex));
    }

    private void ThresholdInput_LostFocus(object sender, RoutedEventArgs e)
    {
        int warning = ParseThreshold(warningThresholdInput.Text, widget.GaugeWarningThreshold);
        int critical = ParseThreshold(criticalThresholdInput.Text, widget.GaugeCriticalThreshold);
        widget.SetGaugeThresholds(warning, critical);
        warningThresholdInput.Text = widget.GaugeWarningThreshold.ToString();
        criticalThresholdInput.Text = widget.GaugeCriticalThreshold.ToString();
    }

    private void TemperatureThresholdInput_LostFocus(object sender, RoutedEventArgs e)
    {
        int warning = ParseThreshold(temperatureWarningInput.Text, widget.TemperatureWarningThreshold);
        int critical = ParseThreshold(temperatureCriticalInput.Text, widget.TemperatureCriticalThreshold);
        widget.SetTemperatureThresholds(warning, critical);
        temperatureWarningInput.Text = widget.TemperatureWarningThreshold.ToString();
        temperatureCriticalInput.Text = widget.TemperatureCriticalThreshold.ToString();
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

    private static ComboBox CreateGaugeColorSelector(DrawingColor color)
    {
        ComboBox selector = new() { Width = 126, HorizontalAlignment = HorizontalAlignment.Left, HorizontalContentAlignment = HorizontalAlignment.Left };
        selector.Items.Add("Grün");
        selector.Items.Add("Gelb");
        selector.Items.Add("Rot");
        selector.Items.Add("Blau");
        selector.SelectedIndex = GaugeColorIndex(color);
        return selector;
    }

    private static TextBox CreateThresholdInput(int value)
    {
        return new TextBox
        {
            Width = 48,
            Margin = new Thickness(8, 0, 0, 0),
            Text = value.ToString(),
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
    }

    private static int ParseThreshold(string text, int fallback)
    {
        return int.TryParse(text, out int value) ? Math.Max(1, Math.Min(99, value)) : fallback;
    }

    private static DrawingColor GaugeColor(int index)
    {
        return index switch
        {
            1 => DrawingColor.FromArgb(235, 190, 45),
            2 => DrawingColor.FromArgb(230, 35, 38),
            3 => DrawingColor.FromArgb(45, 145, 230),
            _ => DrawingColor.FromArgb(55, 190, 105)
        };
    }

    private static int GaugeColorIndex(DrawingColor color)
    {
        DrawingColor[] colors =
        {
            GaugeColor(0), GaugeColor(1), GaugeColor(2), GaugeColor(3)
        };
        int closestIndex = 0;
        int closestDistance = int.MaxValue;
        for (int index = 0; index < colors.Length; index++)
        {
            int distance = Math.Abs(color.R - colors[index].R) +
                           Math.Abs(color.G - colors[index].G) +
                           Math.Abs(color.B - colors[index].B);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = index;
            }
        }

        return closestIndex;
    }

    private void AddSensorSelector(StackPanel panel, string label, SensorSlot slot)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 6, 0, 2) });
        ComboBox selector = new() { Width = 280, Tag = slot, DisplayMemberPath = "Label", HorizontalAlignment = HorizontalAlignment.Left, HorizontalContentAlignment = HorizontalAlignment.Left };
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

    private void AddRelevantSensorSelectors(StackPanel panel)
    {
        bool compact = widget.WidgetSize.Width <= 2 && widget.WidgetSize.Height <= 2;
        bool singleRow = widget.WidgetSize.Width >= 3 && widget.WidgetSize.Height == 1;

        if (compact)
        {
            bool gpu = widget.PanelTarget == PanelTarget.Gpu;
            AddSensorSelector(panel, gpu ? "GPU-Last" : "CPU-Last", gpu ? SensorSlot.GpuLoad : SensorSlot.CpuLoad);
            AddSensorSelector(panel, gpu ? "GPU-Temperatur" : "CPU-Temperatur", gpu ? SensorSlot.GpuTemperature : SensorSlot.CpuTemperature);

            if (widget.WidgetSize.Width == 2)
            {
                AddSensorSelector(panel, gpu ? "GPU-Clock" : "CPU-Clock", gpu ? SensorSlot.GpuClock : SensorSlot.CpuClock);
                AddSensorSelector(panel, gpu ? "GPU-Power" : "CPU-Power", gpu ? SensorSlot.GpuPower : SensorSlot.CpuPower);
            }

            return;
        }

        AddSensorSelector(panel, "CPU-Last", SensorSlot.CpuLoad);
        AddSensorSelector(panel, "CPU-Temperatur", SensorSlot.CpuTemperature);
        AddSensorSelector(panel, "CPU-Clock", SensorSlot.CpuClock);
        AddSensorSelector(panel, "CPU-Power", SensorSlot.CpuPower);
        AddSensorSelector(panel, "GPU-Last", SensorSlot.GpuLoad);
        AddSensorSelector(panel, "GPU-Temperatur", SensorSlot.GpuTemperature);
        AddSensorSelector(panel, "GPU-Clock", SensorSlot.GpuClock);
        AddSensorSelector(panel, "GPU-Power", SensorSlot.GpuPower);

        if (singleRow)
            return;

        AddSensorSelector(panel, "CPU-Lüfter", SensorSlot.CpuFan);
        AddSensorSelector(panel, "GPU-Lüfter", SensorSlot.GpuFan);
        AddSensorSelector(panel, "RAM-Last", SensorSlot.MemoryLoad);
        AddSensorSelector(panel, "RAM-Used", SensorSlot.MemoryUsed);
    }

    private void RebuildRelevantSensorSelectors(StackPanel panel)
    {
        panel.Children.Clear();
        AddRelevantSensorSelectors(panel);
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

        if (slot == SensorSlot.CpuFan)
            return !isGpu && (text.IndexOf("Fan", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              text.IndexOf("Pump", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              text.IndexOf("CPU OPT", StringComparison.OrdinalIgnoreCase) >= 0);

        if (slot == SensorSlot.CpuLoad || slot == SensorSlot.CpuTemperature || slot == SensorSlot.CpuClock ||
            slot == SensorSlot.CpuPower)
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