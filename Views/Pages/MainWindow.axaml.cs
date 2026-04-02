using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;
using TestMpv.Utils;
using TestMpv.ViewModels;

namespace TestMpv.Views.Pages;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    // --- 状态标志 ---
    private bool _isDraggingSlider;
    private int  _updateTimerCooldown;

    // --- 键盘/快进逻辑 ---
    private const double SeekStep         = 5.0;
    private const double LongPressDelayMs = 500;

    private DateTime _keyPressStartTime;
    private bool     _isFastForwarding;
    private Key?     _currentHeldKey;

    // --- 鼠标/点击逻辑 ---
    private DateTime         _mousePressTime;
    private bool             _isMouseLongPressing;
    private bool             _isMousePhysicallyDown;
    private int              _clickCount;
    private DispatcherTimer? _clickResetTimer;
    private DispatcherTimer? _longPressTimer;

    private       DateTime         _lastMouseMoveTime;
    private       DispatcherTimer? _uiVisibilityTimer;
    private const double           HideDelaySeconds = 1;

    // --- 播放器状态 ---
    private double _originalSpeed = 1.0;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel                 =  new MainWindowViewModel();
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        InitializeCommands();     // 先初始化命令
        DataContext = _viewModel; // 再设置 DataContext，确保绑定发生时命令已就绪

        InitializeEvents();
        StartSyncTimer();
        StartUiVisibilityTimer(); // 启动UI显示检测定时器
    }

    private double _lastVolume = 100.0;

    private void InitializeCommands()
    {
        _viewModel.PlayPauseCommand = new RelayCommand(_ => Player.TogglePause());
        _viewModel.PreviousCommand = new RelayCommand(_ =>
        {
            var item = _viewModel.Playlist.GoToPrevious();
            if (item != null)
            {
                Player.LoadFileAsync(item.FilePath);
                UpdateTitleFromPlaylist();
            }
        });
        _viewModel.BackwardCommand = new RelayCommand(_ => Player.Service?.Command($"seek {-SeekStep} relative"));
        _viewModel.ForwardCommand  = new RelayCommand(_ => Player.Service?.Command($"seek {SeekStep} relative"));
        _viewModel.NextCommand = new RelayCommand(_ =>
        {
            var item = _viewModel.Playlist.GoToNext();
            if (item != null)
            {
                Player.LoadFileAsync(item.FilePath);
                UpdateTitleFromPlaylist();
            }
        });

        _viewModel.ToggleMuteCommand = new RelayCommand(_ =>
        {
            if (_viewModel.Volume > 0)
            {
                _lastVolume       = _viewModel.Volume;
                _viewModel.Volume = 0;
            }
            else
            {
                _viewModel.Volume = _lastVolume > 0 ? _lastVolume : 100.0;
            }
        });

        _viewModel.ToggleDanmakuCommand = new RelayCommand(_ =>
        {
            // TODO: Implement Danmaku toggle logic
        });

        _viewModel.ChangeSpeedCommand = new RelayCommand(_ =>
        {
            var speed = _ switch
            {
                double doubleSpeed                                                        => doubleSpeed,
                string stringSpeed when double.TryParse(stringSpeed, out var parsedSpeed) => parsedSpeed,
                _                                                                         => 1.0
            };

            Player.Service?.SetProperty("speed", speed);
        });

        _viewModel.ToggleFullscreenCommand = new RelayCommand(_ =>
        {
            WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
            _viewModel.IsFullscreen = WindowState == WindowState.FullScreen;
        });
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.Volume))
        {
            Player.Service?.SetProperty("volume", _viewModel.Volume);
        }
    }

    #region 初始化与核心定时器

    private void InitializeEvents()
    {
        // 拖拽文件支持
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        // 窗口输入事件
        KeyDown += OnWindowKeyDown;
        KeyUp   += OnWindowKeyUp;

        // 全局鼠标事件（使用 Tunnel 策略，防止被 Popup/LightDismiss 拦截）
        AddHandler(PointerMovedEvent, OnWindowPointerMoved, RoutingStrategies.Tunnel   | RoutingStrategies.Bubble);
        AddHandler(PointerExitedEvent, OnWindowPointerExited, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        // 初始化鼠标长按检测
        _longPressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(LongPressDelayMs) };
        _longPressTimer.Tick += (_, _) =>
        {
            _longPressTimer.Stop();
            if (_isMousePhysicallyDown) StartMouseLongPress();
        };

        // 播放器事件回调
        Player.FileLoaded += (_, fileName) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                // 更新当前播放项的标题（如果路径匹配）
                var currentItem = _viewModel.Playlist.CurrentItem;
                if (currentItem != null && System.IO.Path.GetFileNameWithoutExtension(currentItem.FilePath) == fileName)
                {
                    UpdateTitleFromPlaylist();
                }
                else
                {
                    OverlayControls.Title = fileName;
                }
            });
        };
    }

    private void UpdateTitleFromPlaylist()
    {
        var item = _viewModel.Playlist.CurrentItem;
        if (item != null)
        {
            var index = _viewModel.Playlist.CurrentIndex + 1;
            var count = _viewModel.Playlist.Items.Count;
            OverlayControls.Title = $"{item.Title} ({index}/{count})";
        }
    }

    private void StartSyncTimer()
    {
        // 负责 UI 状态同步（进度条、播放速度显示等）
        DispatcherTimer.Run(() =>
        {
            if (_isDraggingSlider) return true;
            if (_updateTimerCooldown > 0)
            {
                _updateTimerCooldown--;
                return true;
            }

            if (Player.Service != null)
            {
                _viewModel.Duration      = Player.Duration;
                _viewModel.CurrentTime   = Player.Position;
                _viewModel.IsPlaying     = Player.IsPlaying;
                _viewModel.PlaybackSpeed = Player.Service.GetProperty<double>("speed");
                _viewModel.Volume        = Player.Service.GetProperty<double>("volume");
            }

            return true;
        }, TimeSpan.FromMilliseconds(200));
    }

    private void StartUiVisibilityTimer()
    {
        _lastMouseMoveTime = DateTime.Now;

        // 每 500ms 检查一次是否需要隐藏 UI（比频繁启停 Timer 性能更好）
        _uiVisibilityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _uiVisibilityTimer.Tick += (_, _) =>
        {
            // 通过鼠标位置判断是否在控制面板区域内（底部控制栏高度约 60-80 像素）
            // 顶部标题栏也计入控制区域
            var isMouseOverControlArea =
                _lastMousePosition.Y >= (this.Bounds.Height - 80) || _lastMousePosition.Y <= 60;

            // 只要满足下列任何一个条件，就不隐藏面板，并且刷新计时
            if (isMouseOverControlArea ||
                _isDraggingSlider      ||
                OverlayControls.IsPointerOverFlyout)
            {
                _lastMouseMoveTime = DateTime.Now;
                return;
            }

            // 判断距离最后一次移动鼠标是否超过了设定的秒数
            if ((DateTime.Now - _lastMouseMoveTime).TotalSeconds >= HideDelaySeconds)
            {
                if (OverlayControls.Opacity > 0)
                {
                    HideOverlay();
                }
            }
        };
        _uiVisibilityTimer.Start();
    }

    #endregion

    #region 播放控制逻辑

    private void OnSeekStarted(object? sender, RoutedEventArgs e)
    {
        _isDraggingSlider = true;
    }

    private void OnSeekEnded(object? sender, RoutedEventArgs e)
    {
        if (!_isDraggingSlider) return;

        Player.Seek(_viewModel.CurrentTime);
        _updateTimerCooldown = 5;
        Dispatcher.UIThread.Post(() => _isDraggingSlider = false);
    }

    private void OnSeekMoved(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isDraggingSlider) Player.SeekFast(e.NewValue);
    }

    #endregion

    #region 键盘与鼠标交互 (长按倍速/快进)

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            Player.TogglePause();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Left && e.Key != Key.Right) return;

        if (_currentHeldKey == e.Key)
        {
            if (!_isFastForwarding && (DateTime.Now - _keyPressStartTime).TotalMilliseconds > LongPressDelayMs)
            {
                StartKeyFastForward(e.Key);
            }

            if (_isFastForwarding)
            {
                double dir = e.Key == Key.Right ? 1 : -1;
                Player.Service?.Command($"seek {dir * 2} relative+keyframes");
            }
        }
        else
        {
            _currentHeldKey    = e.Key;
            _keyPressStartTime = DateTime.Now;
        }

        e.Handled = true;
    }

    private void OnWindowKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Left && e.Key != Key.Right) return;

        if (e.Key == _currentHeldKey)
        {
            if (_isFastForwarding) StopKeyFastForward();
            else
            {
                if (e.Key == Key.Right)
                    _viewModel.ForwardCommand?.Execute(null);
                else
                    _viewModel.BackwardCommand?.Execute(null);
            }

            _currentHeldKey = null;
        }

        e.Handled = true;
    }

    private void StartKeyFastForward(Key key)
    {
        _isFastForwarding = true;
        _originalSpeed    = Player.Service?.GetProperty<double>("speed") ?? 1.0;
    }

    private void StopKeyFastForward()
    {
        _isFastForwarding = false;
        Player.Service?.SetProperty("speed", _originalSpeed);
    }

    private void OnVideoPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsLeftButtonPressed) return;

        _mousePressTime        = DateTime.Now;
        _isMousePhysicallyDown = true;
        _longPressTimer?.Start();
    }

    private void OnVideoPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _longPressTimer?.Stop();
        _isMousePhysicallyDown = false;

        if (_isMouseLongPressing) StopMouseLongPress();
        else if ((DateTime.Now - _mousePressTime).TotalMilliseconds < LongPressDelayMs)
        {
            HandleClickOrDoubleClick();
        }
    }

    private void StartMouseLongPress()
    {
        if (!_isMousePhysicallyDown) return;
        _isMouseLongPressing = true;
        _originalSpeed       = Player.Service?.GetProperty<double>("speed") ?? 1.0;
        Player.Service?.SetProperty("speed", 2.0);
    }

    private void StopMouseLongPress()
    {
        _isMouseLongPressing = false;
        Player.Service?.SetProperty("speed", _originalSpeed);
    }

    private void HandleClickOrDoubleClick()
    {
        _clickCount++;
        if (_clickCount % 2 == 0) Player.TogglePause();

        _clickResetTimer?.Stop();
        _clickResetTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _clickResetTimer.Tick += (_, _) =>
        {
            _clickCount = 0;
            _clickResetTimer.Stop();
        };
        _clickResetTimer.Start();
    }

    #endregion

    #region 文件处理与弹幕加载

    public async Task OpenMediaAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        if (StringUtils.IsVideoFile(filePath))
        {
            _viewModel.Playlist.LoadFromFile(filePath);
            var item = _viewModel.Playlist.CurrentItem;
            if (item != null)
            {
                await Player.LoadFileAsync(item.FilePath);
                UpdateTitleFromPlaylist();
            }
        }
        else if (StringUtils.IsSubtitleFile(filePath))
        {
            await Player.LoadSubtitleAsync(filePath);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        // 修复过时：e.Data 改为 e.DataTransfer，DataFormats.Files 改为 DataFormat.File
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Link
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        // 1. 调用异步扩展方法获取文件列表
        var files = e.DataTransfer.TryGetFile();

        // 2. 获取第一个文件
        var firstFile = files?.Path.LocalPath;

        if (string.IsNullOrEmpty(firstFile)) return;

        await OpenMediaAsync(firstFile);
    }

    #endregion

    #region 鼠标移动与 UI 隐藏显示控制

    private Avalonia.Point _lastMousePosition;

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        var currentPosition = e.GetPosition(this);
        // 忽略因为控件隐藏/显示导致的虚假鼠标移动事件
        if (Math.Abs(currentPosition.X - _lastMousePosition.X) < 1.0 &&
            Math.Abs(currentPosition.Y - _lastMousePosition.Y) < 1.0)
        {
            return;
        }

        _lastMousePosition = currentPosition;

        _lastMouseMoveTime = DateTime.Now;

        // 如果当前是隐藏状态，则唤醒它
        if (OverlayControls.Opacity < 1.0)
        {
            ShowOverlay();
        }
    }

    private void OnWindowPointerExited(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(this);
        // 检查是否真的离开了窗口物理范围，如果还在窗口内部（例如进入了Popup或遮罩层），则忽略
        if (pos.X > 0 && pos.X < this.Bounds.Width && pos.Y > 0 && pos.Y < this.Bounds.Height)
        {
            return;
        }

        // 鼠标移出软件窗口时，如果鼠标不在控制面板上，直接隐藏（优化体验）
        // 延时一下判断，防止由于鼠标移入Popup（Flyout）导致短暂的触发Exited
        DispatcherTimer.RunOnce(() =>
        {
            var isMouseOverControlArea =
                _lastMousePosition.Y >= (this.Bounds.Height - 80) || _lastMousePosition.Y <= 60;

            if (!isMouseOverControlArea &&
                !_isDraggingSlider      &&
                !OverlayControls.IsPointerOverFlyout)
            {
                HideOverlay();
            }
        }, TimeSpan.FromMilliseconds(100));
    }

    private void ShowOverlay()
    {
        OverlayControls.Opacity          = 1.0;
        OverlayControls.IsHitTestVisible = true;           // 允许点击
        this.Cursor                      = Cursor.Default; // 恢复显示鼠标指针
    }

    private void HideOverlay()
    {
        OverlayControls.Opacity          = 0.0;
        OverlayControls.IsHitTestVisible = false;          // 防止透明状态下误触
        OverlayControls.HideFlyouts();                     // 关闭音量等所有Flyout面板
        this.Cursor = new Cursor(StandardCursorType.None); // 隐藏鼠标指针
    }

    #endregion
}
