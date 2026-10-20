using Avalonia.Controls;
using Reikou.Models;
using Reikou.Models.Enums;
using Reikou.Services;
using Reikou.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Reikou.Presentation.Views.Player;

internal sealed class DesktopVideoContextMenu
{
    private const    string                 AudioTrackGroup    = "audio-tracks";
    private const    string                 SubtitleTrackGroup = "subtitle-tracks";
    private readonly Action                 _disableAudioTrack;
    private readonly Action                 _disableSubtitleTrack;
    private readonly Func<bool>             _getIsFullscreen;
    private readonly Func<MpvPlaybackState> _getPlaybackState;
    private readonly Func<Task>             _loadExternalSubtitle;
    private readonly Action                 _openSettings;
    private readonly Action<long>           _selectAudioTrack;
    private readonly Action<long>           _selectSubtitleTrack;
    private readonly Action<double>         _setSubtitleDelay;
    private readonly Action                 _stop;
    private readonly Action                 _toggleFullscreen;

    private readonly Action _togglePause;

    public DesktopVideoContextMenu(Func<MpvPlaybackState> getPlaybackState,     Func<bool> getIsFullscreen,
                                   Action                 togglePause,          Action stop, Action toggleFullscreen,
                                   Action<long>           selectAudioTrack,     Action disableAudioTrack,
                                   Action<long>           selectSubtitleTrack,  Action disableSubtitleTrack,
                                   Func<Task>             loadExternalSubtitle, Action<double> setSubtitleDelay,
                                   Action                 openSettings)
    {
        _getPlaybackState     = getPlaybackState;
        _getIsFullscreen      = getIsFullscreen;
        _togglePause          = togglePause;
        _stop                 = stop;
        _toggleFullscreen     = toggleFullscreen;
        _selectAudioTrack     = selectAudioTrack;
        _disableAudioTrack    = disableAudioTrack;
        _selectSubtitleTrack  = selectSubtitleTrack;
        _disableSubtitleTrack = disableSubtitleTrack;
        _loadExternalSubtitle = loadExternalSubtitle;
        _setSubtitleDelay     = setSubtitleDelay;
        _openSettings         = openSettings;

        Menu.Opening += (_, _) => Rebuild();
    }

    public ContextMenu Menu { get; } = new();

    private void Rebuild()
    {
        var state = _getPlaybackState();

        Menu.ItemsSource = new Control[]
        {
            CreateCommandItem(state.IsPaused ? "播放" : "暂停", _togglePause),
            CreateCommandItem("停止", _stop),
            CreateCommandItem(_getIsFullscreen() ? "退出全屏" : "全屏", _toggleFullscreen),
            new Separator(),
            CreateAudioMenu(state),
            CreateSubtitleMenu(state),
            new Separator(),
            CreateCommandItem("设置...", _openSettings)
        };
    }

    private MenuItem CreateAudioMenu(MpvPlaybackState state)
    {
        var items = new List<Control>
        {
            CreateRadioItem("关闭音轨", state.AudioTrackId == null, AudioTrackGroup, _disableAudioTrack)
        };

        var tracks = state.Tracks.Where(track => track.Type == MediaTrackType.Audio).ToArray();
        if (tracks.Length == 0)
        {
            items.Add(CreateUnavailableItem("无可用音轨"));
        }
        else
        {
            items.Add(new Separator());
            items.AddRange(tracks.Select(track =>
                                             CreateRadioItem(FormatTrackLabel(track), track.Id == state.AudioTrackId,
                                                             AudioTrackGroup,
                                                             () => _selectAudioTrack(track.Id))));
        }

        return new MenuItem
        {
            Header      = "音轨",
            ItemsSource = items
        };
    }

    private MenuItem CreateSubtitleMenu(MpvPlaybackState state)
    {
        var items = new List<Control>
        {
            CreateAsyncCommandItem("加载外挂字幕...", _loadExternalSubtitle),
            new Separator(),
            CreateRadioItem("关闭字幕", state.SubtitleTrackId == null, SubtitleTrackGroup, _disableSubtitleTrack)
        };

        var tracks = state.Tracks.Where(track => track.Type == MediaTrackType.Subtitle).ToArray();
        if (tracks.Length == 0)
        {
            items.Add(CreateUnavailableItem("无可用字幕轨"));
        }
        else
        {
            items.Add(new Separator());
            items.AddRange(tracks.Select(track =>
                                             CreateRadioItem(FormatTrackLabel(track), track.Id == state.SubtitleTrackId,
                                                             SubtitleTrackGroup,
                                                             () => _selectSubtitleTrack(track.Id))));
        }

        var delay = state.SubtitleDelay;
        items.Add(new Separator());
        items.Add(CreateCommandItem("提前 0.5 秒", () => _setSubtitleDelay(delay - 0.5)));
        items.Add(CreateCommandItem("提前 0.1 秒", () => _setSubtitleDelay(delay - 0.1)));
        items.Add(CreateCommandItem("延后 0.1 秒", () => _setSubtitleDelay(delay + 0.1)));
        items.Add(CreateCommandItem("延后 0.5 秒", () => _setSubtitleDelay(delay + 0.5)));
        items.Add(new Separator());
        items.Add(CreateCommandItem("重置字幕延迟", () => _setSubtitleDelay(0)));

        return new MenuItem
        {
            Header      = "字幕",
            ItemsSource = items
        };
    }

    private static MenuItem CreateCommandItem(string header, Action action) => new()
    {
        Header  = header,
        Command = new RelayCommand(_ => action())
    };

    private static MenuItem CreateAsyncCommandItem(string header, Func<Task> action) => new()
    {
        Header  = header,
        Command = new RelayCommand(async _ => await action())
    };

    private static MenuItem CreateRadioItem(string header, bool isChecked, string groupName, Action action) => new()
    {
        Header     = header,
        ToggleType = MenuItemToggleType.Radio,
        GroupName  = groupName,
        IsChecked  = isChecked,
        Command    = new RelayCommand(_ => action())
    };

    private static MenuItem CreateUnavailableItem(string header) => new()
    {
        Header    = header,
        IsEnabled = false
    };

    private static string FormatTrackLabel(MediaTrackInfo track)
    {
        var details = new List<string>();
        AddDetail(details, track.Language);
        AddDetail(details, track.Title);
        AddDetail(details, track.Codec);

        if (track.IsExternal) details.Add("外挂");

        var description = details.Count == 0 ? $"轨道 {track.Id}" : string.Join(" · ", details);
        return $"{track.Id}: {description}";
    }

    private static void AddDetail(List<string> details, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            !details.Contains(value, StringComparer.OrdinalIgnoreCase))
            details.Add(value);
    }
}
