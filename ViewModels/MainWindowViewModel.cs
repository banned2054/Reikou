using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TestMpv.ViewModels;

internal class MainWindowViewModel : INotifyPropertyChanged
{
    private double _currentTime;
    private double _duration;
    private string _timeText = "00:00 / 00:00";
    private bool   _isPlaying;
    private double _playbackSpeed      = 1.0;
    private double _longPressJumpSpeed = 0.5;

    public event PropertyChangedEventHandler? PropertyChanged;

    public double LongPressJumpSpeed
    {
        get => _longPressJumpSpeed;
        set => SetField(ref _longPressJumpSpeed, value);
    }

    public double CurrentTime
    {
        get => _currentTime;
        set
        {
            if (SetField(ref _currentTime, value)) UpdateTimeText();
        }
    }

    public double Duration
    {
        get => _duration;
        set
        {
            if (SetField(ref _duration, value)) UpdateTimeText();
        }
    }

    public string TimeText
    {
        get => _timeText;
        set => SetField(ref _timeText, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetField(ref _isPlaying, value);
    }

    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set => SetField(ref _playbackSpeed, value);
    }

    private void UpdateTimeText()
    {
        if (Math.Abs(CurrentTime - _lastTimeTextCurrent)  < 0.1 &&
            Math.Abs(Duration    - _lastTimeTextDuration) < 0.1)
            return;

        _lastTimeTextCurrent  = CurrentTime;
        _lastTimeTextDuration = Duration;

        TimeText = $"{FormatTime(CurrentTime)} / {FormatTime(Duration)}";
    }

    private static string FormatTime(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
            seconds = 0;

        var ts = TimeSpan.FromSeconds(seconds);

        return ts switch
        {
            { TotalDays : >= 1 } => $"{(int)ts.TotalDays}.{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}",
            { TotalHours: >= 1 } => $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}",
            _                    => $"{ts.Minutes:D2}:{ts.Seconds:D2}"
        };
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private double _lastUiUpdateTime;

    public void UpdateTimeFromPlayer(double time, double duration)
    {
        // 只在时间变化超过 100ms 才更新 UI
        if (Math.Abs(time - _lastUiUpdateTime) < 0.1)
            return;

        _lastUiUpdateTime = time;

        CurrentTime = time;
        Duration    = duration;
    }

    private double _lastTimeTextCurrent;
    private double _lastTimeTextDuration;
}
