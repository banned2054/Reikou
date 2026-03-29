using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;

namespace TestMpv.Views.Components;

public class VideoTransportControls : TemplatedControl
{
    // --- Styled Properties ---

    public static readonly StyledProperty<double> CurrentTimeProperty =
        AvaloniaProperty.Register<VideoTransportControls, double>(nameof(CurrentTime));

    public double CurrentTime
    {
        get => GetValue(CurrentTimeProperty);
        set => SetValue(CurrentTimeProperty, value);
    }

    public static readonly StyledProperty<double> DurationProperty =
        AvaloniaProperty.Register<VideoTransportControls, double>(nameof(Duration));

    public double Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<VideoTransportControls, bool>(nameof(IsPlaying));

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public static readonly StyledProperty<string> TimeTextProperty =
        AvaloniaProperty.Register<VideoTransportControls, string>(nameof(TimeText), "00:00 / 00:00");

    public string TimeText
    {
        get => GetValue(TimeTextProperty);
        set => SetValue(TimeTextProperty, value);
    }

    public static readonly StyledProperty<double> PlaybackSpeedProperty =
        AvaloniaProperty.Register<VideoTransportControls, double>(nameof(PlaybackSpeed), 1.0);

    public double PlaybackSpeed
    {
        get => GetValue(PlaybackSpeedProperty);
        set => SetValue(PlaybackSpeedProperty, value);
    }

    public static readonly StyledProperty<double> VolumeProperty =
        AvaloniaProperty.Register<VideoTransportControls, double>(nameof(Volume), 100.0);

    public double Volume
    {
        get => GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public static readonly StyledProperty<bool> IsMutedProperty =
        AvaloniaProperty.Register<VideoTransportControls, bool>(nameof(IsMuted));

    public bool IsMuted
    {
        get => GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    public static readonly StyledProperty<bool> IsVolumeFlyoutOpenProperty =
        AvaloniaProperty.Register<VideoTransportControls, bool>(nameof(IsVolumeFlyoutOpen));

    public bool IsVolumeFlyoutOpen
    {
        get => GetValue(IsVolumeFlyoutOpenProperty);
        set => SetValue(IsVolumeFlyoutOpenProperty, value);
    }

    public static readonly StyledProperty<bool> IsPointerOverFlyoutProperty =
        AvaloniaProperty.Register<VideoTransportControls, bool>(nameof(IsPointerOverFlyout));

    public bool IsPointerOverFlyout
    {
        get => GetValue(IsPointerOverFlyoutProperty);
        set => SetValue(IsPointerOverFlyoutProperty, value);
    }

    // --- Commands ---

    public static readonly StyledProperty<System.Windows.Input.ICommand?> PlayPauseCommandProperty =
        AvaloniaProperty.Register<VideoTransportControls, System.Windows.Input.ICommand?>(nameof(PlayPauseCommand));

    public System.Windows.Input.ICommand? PlayPauseCommand
    {
        get => GetValue(PlayPauseCommandProperty);
        set => SetValue(PlayPauseCommandProperty, value);
    }

    public static readonly StyledProperty<System.Windows.Input.ICommand?> PreviousCommandProperty =
        AvaloniaProperty.Register<VideoTransportControls, System.Windows.Input.ICommand?>(nameof(PreviousCommand));

    public System.Windows.Input.ICommand? PreviousCommand
    {
        get => GetValue(PreviousCommandProperty);
        set => SetValue(PreviousCommandProperty, value);
    }

    public static readonly StyledProperty<System.Windows.Input.ICommand?> BackwardCommandProperty =
        AvaloniaProperty.Register<VideoTransportControls, System.Windows.Input.ICommand?>(nameof(BackwardCommand));

    public System.Windows.Input.ICommand? BackwardCommand
    {
        get => GetValue(BackwardCommandProperty);
        set => SetValue(BackwardCommandProperty, value);
    }

    public static readonly StyledProperty<System.Windows.Input.ICommand?> ForwardCommandProperty =
        AvaloniaProperty.Register<VideoTransportControls, System.Windows.Input.ICommand?>(nameof(ForwardCommand));

    public System.Windows.Input.ICommand? ForwardCommand
    {
        get => GetValue(ForwardCommandProperty);
        set => SetValue(ForwardCommandProperty, value);
    }

    public static readonly StyledProperty<System.Windows.Input.ICommand?> NextCommandProperty =
        AvaloniaProperty.Register<VideoTransportControls, System.Windows.Input.ICommand?>(nameof(NextCommand));

    public System.Windows.Input.ICommand? NextCommand
    {
        get => GetValue(NextCommandProperty);
        set => SetValue(NextCommandProperty, value);
    }

    public static readonly StyledProperty<System.Windows.Input.ICommand?> ToggleMuteCommandProperty =
        AvaloniaProperty.Register<VideoTransportControls, System.Windows.Input.ICommand?>(nameof(ToggleMuteCommand));

    public System.Windows.Input.ICommand? ToggleMuteCommand
    {
        get => GetValue(ToggleMuteCommandProperty);
        set => SetValue(ToggleMuteCommandProperty, value);
    }

    public static readonly StyledProperty<System.Windows.Input.ICommand?> ToggleDanmakuCommandProperty =
        AvaloniaProperty.Register<VideoTransportControls, System.Windows.Input.ICommand?>(nameof(ToggleDanmakuCommand));

    public System.Windows.Input.ICommand? ToggleDanmakuCommand
    {
        get => GetValue(ToggleDanmakuCommandProperty);
        set => SetValue(ToggleDanmakuCommandProperty, value);
    }

    public static readonly StyledProperty<System.Windows.Input.ICommand?> ChangeSpeedCommandProperty =
        AvaloniaProperty.Register<VideoTransportControls, System.Windows.Input.ICommand?>(nameof(ChangeSpeedCommand));

    public System.Windows.Input.ICommand? ChangeSpeedCommand
    {
        get => GetValue(ChangeSpeedCommandProperty);
        set => SetValue(ChangeSpeedCommandProperty, value);
    }

    public static readonly StyledProperty<System.Windows.Input.ICommand?> ToggleFullscreenCommandProperty =
        AvaloniaProperty.Register<VideoTransportControls, System.Windows.Input.ICommand?>(nameof(ToggleFullscreenCommand));

    public System.Windows.Input.ICommand? ToggleFullscreenCommand
    {
        get => GetValue(ToggleFullscreenCommandProperty);
        set => SetValue(ToggleFullscreenCommandProperty, value);
    }

    public static readonly StyledProperty<bool> IsFullscreenProperty =
        AvaloniaProperty.Register<VideoTransportControls, bool>(nameof(IsFullscreen));

    public bool IsFullscreen
    {
        get => GetValue(IsFullscreenProperty);
        set => SetValue(IsFullscreenProperty, value);
    }

    // --- Routed Events ---

    public static readonly RoutedEvent<RoutedEventArgs> SeekStartedEvent =
        RoutedEvent.Register<VideoTransportControls, RoutedEventArgs>(nameof(SeekStarted), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs> SeekStarted
    {
        add => AddHandler(SeekStartedEvent, value);
        remove => RemoveHandler(SeekStartedEvent, value);
    }

    public static readonly RoutedEvent<RoutedEventArgs> SeekEndedEvent =
        RoutedEvent.Register<VideoTransportControls, RoutedEventArgs>(nameof(SeekEnded), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs> SeekEnded
    {
        add => AddHandler(SeekEndedEvent, value);
        remove => RemoveHandler(SeekEndedEvent, value);
    }

    public static readonly RoutedEvent<RangeBaseValueChangedEventArgs> SeekMovedEvent =
        RoutedEvent.Register<VideoTransportControls, RangeBaseValueChangedEventArgs>(nameof(SeekMoved),
                 RoutingStrategies.Bubble);

    public event EventHandler<RangeBaseValueChangedEventArgs> SeekMoved
    {
        add => AddHandler(SeekMovedEvent, value);
        remove => RemoveHandler(SeekMovedEvent, value);
    }

    // --- Template Parts ---

    private Slider? _timeSlider;
    private Button? _previousButton;
    private Button? _backwardButton;
    private Button? _playPauseButton;
    private Button? _forwardButton;
    private Button? _nextButton;
    private Button? _danmakuButton;
    private Button? _volumeButton;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // Detach old events
        if (_timeSlider != null)
        {
            _timeSlider.RemoveHandler(PointerPressedEvent, OnSliderPointerPressed);
            _timeSlider.RemoveHandler(PointerReleasedEvent, OnSliderPointerReleased);
            _timeSlider.ValueChanged -= OnSliderValueChanged;
        }

        if (_volumeButton is { Flyout: not null })
        {
            _volumeButton.Flyout.Opened -= OnVolumeFlyoutOpened;
            _volumeButton.Flyout.Closed -= OnVolumeFlyoutClosed;
            if (_volumeButton.Flyout is Flyout { Content: Control content })
            {
                content.PointerEntered -= OnFlyoutContentPointerEntered;
                content.PointerExited  -= OnFlyoutContentPointerExited;
            }
        }

        // Find new parts
        _timeSlider      = e.NameScope.Find<Slider>("PART_TimeSlider");
        _previousButton  = e.NameScope.Find<Button>("PART_PreviousButton");
        _backwardButton  = e.NameScope.Find<Button>("PART_BackwardButton");
        _playPauseButton = e.NameScope.Find<Button>("PART_PlayPauseButton");
        _forwardButton   = e.NameScope.Find<Button>("PART_ForwardButton");
        _nextButton      = e.NameScope.Find<Button>("PART_NextButton");
        _danmakuButton     = e.NameScope.Find<Button>("PART_DanmakuButton");
        _volumeButton    = e.NameScope.Find<Button>("PART_VolumeButton");

        // Attach new events
        if (_timeSlider != null)
        {
            _timeSlider.AddHandler(PointerPressedEvent, OnSliderPointerPressed,
                                   RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
            _timeSlider.AddHandler(PointerReleasedEvent, OnSliderPointerReleased,
                                   RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
            _timeSlider.ValueChanged += OnSliderValueChanged;
        }

        if (_volumeButton is { Flyout: not null })
        {
            _volumeButton.Flyout.Opened += OnVolumeFlyoutOpened;
            _volumeButton.Flyout.Closed += OnVolumeFlyoutClosed;

            if (_volumeButton.Flyout is Flyout { Content: Control content2 })
            {
                content2.PointerEntered += OnFlyoutContentPointerEntered;
                content2.PointerExited  += OnFlyoutContentPointerExited;
            }
        }
    }

    public void HideFlyouts()
    {
        _volumeButton?.Flyout?.Hide();
    }

    private void OnFlyoutContentPointerEntered(object? sender, PointerEventArgs e) => IsPointerOverFlyout = true;
    private void OnFlyoutContentPointerExited(object?  sender, PointerEventArgs e) => IsPointerOverFlyout = false;

    private void OnVolumeFlyoutOpened(object? sender, EventArgs e) => IsVolumeFlyoutOpen = true;

    private void OnVolumeFlyoutClosed(object? sender, EventArgs e)
    {
        IsVolumeFlyoutOpen  = false;
        IsPointerOverFlyout = false;
    }

    private void OnSliderPointerPressed(object? sender, PointerPressedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(SeekStartedEvent));

    private void OnSliderPointerReleased(object? sender, PointerReleasedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(SeekEndedEvent));

    private void OnSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e) =>
        RaiseEvent(new RangeBaseValueChangedEventArgs(e.OldValue, e.NewValue, SeekMovedEvent));
}
