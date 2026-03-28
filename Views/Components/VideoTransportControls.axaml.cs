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
        AvaloniaProperty.Register<VideoTransportControls, bool>(nameof(IsMuted), false);

    public bool IsMuted
    {
        get => GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    public static readonly StyledProperty<bool> IsVolumeFlyoutOpenProperty =
        AvaloniaProperty.Register<VideoTransportControls, bool>(nameof(IsVolumeFlyoutOpen), false);

    public bool IsVolumeFlyoutOpen
    {
        get => GetValue(IsVolumeFlyoutOpenProperty);
        set => SetValue(IsVolumeFlyoutOpenProperty, value);
    }

    public static readonly StyledProperty<bool> IsPointerOverFlyoutProperty =
        AvaloniaProperty.Register<VideoTransportControls, bool>(nameof(IsPointerOverFlyout), false);

    public bool IsPointerOverFlyout
    {
        get => GetValue(IsPointerOverFlyoutProperty);
        set => SetValue(IsPointerOverFlyoutProperty, value);
    }

    // --- Routed Events ---

    public static readonly RoutedEvent<RoutedEventArgs> PlayPauseRequestedEvent =
        RoutedEvent.Register<VideoTransportControls, RoutedEventArgs>(nameof(PlayPauseRequested),
                                                                      RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs> PlayPauseRequested
    {
        add => AddHandler(PlayPauseRequestedEvent, value);
        remove => RemoveHandler(PlayPauseRequestedEvent, value);
    }

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
    private Button? _playPauseButton;
    private Button? _backwardButton;
    private Button? _forwardButton;
    private Button? _danmuButton;
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

        if (_playPauseButton != null) _playPauseButton.Click -= OnPlayPauseClick;
        if (_backwardButton  != null) _backwardButton.Click  -= OnPlayPauseClick;
        if (_forwardButton   != null) _forwardButton.Click   -= OnPlayPauseClick;
        if (_danmuButton     != null) _danmuButton.Click     -= OnPlayPauseClick;
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
        _playPauseButton = e.NameScope.Find<Button>("PART_PlayPauseButton");
        _backwardButton  = e.NameScope.Find<Button>("PART_BackwardButton");
        _forwardButton   = e.NameScope.Find<Button>("PART_ForwardButton");
        _danmuButton     = e.NameScope.Find<Button>("PART_DanmuButton");
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

        if (_playPauseButton != null) _playPauseButton.Click += OnPlayPauseClick;
        if (_backwardButton  != null) _backwardButton.Click  += OnPlayPauseClick;
        if (_forwardButton   != null) _forwardButton.Click   += OnPlayPauseClick;
        if (_danmuButton     != null) _danmuButton.Click     += OnPlayPauseClick;
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

    private void OnPlayPauseClick(object? sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(PlayPauseRequestedEvent));
}
