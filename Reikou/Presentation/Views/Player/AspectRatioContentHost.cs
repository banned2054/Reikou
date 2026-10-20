using Avalonia;
using Avalonia.Controls;
using System;

namespace Reikou.Presentation.Views.Player;

/// <summary>
///     Arranges its child inside the actual video content rectangle, excluding the
///     letterbox area produced by mpv's default keep-aspect behavior.
/// </summary>
public sealed class AspectRatioContentHost : Decorator
{
    private double _aspectRatio;

    public double AspectRatio
    {
        get => _aspectRatio;
        set
        {
            var sanitized = double.IsFinite(value) && value > 0 ? value : 0;
            if (Math.Abs(_aspectRatio - sanitized) < 0.0001) return;

            _aspectRatio = sanitized;
            InvalidateMeasure();
            InvalidateArrange();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Child?.Measure(ContentSize(availableSize));
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child == null) return finalSize;

        var contentSize = ContentSize(finalSize);
        var offsetX     = (finalSize.Width  - contentSize.Width)  / 2;
        var offsetY     = (finalSize.Height - contentSize.Height) / 2;
        Child.Arrange(new Rect(offsetX, offsetY, contentSize.Width, contentSize.Height));
        return finalSize;
    }

    private Size ContentSize(Size availableSize)
    {
        if (_aspectRatio <= 0 || availableSize.Width <= 0 || availableSize.Height <= 0) return availableSize;

        var availableAspectRatio = availableSize.Width / availableSize.Height;
        return availableAspectRatio > _aspectRatio
            ? new Size(availableSize.Height                     * _aspectRatio, availableSize.Height)
            : new Size(availableSize.Width, availableSize.Width / _aspectRatio);
    }
}
