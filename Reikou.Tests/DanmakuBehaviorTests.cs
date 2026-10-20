using Banned.Avalonia.Danmaku.Models.Playback;
using Banned.Danmaku.Models;
using NUnit.Framework;
using Reikou.Models.Settings;
using Reikou.Services;
using Reikou.Services.Danmaku;
using System.Text.Json;

namespace Reikou.Tests;

/// <summary>
///     本地弹幕数据层非视觉验证：处理管道、设置归一化与 JSON 兼容、关联持久化
///     （含 auto-save revision、损坏回退）、provider 关联/pending 重开恢复/延迟覆盖/
///     负偏移过滤/密度顺序。无外部测试包，退出码 0 为全部通过。
/// </summary>
[TestFixture]
public sealed class DanmakuBehaviorTests
{
    private const float Tolerance = 0.000001f;

    [Test]
    public void ProcessingPipeline() => CheckProcessor();

    [Test]
    public void ProcessingOptions() => CheckProcessingOptions();

    [Test]
    public void SettingsAndJsonCompatibility() => CheckSettingsAndJson();

    [Test]
    public async Task AssociationStorePersistence() => await CheckAssociationStoreAsync();

    [Test]
    public async Task XmlProviderBehavior() => await CheckProviderAsync();

    private static void Check(string name, bool condition) => Assert.That(condition, Is.True, name);

    private static DanmakuItem Item(double seconds, DanmakuMode mode, string text) => new()
    {
        Time     = TimeSpan.FromSeconds(seconds),
        Mode     = mode,
        Text     = text,
        FontSize = 25,
        Color    = 0xFFFFFF
    };

    private static DanmakuItem[] SampleItems() =>
    [
        Item(10, DanmakuMode.ScrollRightToLeft, "scroll a"),
        Item(20, DanmakuMode.Top, "top one"),
        Item(5, DanmakuMode.Bottom, "bottom one"),
        Item(10.5, DanmakuMode.ScrollLeftToRight, "scroll b")
    ];

    private static void CheckProcessor()
    {
        // 基线：无配置全量保留并按时间排序。
        var plain = DanmakuDocumentProcessor.Process(SampleItems(), null);
        Check("plain: keeps all items", plain.Items.Count == 4);
        Check("plain: sorted by time",
              plain.Items.Select(item => item.Time.TotalSeconds).SequenceEqual([5, 10, 10.5, 20]));

        // 延迟：正值延后、负值提前、提前出界丢弃。
        var delayed = DanmakuDocumentProcessor.Process(SampleItems(),
                                                       new DanmakuProcessingOptions
                                                           { Delay = TimeSpan.FromSeconds(2.5) });
        Check("delay+: shifts all items", delayed.Items.Count    == 4                         &&
                                          delayed.Items[0].Time  == TimeSpan.FromSeconds(7.5) &&
                                          delayed.Items[^1].Time == TimeSpan.FromSeconds(22.5));

        var earlier =
            DanmakuDocumentProcessor.Process([
                Item(10, DanmakuMode.ScrollRightToLeft, "a"), Item(1, DanmakuMode.ScrollRightToLeft, "b")
            ], new DanmakuProcessingOptions { Delay = TimeSpan.FromSeconds(-2) });
        Check("delay-: shifts and drops before zero", earlier.Items.Count   == 1                       &&
                                                      earlier.Items[0].Time == TimeSpan.FromSeconds(8) &&
                                                      earlier.Items[0].Text == "a");

        // 关键词：子串、OrdinalIgnoreCase；空白关键词被 Normalize 丢弃，空白文本保留。
        var keywords = DanmakuDocumentProcessor.Process([
            Item(1, DanmakuMode.ScrollRightToLeft, "hello SPAM world"),
            Item(2, DanmakuMode.ScrollRightToLeft, "clean"),
            Item(3, DanmakuMode.ScrollRightToLeft, "  ")
        ], new DanmakuProcessingOptions { BlockedKeywords = ["spam", ""] });
        Check("keywords: case-insensitive substring, blank text kept", keywords.Items is
        [
            { Text: "clean" },
            { Text: "  " }
        ]);

        // 类型开关：Scroll 覆盖双向滚动；Code 不受开关限制。
        var typed = DanmakuDocumentProcessor.Process([
            Item(1, DanmakuMode.ScrollRightToLeft, "r"),
            Item(2, DanmakuMode.ScrollLeftToRight, "l"),
            Item(3, DanmakuMode.Top, "t"),
            Item(4, DanmakuMode.Bottom, "b"),
            Item(5, (DanmakuMode)8, "code")
        ], new DanmakuProcessingOptions { ShowTop = false });
        Check("types: hides top only", typed.Items.Select(item => item.Text).SequenceEqual(["r", "l", "b", "code"]));

        var noScroll = DanmakuDocumentProcessor.Process([
            Item(1, DanmakuMode.ScrollRightToLeft, "r"),
            Item(2, DanmakuMode.Advanced, "adv"),
            Item(3, DanmakuMode.Top, "t")
        ], new DanmakuProcessingOptions { ShowScroll = false });
        Check("types: hides scroll and advanced", noScroll.Items.Select(item => item.Text).SequenceEqual(["t"]));

        // 数量密度：每个 1 秒窗口保留最早 N 条；0 不限制；同秒保持输入顺序。
        var dense = DanmakuDocumentProcessor.Process([
            Item(10.9, DanmakuMode.ScrollRightToLeft, "late"),
            Item(10.1, DanmakuMode.ScrollRightToLeft, "first"),
            Item(10.5, DanmakuMode.ScrollRightToLeft, "second"),
            Item(11.2, DanmakuMode.ScrollRightToLeft, "next-window")
        ], new DanmakuProcessingOptions { MaxCountPerSecond = 2 });
        Check("density: keeps earliest per 1s window",
              dense.Items.Select(item => item.Text).SequenceEqual(["first", "second", "next-window"]));

        var stable = DanmakuDocumentProcessor.Process([
            Item(5, DanmakuMode.ScrollRightToLeft, "a"),
            Item(5, DanmakuMode.ScrollRightToLeft, "b"),
            Item(5, DanmakuMode.ScrollRightToLeft, "c")
        ], new DanmakuProcessingOptions { MaxCountPerSecond = 2 });
        Check("density: stable input order for equal timestamps",
              stable.Items.Select(item => item.Text).SequenceEqual(["a", "b"]));

        var unlimited =
            DanmakuDocumentProcessor.Process([
                Item(1, DanmakuMode.ScrollRightToLeft, "1"), Item(1.2, DanmakuMode.ScrollRightToLeft, "2")
            ], new DanmakuProcessingOptions { MaxCountPerSecond = 0 });
        Check("density: zero means unlimited", unlimited.Items.Count == 2);

        // 组合顺序：类型/关键词过滤后再偏移，密度按偏移后的时间分窗。
        var combined = DanmakuDocumentProcessor.Process([
            Item(9.8, DanmakuMode.ScrollRightToLeft, "a"),
            Item(9.9, DanmakuMode.Top, "b"),
            Item(10.0, DanmakuMode.ScrollRightToLeft, "c")
        ], new DanmakuProcessingOptions
        {
            Delay             = TimeSpan.FromSeconds(0.5),
            ShowTop           = false,
            MaxCountPerSecond = 1
        });
        Check("combined: filter -> shift -> quota on shifted time",
              combined.Items.Select(item => item.Text).SequenceEqual(["a"]));
    }

    private static void CheckProcessingOptions()
    {
        var normalized = new DanmakuProcessingOptions
        {
            Delay             = TimeSpan.FromSeconds(9999),
            BlockedKeywords   = ["  a  ", "", "A", null!, "b"],
            MaxCountPerSecond = 5000
        }.Normalize();
        Check("options: clamps delay to ±600", normalized.Delay == TimeSpan.FromSeconds(600));
        Check("options: trims/dedupes keywords case-insensitively",
              normalized.BlockedKeywords.SequenceEqual(["a", "b"], StringComparer.Ordinal));
        Check("options: clamps max count per second", normalized.MaxCountPerSecond == 1000);

        // 值相等：不同列表实例、大小写不同但内容相同应相等（供“仅实际变化才重赋 provider”）。
        var left  = new DanmakuProcessingOptions { BlockedKeywords = ["x"], Delay = TimeSpan.FromSeconds(1) };
        var right = new DanmakuProcessingOptions { BlockedKeywords = ["X"], Delay = TimeSpan.FromSeconds(1) };
        Check("options: value equality ignores list instance and case", left == right);
        Check("options: value inequality on change", left                    != left with { ShowTop = false });

        var fromSettings = DanmakuProcessingOptions.FromSettings(new DanmakuSettings
        {
            DelaySeconds      = 3,
            BlockedKeywords   = ["k1", "k2"],
            ShowScroll        = false,
            MaxCountPerSecond = 7
        });
        Check("fromSettings: maps all fields",
              fromSettings.Delay == TimeSpan.FromSeconds(3)            &&
              fromSettings.BlockedKeywords.SequenceEqual(["k1", "k2"]) &&
              fromSettings is { ShowScroll: false, ShowTop: true, ShowBottom: true, MaxCountPerSecond: 7 });
    }

    private static void CheckSettingsAndJson()
    {
        var normalized = new DanmakuSettings
        {
            DelaySeconds      = 9999,
            BlockedKeywords   = ["", " kw ", "KW"],
            MaxCountPerSecond = -5
        }.Normalize();
        Check("settings: clamps delay/count and normalizes keywords",
              Math.Abs(normalized.DelaySeconds - 600)           < Tolerance &&
              normalized.BlockedKeywords?.SequenceEqual(["kw"]) == true     &&
              normalized is { MaxCountPerSecond: 0, Density: 100 });

        // JSON sourcegen：新字段 camelCase 往返。
        var settings = new AppSettings
        {
            Danmaku = new DanmakuSettings
            {
                DelaySeconds      = -1.5,
                BlockedKeywords   = ["ad"],
                ShowTop           = false,
                MaxCountPerSecond = 12
            }
        };
        var json     = JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings);
        var restored = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings);
        Check("json: danmaku fields round-trip",
              json.Contains("\"delaySeconds\"", StringComparison.Ordinal)         &&
              json.Contains("\"blockedKeywords\"", StringComparison.Ordinal)      &&
              json.Contains("\"maxCountPerSecond\"", StringComparison.Ordinal)    &&
              restored is not null                                                &&
              Math.Abs(restored.Danmaku.DelaySeconds - -1.5)          < Tolerance &&
              restored.Danmaku.BlockedKeywords?.SequenceEqual(["ad"]) == true     &&
              restored.Danmaku is { ShowTop: false, MaxCountPerSecond: 12 });

        // 旧 settings.json 无新字段时：布尔默认值由主构造参数默认值承载（sourcegen 对缺省
        // 成员赋 default(T) 而非属性初始化器），关键词列表经 Normalize 回退为空集合。
        var legacy = JsonSerializer.Deserialize("{\"schemaVersion\":2,\"danmaku\":{\"enabled\":true,\"density\":50}}",
                                                AppSettingsJsonContext.Default.AppSettings);
        if (legacy is null)
        {
            Check("json: legacy settings deserialize", false);
            return;
        }

        var legacyDanmaku = legacy.Danmaku;
        Check("json: legacy danmaku keeps ctor defaults for missing members",
              legacyDanmaku.Enabled && legacyDanmaku is
              {
                  ShowScroll: true, ShowTop: true, ShowBottom: true, DelaySeconds: 0, MaxCountPerSecond: 0, Density: 50
              });
        var normalizedLegacy = legacy.Normalize();
        Check("json: legacy settings normalize to safe defaults",
              normalizedLegacy.Danmaku.BlockedKeywords is not null &&
              normalizedLegacy.Danmaku.BlockedKeywords.Count == 0  &&
              normalizedLegacy.Danmaku.ShowTop);
    }

    private static async Task CheckAssociationStoreAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "reikou-danmaku-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var storePath = Path.Combine(directory, "associations.json");

        try
        {
            var store = new DanmakuAssociationStore(storePath);

            Check("store: missing entry returns null", store.GetEntry(@"D:\Video\a.mkv") is null);

            store.SetEntry(@"D:\Video\a.MKV", @"D:\Danmaku\a.xml");
            if (OperatingSystem.IsWindows())
                Check("store: video path key ignores case", store.GetEntry(@"D:\VIDEO\A.mkv")?.XmlPath is not null);
            else
                Check("store: video path key is case sensitive",
                      store.GetEntry(@"D:\VIDEO\A.mkv") is null && store.GetEntry(@"D:\Video\a.MKV") is not null);
            Check("store: new entry inherits global delay", store.GetEntry(@"D:\Video\a.MKV")?.DelaySeconds is null);

            // 换 XML 保留已有 override delay；显式传值才覆盖。
            store.TryUpdateDelay(@"D:\Video\a.MKV", 2.5);
            store.SetEntry(@"D:\Video\a.MKV", @"D:\Danmaku\a2.xml");
            store.SetEntry(@"D:\Video\a.MKV", @"D:\Danmaku\a2.xml");
            Check("store: reselecting same xml keeps delay override",
                  store.GetEntry(@"D:\Video\a.MKV")?.DelaySeconds == 2.5);
            var entry = store.GetEntry(@"D:\Video\a.MKV");
            Check("store: replace xml keeps delay override",
                  DanmakuPaths.AreEqual(entry?.XmlPath ?? "", Path.GetFullPath(@"D:\Danmaku\a2.xml")) &&
                  entry?.DelaySeconds == 2.5);

            store.SetEntry(@"D:\Video\b.mkv", @"D:\Danmaku\b.xml", 0);
            Check("store: explicit zero overrides", store.GetEntry(@"D:\Video\b.mkv")?.DelaySeconds == 0);

            store.TryUpdateDelay(@"D:\Video\b.mkv", null);
            Check("store: null delay restores global inheritance",
                  store.GetEntry(@"D:\Video\b.mkv")?.DelaySeconds is null);

            Check("store: update delay on missing entry fails", !store.TryUpdateDelay(@"D:\Video\zz.mkv", 1));
            Check("store: remove entry",
                  store.RemoveEntry(@"D:\Video\b.mkv") && store.GetEntry(@"D:\Video\b.mkv") is null);

            // 显式保存 + 重新加载。
            await store.SaveAsync();
            var reloaded  = new DanmakuAssociationStore(storePath);
            var persisted = reloaded.GetEntry(@"D:\Video\a.MKV");
            Check("store: persists across instances", persisted is not null &&
                                                      DanmakuPaths.AreEqual(persisted.XmlPath,
                                                                            Path.GetFullPath(@"D:\Danmaku\a2.xml")) &&
                                                      persisted.DelaySeconds == 2.5);

            var raw = File.ReadAllText(storePath);
            Check("store: file is camelCase versioned json",
                  raw.Contains("\"version\": 1", StringComparison.Ordinal) &&
                  raw.Contains("\"xmlPath\"", StringComparison.Ordinal)    &&
                  raw.Contains("\"delaySeconds\"", StringComparison.Ordinal));

            // 主文件损坏回退 .bak。bak 只保留上一次替换前的状态，这里只断言早期条目可恢复。
            File.Copy(storePath, storePath + ".bak", true);
            store.SetEntry(@"D:\Video\c.mkv", @"D:\Danmaku\c.xml", 1);
            await store.SaveAsync();
            await DrainAutoSaveAsync();
            File.WriteAllText(storePath, "{ not json");
            var recovered = new DanmakuAssociationStore(storePath);
            Check("store: corrupt file falls back to backup",
                  recovered.GetEntry(@"D:\Video\a.MKV") is not null);

            // 未来版本 schema 不被信任，但主文件无效时仍回退 .bak 的有效历史数据。
            File.WriteAllText(storePath, "{\"version\":99,\"entries\":{}}");
            var futureVersion = new DanmakuAssociationStore(storePath);
            Check("store: unsupported version falls back to backup entries",
                  futureVersion.GetEntry(@"D:\Video\a.MKV") is not null);
            await futureVersion.SaveAsync();
            File.WriteAllText(storePath, "broken again");
            Check("store: saving recovered data preserves valid backup",
                  new DanmakuAssociationStore(storePath).GetEntry(@"D:\Video\a.MKV") is not null);

            // 非法路径条目跳过，不影响合法条目。
            await DrainAutoSaveAsync();
            File.WriteAllText(storePath,
                              "{\"version\":1,\"entries\":{\"D:\\\\bad\\u0000path\":{\"xmlPath\":\"x.xml\"}," +
                              "\"D:\\\\video\\\\good.mkv\":{\"xmlPath\":\"D:\\\\danmaku\\\\good.xml\"}}}");
            var withBadEntry = new DanmakuAssociationStore(storePath);
            Check("store: invalid entries skipped, valid kept",
                  DanmakuPaths.AreEqual(withBadEntry.GetEntry(@"D:\video\good.mkv")?.XmlPath ?? "",
                                        Path.GetFullPath(@"D:\danmaku\good.xml")));

            // auto-save revision：快照后到达的变更仍会落盘（循环保存到最新）。
            var autoStore = new DanmakuAssociationStore(storePath);
            autoStore.SetEntry(@"D:\Video\s1.mkv", @"D:\Danmaku\s1.xml");
            autoStore.SetEntry(@"D:\Video\s2.mkv", @"D:\Danmaku\s2.xml", 4);
            var autoPersisted = await WaitForAsync(() =>
            {
                try
                {
                    var probe = new DanmakuAssociationStore(storePath);
                    return probe.GetEntry(@"D:\Video\s1.mkv") is not null &&
                           probe.GetEntry(@"D:\Video\s2.mkv")?.DelaySeconds == 4;
                }
                catch (IOException)
                {
                    return false;
                }
            }, TimeSpan.FromSeconds(10));
            Check("store: auto-save persists latest revision", autoPersisted);
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    private static async Task CheckProviderAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "reikou-danmaku-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var xmlPath   = Path.Combine(directory, "sample.xml");
            var otherPath = Path.Combine(directory, "other.xml");
            await File.WriteAllTextAsync(xmlPath,
                                         "<i><d p=\"10,1,25,16777215,0,0,0,0\">hello</d>" +
                                         "<d p=\"20,5,25,16777215,0,0,0,0\">top</d>"      +
                                         "<d p=\"5,4,25,16777215,0,0,0,0\">bottom</d></i>");
            await File.WriteAllTextAsync(otherPath,
                                         "<i><d p=\"7,1,25,16777215,0,0,0,0\">other</d></i>");

            var videoPath = Path.Combine(directory, "video.mkv");
            var mediaId   = new PlaybackMediaId(videoPath);
            var store     = new DanmakuAssociationStore(Path.Combine(directory, "provider-associations.json"));

            // 无处理配置：解析 + 全量返回。
            var provider = new XmlDanmakuSourceProvider(store, DanmakuProcessingOptions.Default);
            var bare     = await provider.WithSource(mediaId, xmlPath).LoadAsync(mediaId, CancellationToken.None);
            Check("provider: parses xml without processing", bare?.Items is [{ Text: "bottom" }, _, _]);

            // 同一 store 的新 provider 自动恢复关联。
            await store.SaveAsync();
            var freshProvider = new XmlDanmakuSourceProvider(store, DanmakuProcessingOptions.Default);
            var restored      = await freshProvider.LoadAsync(mediaId, CancellationToken.None);
            Check("provider: auto-restores persisted association", restored is not null && restored.Items.Count == 3);

            // pending：先选 XML 后开媒体，首个 LoadAsync 绑定并持久化；
            // 用全新 store 实例 + 全新 provider 重开恢复。
            var pendingStoreFile = Path.Combine(directory, "pending-associations.json");
            var pendingStore     = new DanmakuAssociationStore(pendingStoreFile);
            var pendingProvider = new XmlDanmakuSourceProvider(pendingStore, DanmakuProcessingOptions.Default)
               .WithSource(null, otherPath);
            var pendingMedia = new PlaybackMediaId(Path.Combine(directory, "pending-video.mp4"));
            var pendingDoc   = await pendingProvider.LoadAsync(pendingMedia, CancellationToken.None);
            Check("provider: pending binds on first load", pendingDoc is not null &&
                                                           pendingDoc.Items[0].Text == "other");
            // 必须用发生过绑定改动的同一 store 实例落盘；新建实例会先读盘（可能还没写入）。
            await pendingStore.SaveAsync();
            var reopenedDoc = await new XmlDanmakuSourceProvider(new DanmakuAssociationStore(pendingStoreFile),
                                                                 DanmakuProcessingOptions.Default)
               .LoadAsync(pendingMedia, CancellationToken.None);
            Check("provider: pending association restores via reopened store+provider",
                  reopenedDoc is not null && reopenedDoc.Items[0].Text == "other");

            // 处理配置生效 + WithProcessing 不可变。
            var processingProvider = new XmlDanmakuSourceProvider(store,
                                                                  new DanmakuProcessingOptions
                                                                      { ShowTop = false, BlockedKeywords = ["hello"] });
            var filtered = await processingProvider.LoadAsync(mediaId, CancellationToken.None);
            Check("provider: applies processing options", filtered is not null &&
                                                          filtered.Items.Select(item => item.Text)
                                                                  .SequenceEqual(["bottom"]));

            var originalOptions = processingProvider.ProcessingOptions;
            var updated = processingProvider.WithProcessing(new DanmakuProcessingOptions
            {
                Delay             = TimeSpan.FromSeconds(1),
                MaxCountPerSecond = 1
            });
            Check("provider: with-processing returns new instance, original unchanged",
                  !ReferenceEquals(processingProvider, updated)           &&
                  processingProvider.ProcessingOptions == originalOptions &&
                  updated.ProcessingOptions.Delay      == TimeSpan.FromSeconds(1));
            var withDelay = await updated.LoadAsync(mediaId, CancellationToken.None);
            Check("provider: delay applies through provider",
                  withDelay is not null && withDelay.Items.All(item => item.Time >= TimeSpan.FromSeconds(6)));

            // 延迟语义：显式值覆盖全局、null 继承全局、显式 0 取消偏移。
            var delayStore = new DanmakuAssociationStore(Path.Combine(directory, "delay-associations.json"));
            var delayProvider0 = new XmlDanmakuSourceProvider(delayStore,
                                                              new DanmakuProcessingOptions
                                                                  { Delay = TimeSpan.FromSeconds(2) });
            delayStore.SetEntry(videoPath, xmlPath, 3);
            var overridden = await delayProvider0.LoadAsync(mediaId, CancellationToken.None);
            Check("provider: explicit per-media delay overrides global",
                  overridden is not null && overridden.Items[0].Time == TimeSpan.FromSeconds(8));

            delayStore.TryUpdateDelay(videoPath, null);
            var inherited = await delayProvider0.LoadAsync(mediaId, CancellationToken.None);
            Check("provider: null per-media delay inherits global",
                  inherited is not null && inherited.Items[0].Time == TimeSpan.FromSeconds(7));

            delayStore.TryUpdateDelay(videoPath, 0);
            var explicitZero = await delayProvider0.LoadAsync(mediaId, CancellationToken.None);
            Check("provider: explicit zero delay disables global shift",
                  explicitZero is not null && explicitZero.Items[0].Time == TimeSpan.FromSeconds(5));

            // 换 XML 时保留 override delay（provider WithSource -> store SetEntry）。
            delayStore.TryUpdateDelay(videoPath, 5);
            var replaced = await delayProvider0.WithSource(mediaId, otherPath)
                                               .LoadAsync(mediaId, CancellationToken.None);
            Check("provider: replacing xml keeps delay override",
                  replaced?.Items is [{ Text: "other" }]                                   &&
                  replaced.Items[0].Time                       == TimeSpan.FromSeconds(12) &&
                  delayStore.GetEntry(videoPath)?.DelaySeconds == 5);

            // 负偏移：早于 0 的弹幕被过滤。
            var negativeStore = new DanmakuAssociationStore(Path.Combine(directory, "negative-associations.json"));
            negativeStore.SetEntry(videoPath, xmlPath, -6);
            var negative = await new XmlDanmakuSourceProvider(negativeStore, DanmakuProcessingOptions.Default)
               .LoadAsync(mediaId, CancellationToken.None);
            Check("provider: negative offset drops before-zero items",
                  negative is not null &&
                  negative.Items.Select(item => (item.Text, item.Time.TotalSeconds))
                          .SequenceEqual([("hello", 4.0), ("top", 14.0)]));

            // 过期/取消：已取消令牌立即抛 OperationCanceledException（解析本身不做 token 中断，
            // 由前后 ct 检查 + 库侧忽略过期 revision 结果兜底）。
            var canceledSource = new CancellationTokenSource();
            canceledSource.Cancel();
            var canceled = false;
            try
            {
                await provider.LoadAsync(mediaId, canceledSource.Token);
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }

            Check("provider: canceled token throws operation canceled", canceled);

            // 关联指向已删除 XML 时不绑定，返回 null 让视频继续播放。
            var goneStore = new DanmakuAssociationStore(Path.Combine(directory, "gone-associations.json"));
            var goneMedia = new PlaybackMediaId(Path.Combine(directory, "gone-video.mkv"));
            goneStore.SetEntry(goneMedia.Value, otherPath);
            await goneStore.SaveAsync();
            File.Delete(otherPath);
            var goneDoc = await new XmlDanmakuSourceProvider(goneStore, DanmakuProcessingOptions.Default)
               .LoadAsync(goneMedia, CancellationToken.None);
            Check("provider: deleted xml resolves to null", goneDoc is null);
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    private static async Task<bool> WaitForAsync(Func<bool> probe, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (probe()) return true;

            await Task.Delay(100);
        }

        return probe();
    }

    /// <summary>
    ///     等待后台 auto-save 单飞循环排空。其写入内容与显式 SaveAsync 相同，
    ///     这里只避免它在后续测试手工覆写 fixture 文件之后再回写。
    /// </summary>
    private static Task DrainAutoSaveAsync() => Task.Delay(300);

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, true);
        }
        catch (IOException)
        {
        }
    }
}
