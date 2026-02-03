using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Serilog;
using System;
using System.Linq;
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
    private DispatcherTimer? _longPressTimer; // [修正] 改为类成员变量，以便取消

    // --- 记录原始倍速 ---
    private double _originalSpeed = 1.0;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel  = new MainWindowViewModel();
        DataContext = _viewModel;

        InitializeEvents();
        StartSyncTimer();
    }

    private void InitializeEvents()
    {
        // 拖拽文件
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        // 进度条防冲突
        TimeSlider.AddHandler(PointerPressedEvent, (_, _) =>
        {
            _isDraggingSlider = true;
            Log.Debug("Slider drag start");
        }, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        TimeSlider.AddHandler(PointerReleasedEvent, OnSliderPointerReleased,
                              RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);

        // 键盘
        KeyDown += OnWindowKeyDown;
        KeyUp   += OnWindowKeyUp;

        // [修正] 初始化长按检测定时器
        _longPressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(LongPressDelayMs) };
        _longPressTimer.Tick += (s, e) =>
        {
            _longPressTimer.Stop(); // 触发一次后自动停止
            if (_isMousePhysicallyDown)
            {
                StartMouseLongPress();
            }
        };
    }

    private void StartSyncTimer()
    {
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

    #region 播放控制 (Play/Pause/Slider)

    private void OnPlayPauseClick(object sender, RoutedEventArgs e) => Player.TogglePause();

    private void OnSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isDraggingSlider) Player.SeekFast(e.NewValue);
    }

    private void OnSliderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingSlider) return;

        Player.Seek(_viewModel.CurrentTime);
        _updateTimerCooldown = 5;
        Dispatcher.UIThread.Post(() => _isDraggingSlider = false);
    }

    #endregion

    #region 文件拖拽 (Drag & Drop)

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Link : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var files     = e.Data.GetFiles();
        var firstFile = files?.FirstOrDefault()?.Path.LocalPath;
        if (!string.IsNullOrEmpty(firstFile))
        {
            await Player.LoadFileAsync(firstFile);
        }
    }

    #endregion

    #region 键盘交互 (Short seek / Long press fast-forward)

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
                Player.Service?.Command($"seek {dir * _viewModel.LongPressJumpSpeed} relative+keyframes");
                Player.Service?.Command($"show-text \"{(dir > 0 ? ">>" : "<<")} 快速搜索\"");
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
            if (_isFastForwarding)
            {
                StopKeyFastForward();
            }
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
        Player.Service?.Command("show-text \"恢复播放\"");
    }

    #endregion

    #region 鼠标交互 (Double Click / Long Press Speed)

    private void OnVideoPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsLeftButtonPressed) return;

        _mousePressTime        = DateTime.Now;
        _isMousePhysicallyDown = true;

        // [修正] 重置并启动长按计时器
        _longPressTimer?.Stop();
        _longPressTimer?.Start();
    }

    private void OnVideoPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // [修正] 关键点：松开鼠标时，立即扼杀长按计时器
        // 这样即使 500ms 到了，Tick 事件也不会触发
        _longPressTimer?.Stop();

        _isMousePhysicallyDown = false;

        if (_isMouseLongPressing)
        {
            StopMouseLongPress();
        }
        else
        {
            if ((DateTime.Now - _mousePressTime).TotalMilliseconds < LongPressDelayMs)
            {
                HandleClickOrDoubleClick();
            }
        }
    }

    private void StartMouseLongPress()
    {
        // 再次确认状态，防止极端的竞态条件
        if (!_isMousePhysicallyDown) return;

        _isMouseLongPressing = true;
        _originalSpeed       = Player.Service?.GetProperty<double>("speed") ?? 1.0;
        Player.Service?.SetProperty("speed", 2.0);
        Player.Service?.Command("show-text \"倍速播放 2.0x\"");
    }

    private void StopMouseLongPress()
    {
        _isMouseLongPressing = false;
        Player.Service?.SetProperty("speed", _originalSpeed);
        Player.Service?.Command("show-text \"恢复播放\"");
    }

    private void HandleClickOrDoubleClick()
    {
        _clickCount++;

        if (_clickCount % 2 == 0)
        {
            Player.TogglePause();
        }

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
}
