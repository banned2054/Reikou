using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Windows.Input;

namespace Reikou.Views.Components;

public class VideoTransportControls : TemplatedControl
{
    // --- Styled Properties ---

    public static readonly StyledProperty<double> CurrentTimeProperty =
        AvaloniaProperty.Register<VideoTransportControls, double>(nameof(CurrentTime));

    public static readonly StyledProperty<double> DurationProperty =
        AvaloniaProperty.Register<VideoTransportControls, double>(nameof(Duration));

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<VideoTransportControls, bool>(nameof(IsPlaying));

    public static readonly StyledProperty<string> TimeTextProperty =
        AvaloniaProperty.Register<VideoTransportControls, string>(nameof(TimeText), "00:00 / 00:00");

    public static readonly StyledProperty<double> PlaybackSpeedProperty =
        AvaloniaProperty.Register<VideoTransportControls, double>(nameof(PlaybackSpeed), 1.0);

    public static readonly StyledProperty<string> PlaybackSpeedTextProperty =
        AvaloniaProperty.Register<VideoTransportControls, string>(nameof(PlaybackSpeedText), "倍速");

    public static readonly StyledProperty<double> VolumeProperty =
        AvaloniaProperty.Register<VideoTransportControls, double>(nameof(Volume), 100.0);

    public static readonly StyledProperty<double> MaximumVolumeProperty =
        AvaloniaProperty.Register<VideoTransportControls, double>(nameof(MaximumVolume), 130.0);

    public static readonly StyledProperty<bool> IsMutedProperty =
        AvaloniaProperty.Register<VideoTransportControls, bool>(nameof(IsMuted));

    public static readonly StyledProperty<bool> IsVolumeFlyoutOpenProperty =
        AvaloniaProperty.Register<VideoTransportControls, bool>(nameof(IsVolumeFlyoutOpen));

    public static readonly StyledProperty<bool> IsPointerOverFlyoutProperty =
        AvaloniaProperty.Register<VideoTransportControls, bool>(nameof(IsPointerOverFlyout));

    // --- Commands ---

    public static readonly StyledProperty<ICommand?> PlayPauseCommandProperty =
        AvaloniaProperty.Register<VideoTransportControls, ICommand?>(nameof(PlayPauseCommand));

    public static readonly StyledProperty<ICommand?> PreviousCommandProperty =
        AvaloniaProperty.Register<VideoTransportControls, ICommand?>(nameof(PreviousCommand));

    public static readonly StyledProperty<ICommand?> BackwardCommandProperty =
        AvaloniaProperty.Register<VideoTransportControls, ICommand?>(nameof(BackwardCommand));

    public static readonly StyledProperty<ICommand?> ForwardCommandProperty =
        AvaloniaProperty.Register<VideoTransportControls, ICommand?>(nameof(ForwardCommand));

    public static readonly StyledProperty<ICommand?> NextCommandProperty =
        AvaloniaProperty.Register<VideoTransportControls, ICommand?>(nameof(NextCommand));

    public static readonly StyledProperty<ICommand?> ToggleMuteCommandProperty =
        AvaloniaProperty.Register<VideoTransportControls, ICommand?>(nameof(ToggleMuteCommand));

    public static readonly StyledProperty<ICommand?> ToggleDanmakuCommandProperty =
        AvaloniaProperty.Register<VideoTransportControls, ICommand?>(nameof(ToggleDanmakuCommand));

    public static readonly StyledProperty<ICommand?> ChangeSpeedCommandProperty =
        AvaloniaProperty.Register<VideoTransportControls, ICommand?>(nameof(ChangeSpeedCommand));

    public static readonly StyledProperty<ICommand?> ToggleFullscreenCommandProperty =
        AvaloniaProperty
           .Register<VideoTransportControls, ICommand?>(nameof(ToggleFullscreenCommand));

    public static readonly StyledProperty<bool> IsFullscreenProperty =
        AvaloniaProperty.Register<VideoTransportControls, bool>(nameof(IsFullscreen));

    // --- Routed Events ---

    public static readonly RoutedEvent<RoutedEventArgs> SeekStartedEvent =
        RoutedEvent.Register<VideoTransportControls, RoutedEventArgs>(nameof(SeekStarted), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> SeekEndedEvent =
        RoutedEvent.Register<VideoTransportControls, RoutedEventArgs>(nameof(SeekEnded), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RangeBaseValueChangedEventArgs> SeekMovedEvent =
        RoutedEvent.Register<VideoTransportControls, RangeBaseValueChangedEventArgs>(nameof(SeekMoved),
                 RoutingStrategies.Bubble);

    private Button? _backwardButton;
    private Button? _forwardButton;

    private bool    _isSpeedFlyoutOpen;
    private Button? _nextButton;
    private Button? _playPauseButton;
    private Button? _previousButton;
    private Button? _speedButton;

    // --- Template Parts ---

    private Slider? _timeSlider;

    private Button? _volumeButton;

    private DispatcherTimer? _volumeCloseTimer;
    private Border?          _volumePanel;
    private Popup?           _volumePopup;

    public double CurrentTime
    {
        get => GetValue(CurrentTimeProperty);
        set => SetValue(CurrentTimeProperty, value);
    }

    public double Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public string TimeText
    {
        get => GetValue(TimeTextProperty);
        set => SetValue(TimeTextProperty, value);
    }

    public double PlaybackSpeed
    {
        get => GetValue(PlaybackSpeedProperty);
        set => SetValue(PlaybackSpeedProperty, value);
    }

    public string PlaybackSpeedText
    {
        get => GetValue(PlaybackSpeedTextProperty);
        set => SetValue(PlaybackSpeedTextProperty, value);
    }

    public double Volume
    {
        get => GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public double MaximumVolume
    {
        get => GetValue(MaximumVolumeProperty);
        set => SetValue(MaximumVolumeProperty, value);
    }

    public bool IsMuted
    {
        get => GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    public bool IsVolumeFlyoutOpen
    {
        get => GetValue(IsVolumeFlyoutOpenProperty);
        set => SetValue(IsVolumeFlyoutOpenProperty, value);
    }

    public bool IsPointerOverFlyout
    {
        get => GetValue(IsPointerOverFlyoutProperty);
        set => SetValue(IsPointerOverFlyoutProperty, value);
    }

    public ICommand? PlayPauseCommand
    {
        get => GetValue(PlayPauseCommandProperty);
        set => SetValue(PlayPauseCommandProperty, value);
    }

    public ICommand? PreviousCommand
    {
        get => GetValue(PreviousCommandProperty);
        set => SetValue(PreviousCommandProperty, value);
    }

    public ICommand? BackwardCommand
    {
        get => GetValue(BackwardCommandProperty);
        set => SetValue(BackwardCommandProperty, value);
    }

    public ICommand? ForwardCommand
    {
        get => GetValue(ForwardCommandProperty);
        set => SetValue(ForwardCommandProperty, value);
    }

    public ICommand? NextCommand
    {
        get => GetValue(NextCommandProperty);
        set => SetValue(NextCommandProperty, value);
    }

    public ICommand? ToggleMuteCommand
    {
        get => GetValue(ToggleMuteCommandProperty);
        set => SetValue(ToggleMuteCommandProperty, value);
    }

    public ICommand? ToggleDanmakuCommand
    {
        get => GetValue(ToggleDanmakuCommandProperty);
        set => SetValue(ToggleDanmakuCommandProperty, value);
    }

    public ICommand? ChangeSpeedCommand
    {
        get => GetValue(ChangeSpeedCommandProperty);
        set => SetValue(ChangeSpeedCommandProperty, value);
    }

    public ICommand? ToggleFullscreenCommand
    {
        get => GetValue(ToggleFullscreenCommandProperty);
        set => SetValue(ToggleFullscreenCommandProperty, value);
    }

    public bool IsFullscreen
    {
        get => GetValue(IsFullscreenProperty);
        set => SetValue(IsFullscreenProperty, value);
    }

    public event EventHandler<RoutedEventArgs> SeekStarted
    {
        add => AddHandler(SeekStartedEvent, value);
        remove => RemoveHandler(SeekStartedEvent, value);
    }

    public event EventHandler<RoutedEventArgs> SeekEnded
    {
        add => AddHandler(SeekEndedEvent, value);
        remove => RemoveHandler(SeekEndedEvent, value);
    }

    public event EventHandler<RangeBaseValueChangedEventArgs> SeekMoved
    {
        add => AddHandler(SeekMovedEvent, value);
        remove => RemoveHandler(SeekMovedEvent, value);
    }

    public void HideFlyouts()
    {
        CancelPendingVolumeClose();
        IsVolumeFlyoutOpen = false;
        UpdatePointerOverFlyoutState();
        _speedButton?.Flyout?.Hide();
    }

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

        if (_volumeButton != null)
        {
            _volumeButton.PointerEntered -= OnVolumeButtonPointerEntered;
            _volumeButton.PointerExited  -= OnVolumeButtonPointerExited;
        }

        if (_volumePanel != null)
        {
            _volumePanel.PointerEntered -= OnVolumePanelPointerEntered;
            _volumePanel.PointerExited  -= OnVolumePanelPointerExited;
        }

        if (_speedButton is { Flyout: not null })
        {
            _speedButton.Flyout.Opened -= OnSpeedFlyoutOpened;
            _speedButton.Flyout.Closed -= OnSpeedFlyoutClosed;
            if (_speedButton.Flyout is Flyout { Content: Control content })
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
        _volumeButton    = e.NameScope.Find<Button>("PART_VolumeButton");
        _volumePopup     = e.NameScope.Find<Popup>("PART_VolumePopup");
        _volumePanel     = e.NameScope.Find<Border>("PART_VolumePanel");
        _speedButton     = e.NameScope.Find<Button>("PART_SpeedButton");

        // Attach new events
        if (_timeSlider != null)
        {
            _timeSlider.AddHandler(PointerPressedEvent, OnSliderPointerPressed,
                                   RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
            _timeSlider.AddHandler(PointerReleasedEvent, OnSliderPointerReleased,
                                   RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
            _timeSlider.ValueChanged += OnSliderValueChanged;
        }

        if (_volumeButton != null)
        {
            _volumeButton.PointerEntered += OnVolumeButtonPointerEntered;
            _volumeButton.PointerExited  += OnVolumeButtonPointerExited;
        }

        if (_volumePanel != null)
        {
            _volumePanel.PointerEntered += OnVolumePanelPointerEntered;
            _volumePanel.PointerExited  += OnVolumePanelPointerExited;
        }

        if (_speedButton is not { Flyout: not null }) return;
        _speedButton.Flyout.Opened += OnSpeedFlyoutOpened;
        _speedButton.Flyout.Closed += OnSpeedFlyoutClosed;

        if (_speedButton.Flyout is not Flyout { Content: Control content3 }) return;
        content3.PointerEntered += OnFlyoutContentPointerEntered;
        content3.PointerExited  += OnFlyoutContentPointerExited;
    }

    private void OnFlyoutContentPointerEntered(object? sender, PointerEventArgs e)
    {
        _isSpeedFlyoutOpen = true;
        UpdatePointerOverFlyoutState();
    }

    private void OnFlyoutContentPointerExited(object? sender, PointerEventArgs e)
    {
        _isSpeedFlyoutOpen = false;
        UpdatePointerOverFlyoutState();
    }

    private void OnVolumeButtonPointerEntered(object? sender, PointerEventArgs e)
    {
        CancelPendingVolumeClose();
        IsVolumeFlyoutOpen = true;
        UpdatePointerOverFlyoutState();
    }

    private void OnVolumeButtonPointerExited(object? sender, PointerEventArgs e) => ScheduleVolumeClose();

    private void OnVolumePanelPointerEntered(object? sender, PointerEventArgs e)
    {
        CancelPendingVolumeClose();
        IsVolumeFlyoutOpen = true;
        UpdatePointerOverFlyoutState();
    }

    private void OnVolumePanelPointerExited(object? sender, PointerEventArgs e) => ScheduleVolumeClose();

    private void ScheduleVolumeClose()
    {
        _volumeCloseTimer ??= CreateVolumeCloseTimer();
        _volumeCloseTimer.Stop();
        _volumeCloseTimer.Start();
    }

    private DispatcherTimer CreateVolumeCloseTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        timer.Tick += OnVolumeCloseTimerTick;
        return timer;
    }

    private void OnVolumeCloseTimerTick(object? sender, EventArgs e)
    {
        CancelPendingVolumeClose();
        CheckAndCloseVolumePanel();
    }

    private void CancelPendingVolumeClose() => _volumeCloseTimer?.Stop();

    private void CheckAndCloseVolumePanel()
    {
        if (_volumeButton?.IsPointerOver == true ||
            _volumePanel?.IsPointerOver  == true ||
            _volumePopup?.IsPointerOver  == true)
        {
            IsVolumeFlyoutOpen = true;
            UpdatePointerOverFlyoutState();
            return;
        }

        IsVolumeFlyoutOpen = false;
        UpdatePointerOverFlyoutState();
    }

    private void UpdatePointerOverFlyoutState() => IsPointerOverFlyout = IsVolumeFlyoutOpen || _isSpeedFlyoutOpen;

    private void OnSpeedFlyoutOpened(object? sender, EventArgs e)
    {
        _isSpeedFlyoutOpen = true;
        UpdatePointerOverFlyoutState();
    }

    private void OnSpeedFlyoutClosed(object? sender, EventArgs e)
    {
        _isSpeedFlyoutOpen = false;
        UpdatePointerOverFlyoutState();
    }

    private void OnSliderPointerPressed(object? sender, PointerPressedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(SeekStartedEvent));

    private void OnSliderPointerReleased(object? sender, PointerReleasedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(SeekEndedEvent));

    private void OnSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e) =>
        RaiseEvent(new RangeBaseValueChangedEventArgs(e.OldValue, e.NewValue, SeekMovedEvent));
}
