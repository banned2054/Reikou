using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TestMpv.Models;

public class PlaylistItem : INotifyPropertyChanged
{
    private string _filePath = string.Empty;
    private string _title    = string.Empty;
    private double _duration;
    private bool   _isPlaying;

    public string FilePath
    {
        get => _filePath;
        set => SetField(ref _filePath, value);
    }

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public double Duration
    {
        get => _duration;
        set => SetField(ref _duration, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetField(ref _isPlaying, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
