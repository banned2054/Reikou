using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Reikou.Views.Controls;

public class ViewportIcon : TemplatedControl
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<ViewportIcon, Geometry?>(nameof(Data));

    public static readonly StyledProperty<double> ViewBoxWidthProperty =
        AvaloniaProperty.Register<ViewportIcon, double>(nameof(ViewBoxWidth), 24);

    public static readonly StyledProperty<double> ViewBoxHeightProperty =
        AvaloniaProperty.Register<ViewportIcon, double>(nameof(ViewBoxHeight), 24);

    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public double ViewBoxWidth
    {
        get => GetValue(ViewBoxWidthProperty);
        set => SetValue(ViewBoxWidthProperty, value);
    }

    public double ViewBoxHeight
    {
        get => GetValue(ViewBoxHeightProperty);
        set => SetValue(ViewBoxHeightProperty, value);
    }

    protected override System.Type StyleKeyOverride => typeof(ViewportIcon);
}
