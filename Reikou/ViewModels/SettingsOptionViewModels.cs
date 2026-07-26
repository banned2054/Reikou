using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Reikou.ViewModels;

internal abstract class SettingsOptionViewModel : INotifyPropertyChanged
{
    protected SettingsOptionViewModel(string title, string description, bool restartRequired)
    {
        Title = title;
        Description = description;
        RestartRequired = restartRequired;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; }
    public string Description { get; }
    public bool RestartRequired { get; }

    public bool Matches(string searchText) =>
        string.IsNullOrWhiteSpace(searchText) ||
        Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
        Description.Contains(searchText, StringComparison.OrdinalIgnoreCase);

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void RaisePropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed class ToggleSettingViewModel : SettingsOptionViewModel
{
    private readonly Action<bool> _changed;
    private bool _value;

    public ToggleSettingViewModel(
        string title,
        string description,
        bool value,
        Action<bool> changed,
        bool restartRequired = false)
        : base(title, description, restartRequired)
    {
        _value = value;
        _changed = changed;
    }

    public bool Value
    {
        get => _value;
        set
        {
            if (SetField(ref _value, value))
            {
                _changed(value);
            }
        }
    }
}

internal sealed record SettingChoice(string Label, int Value);

internal sealed class ChoiceSettingViewModel : SettingsOptionViewModel
{
    private readonly Action<int> _changed;
    private SettingChoice _selectedChoice;

    public ChoiceSettingViewModel(
        string title,
        string description,
        IEnumerable<SettingChoice> choices,
        int selectedValue,
        Action<int> changed,
        bool restartRequired = false)
        : base(title, description, restartRequired)
    {
        Choices = new ObservableCollection<SettingChoice>(choices);
        _selectedChoice = Choices.FirstOrDefault(choice => choice.Value == selectedValue) ?? Choices[0];
        _changed = changed;
    }

    public ObservableCollection<SettingChoice> Choices { get; }

    public SettingChoice SelectedChoice
    {
        get => _selectedChoice;
        set
        {
            if (value != null && SetField(ref _selectedChoice, value))
            {
                _changed(value.Value);
            }
        }
    }
}

internal sealed class NumberSettingViewModel : SettingsOptionViewModel
{
    private readonly Action<double> _changed;
    private double _value;

    public NumberSettingViewModel(
        string title,
        string description,
        double value,
        double minimum,
        double maximum,
        double increment,
        string suffix,
        Action<double> changed,
        bool restartRequired = false)
        : base(title, description, restartRequired)
    {
        Minimum = minimum;
        Maximum = maximum;
        Increment = increment;
        Suffix = suffix;
        _value = Math.Clamp(value, minimum, maximum);
        _changed = changed;
    }

    public double Minimum { get; }
    public double Maximum { get; }
    public double Increment { get; }
    public string Suffix { get; }

    public double SliderValue
    {
        get => _value;
        set => UpdateValue(value);
    }

    public decimal? NumericValue
    {
        get => (decimal)_value;
        set
        {
            if (value.HasValue)
            {
                UpdateValue((double)value.Value);
            }
        }
    }

    private void UpdateValue(double value)
    {
        var normalized = Math.Round(Math.Clamp(value, Minimum, Maximum), 4);
        if (!SetField(ref _value, normalized, nameof(SliderValue)))
        {
            return;
        }

        RaisePropertyChanged(nameof(NumericValue));
        _changed(normalized);
    }
}

internal class TextSettingViewModel : SettingsOptionViewModel
{
    private readonly Action<string> _changed;
    private string _value;

    public TextSettingViewModel(
        string title,
        string description,
        string value,
        Action<string> changed,
        bool restartRequired = false)
        : base(title, description, restartRequired)
    {
        _value = value;
        _changed = changed;
    }

    public string Value
    {
        get => _value;
        set
        {
            value ??= string.Empty;
            if (SetField(ref _value, value))
            {
                _changed(value);
            }
        }
    }
}

internal enum SettingPathKind
{
    File,
    Directory
}

internal sealed class PathSettingViewModel : TextSettingViewModel
{
    public PathSettingViewModel(
        string title,
        string description,
        string value,
        SettingPathKind pathKind,
        Action<string> changed,
        bool restartRequired = false)
        : base(title, description, value, changed, restartRequired)
    {
        PathKind = pathKind;
    }

    public SettingPathKind PathKind { get; }
}

internal sealed class ReadOnlySettingViewModel : SettingsOptionViewModel
{
    public ReadOnlySettingViewModel(string title, string description, string value)
        : base(title, description, false)
    {
        Value = value;
    }

    public string Value { get; }
}

internal sealed class SettingsCategoryViewModel
{
    public SettingsCategoryViewModel(string title, string description, IEnumerable<SettingsOptionViewModel> options)
    {
        Title = title;
        Description = description;
        Options = new ObservableCollection<SettingsOptionViewModel>(options);
    }

    public string Title { get; }
    public string Description { get; }
    public ObservableCollection<SettingsOptionViewModel> Options { get; }
}
