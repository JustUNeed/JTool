# JTool 架构说明（面向 AI 协作）

> 本文件的目的：让任意一个新的对话能仅凭此文档建立对 JTool 的完整心智模型，
> 无需重新阅读全部源码即可定位模块、理解数据流、回答修改类问题。
> 阅读顺序建议：先看「一句话概述」→「核心抽象」→「数据流」→「目录地图」，再按需深入。

---

## 一句话概述

JTool 是一个 .NET 8 + WPF 的 Windows 桌面常驻悬浮球工具。屏幕边缘有一个悬浮球，
鼠标移入展开为「快捷面板」；把文件 / 图片 / 文本 / URL 拖到球上会进入「投放态」，
弹出若干「投放槽」，松手即执行对应动作。三大功能：快捷启动、文件搬运、剪贴看板。

技术栈：WPF (MVVM) + CommunityToolkit.Mvvm + Microsoft.Extensions.DependencyInjection
+ gong-wpf-dragdrop（网格排序）+ Hardcodet.NotifyIcon.Wpf（托盘）。

---

## 设计哲学（最重要，先读这段）

整个项目围绕一个核心思想：**控件自治 + 双扩展线解耦**。

1. **宿主只是容器**。`FloatWindow`（快捷面板）不知道里面放了什么控件，它只负责：
   悬浮球↔面板↔投放槽三态切换、窗口拖动、尺寸缩放、鼠标移入移出折叠。
   它通过 `ItemsControl` 绑定一个 `ObservableCollection<IPanelWidget>` 来渲染任意控件。

2. **每个功能是一个自治控件**。快捷网格、图片看板、文本看板各自是一个 UserControl，
   各自持有自己的 ViewModel、各自管理自己的数据、各自持久化到独立的 json 文件。
   控件之间互不引用、互不知道彼此存在。

3. **两条正交的扩展线**：
   - `IPanelWidget`：决定「一个控件如何在面板里显示与自治」。
   - `IDropSlotProvider`：决定「拖入某种数据时，贡献哪些投放槽（落点）」。
   一个控件可以只实现其中一个，也可以两个都实现（看板和网格都是两个都实现）。

4. **拖拽数据三段解耦**：解析（`DropParser`）→ 路由汇总（`DropRouter`）→ 落点执行（各 `DropSlot`）。
   View 层和控件**永远不直接接触 `IDataObject` 或解析正则**，只读强类型的 `DropContext`。

> 记住这条规则：**新增功能 = 新增一个控件 + 在 `App.BuildServices` 注册两行，宿主和其它控件零改动。**

---

## 核心抽象（四个关键类型）

| 类型 | 位置 | 职责 | 谁实现/产出 |
|------|------|------|------------|
| `IPanelWidget` | `Hosting/IPanelWidget.cs` | 面板控件契约：`Title` / `HasContent` / `View` | 三个 Widget 的 Control 类 |
| `DropContext` | `DragDrop/DropContext.cs` | 拖入数据的强类型解析结果（Files/Folders/Bitmap/ImageUrl/Text） | `DropParser.Parse()` 产出 |
| `IDropSlotProvider` | `DragDrop/DropSlot.cs` | 「给定 DropContext，贡献 0..N 个投放槽」 | 各 Widget VM + `TargetDirSlotProvider` |
| `DropSlot` | `DragDrop/DropSlot.cs` | 一个落点 = `Title` + `Action<DropContext> OnDrop` | provider 内 new 出来 |

`DropRouter`（`DragDrop/DropRouter.cs`）是把上面串起来的协调者：
`CanAccept(data)` → `Parse(data)` 得到 `DropContext` → `CollectSlots(ctx)` 遍历所有
注册的 provider 汇总出本次要显示的所有 `DropSlot`。

---

## 数据流（两条主线）

### 主线 A：拖入数据 → 投放执行

Copy
用户拖入 (IDataObject) │ ▼ FloatWindow.Window_DragEnter DropRouter.CanAccept() ── 不接受 → 显示 None 光标 │ 接受 ▼ DropRouter.Parse() → DropContext（强类型，含 Files/Bitmap/ImageUrl/Text 等） │ ▼ FloatWindow.ShowDrop() 切到投放态 DropRouter.CollectSlots(ctx) → 遍历所有 IDropSlotProvider │ ├─ ShortcutGridViewModel → 有文件时给「＋添加快捷方式」 │ ├─ ImageBoardViewModel → 有位图/图片URL时给「图片到看板」 │ ├─ TextBoardViewModel → 纯文本时给「文本到看板」 │ └─ TargetDirSlotProvider → 给「登记目录」+ 每个已登记目录一个「→ 目录名」 │ ▼ FloatWindow.BuildSlots() 把每个 DropSlot 渲染成一个可投放的 Border 按钮 用户松手到某个槽 │ ▼ Border.Drop → slot.OnDrop(ctx) 对应控件/provider 自行处理入库 + 持久化

Copy
关键点：投放槽的「有哪些」「松手做什么」完全由 provider 决定，View 只是把它们画出来。
要增删一种落点，改对应 provider 的 `GetSlots()`，不动 View。

### 主线 B：控件自治的增删改持久化

每个 Widget VM 内部都是同一套模式（以快捷网格为例）：
- 内存集合 `ObservableCollection<XxxItemViewModel> Items`（绑定 UI）
- 数据模型容器 `XxxData`（含 `List<XxxItem>`，序列化用）
- `JsonStore<XxxData>`（`Core/JsonStore.cs`）负责读写自己的 json
- 增删/排序后调用私有 `Save()` 落盘
- `HasContent` 决定该控件在面板里是否显示（空则隐藏标题与内容）

---

## 目录地图（按层）

JTool/ ├─ App.xaml(.cs) ★ DI 容器装配 + 启动。新增控件就改这里 │ ├─ Core/ 基础设施，跨控件复用，无业务 │ ├─ Paths.cs 所有持久化路径集中点（%AppData%\JTool\） │ ├─ Logger.cs 统一日志（替代旧代码里的空 catch），写 log.txt │ ├─ JsonStore.cs 泛型 json 读写，各控件持久化都用它 │ └─ NativeMethods.cs Win32 P/Invoke：图标提取 + 鼠标屏幕坐标命中测试 │ ├─ Hosting/ 宿主层（容器，不含业务） │ ├─ IPanelWidget.cs 面板控件契约 │ ├─ FloatWindow.xaml(.cs) 三态切换/窗口拖动/缩放/折叠/投放槽渲染 │ └─ FloatWindowViewModel.cs窗口几何(持久化 window.json)、可见性、Widgets列表、设置/退出命令 │ ├─ DragDrop/ 数据处理解耦层 │ ├─ DropContext.cs 强类型解析结果 │ ├─ DropParser.cs IDataObject → DropContext（含所有正则/格式判断） │ ├─ DropSlot.cs DropSlot + IDropSlotProvider 接口 │ └─ DropRouter.cs 解析 + 汇总所有 provider 的槽 │ ├─ Services/ 纯 IO 服务，无 UI、无业务分支 │ ├─ IconService.cs 提取文件/目录图标，ConcurrentDictionary + 容量上限 │ ├─ WebImageService.cs 网络图片下载，带超时/大小上限/Content-Type 校验 │ ├─ FileMoveService.cs 文件搬运，用 VB.FileSystem（带系统进度框，无注入隐患） │ ├─ TargetDirStore.cs 已登记的目标目录，持久化 targetdirs.json │ └─ TargetDirSlotProvider 把目标目录暴露成投放槽（实现 IDropSlotProvider） │ ├─ Widgets/ 三个自治控件 │ ├─ ShortcutGrid/ 快捷网格：实现 IPanelWidget + IDropSlotProvider + IDropTarget(排序) │ │ ├─ ShortcutGridControl.xaml(.cs) UserControl，实现 IPanelWidget │ │ ├─ ShortcutGridViewModel.cs 增删/排序/启动/持久化 + 贡献投放槽 │ │ ├─ ShortcutItemViewModel.cs 单项（含图标） │ │ └─ ShortcutItem.cs model + ShortcutData 容器 │ ├─ ImageBoard/ 图片看板：实现 IPanelWidget + IDropSlotProvider │ │ ├─ ImageBoardControl.xaml(.cs) │ │ ├─ ImageBoardViewModel.cs 位图保存/URL下载占位/复制/删除/持久化 │ │ ├─ ImageBoardItemViewModel.cs 单项（含缩略图 + 加载态 Ready/Loading/Failed） │ │ └─ ImageBoardItem.cs model + 容器 + ImageLoadState 枚举 │ └─ TextBoard/ 文本看板：实现 IPanelWidget + IDropSlotProvider │ ├─ TextBoardControl.xaml(.cs) │ ├─ TextBoardViewModel.cs 增删/复制/持久化 + 贡献投放槽 │ ├─ TextBoardItemViewModel.cs 单项（含单行 Preview） │ └─ TextBoardItem.cs model + 容器 │ └─ Settings/ 全局设置 ├─ AppSettings.cs model：AutoStart / Topmost / BallSize / EnableImageDownload ├─ SettingsService.cs 加载保存 settings.json + 开机自启写注册表 Run 键 ├─ SettingsViewModel.cs 绑定层 └─ SettingsWindow.xaml(.cs) 设置窗口（已删除旧的快捷项配置 UI）

Copy
---

## 持久化布局（全部在 `%AppData%\JTool\`）

| 文件 / 目录 | 拥有者 | 内容 |
|------------|--------|------|
| `shortcuts.json` | ShortcutGridViewModel | 快捷项列表（Name, Path） |
| `images.json` | ImageBoardViewModel | 图片看板索引（仅文件名 + 时间） |
| `board/images/` | ImageBoardViewModel | 实际图片文件（img_时间戳.png） |
| `texts.json` | TextBoardViewModel | 文本看板条目 |
| `targetdirs.json` | TargetDirStore | 已登记的目标目录路径 |
| `settings.json` | SettingsService | 全局设置 |
| `window.json` | FloatWindowViewModel | 窗口位置与面板尺寸 |
| `log.txt` | Logger | 运行日志 |

设计原则：**每个控件存自己的文件，互不依赖**。不读旧版 `config.json`（旧数据已弃用）。

---

## DI 装配（App.BuildServices）

所有对象在 `App.xaml.cs` 的 `BuildServices()` 里注册一次，构造函数自动注入。要点：

- **Widget VM 注册为 Singleton**：因为同一个 VM 既要被 Control 当 DataContext（IPanelWidget），
  又要被当作 IDropSlotProvider 收集投放槽，必须是同一个实例，所以用
  `sp => sp.GetRequiredService<XxxViewModel>()` 复用单例。
- **IPanelWidget 的注册顺序 = 面板里控件的显示顺序**。
- `DropRouter` 通过构造注入 `IEnumerable<IDropSlotProvider>` 自动拿到所有 provider。
- `FloatWindowViewModel` 通过构造注入 `IEnumerable<IPanelWidget>` 自动拿到所有控件。

---

## 三态切换机制（FloatWindow 的核心交互）

窗口 `WindowStyle=None` + 透明背景，靠切换三个 Border 的可见性 + 改窗口尺寸实现形态变化：

- **悬浮球态 `ShowBallOnly()`**：只显示 BallPanel，窗口 = 球大小。
- **面板态 `ShowMenu()`**：鼠标移入球触发，显示 MenuPanel，窗口 = PanelWidth×PanelHeight。
- **投放态 `ShowDrop()`**：拖入时触发，显示 DropPanel（投放槽），高度自适应。

折叠逻辑：用 `DispatcherTimer`（120ms）+ `NativeMethods.GetCursorScreenPoint()` 做命中测试，
因为 WPF 在透明窗 + 子元素场景下 `MouseLeave` 会误触发，必须用屏幕坐标兜底判断鼠标是否真在窗口内。

---

## 如何扩展（高频问题的标准答案）

### 新增一个面板控件（例如「代码片段看板」）
1. 在 `Widgets/` 下建目录，仿照 TextBoard 写 `XxxControl.xaml(.cs)`（实现 `IPanelWidget`）
   + `XxxViewModel.cs`（用 `JsonStore<XxxData>` 自持久化）+ model。
2. 若需要接收拖拽，让 VM 实现 `IDropSlotProvider.GetSlots()`。
3. 在 `App.BuildServices` 注册：VM 一行（Singleton）、IPanelWidget 一行、（可选）IDropSlotProvider 一行。
4. 宿主与其它控件无需改动。

### 新增一种拖入数据类型
1. 在 `DropContext` 加字段，在 `DropParser.Parse()` 里填充它。
2. 在需要响应它的 provider 的 `GetSlots()` 里增加对应的 `DropSlot`。

### 新增一个全局设置项
1. 在 `AppSettings` 加属性；2. 在 `SettingsViewModel` 加绑定属性；
3. 在 `SettingsWindow.xaml` 加控件；4. 在使用处读 `SettingsService.Current.Xxx`。
（若需副作用如开机自启那样写注册表，在 `SettingsService` 里处理。）

---

## 已知约定与注意事项

- **所有异常走 `Logger`，不要写空 catch**。这是从旧版本重构时的明确目标。
- **View 层禁止出现业务分支和 `IDataObject` 解析**，一律走 `DropContext` / provider。
- 图片看板的 URL 下载是「乐观占位」：先插一个 Loading 占位项，后台下完替换为 Ready，
  失败则置为 Failed（不持久化失败项）。受 `AppSettings.EnableImageDownload` 开关控制。
- `async void` 仅出现在事件处理器 / 投放回调中，内部必须自带 try-catch（已包 Logger）。
- 旧目录 `Helpers/ Models/ ViewModels/ Views/` 与旧 `Services/` 已全部删除，不要再引用。
- `MainWindow.xaml(.cs)` 已删除，入口窗口是 `Hosting/FloatWindow`。

---

## 向 AI 提问时的建议上下文

提问某个修改时，最好附上：(1) 涉及哪个层（Hosting / DragDrop / Widgets / Services / Settings）；
(2) 是改「显示」（Control/XAML）还是「逻辑」（ViewModel）还是「数据」（model/JsonStore）；
(3) 是否触及两条扩展线（IPanelWidget / IDropSlotProvider）。
按本文「目录地图」和「核心抽象」表定位，通常一两个文件即可命中。