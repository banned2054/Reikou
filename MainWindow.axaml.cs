using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using TestMpv.ViewModels;

namespace TestMpv;

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
        _viewModel  = new MainWindowViewModel();
        DataContext = _viewModel;

        InitializeEvents();
        StartSyncTimer();
        StartUiVisibilityTimer(); // 启动UI显示检测定时器
    }

    #region 初始化与核心定时器

    private void InitializeEvents()
    {
        // 拖拽文件支持
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        // 进度条交互：防止进度条更新与用户拖拽冲突 [cite: 2]
        TimeSlider.AddHandler(PointerPressedEvent, (_, _) => { _isDraggingSlider = true; },
                              RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        TimeSlider.AddHandler(PointerReleasedEvent, OnSliderPointerReleased,
                              RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);

        // 窗口输入事件
        KeyDown += OnWindowKeyDown;
        KeyUp   += OnWindowKeyUp;

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
            Dispatcher.UIThread.Post(() => { VideoText.Text = fileName; }); // 更新 UI 标题 [cite: 2]
        };
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
            // 条件1：鼠标悬浮在控制面板上（OverlayControls）
            // 条件2：正在拖拽进度条
            if (OverlayControls.IsPointerOver || _isDraggingSlider)
            {
                // 只要满足上述条件，就不断刷新最后活动时间，防止隐藏
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

    private void OnPlayPauseClick(object sender, RoutedEventArgs e) => Player.TogglePause();

    private void OnSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isDraggingSlider) Player.SeekFast(e.NewValue); // 拖动时使用快速跳转 [cite: 3]
    }

    private void OnSliderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingSlider) return;

        Player.Seek(_viewModel.CurrentTime); // 释放时进行精确跳转 [cite: 2, 3]
        _updateTimerCooldown = 5;
        Dispatcher.UIThread.Post(() => _isDraggingSlider = false);
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
                Player.Service?.Command($"seek {dir * 2} relative+keyframes"); // 长按快进逻辑 [cite: 2]
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
                double dir = e.Key == Key.Right ? 1 : -1;
                Player.Service?.Command($"seek {dir * SeekStep} relative");
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
        _longPressTimer?.Start(); // 开启长按判定 [cite: 2]
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
        Player.Service?.SetProperty("speed", 2.0); // 触发2倍速播放 [cite: 2]
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
        // 需要引用 using Avalonia.Input;
        var files = e.DataTransfer.TryGetFile();

        // 2. 获取第一个文件
        var firstFile = files?.Path.LocalPath;

        if (string.IsNullOrEmpty(firstFile)) return;

        var extension = System.IO.Path.GetExtension(firstFile).ToLower();
        if (IsVideoFile(extension)) await Player.LoadFileAsync(firstFile);
        else if (IsSubtitleFile(extension)) await Player.LoadSubtitleAsync(firstFile);
    }

    private bool IsVideoFile(string    ext) => new[] { ".mp4", ".mkv", ".avi", ".mov", ".flv" }.Contains(ext);
    private bool IsSubtitleFile(string ext) => new[] { ".ass", ".srt", ".vtt", ".sub" }.Contains(ext);

    #endregion

    #region 鼠标移动与 UI 隐藏显示控制

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        _lastMouseMoveTime = DateTime.Now;

        // 如果当前是隐藏状态，则唤醒它
        if (OverlayControls.Opacity < 1.0)
        {
            ShowOverlay();
        }
    }

    private void OnWindowPointerExited(object? sender, PointerEventArgs e)
    {
        // 鼠标移出软件窗口时，如果鼠标不在控制面板上，直接隐藏（优化体验）
        if (!OverlayControls.IsPointerOver && !_isDraggingSlider)
        {
            HideOverlay();
        }
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
        OverlayControls.IsHitTestVisible = false;                               // 防止透明状态下误触
        this.Cursor                      = new Cursor(StandardCursorType.None); // 隐藏鼠标指针
    }

    #endregion
}
