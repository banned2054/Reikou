using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Reikou.ViewModels;

internal abstract class SettingsOptionViewModel(string title, string description, bool restartRequired)
    : INotifyPropertyChanged
{
    public string Title           { get; } = title;
    public string Description     { get; } = description;
    public bool   RestartRequired { get; } = restartRequired;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool Matches(string searchText) =>
        string.IsNullOrWhiteSpace(searchText)                          ||
        Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
        Description.Contains(searchText, StringComparison.OrdinalIgnoreCase);

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void RaisePropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed class ToggleSettingViewModel(
    string       title,
    string       description,
    bool         value,
    Action<bool> changed,
    bool         restartRequired = false)
    : SettingsOptionViewModel(title, description, restartRequired)
{
    private bool _value = value;

    public bool Value
    {
        get => _value;
        set
        {
            if (SetField(ref _value, value)) changed(value);
        }
    }
}

internal sealed record SettingChoice(string Label, int Value);

internal sealed class ChoiceSettingViewModel : SettingsOptionViewModel
{
    private readonly Action<int> _changed;

    private SettingChoice _selectedChoice;

    public ChoiceSettingViewModel(string                     title,
                                  string                     description,
                                  IEnumerable<SettingChoice> choices,
                                  int                        selectedValue,
                                  Action<int>                changed,
                                  bool                       restartRequired = false)
        : base(title, description, restartRequired)
    {
        Choices         = new ObservableCollection<SettingChoice>(choices);
        _selectedChoice = Choices.FirstOrDefault(choice => choice.Value == selectedValue) ?? Choices[0];
        _changed        = changed;
    }

    public ObservableCollection<SettingChoice> Choices { get; }

    public SettingChoice SelectedChoice
    {
        get => _selectedChoice;
        set
        {
            if (value != null && SetField(ref _selectedChoice, value)) _changed(value.Value);
        }
    }
}

internal sealed class NumberSettingViewModel(
    string         title,
    string         description,
    double         value,
    double         minimum,
    double         maximum,
    double         increment,
    string         suffix,
    Action<double> changed,
    bool           restartRequired = false)
    : SettingsOptionViewModel(title, description, restartRequired)
{
    private double _value = Math.Clamp(value, minimum, maximum);
    public  double Minimum   { get; } = minimum;
    public  double Maximum   { get; } = maximum;
    public  double Increment { get; } = increment;
    public  string Suffix    { get; } = suffix;

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
            if (value.HasValue) UpdateValue((double)value.Value);
        }
    }

    private void UpdateValue(double value)
    {
        var normalized = Math.Round(Math.Clamp(value, Minimum, Maximum), 4);
        if (!SetField(ref _value, normalized, nameof(SliderValue))) return;

        RaisePropertyChanged(nameof(NumericValue));
        changed(normalized);
    }
}

internal class TextSettingViewModel(
    string         title,
    string         description,
    string         value,
    Action<string> changed,
    bool           restartRequired = false)
    : SettingsOptionViewModel(title, description, restartRequired)
{
    private string _value = value;

    public string Value
    {
        get => _value;
        set
        {
            value ??= string.Empty;
            if (SetField(ref _value, value)) changed(value);
        }
    }
}

internal enum SettingPathKind
{
    File,
    Directory
}

internal sealed class PathSettingViewModel(
    string          title,
    string          description,
    string          value,
    SettingPathKind pathKind,
    Action<string>  changed,
    bool            restartRequired = false)
    : TextSettingViewModel(title, description, value, changed, restartRequired)
{
    public SettingPathKind PathKind { get; } = pathKind;
}

internal sealed class ReadOnlySettingViewModel(string title, string description, string value)
    : SettingsOptionViewModel(title, description, false)
{
    public string Value { get; } = value;
}

internal sealed class SettingsCategoryViewModel(
    string                               title,
    string                               description,
    IEnumerable<SettingsOptionViewModel> options)
{
    public string Title       { get; } = title;
    public string Description { get; } = description;

    public ObservableCollection<SettingsOptionViewModel> Options { get; } = new(options);
}
