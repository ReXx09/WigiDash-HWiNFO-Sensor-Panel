using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
}