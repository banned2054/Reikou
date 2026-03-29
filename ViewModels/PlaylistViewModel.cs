using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TestMpv.ViewModels;

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

public class PlaylistViewModel : INotifyPropertyChanged
{
    private int  _currentIndex = -1;
    private bool _isLooping;
    private bool _isShuffling;

    public ObservableCollection<PlaylistItem> Items { get; } = [];

    public int CurrentIndex
    {
        get => _currentIndex;
        private set
        {
            if (SetField(ref _currentIndex, value))
            {
                UpdatePlayingStates();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentItem)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanGoPrevious)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanGoNext)));
            }
        }
    }

    public PlaylistItem? CurrentItem =>
        _currentIndex >= 0 && _currentIndex < Items.Count ? Items[_currentIndex] : null;

    public bool CanGoPrevious => Items.Count > 1;

    public bool CanGoNext => Items.Count > 1;

    public bool IsLooping
    {
        get => _isLooping;
        set => SetField(ref _isLooping, value);
    }

    public bool IsShuffling
    {
        get => _isShuffling;
        set => SetField(ref _isShuffling, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void AddFile(string filePath)
    {
        if (Items.Any(x => x.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
            return;

        var item = new PlaylistItem
        {
            FilePath = filePath,
            Title    = Path.GetFileNameWithoutExtension(filePath)
        };

        Items.Add(item);

        if (Items.Count == 1)
        {
            CurrentIndex = 0;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanGoPrevious)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanGoNext)));
    }

    public void AddFiles(IEnumerable<string> filePaths)
    {
        foreach (var path in filePaths)
        {
            AddFile(path);
        }
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= Items.Count) return;

        Items.RemoveAt(index);

        if (Items.Count == 0)
        {
            CurrentIndex = -1;
        }
        else if (index <= _currentIndex)
        {
            CurrentIndex = Math.Max(0, _currentIndex - 1);
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanGoPrevious)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanGoNext)));
    }

    public void Clear()
    {
        Items.Clear();
        CurrentIndex = -1;
    }

    public PlaylistItem? GoTo(int index)
    {
        if (index < 0 || index >= Items.Count) return null;
        CurrentIndex = index;
        return CurrentItem;
    }

    public PlaylistItem? GoToPrevious()
    {
        if (Items.Count == 0) return null;

        if (IsShuffling)
        {
            var random = new Random();
            CurrentIndex = random.Next(Items.Count);
        }
        else
        {
            var newIndex = _currentIndex - 1;
            if (newIndex < 0)
            {
                newIndex = IsLooping ? Items.Count - 1 : 0;
            }

            CurrentIndex = newIndex;
        }

        return CurrentItem;
    }

    public PlaylistItem? GoToNext()
    {
        if (Items.Count == 0) return null;

        if (IsShuffling)
        {
            var random = new Random();
            CurrentIndex = random.Next(Items.Count);
        }
        else
        {
            var newIndex = _currentIndex + 1;
            if (newIndex >= Items.Count)
            {
                newIndex = IsLooping ? 0 : Items.Count - 1;
            }

            CurrentIndex = newIndex;
        }

        return CurrentItem;
    }

    public PlaylistItem? GoToFile(string filePath)
    {
        var index = Items.ToList().FindIndex(x => x.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return null;
        return GoTo(index);
    }

    public void MoveItem(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Items.Count) return;
        if (newIndex < 0 || newIndex >= Items.Count) return;
        if (oldIndex == newIndex) return;

        var item = Items[oldIndex];
        Items.RemoveAt(oldIndex);
        Items.Insert(newIndex, item);

        if (_currentIndex == oldIndex)
        {
            CurrentIndex = newIndex;
        }
        else if (oldIndex < _currentIndex && newIndex >= _currentIndex)
        {
            CurrentIndex--;
        }
        else if (oldIndex > _currentIndex && newIndex <= _currentIndex)
        {
            CurrentIndex++;
        }
    }

    private void UpdatePlayingStates()
    {
        for (var i = 0; i < Items.Count; i++)
        {
            Items[i].IsPlaying = i == _currentIndex;
        }
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
