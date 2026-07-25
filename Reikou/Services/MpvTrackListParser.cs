using Reikou.Models;
using Reikou.Models.Enums;
using Reikou.Models.Structs;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Reikou.Services;

internal static class MpvTrackListParser
{
    private const           int MaximumTrackCount = 10_000;
    private static readonly int MpvNodeSize       = Marshal.SizeOf<MpvNode>();

    public static ImmutableArray<MediaTrackInfo> Parse(MpvEventProperty property)
    {
        if (property.format != MpvFormat.Node || property.data == nint.Zero)
        {
            return [];
        }

        var root = Marshal.PtrToStructure<MpvNode>(property.data);
        if (root.format != MpvFormat.NodeArray || root.list == nint.Zero)
        {
            return [];
        }

        var list = Marshal.PtrToStructure<MpvNodeList>(root.list);
        if (list.Num <= 0 || list.Num > MaximumTrackCount || list.Values == nint.Zero)
        {
            return [];
        }

        var tracks = ImmutableArray.CreateBuilder<MediaTrackInfo>(list.Num);
        for (var index = 0; index < list.Num; index++)
        {
            var node = ReadNode(list.Values, index);
            if (TryParseTrack(node, out var track))
            {
                tracks.Add(track);
            }
        }

        return tracks.MoveToImmutable();
    }

    private static bool TryParseTrack(MpvNode node, out MediaTrackInfo track)
    {
        track = null!;
        if (node.format != MpvFormat.NodeMap || node.list == nint.Zero)
        {
            return false;
        }

        var map = Marshal.PtrToStructure<MpvNodeList>(node.list);
        if (map.Num    <= 0                ||
            map.Num    > MaximumTrackCount ||
            map.Values == nint.Zero        ||
            map.Keys   == nint.Zero)
        {
            return false;
        }

        long    id         = 0;
        var     type       = MediaTrackType.Unknown;
        string? language   = null;
        string? title      = null;
        string? codec      = null;
        var     isSelected = false;
        var     isDefault  = false;
        var     isForced   = false;
        var     isExternal = false;

        for (var index = 0; index < map.Num; index++)
        {
            var keyPointer = Marshal.ReadIntPtr(map.Keys, checked(index * nint.Size));
            var key        = keyPointer == nint.Zero ? null : Marshal.PtrToStringUTF8(keyPointer);
            if (key == null)
            {
                continue;
            }

            var value = ReadNode(map.Values, index);
            switch (key)
            {
                case "id" :
                    id = ReadLong(value) ?? 0;
                    break;
                case "type" :
                    type = ParseTrackType(ReadString(value));
                    break;
                case "lang" :
                    language = Normalize(ReadString(value));
                    break;
                case "title" :
                    title = Normalize(ReadString(value));
                    break;
                case "codec" :
                    codec = Normalize(ReadString(value));
                    break;
                case "selected" :
                    isSelected = ReadFlag(value);
                    break;
                case "default" :
                    isDefault = ReadFlag(value);
                    break;
                case "forced" :
                    isForced = ReadFlag(value);
                    break;
                case "external" :
                    isExternal = ReadFlag(value);
                    break;
                case "external-filename" :
                    isExternal |= !string.IsNullOrWhiteSpace(ReadString(value));
                    break;
            }
        }

        if (id <= 0 || type == MediaTrackType.Unknown)
        {
            return false;
        }

        track = new MediaTrackInfo(id, type, language, title, codec, isSelected, isDefault, isForced, isExternal);
        return true;
    }

    private static MpvNode ReadNode(nint values, int index)
    {
        var pointer = nint.Add(values, checked(index * MpvNodeSize));
        return Marshal.PtrToStructure<MpvNode>(pointer);
    }

    private static long? ReadLong(MpvNode node) => node.format == MpvFormat.Long ? node.int64 : null;
    private static bool  ReadFlag(MpvNode node) => node.format == MpvFormat.Flag && node.flag != 0;

    private static string? ReadString(MpvNode node) => node.format == MpvFormat.String && node.str != nint.Zero
        ? Marshal.PtrToStringUTF8(node.str)
        : null;

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static MediaTrackType ParseTrackType(string? value) => value switch
    {
        "video" => MediaTrackType.Video,
        "audio" => MediaTrackType.Audio,
        "sub"   => MediaTrackType.Subtitle,
        _       => MediaTrackType.Unknown
    };
}
