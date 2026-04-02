using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Windows.Input;

namespace TestMpv.Views.Components;

public class VideoOverlayControl : TemplatedControl
{
    // --- 内部状态 ---
    private int _pointerOverCount;

    // --- Styled Properties ---

    public static readonly StyledProperty<double> CurrentTimeProperty =
        AvaloniaProperty.Register<VideoOverlayControl, double>(nameof(CurrentTime));

    public double CurrentTime
    {
        get => GetValue(CurrentTimeProperty);
        set => SetValue(CurrentTimeProperty, value);
    }

    public static readonly StyledProperty<double> DurationProperty =
        AvaloniaProperty.Register<VideoOverlayControl, double>(nameof(Duration));

    public double Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<VideoOverlayControl, bool>(nameof(IsPlaying));

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public static readonly StyledProperty<string> TimeTextProperty =
        AvaloniaProperty.Register<VideoOverlayControl, string>(nameof(TimeText), "00:00 / 00:00");

    public string TimeText
    {
        get => GetValue(TimeTextProperty);
        set => SetValue(TimeTextProperty, value);
    }

    public static readonly StyledProperty<double> PlaybackSpeedProperty =
        AvaloniaProperty.Register<VideoOverlayControl, double>(nameof(PlaybackSpeed), 1.0);

    public double PlaybackSpeed
    {
        get => GetValue(PlaybackSpeedProperty);
        set => SetValue(PlaybackSpeedProperty, value);
    }

    public static readonly StyledProperty<string> PlaybackSpeedTextProperty =
        AvaloniaProperty.Register<VideoOverlayControl, string>(nameof(PlaybackSpeedText), "倍速");

    public string PlaybackSpeedText
    {
        get => GetValue(PlaybackSpeedTextProperty);
        set => SetValue(PlaybackSpeedTextProperty, value);
    }

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<VideoOverlayControl, string>(nameof(Title), "TestMpv");

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<double> VolumeProperty =
        AvaloniaProperty.Register<VideoOverlayControl, double>(nameof(Volume), 100.0);

    public double Volume
    {
        get => GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public static readonly StyledProperty<bool> IsMutedProperty =
        AvaloniaProperty.Register<VideoOverlayControl, bool>(nameof(IsMuted), false);

    public bool IsMuted
    {
        get => GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    public static readonly StyledProperty<bool> IsVolumeFlyoutOpenProperty =
        AvaloniaProperty.Register<VideoOverlayControl, bool>(nameof(IsVolumeFlyoutOpen), false);

    public bool IsVolumeFlyoutOpen
    {
        get => GetValue(IsVolumeFlyoutOpenProperty);
        set => SetValue(IsVolumeFlyoutOpenProperty, value);
    }

    public static readonly StyledProperty<bool> IsPointerOverFlyoutProperty =
        AvaloniaProperty.Register<VideoOverlayControl, bool>(nameof(IsPointerOverFlyout), false);

    public bool IsPointerOverFlyout
    {
        get => GetValue(IsPointerOverFlyoutProperty);
        set => SetValue(IsPointerOverFlyoutProperty, value);
    }

    // --- Commands ---

    public static readonly StyledProperty<System.Windows.Input.ICommand?> PlayPauseCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, System.Windows.Input.ICommand?>(nameof(PlayPauseCommand));

    public System.Windows.Input.ICommand? PlayPauseCommand
    {
        get => GetValue(PlayPauseCommandProperty);
        set => SetValue(PlayPauseCommandProperty, value);
    }

    public static readonly StyledProperty<System.Windows.Input.ICommand?> PreviousCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, System.Windows.Input.ICommand?>(nameof(PreviousCommand));

    public System.Windows.Input.ICommand? PreviousCommand
    {
        get => GetValue(PreviousCommandProperty);
        set => SetValue(PreviousCommandProperty, value);
    }

    public static readonly StyledProperty<System.Windows.Input.ICommand?> BackwardCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, System.Windows.Input.ICommand?>(nameof(BackwardCommand));

    public System.Windows.Input.ICommand? BackwardCommand
    {
        get => GetValue(BackwardCommandProperty);
        set => SetValue(BackwardCommandProperty, value);
    }

    public static readonly StyledProperty<System.Windows.Input.ICommand?> ForwardCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, System.Windows.Input.ICommand?>(nameof(ForwardCommand));

    public System.Windows.Input.ICommand? ForwardCommand
    {
        get => GetValue(ForwardCommandProperty);
        set => SetValue(ForwardCommandProperty, value);
    }

    public static readonly StyledProperty<System.Windows.Input.ICommand?> NextCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, System.Windows.Input.ICommand?>(nameof(NextCommand));

    public System.Windows.Input.ICommand? NextCommand
    {
        get => GetValue(NextCommandProperty);
        set => SetValue(NextCommandProperty, value);
    }

    public static readonly StyledProperty<System.Windows.Input.ICommand?> ToggleMuteCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, System.Windows.Input.ICommand?>(nameof(ToggleMuteCommand));

    public System.Windows.Input.ICommand? ToggleMuteCommand
    {
        get => GetValue(ToggleMuteCommandProperty);
        set => SetValue(ToggleMuteCommandProperty, value);
    }

    public static readonly StyledProperty<System.Windows.Input.ICommand?> ToggleDanmakuCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, System.Windows.Input.ICommand?>(nameof(ToggleDanmakuCommand));

    public System.Windows.Input.ICommand? ToggleDanmakuCommand
    {
        get => GetValue(ToggleDanmakuCommandProperty);
        set => SetValue(ToggleDanmakuCommandProperty, value);
    }

    public static readonly StyledProperty<System.Windows.Input.ICommand?> ChangeSpeedCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, System.Windows.Input.ICommand?>(nameof(ChangeSpeedCommand));

    public System.Windows.Input.ICommand? ChangeSpeedCommand
    {
        get => GetValue(ChangeSpeedCommandProperty);
        set => SetValue(ChangeSpeedCommandProperty, value);
    }

    public static readonly StyledProperty<System.Windows.Input.ICommand?> ToggleFullscreenCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, System.Windows.Input.ICommand?>(nameof(ToggleFullscreenCommand));

    public System.Windows.Input.ICommand? ToggleFullscreenCommand
    {
        get => GetValue(ToggleFullscreenCommandProperty);
        set => SetValue(ToggleFullscreenCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> TakeScreenshotCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, ICommand?>(nameof(TakeScreenshotCommand));

    public ICommand? TakeScreenshotCommand
    {
        get => GetValue(TakeScreenshotCommandProperty);
        set => SetValue(TakeScreenshotCommandProperty, value);
    }

    public static readonly StyledProperty<bool> IsFullscreenProperty =
        AvaloniaProperty.Register<VideoOverlayControl, bool>(nameof(IsFullscreen));

    public bool IsFullscreen
    {
        get => GetValue(IsFullscreenProperty);
        set => SetValue(IsFullscreenProperty, value);
    }

    public static readonly StyledProperty<bool> IsPointerOverToolAreaProperty =
        AvaloniaProperty.Register<VideoOverlayControl, bool>(nameof(IsPointerOverToolArea));

    public bool IsPointerOverToolArea
    {
        get => GetValue(IsPointerOverToolAreaProperty);
        set => SetValue(IsPointerOverToolAreaProperty, value);
    }

    // --- 属性：是否有指针在控件上（包括子元素） ---
    public bool IsPointerOverControl => _pointerOverCount > 0;

    // --- Routed Events ---

    public static readonly RoutedEvent<RoutedEventArgs> SeekStartedEvent =
        RoutedEvent.Register<VideoOverlayControl, RoutedEventArgs>(nameof(SeekStarted), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs> SeekStarted
    {
        add => AddHandler(SeekStartedEvent, value);
        remove => RemoveHandler(SeekStartedEvent, value);
    }

    public static readonly RoutedEvent<RoutedEventArgs> SeekEndedEvent =
        RoutedEvent.Register<VideoOverlayControl, RoutedEventArgs>(nameof(SeekEnded), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs> SeekEnded
    {
        add => AddHandler(SeekEndedEvent, value);
        remove => RemoveHandler(SeekEndedEvent, value);
    }

    public static readonly RoutedEvent<RangeBaseValueChangedEventArgs> SeekMovedEvent =
        RoutedEvent.Register<VideoOverlayControl, RangeBaseValueChangedEventArgs>(nameof(SeekMoved),
                 RoutingStrategies.Bubble);

    public event EventHandler<RangeBaseValueChangedEventArgs> SeekMoved
    {
        add => AddHandler(SeekMovedEvent, value);
        remove => RemoveHandler(SeekMovedEvent, value);
    }

    // --- Template Parts ---

    private VideoTransportControls? _transportControls;
    private Control?                _rightToolArea;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // 清理旧的指针事件
        PointerEntered -= OnControlPointerEntered;
        PointerExited  -= OnControlPointerExited;

        // 附加指针事件
        PointerEntered += OnControlPointerEntered;
        PointerExited  += OnControlPointerExited;

        if (_rightToolArea != null)
        {
            _rightToolArea.PointerEntered -= OnRightToolAreaPointerEntered;
            _rightToolArea.PointerExited  -= OnRightToolAreaPointerExited;
        }

        // Detach old events
        if (_transportControls != null)
        {
            _transportControls.SeekStarted -= OnSeekStartedFromTransport;
            _transportControls.SeekEnded   -= OnSeekEndedFromTransport;
            _transportControls.SeekMoved   -= OnSeekMovedFromTransport;
        }

        // Find new parts
        _transportControls = e.NameScope.Find<VideoTransportControls>("PART_TransportControls");
        _rightToolArea     = e.NameScope.Find<Control>("PART_RightToolArea");

        // Attach new events
        if (_transportControls != null)
        {
            _transportControls.SeekStarted += OnSeekStartedFromTransport;
            _transportControls.SeekEnded   += OnSeekEndedFromTransport;
            _transportControls.SeekMoved   += OnSeekMovedFromTransport;
        }

        if (_rightToolArea != null)
        {
            _rightToolArea.PointerEntered += OnRightToolAreaPointerEntered;
            _rightToolArea.PointerExited  += OnRightToolAreaPointerExited;
        }
    }

    public void HideFlyouts()
    {
        _transportControls?.HideFlyouts();
    }

    private void OnSeekStartedFromTransport(object? sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(SeekStartedEvent));

    private void OnSeekEndedFromTransport(object? sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(SeekEndedEvent));

    private void OnSeekMovedFromTransport(object? sender, RangeBaseValueChangedEventArgs e) =>
        RaiseEvent(new RangeBaseValueChangedEventArgs(e.OldValue, e.NewValue, SeekMovedEvent));

    private void OnControlPointerEntered(object? sender, PointerEventArgs e)
    {
        _pointerOverCount++;
    }

    private void OnControlPointerExited(object? sender, PointerEventArgs e)
    {
        if (_pointerOverCount > 0)
            _pointerOverCount--;
    }

    private void OnRightToolAreaPointerEntered(object? sender, PointerEventArgs e) => IsPointerOverToolArea = true;

    private void OnRightToolAreaPointerExited(object? sender, PointerEventArgs e) => IsPointerOverToolArea = false;
}
