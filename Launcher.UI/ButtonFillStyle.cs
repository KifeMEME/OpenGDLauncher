using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Launcher.UI;

internal static class ButtonFillStyle
{
    private const string HoverLayerName = "HoverFillLayer";

    // The overlay reveals the theme's hover PNG without stretching it. Only its clipping width changes.
    public static Style Create(double width, double height, Color foreground, Brush normal, Brush hover, Brush pressed, Brush disabled)
    {
        var surface = new FrameworkElementFactory(typeof(Border));
        surface.SetValue(UIElement.ClipToBoundsProperty, true);
        surface.SetBinding(Border.BackgroundProperty, TemplateBinding("Background"));
        surface.SetBinding(Border.BorderBrushProperty, TemplateBinding("BorderBrush"));
        surface.SetBinding(Border.BorderThicknessProperty, TemplateBinding("BorderThickness"));

        var layers = new FrameworkElementFactory(typeof(Grid));
        var hoverLayer = new FrameworkElementFactory(typeof(Border));
        hoverLayer.Name = HoverLayerName;
        hoverLayer.SetValue(Border.BackgroundProperty, hover);
        hoverLayer.SetValue(UIElement.IsHitTestVisibleProperty, false);
        hoverLayer.SetValue(UIElement.ClipProperty, new RectangleGeometry(new Rect(0, 0, 0, 0)));
        layers.AppendChild(hoverLayer);

        var pressedLayer = new FrameworkElementFactory(typeof(Border));
        pressedLayer.Name = "PressedFillLayer";
        pressedLayer.SetValue(Border.BackgroundProperty, pressed);
        pressedLayer.SetValue(UIElement.IsHitTestVisibleProperty, false);
        pressedLayer.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        layers.AppendChild(pressedLayer);

        var disabledLayer = new FrameworkElementFactory(typeof(Border));
        disabledLayer.Name = "DisabledFillLayer";
        disabledLayer.SetValue(Border.BackgroundProperty, disabled);
        disabledLayer.SetValue(UIElement.IsHitTestVisibleProperty, false);
        disabledLayer.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        layers.AppendChild(disabledLayer);

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetBinding(FrameworkElement.HorizontalAlignmentProperty, TemplateBinding("HorizontalContentAlignment"));
        content.SetBinding(FrameworkElement.VerticalAlignmentProperty, TemplateBinding("VerticalContentAlignment"));
        layers.AppendChild(content);
        surface.AppendChild(layers);

        var template = new ControlTemplate(typeof(Button)) { VisualTree = surface };
        template.Triggers.Add(new Trigger
        {
            Property = Button.IsPressedProperty, Value = true,
            Setters = { new Setter(UIElement.VisibilityProperty, Visibility.Visible, "PressedFillLayer") }
        });
        template.Triggers.Add(new Trigger
        {
            Property = Button.IsEnabledProperty, Value = false,
            Setters = { new Setter(UIElement.VisibilityProperty, Visibility.Visible, "DisabledFillLayer") }
        });

        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Button.TemplateProperty, template));
        style.Setters.Add(new Setter(Button.WidthProperty, width));
        style.Setters.Add(new Setter(Button.HeightProperty, height));
        style.Setters.Add(new Setter(Button.MarginProperty, new Thickness(5)));
        style.Setters.Add(new Setter(Button.BorderBrushProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Button.BackgroundProperty, normal));
        style.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(foreground)));
        style.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));
        style.Setters.Add(new EventSetter(UIElement.MouseEnterEvent, new MouseEventHandler((sender, _) => Animate(sender, 1, 240))));
        style.Setters.Add(new EventSetter(UIElement.MouseLeaveEvent, new MouseEventHandler((sender, _) => Animate(sender, 0, 190))));
        return style;
    }

    private static System.Windows.Data.Binding TemplateBinding(string property) =>
        new(property) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) };

    private static void Animate(object sender, double target, int milliseconds)
    {
        if (sender is not Button button || button.Template.FindName(HoverLayerName, button) is not Border layer || layer.Clip is not RectangleGeometry clip) return;
        if (clip.IsFrozen) { clip = clip.Clone(); layer.Clip = clip; }
        clip.BeginAnimation(RectangleGeometry.RectProperty, new RectAnimation
        {
            To = new Rect(0, 0, layer.ActualWidth * target, layer.ActualHeight),
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        }, HandoffBehavior.SnapshotAndReplace);
    }
}
