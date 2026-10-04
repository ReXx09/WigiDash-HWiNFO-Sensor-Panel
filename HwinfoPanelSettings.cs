using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        StackPanel sensorPanel = new() { Margin = new Thickness(0, 8, 0, 0) };
        targetSelector.SelectionChanged += (_, _) =>
        {
            if (isFiveByFour)
                widget.SetFiveByFourGaugeMode((FiveByFourGaugeMode)targetSelector.SelectedIndex);
            else
                widget.SetPanelTarget((PanelTarget)targetSelector.SelectedIndex);
            RebuildRelevantSensorSelectors(sensorPanel);
        };
        targetColumn.Children.Add(targetSelector);
        targetColumn.Children.Add(new TextBlock { Text = "RAM-Gauge", Margin = new Thickness(0, 8, 0, 4) });
        ComboBox ramGaugeAlignmentSelector = new()
        {
            Width = 126,
            ItemsSource = new[] { "Rechts", "Links" },
            SelectedIndex = widget.RamGaugeAlignment == MemoryGaugeAlignment.Left ? 1 : 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        ramGaugeAlignmentSelector.SelectionChanged += (_, _) =>
        {
            if (ramGaugeAlignmentSelector.SelectedIndex >= 0)
                widget.SetRamGaugeAlignment((MemoryGaugeAlignment)ramGaugeAlignmentSelector.SelectedIndex);
        };
        targetColumn.Children.Add(ramGaugeAlignmentSelector);
        targetColumn.Children.Add(new TextBlock { Text = "Netzwerk-Skala (MB/s)", Margin = new Thickness(0, 8, 0, 4) });
        TextBox networkScaleInput = new()
        {
            Width = 126,
            Text = widget.NetworkScaleMegabytesPerSecond.ToString("0.##"),
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        networkScaleInput.LostFocus += (_, _) =>
        {
            if (double.TryParse(networkScaleInput.Text, out double scale))
                widget.SetNetworkScale(scale);
            networkScaleInput.Text = widget.NetworkScaleMegabytesPerSecond.ToString("0.##");
        };
        targetColumn.Children.Add(networkScaleInput);
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
        accentColumn.Children.Add(new TextBlock { Text = "VRAM-Gauge", Margin = new Thickness(0, 8, 0, 4) });
        ComboBox vramGaugeAlignmentSelector = new()
        {
            Width = 126,
            ItemsSource = new[] { "Rechts", "Links" },
            SelectedIndex = widget.VramGaugeAlignment == MemoryGaugeAlignment.Left ? 1 : 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        vramGaugeAlignmentSelector.SelectionChanged += (_, _) =>
        {
            if (vramGaugeAlignmentSelector.SelectedIndex >= 0)
                widget.SetVramGaugeAlignment((MemoryGaugeAlignment)vramGaugeAlignmentSelector.SelectedIndex);
        };
        accentColumn.Children.Add(vramGaugeAlignmentSelector);
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
        Expander gaugeColorExpander = new()
        {
            Header = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(225, 238, 250)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(8, 5, 8, 5),
                Child = new TextBlock
                {
                    Text = "CPU / GPU Gaugefarbe",
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 75, 115))
                }
            },
            IsExpanded = true,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 12, 0, 0),
            Content = gaugeOptions
        };
        panel.Children.Add(gaugeColorExpander);

        panel.Children.Add(new TextBlock { Text = "Aktualisierungsintervall", Margin = new Thickness(0, 16, 0, 4) });
        intervalSlider = new Slider { Minimum = 100, Maximum = 2000, TickFrequency = 100, IsSnapToTickEnabled = true, Value = widget.UpdateIntervalMilliseconds };
        intervalSlider.ValueChanged += IntervalSlider_ValueChanged;
        panel.Children.Add(intervalSlider);
        intervalValue = new TextBlock { Margin = new Thickness(0, 4, 0, 0) };
        panel.Children.Add(intervalValue);

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
        StackPanel headerOptions = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        StackPanel timeZoneColumn = new() { Width = 145, Margin = new Thickness(0, 0, 8, 0) };
        timeZoneColumn.Children.Add(new TextBlock { Text = "Zeitzone", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) });
        ComboBox timeZoneSelector = new()
        {
            Width = 137,
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
        timeZoneColumn.Children.Add(timeZoneSelector);
        headerOptions.Children.Add(timeZoneColumn);

        StackPanel headerActionColumn = new() { Width = 145, Margin = new Thickness(0, 0, 8, 0) };
        headerActionColumn.Children.Add(new TextBlock { Text = "Header-Touchaktion", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) });
        ComboBox headerActionSelector = new()
        {
            Width = 137,
            ItemsSource = new[] { "Keine Aktion", "Anzeige umschalten", "Jetzt aktualisieren", "Externe Aktion" },
            SelectedIndex = (int)widget.HeaderTouchAction,
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        headerActionSelector.SelectionChanged += (_, _) =>
        {
            if (headerActionSelector.SelectedIndex >= 0)
                widget.SetHeaderTouchAction((HeaderTouchAction)headerActionSelector.SelectedIndex);
        };
        headerActionColumn.Children.Add(headerActionSelector);
        headerOptions.Children.Add(headerActionColumn);

        StackPanel externalActionColumn = new() { Width = 180 };
        externalActionColumn.Children.Add(new TextBlock { Text = "Externe Aktion", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) });
        ComboBox externalActionSelector = new()
        {
            Width = 172,
            ItemsSource = widget.AvailableExternalActions,
            DisplayMemberPath = "Value",
            SelectedValuePath = "Key",
            SelectedValue = widget.HeaderExternalActionId,
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        externalActionSelector.SelectionChanged += (_, _) =>
        {
            widget.SetHeaderExternalAction(externalActionSelector.SelectedValue is Guid actionId ? actionId : null);
        };
        externalActionColumn.Children.Add(externalActionSelector);
        headerOptions.Children.Add(externalActionColumn);
        panel.Children.Add(headerOptions);

        StackPanel timeOptions = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        StackPanel timeSizeColumn = new() { Width = 145, Margin = new Thickness(0, 0, 8, 0) };
        timeSizeColumn.Children.Add(new TextBlock { Text = "Uhrgröße", Margin = new Thickness(0, 0, 0, 4) });
        ComboBox timeSizeSelector = new()
        {
            Width = 137,
            ItemsSource = new[] { "Klein", "Mittel", "Groß" },
            SelectedIndex = widget.TimeFontSize <= 13 ? 0 : widget.TimeFontSize >= 18 ? 2 : 1,
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        timeSizeSelector.SelectionChanged += (_, _) =>
        {
            int[] sizes = { 12, 15, 18 };
            if (timeSizeSelector.SelectedIndex >= 0)
                widget.SetTimeFontSize(sizes[timeSizeSelector.SelectedIndex]);
        };
        timeSizeColumn.Children.Add(timeSizeSelector);
        timeOptions.Children.Add(timeSizeColumn);

        StackPanel timeColorColumn = new() { Width = 145 };
        timeColorColumn.Children.Add(new TextBlock { Text = "Uhrfarbe", Margin = new Thickness(0, 0, 0, 4) });
        ComboBox timeColorSelector = CreateTimeColorSelector(widget.TimeColor);
        timeColorSelector.SelectionChanged += (_, _) =>
        {
            if (timeColorSelector.SelectedIndex >= 0)
                widget.SetTimeColor(TimeColor(timeColorSelector.SelectedIndex));
        };
        timeColorColumn.Children.Add(timeColorSelector);
        timeOptions.Children.Add(timeColorColumn);
        panel.Children.Add(timeOptions);

        Expander sensorExpander = new()
        {
            Header = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(225, 238, 250)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(8, 5, 8, 5),
                Child = new TextBlock
                {
                    Text = "HWiNFO-Sensoren",
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 75, 115))
                }
            },
            IsExpanded = false,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = sensorPanel
        };
        panel.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 145, 230)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 18, 0, 0),
            Child = sensorExpander
        });
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

    private static ComboBox CreateTimeColorSelector(DrawingColor color)
    {
        ComboBox selector = new() { Width = 137, HorizontalAlignment = HorizontalAlignment.Left, HorizontalContentAlignment = HorizontalAlignment.Left };
        selector.Items.Add("Weiß");
        selector.Items.Add("Blau");
        selector.Items.Add("Gelb");
        selector.Items.Add("Grün");
        selector.Items.Add("Rot");
        selector.SelectedIndex = TimeColorIndex(color);
        return selector;
    }

    private static DrawingColor TimeColor(int index)
    {
        return index switch
        {
            1 => DrawingColor.FromArgb(45, 145, 230),
            2 => DrawingColor.FromArgb(235, 190, 45),
            3 => DrawingColor.FromArgb(55, 190, 105),
            4 => DrawingColor.FromArgb(230, 35, 38),
            _ => DrawingColor.White
        };
    }

    private static int TimeColorIndex(DrawingColor color)
    {
        DrawingColor[] colors =
        {
            TimeColor(0), TimeColor(1), TimeColor(2), TimeColor(3), TimeColor(4)
        };
        int closestIndex = 0;
        int closestDistance = int.MaxValue;
        for (int index = 0; index < colors.Length; index++)
        {
            int distance = Math.Abs(color.R - colors[index].R) + Math.Abs(color.G - colors[index].G) + Math.Abs(color.B - colors[index].B);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = index;
            }
        }

        return closestIndex;
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

    private void AddSensorSelector(Panel panel, string label, SensorSlot slot)
    {
        panel.Children.Add(CreateSensorSelectorGroup(label, slot));
    }

    private StackPanel CreateSensorSelectorGroup(string label, SensorSlot slot)
    {
        StackPanel selectorGroup = new() { Margin = new Thickness(0, 0, 8, 8) };
        selectorGroup.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 6, 0, 2) });
        ComboBox selector = new() { Width = 220, Tag = slot, DisplayMemberPath = "Label", HorizontalAlignment = HorizontalAlignment.Left, HorizontalContentAlignment = HorizontalAlignment.Left };
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
        selectorGroup.Children.Add(selector);
        return selectorGroup;
    }

    private void AddSensorPair(Grid grid, int row, string leftLabel, SensorSlot leftSlot, string rightLabel, SensorSlot rightSlot)
    {
        StackPanel left = CreateSensorSelectorGroup(leftLabel, leftSlot);
        StackPanel right = CreateSensorSelectorGroup(rightLabel, rightSlot);
        Grid.SetColumn(right, 2);
        Grid.SetRow(left, row);
        Grid.SetRow(right, row);
        grid.Children.Add(left);
        grid.Children.Add(right);
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

        panel.Children.Add(new TextBlock { Text = "CPU / GPU", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 4) });
        Grid cpuGpuGrid = CreateSensorGrid();
        AddSensorPair(cpuGpuGrid, 0, "CPU-Last", SensorSlot.CpuLoad, "GPU-Last", SensorSlot.GpuLoad);
        AddSensorPair(cpuGpuGrid, 1, "CPU-Temperatur", SensorSlot.CpuTemperature, "GPU-Temperatur", SensorSlot.GpuTemperature);
        AddSensorPair(cpuGpuGrid, 2, "CPU-Clock", SensorSlot.CpuClock, "GPU-Clock", SensorSlot.GpuClock);
        AddSensorPair(cpuGpuGrid, 3, "CPU-Power", SensorSlot.CpuPower, "GPU-Power", SensorSlot.GpuPower);
        if (!singleRow)
            AddSensorPair(cpuGpuGrid, 4, "CPU-Lüfter", SensorSlot.CpuFan, "GPU-Lüfter", SensorSlot.GpuFan);
        panel.Children.Add(cpuGpuGrid);

        if (singleRow)
            return;

        AddSensorSelector(panel, "GPU-VRAM", SensorSlot.GpuMemory);

        panel.Children.Add(new TextBlock { Text = "RAM", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8, 0, 4) });
        Grid memoryGrid = CreateSensorGrid();
        AddSensorPair(memoryGrid, 0, "RAM-Last", SensorSlot.MemoryLoad, "RAM-Used", SensorSlot.MemoryUsed);
        panel.Children.Add(memoryGrid);
        AddSensorSelector(panel, "RAM-Takt", SensorSlot.MemoryClock);

        Border networkSeparator = new() { BorderBrush = System.Windows.Media.Brushes.Gray, BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 10, 0, 6) };
        panel.Children.Add(networkSeparator);
        panel.Children.Add(new TextBlock { Text = "NETZWERK", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) });
        Grid networkGrid = CreateSensorGrid();
        AddSensorPair(networkGrid, 0, "Upload", SensorSlot.NetworkUpload, "Download", SensorSlot.NetworkDownload);
        panel.Children.Add(networkGrid);
    }

    private static Grid CreateSensorGrid()
    {
        Grid grid = new();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (int row = 0; row < 5; row++)
            grid.RowDefinitions.Add(new RowDefinition());
        return grid;
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
        string unit = sensor.Unit ?? string.Empty;
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
            slot == SensorSlot.GpuPower || slot == SensorSlot.GpuFan)
            return isGpu;

        if (slot == SensorSlot.GpuMemory)
        {
            bool memoryUnit = unit.Equals("MB", StringComparison.OrdinalIgnoreCase) ||
                               unit.Equals("GB", StringComparison.OrdinalIgnoreCase);
            return isGpu && memoryUnit &&
                   (text.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("VRAM", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("Dedicated", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (slot == SensorSlot.MemoryLoad)
        {
            bool percentageUnit = unit.IndexOf("%", StringComparison.OrdinalIgnoreCase) >= 0;
            return isMemory && !isCpu && !isGpu &&
                   (percentageUnit || text.IndexOf("Load", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("Usage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("Utilization", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (slot == SensorSlot.MemoryUsed)
        {
            bool memoryUnit = unit.Equals("MB", StringComparison.OrdinalIgnoreCase) ||
                               unit.Equals("GB", StringComparison.OrdinalIgnoreCase);
            return isMemory && !isCpu && !isGpu && memoryUnit &&
                   (text.IndexOf("Used", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) >= 0) &&
                   text.IndexOf("Load", StringComparison.OrdinalIgnoreCase) < 0 &&
                   text.IndexOf("Usage", StringComparison.OrdinalIgnoreCase) < 0;
        }

        if (slot == SensorSlot.MemoryClock)
        {
            bool clockUnit = unit.Equals("MHz", StringComparison.OrdinalIgnoreCase);
            return isMemory && !isCpu && !isGpu && clockUnit &&
                   (text.IndexOf("Clock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("Frequency", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (slot == SensorSlot.NetworkUpload || slot == SensorSlot.NetworkDownload)
            return text.IndexOf("Network", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Ethernet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Wi-Fi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("WiFi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Upload", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Download", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Transmit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Receive", StringComparison.OrdinalIgnoreCase) >= 0;

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