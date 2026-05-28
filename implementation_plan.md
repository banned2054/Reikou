# Playlist Item Styling Refactor Plan

## 现状分析 (Problem Analysis)
当前在 `VideoOverlayControl.axaml` 中，播放列表面板的 `ItemsControl` 中的每一项由一个 `Button` 包裹了一个 `Grid` 排版。

目前遇到的问题（**子项宽带随文字长短变化，无法占满整个面板，且两端不对齐**）的原因如下：
1. **容器未拉伸**：`DataTemplate` 中的 `<Button>` 默认采用内容决定的自适应宽度，没有指定 `HorizontalAlignment="Stretch"`，因此它的宽度其实等于 `名称宽度 + 时长宽度 + 留白`。
2. **星号失效**：虽然内部使用了 `<Grid ColumnDefinitions="Auto,*,Auto">`，并且期待 `*` 列能撑开空间将文件名与时长左右排开，但由于父级 `Button` 没有固定或拉伸（Stretch）的宽度，`Grid` 获取不到可用拉伸空间的上限，于是 `*` 列相当于退化成了按内容的实际宽度渲染。这就导致整个播放列表呈现参差不齐的状态。

## 解决计划 (Proposed Changes)

我们将对 `Reikou/Views/Components/VideoOverlayControl.axaml` 进行 XAML 布局上的调整以实现你的需求：

### XAML 布局修改点
1. **让列表项填满容器**
   给 `DataTemplate` 中的 `Button` 添加 `HorizontalAlignment="Stretch"`。这会强制每一个视频项水平拉伸，使其接近外部面板的完整宽度（扣除外框的 Padding 和 ScrollViewer 的滚动条空间）。

2. **左右对齐布局（已满足要求，只需拉伸生效）**
   当前的 `<Grid ColumnDefinitions="Auto,*,Auto">` 结构本来就是非常合理的。当 Button 被强制拉伸后：
   - `Column="0"` (`Auto`)：播放指示点（如果是当前播放的小蓝点）。
   - `Column="1"` (`*`)：文件名。此时它会占据所有剩余的弹性空间！如果文件名过长，`TextTrimming="CharacterEllipsis"` 就会生效变成 `文件名...` 的形式缩略，而不会撑破面板。
   - `Column="2"` (`Auto`)：时长文本。它会被自然而然地顶到最右侧，实现完美的左靠文件名，右靠时长。

3. **固定或统一样式高度调整**
   当前它内部有着 `Border Padding="10,8"`，所以高度略高于文本。为了防止不同的行之间感觉出现偏差（虽然只有一行字一般偏差不大），可以在按键或者内部 Border 添加约束：
   - 比如添加统一的稍微固定值 `Height="40"`（或 `MinHeight="40"`）结合 `VerticalAlignment="Center"`，使列表看起来更有秩序和精致感。

### Reikou/Views/Components/VideoOverlayControl.axaml
#### [修改] 
- 修改 `VideoOverlayControl.axaml` 中 `<DataTemplate DataType="models:PlaylistItem">` 下的 `<Button>`。
- 增加属性：`HorizontalAlignment="Stretch"`。
- （可选）加入固定的最小高度，让各个列表的高度保持一致。

## 验证计划 (Verification Plan)
- 打开视频文件播放。
- 呼出列表（List Button）的 Flyout 面板。
- 验证所有 Playlist 项宽度是否占据统一的整行宽度。
- 验证文件名是否在左侧，视频总时长被推挤到最右侧。
- 验证存在超长文件名的播放视频时，文本是否正常缩略截断而不导致水平撑开变形。

## Open Questions 待定反馈
请查阅上面的分析。这只是简单的 XAML 样式的处理。如果觉得方案没问题，请告诉我继续帮你执行修改！
