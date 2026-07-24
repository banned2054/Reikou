using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace Reikou.Views.Controls;

/// <summary>
/// Keeps its child at a stable logical size while the host is being resized and
/// previews the new size through a compositor-backed render transform.
/// </summary>
public sealed class LiveResizeViewportHost : Decorator
{
    private static readonly TimeSpan DefaultResizeEndDelay = TimeSpan.FromMilliseconds(160);

    private readonly DispatcherTimer _resizeEndTimer;

    private Size _committedSize;
    private Size _displaySize;
    private bool _commitOnNextArrange;
    private bool _platformLiveResizeActive;
    private int  _resizeGeneration;

#if DEBUG
    private int  _previewSizeChangeCount;
    private int  _arrangeCount;
    private long _arrangeElapsedTicks;
#endif

    public LiveResizeViewportHost()
    {
        ClipToBounds = true;

        _resizeEndTimer = new DispatcherTimer
        {
            Interval = DefaultResizeEndDelay
        };
        _resizeEndTimer.Tick += OnResizeEndTimerTick;
    }

    public TimeSpan ResizeEndDelay
    {
        get => _resizeEndTimer.Interval;
        set => _resizeEndTimer.Interval = value > TimeSpan.Zero ? value : DefaultResizeEndDelay;
    }

    /// <summary>
    /// Starts a platform-reported interactive resize session. While active, the
    /// fallback debounce cannot commit an intermediate framebuffer size.
    /// </summary>
    public void BeginLiveResize()
    {
        _resizeGeneration++;
        _platformLiveResizeActive = true;
        _resizeEndTimer.Stop();
    }

    /// <summary>
    /// Ends a platform-reported interactive resize session and commits the last
    /// display size once.
    /// </summary>
    public void EndLiveResize()
    {
        if (!_platformLiveResizeActive)
        {
            return;
        }

        _platformLiveResizeActive = false;
        _resizeEndTimer.Stop();
        QueueCommitAfterPreview(_resizeGeneration, _displaySize);
    }

    /// <summary>
    /// Updates the compositor preview directly from the top-level client size,
    /// without waiting for the next layout pass.
    /// </summary>
    public void PreviewDisplaySize(Size displaySize)
    {
        EnsureInitialSize(displaySize);
        if (!IsValidSize(displaySize) || !IsValidSize(_committedSize))
        {
            return;
        }

        UpdateDisplaySize(displaySize);
        ApplyPreviewTransform(requestCommit : true);
    }

    /// <summary>
    /// Commits the size received by the next layout pass without waiting for the
    /// live-resize debounce. Used for maximize, restore, fullscreen and DPI changes.
    /// </summary>
    public void CommitOnNextArrange()
    {
        _resizeGeneration++;
        _resizeEndTimer.Stop();
        _commitOnNextArrange = true;
        InvalidateArrange();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureInitialSize(availableSize);

        if (Child != null && IsValidSize(_committedSize))
        {
            Child.Measure(_committedSize);
        }

        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
#if DEBUG
        var arrangeStarted = Stopwatch.GetTimestamp();
#endif
        EnsureInitialSize(finalSize);

        if (!IsValidSize(finalSize) || Child == null || !IsValidSize(_committedSize))
        {
            return finalSize;
        }

        UpdateDisplaySize(finalSize);

        if (_commitOnNextArrange)
        {
            _commitOnNextArrange = false;
            CommitDisplaySize(invalidateLayout : true);
        }

        // Keep the child at one stable layout rectangle. The compositor stretches
        // that continuously rendered surface to the live window size, so neither
        // mpv's FBO nor the danmaku viewport is resized until the final commit.
        Child.Arrange(new Rect(_committedSize));
        ApplyPreviewTransform(requestCommit : false);

#if DEBUG
        _arrangeElapsedTicks += Stopwatch.GetTimestamp() - arrangeStarted;
        _arrangeCount++;
#endif

        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _resizeEndTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private void EnsureInitialSize(Size size)
    {
        if (!IsValidSize(size))
        {
            return;
        }

        if (!IsValidSize(_committedSize))
        {
            _committedSize = size;
        }

        if (!IsValidSize(_displaySize))
        {
            _displaySize = size;
        }
    }

    private void RestartResizeEndTimer()
    {
        if (_platformLiveResizeActive)
        {
            return;
        }

        _resizeEndTimer.Stop();
        _resizeEndTimer.Start();
    }

    private void OnResizeEndTimerTick(object? sender, EventArgs e)
    {
        _resizeEndTimer.Stop();

        if (_platformLiveResizeActive)
        {
            return;
        }

        QueueCommitAfterPreview(_resizeGeneration, _displaySize);
    }

    private void CommitDisplaySize(bool invalidateLayout)
    {
        _resizeEndTimer.Stop();

        if (!IsValidSize(_displaySize) || AreClose(_committedSize, _displaySize))
        {
            return;
        }

#if DEBUG
        var arrangeMilliseconds = _arrangeElapsedTicks * 1000.0 / Stopwatch.Frequency;
        Debug.WriteLine(
                        $"[LiveResize] committed {_committedSize.Width:0.#}x{_committedSize.Height:0.#} " +
                        $"-> {_displaySize.Width:0.#}x{_displaySize.Height:0.#}; "                        +
                        $"preview changes={_previewSizeChangeCount}; arrange calls={_arrangeCount}; "     +
                        $"arrange total={arrangeMilliseconds:0.00}ms"
                       );
        _previewSizeChangeCount = 0;
        _arrangeCount           = 0;
        _arrangeElapsedTicks    = 0;
#endif

        _committedSize = _displaySize;
        ResetPreviewTransform();

        if (invalidateLayout)
        {
            InvalidateMeasure();
            InvalidateArrange();
        }
    }

    private void UpdateDisplaySize(Size displaySize)
    {
        if (AreClose(_displaySize, displaySize))
        {
            return;
        }

        _displaySize = displaySize;
#if DEBUG
        _previewSizeChangeCount++;
#endif
        if (AreClose(_displaySize, _committedSize))
        {
            _resizeEndTimer.Stop();
        }
        else
        {
            RestartResizeEndTimer();
        }
    }

    private void QueueCommitAfterPreview(int generation, Size expectedDisplaySize)
    {
        var visual = Child == null ? null : ElementComposition.GetElementVisual(Child);
        if (visual == null)
        {
            QueueCommitOnNextArrange(generation, expectedDisplaySize);
            return;
        }

        ApplyPreviewTransform(requestCommit : false);
        _ = CommitAfterPreviewAsync(visual, generation, expectedDisplaySize);
    }

    private async Task CommitAfterPreviewAsync(
        CompositionVisual visual,
        int               generation,
        Size              expectedDisplaySize)
    {
        try
        {
            await visual.Compositor.RequestCommitAsync();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        Dispatcher.UIThread.Post(
                                 () => QueueCommitOnNextArrange(generation, expectedDisplaySize),
                                 DispatcherPriority.Render
                                );
    }

    private void QueueCommitOnNextArrange(int generation, Size expectedDisplaySize)
    {
        if (_platformLiveResizeActive       ||
            generation != _resizeGeneration ||
            !AreClose(_displaySize, expectedDisplaySize))
        {
            return;
        }

        _commitOnNextArrange = true;
        InvalidateArrange();
    }

    private void ApplyPreviewTransform(bool requestCommit)
    {
        if (Child == null)
        {
            return;
        }

        var visual = ElementComposition.GetElementVisual(Child);
        if (visual == null)
        {
            return;
        }

        var scale = Math.Min(
                             _displaySize.Width  / _committedSize.Width,
                             _displaySize.Height / _committedSize.Height
                            );
        var offsetX = (_displaySize.Width  - _committedSize.Width  * scale) / 2;
        var offsetY = (_displaySize.Height - _committedSize.Height * scale) / 2;

        visual.Scale       = new Vector3D(scale, scale, 1);
        visual.Translation = new Vector3D(offsetX, offsetY, 0);

        if (requestCommit)
        {
            _ = RequestCommitIgnoringDisposalAsync(visual.Compositor);
        }
    }

    private static async Task RequestCommitIgnoringDisposalAsync(Compositor compositor)
    {
        try
        {
            await compositor.RequestCommitAsync();
        }
        catch (ObjectDisposedException)
        {
            // The top-level can disappear while a final resize update is queued.
        }
    }

    private void ResetPreviewTransform()
    {
        if (Child == null)
        {
            return;
        }

        var visual = ElementComposition.GetElementVisual(Child);
        if (visual == null)
        {
            return;
        }

        visual.Scale       = new Vector3D(1, 1, 1);
        visual.Translation = default;
    }

    private static bool IsValidSize(Size size)
    {
        return double.IsFinite(size.Width) && double.IsFinite(size.Height) &&
               size.Width > 0              && size.Height > 0;
    }

    private static bool AreClose(Size left, Size right)
    {
        return Math.Abs(left.Width  - right.Width)  < 0.01 &&
               Math.Abs(left.Height - right.Height) < 0.01;
    }
}
