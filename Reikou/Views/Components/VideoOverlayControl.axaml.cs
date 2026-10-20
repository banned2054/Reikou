using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Reikou.Models;
using System;
using System.Collections;
using System.Windows.Input;

namespace Reikou.Views.Components;

public class VideoOverlayControl : TemplatedControl
{
    // --- Styled Properties ---

    public static readonly StyledProperty<double> CurrentTimeProperty =
        AvaloniaProperty.Register<VideoOverlayControl, double>(nameof(CurrentTime));

    public static readonly StyledProperty<double> DurationProperty =
        AvaloniaProperty.Register<VideoOverlayControl, double>(nameof(Duration));

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<VideoOverlayControl, bool>(nameof(IsPlaying));

    public static readonly StyledProperty<string> TimeTextProperty =
        AvaloniaProperty.Register<VideoOverlayControl, string>(nameof(TimeText), "00:00 / 00:00");

    public static readonly StyledProperty<double> PlaybackSpeedProperty =
        AvaloniaProperty.Register<VideoOverlayControl, double>(nameof(PlaybackSpeed), 1.0);

    public static readonly StyledProperty<string> PlaybackSpeedTextProperty =
        AvaloniaProperty.Register<VideoOverlayControl, string>(nameof(PlaybackSpeedText), "倍速");

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<VideoOverlayControl, string>(nameof(Title), "TestMpv");

    public static readonly StyledProperty<double> VolumeProperty =
        AvaloniaProperty.Register<VideoOverlayControl, double>(nameof(Volume), 100.0);

    public static readonly StyledProperty<double> MaximumVolumeProperty =
        AvaloniaProperty.Register<VideoOverlayControl, double>(nameof(MaximumVolume), 130.0);

    public static readonly StyledProperty<bool> IsMutedProperty =
        AvaloniaProperty.Register<VideoOverlayControl, bool>(nameof(IsMuted));

    public static readonly StyledProperty<bool> IsVolumeFlyoutOpenProperty =
        AvaloniaProperty.Register<VideoOverlayControl, bool>(nameof(IsVolumeFlyoutOpen));

    public static readonly StyledProperty<bool> IsPointerOverFlyoutProperty =
        AvaloniaProperty.Register<VideoOverlayControl, bool>(nameof(IsPointerOverFlyout));

    public static readonly StyledProperty<IEnumerable?> PlaylistItemsProperty =
        AvaloniaProperty.Register<VideoOverlayControl, IEnumerable?>(nameof(PlaylistItems));

    public static readonly StyledProperty<bool> IsPlaylistPopupOpenProperty =
        AvaloniaProperty.Register<VideoOverlayControl, bool>(nameof(IsPlaylistPopupOpen));

    // --- Commands ---

    public static readonly StyledProperty<ICommand?> PlayPauseCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, ICommand?>(nameof(PlayPauseCommand));

    public static readonly StyledProperty<ICommand?> PreviousCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, ICommand?>(nameof(PreviousCommand));

    public static readonly StyledProperty<ICommand?> BackwardCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, ICommand?>(nameof(BackwardCommand));

    public static readonly StyledProperty<ICommand?> ForwardCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, ICommand?>(nameof(ForwardCommand));

    public static readonly StyledProperty<ICommand?> NextCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, ICommand?>(nameof(NextCommand));

    public static readonly StyledProperty<ICommand?> ToggleMuteCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, ICommand?>(nameof(ToggleMuteCommand));

    public static readonly StyledProperty<ICommand?> ToggleDanmakuCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, ICommand?>(nameof(ToggleDanmakuCommand));

    public static readonly StyledProperty<ICommand?> ChangeSpeedCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, ICommand?>(nameof(ChangeSpeedCommand));

    public static readonly StyledProperty<ICommand?> ToggleFullscreenCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, ICommand?>(nameof(ToggleFullscreenCommand));

    public static readonly StyledProperty<ICommand?> TakeScreenshotCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, ICommand?>(nameof(TakeScreenshotCommand));

    public static readonly StyledProperty<ICommand?> OpenSettingsCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, ICommand?>(nameof(OpenSettingsCommand));

    public static readonly StyledProperty<ICommand?> SelectPlaylistItemCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, ICommand?>(nameof(SelectPlaylistItemCommand));

    public static readonly StyledProperty<ICommand?> AddPlaylistItemCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, ICommand?>(nameof(AddPlaylistItemCommand));

    public static readonly StyledProperty<ICommand?> RemovePlaylistItemCommandProperty =
        AvaloniaProperty.Register<VideoOverlayControl, ICommand?>(nameof(RemovePlaylistItemCommand));

    public static readonly StyledProperty<bool> IsFullscreenProperty =
        AvaloniaProperty.Register<VideoOverlayControl, bool>(nameof(IsFullscreen));

    public static readonly StyledProperty<bool> IsPointerOverToolAreaProperty =
        AvaloniaProperty.Register<VideoOverlayControl, bool>(nameof(IsPointerOverToolArea));

    // --- Routed Events ---

    public static readonly RoutedEvent<RoutedEventArgs> SeekStartedEvent =
        RoutedEvent.Register<VideoOverlayControl, RoutedEventArgs>(nameof(SeekStarted), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> SeekEndedEvent =
        RoutedEvent.Register<VideoOverlayControl, RoutedEventArgs>(nameof(SeekEnded), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RangeBaseValueChangedEventArgs> SeekMovedEvent =
        RoutedEvent.Register<VideoOverlayControl, RangeBaseValueChangedEventArgs>(nameof(SeekMoved),
                 RoutingStrategies.Bubble);

    private Button? _playlistButton;
    private Popup?  _playlistPopup;
    private Border? _playlistPopupPanel;

    // --- 内部状态 ---
    private int _pointerOverCount;

    private Control? _rightToolArea;
    private int      _toolAreaPointerOverCount;
    private Control? _topRightToolArea;

    // --- Template Parts ---

    private VideoTransportControls? _transportControls;

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

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
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

    public IEnumerable? PlaylistItems
    {
        get => GetValue(PlaylistItemsProperty);
        set => SetValue(PlaylistItemsProperty, value);
    }

    public bool IsPlaylistPopupOpen
    {
        get => GetValue(IsPlaylistPopupOpenProperty);
        set => SetValue(IsPlaylistPopupOpenProperty, value);
    }

    public bool IsPlaylistPopupActive =>
        IsPlaylistPopupOpen || _playlistPopupPanel?.IsPointerOver == true;

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

    public ICommand? TakeScreenshotCommand
    {
        get => GetValue(TakeScreenshotCommandProperty);
        set => SetValue(TakeScreenshotCommandProperty, value);
    }

    public ICommand? OpenSettingsCommand
    {
        get => GetValue(OpenSettingsCommandProperty);
        set => SetValue(OpenSettingsCommandProperty, value);
    }

    public ICommand? AddPlaylistItemCommand
    {
        get => GetValue(AddPlaylistItemCommandProperty);
        set => SetValue(AddPlaylistItemCommandProperty, value);
    }

    public ICommand? RemovePlaylistItemCommand
    {
        get => GetValue(RemovePlaylistItemCommandProperty);
        set => SetValue(RemovePlaylistItemCommandProperty, value);
    }

    public ICommand? SelectPlaylistItemCommand
    {
        get => GetValue(SelectPlaylistItemCommandProperty);
        set => SetValue(SelectPlaylistItemCommandProperty, value);
    }

    public bool IsFullscreen
    {
        get => GetValue(IsFullscreenProperty);
        set => SetValue(IsFullscreenProperty, value);
    }

    public bool IsPointerOverToolArea
    {
        get => GetValue(IsPointerOverToolAreaProperty);
        set => SetValue(IsPointerOverToolAreaProperty, value);
    }

    // --- 属性：是否有指针在控件上（包括子元素） ---
    public bool IsPointerOverControl => _pointerOverCount > 0;

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
        _transportControls?.HideFlyouts();
        IsPlaylistPopupOpen = false;
    }

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
            _rightToolArea.PointerEntered -= OnToolAreaPointerEntered;
            _rightToolArea.PointerExited  -= OnToolAreaPointerExited;
        }

        if (_topRightToolArea != null)
        {
            _topRightToolArea.PointerEntered -= OnToolAreaPointerEntered;
            _topRightToolArea.PointerExited  -= OnToolAreaPointerExited;
        }

        if (_playlistButton != null) _playlistButton.Click -= OnPlaylistButtonClick;

        if (_playlistPopup != null)
        {
            _playlistPopup.Opened -= OnPlaylistPopupOpened;
            _playlistPopup.Closed -= OnPlaylistPopupClosed;
        }

        if (_playlistPopupPanel != null)
        {
            _playlistPopupPanel.RemoveHandler(Button.ClickEvent, OnPlaylistItemButtonClick);
            _playlistPopupPanel.PointerEntered -= OnPlaylistPopupPointerEntered;
            _playlistPopupPanel.PointerExited  -= OnPlaylistPopupPointerExited;
        }

        // Detach old events
        if (_transportControls != null)
        {
            _transportControls.SeekStarted -= OnSeekStartedFromTransport;
            _transportControls.SeekEnded   -= OnSeekEndedFromTransport;
            _transportControls.SeekMoved   -= OnSeekMovedFromTransport;
        }

        // Find new parts
        _transportControls  = e.NameScope.Find<VideoTransportControls>("PART_TransportControls");
        _rightToolArea      = e.NameScope.Find<Control>("PART_RightToolArea");
        _topRightToolArea   = e.NameScope.Find<Control>("PART_TopRightToolArea");
        _playlistButton     = e.NameScope.Find<Button>("PART_PlaylistButton");
        _playlistPopup      = e.NameScope.Find<Popup>("PART_PlaylistPopup");
        _playlistPopupPanel = e.NameScope.Find<Border>("PART_PlaylistPopupPanel");

        // Attach new events
        if (_transportControls != null)
        {
            _transportControls.SeekStarted += OnSeekStartedFromTransport;
            _transportControls.SeekEnded   += OnSeekEndedFromTransport;
            _transportControls.SeekMoved   += OnSeekMovedFromTransport;
        }

        if (_rightToolArea != null)
        {
            _rightToolArea.PointerEntered += OnToolAreaPointerEntered;
            _rightToolArea.PointerExited  += OnToolAreaPointerExited;
        }

        if (_topRightToolArea != null)
        {
            _topRightToolArea.PointerEntered += OnToolAreaPointerEntered;
            _topRightToolArea.PointerExited  += OnToolAreaPointerExited;
        }

        if (_playlistButton != null) _playlistButton.Click += OnPlaylistButtonClick;

        if (_playlistPopup != null)
        {
            _playlistPopup.Opened += OnPlaylistPopupOpened;
            _playlistPopup.Closed += OnPlaylistPopupClosed;
        }

        if (_playlistPopupPanel == null) return;
        _playlistPopupPanel.AddHandler(Button.ClickEvent, OnPlaylistItemButtonClick, RoutingStrategies.Bubble, true);
        _playlistPopupPanel.PointerEntered += OnPlaylistPopupPointerEntered;
        _playlistPopupPanel.PointerExited  += OnPlaylistPopupPointerExited;
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

    private void OnToolAreaPointerEntered(object? sender, PointerEventArgs e)
    {
        _toolAreaPointerOverCount++;
        IsPointerOverToolArea = _toolAreaPointerOverCount > 0;
    }

    private void OnToolAreaPointerExited(object? sender, PointerEventArgs e)
    {
        if (_toolAreaPointerOverCount > 0)
            _toolAreaPointerOverCount--;

        IsPointerOverToolArea = _toolAreaPointerOverCount > 0;
    }

    private void OnPlaylistButtonClick(object? sender, RoutedEventArgs e)
    {
        IsPlaylistPopupOpen = !IsPlaylistPopupOpen;
    }

    private void OnPlaylistPopupOpened(object? sender, EventArgs e)
    {
        IsPlaylistPopupOpen = true;
    }

    private void OnPlaylistPopupClosed(object? sender, EventArgs e)
    {
        IsPlaylistPopupOpen = false;
    }

    private void OnPlaylistItemButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button { DataContext: PlaylistItem item })
            return;

        if (SelectPlaylistItemCommand?.CanExecute(item) != true)
            return;

        SelectPlaylistItemCommand.Execute(item);
        e.Handled = true;
    }

    private void OnPlaylistPopupPointerEntered(object? sender, PointerEventArgs e)
    {
    }

    private void OnPlaylistPopupPointerExited(object? sender, PointerEventArgs e)
    {
    }
}
