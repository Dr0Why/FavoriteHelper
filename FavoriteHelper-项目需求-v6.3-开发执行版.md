# Windows Photos 收藏辅助工具（Portable）项目需求 v6.3 / 开发执行版

> 本文档是 FavoriteHelper v6.3 的唯一开发规格（Source of Truth）。
>
> v6.3 基于 v6.2 已完成的 Source Session、`.lnk` 四态、安全创建/删除、Windows 10/11 Photos 兼容、旧收藏 Repair、批量 Export 与一次性 command mode，并吸收 Round 3C 右键集成实机失败后的产品决策：**不再以 Explorer 右键扩展作为第一版入口，改为托盘 Export / Repair 独立拖放窗口，并新增 Configuration。**
>
> v6.3 同时将收藏目录从固定名称改为可配置名称，配置项为 `favorite_folder_name`，默认值为 `Favorite`。名称修改后只影响当前及后续操作；名称不同的旧收藏目录不迁移、不扫描、不自动识别。
>
> v6.3 取代 v6.2 作为后续开发唯一产品规格。后续 Codex / coding agent 的单轮任务提示词不得另行复制一套与本文冲突的产品规则；任务提示词只描述本轮 scope、特别约束、验证与输出格式。
>
> 已验证的 Source Session、Photos 身份识别、键盘 Hook、`.lnk` 四态和安全文件操作不得无理由重构。后续正式开发不得重新采用已被 Phase 1 否定的旧路径获取路线；`spikes/phase1/` 与 Round 3C Explorer 集成 spikes 仅作为技术证据和回归参考，不作为当前产品入口设计来源。

---

# 1. 项目目标

开发一个 **Windows Portable 收藏辅助工具**，用于配合 Windows 自带 **照片（Microsoft Photos）** 浏览本地图片。

核心目标：

* 用户从 Windows 文件资源管理器（File Explorer）显式通过 FavoriteHelper 打开一张本地图片进入 Microsoft Photos。
* FavoriteHelper 在打开之前从当前 Explorer Shell View 获取并绑定真实文件来源（Source Session）。
* 在 Microsoft Photos 中浏览该来源图片时，通过快捷键快速收藏或取消收藏。
* 不修改任何原始图片文件。
* 不创建收藏数据库。
* 收藏状态由对应 `.lnk` 快捷方式及其与当前图片之间的有效相对寻址关系决定。
* 收藏快捷方式使用**相对寻址关系**，使整个图片目录被移动、图片目录本身改名、上级目录改名或盘符变化后，只要目录内部结构保持不变，收藏关系仍然有效。
* 收藏目录名称由 `config.json` 中的 `favorite_folder_name` 决定，默认值为 `Favorite`。
* 用户修改收藏目录名称后，只使用新的当前配置名称；名称不同的旧收藏目录不迁移、不扫描、不合并、不删除，也不自动作为当前收藏状态来源。
* 程序整体便携，可以直接移动运行。
* Windows 原生、稳定、低资源占用。
* 所有涉及用户文件的操作遵循：**无法确定时宁可不操作，也不能误改用户文件。**
* 用户可从托盘打开 **Repair** 独立窗口，将一个或多个明确选中的收藏 `.lnk` 从 Explorer 拖入窗口，安全重建旧 Win10 收藏快捷方式中过期的 Shell 目标描述。
* 用户可从托盘打开 **Export** 独立窗口，将一个或多个明确选中的合法收藏 `.lnk` 从 Explorer 拖入窗口，把对应原图片复制到快捷方式同目录下的 `FavoriteHelper export` 文件夹。
* Repair / Export 必须是用户显式操作，不得自动扫描、自动发现或后台修改收藏集合。
* 托盘提供 **Configuration** 窗口；第一版仅允许修改 `favorite_folder_name`。

v6.3 的核心路径识别原则为：

> **完整路径的权威来源是 File Explorer Source Session；Microsoft Photos 只提供当前图片 basename，用于在已绑定 SourceSnapshot 中进行唯一映射。**

禁止再把“从 Microsoft Photos 直接获取当前完整文件路径”作为生产架构前提。

---

# 2. 程序形式要求

## 2.1 Portable

程序必须设计为 Portable。

要求：

* 整个程序文件夹可以直接移动。
* 移动后无需重新安装。
* 不依赖固定安装目录。
* 不依赖注册表保存程序配置。
* 配置文件、日志等程序自身数据存储于程序目录。
* 正式 Portable 构建应采用 **self-contained** 发布，不要求用户另外安装指定版本的 .NET Runtime。
* 第一版正式目标架构为 **Windows x64**。

示例：

```text
FavoriteHelper/
├── FavoriteHelper.exe
├── config.json
├── logs/
└── README.txt
```

例如：

```text
D:\Tools\FavoriteHelper
        ↓
E:\PortableApps\FavoriteHelper
```

移动后程序仍应正常运行。

允许 Windows 或 .NET 为正常运行产生不可避免的系统级运行时行为，但程序不得把自身核心配置、收藏状态或用户数据依赖于固定注册表项或固定安装路径。

v6.3 第一版不再要求 Explorer Shell 注册、MSIX、sparse package、COM Shell Extension 或其他安装型右键集成。Export / Repair / Configuration 均由当前常驻 FavoriteHelper 托盘打开独立窗口，因此程序目录移动后无需重新安装任何 Explorer integration。

---

# 3. 平台与技术方向

目标：

* Windows 原生体验。
* 稳定。
* 低资源占用。
* 类似系统小工具。

第一版目标环境：

* Windows 10 / Windows 11 x64。
* 以开发和验收时可取得的 Microsoft Photos 稳定版为主要目标。
* 若不同 Windows / Photos 版本存在行为差异，应在实际测试中记录并明确支持范围。
* “当前稳定版”不是永久自动扩展的兼容承诺；每次正式验收必须记录实际验证环境，包括 FavoriteHelper 版本、Windows edition/build 和 Microsoft Photos package/version。
* 后续 Photos 或 Windows 更新后，如关键行为发生变化，应重新验证并更新已确认支持范围。
* 当前兼容实现必须精确支持已验证的 Photos 进程身份：Windows 10 旧版 `PhotosApp.exe` 与当前 Windows 11 新版 `Photos.exe`。进程名匹配应集中实现并采用精确允许列表，不得使用 `Contains("Photo")` 等宽泛匹配。

推荐技术：

* C#
* .NET
* Windows API

允许使用：

* Windows UI Automation（仅用于被动读取当前 Photos 状态 / basename 等安全信息）。
* Windows Shell API。
* `IFolderView` / `IFolderView2` / `IShellItem` 等 Shell View 能力。
* Windows Notification Area / Shell Notification API。
* `WH_KEYBOARD_LL` 等 Windows Keyboard API。
* Windows 文件身份 API。
* Windows Shell Link API。
* 其他不修改、不注入 Microsoft Photos / Explorer 的受支持 Windows API。

禁止：

* 修改 Microsoft Photos 程序文件。
* 注入 Microsoft Photos 或 Explorer 进程。
* DLL 注入、内存修改、私有函数 Hook。
* 读取 Photos 进程内存来猜测当前图片。
* 使用浏览器框架实现核心程序。
* 使用截图或 OCR 猜测当前图片。
* 根据窗口标题或不完整文件名反查路径。
* 使用 Windows Search、Everything、全盘搜索或目录扫描，根据同名文件猜测当前路径。
* 读取 Photos 私有数据库或未承诺稳定的内部数据作为生产核心方案。
* ETW、文件句柄枚举等启发式方式猜测“当前显示文件”。
* 依赖 Explorer → Photos 前台切换的时间相关性推测用户打开了哪张图。
* 使用明显依赖特定界面布局、坐标或像素位置的脆弱方案作为唯一实现方式。

---

# 4. 运行模式

程序启动后：

```text
FavoriteHelper.exe
        ↓
后台运行
        ↓
系统托盘显示图标
        ↓
安装键盘监听
        ↓
等待 Explorer 显式打开 / Photos 收藏操作 / 托盘工具窗口操作
```

正常运行时不显示主窗口。

程序应尽量保持较低 CPU、内存和后台资源占用。

系统托盘至少应提供：

```text
FavoriteHelper
├── Export...
├── Repair...
├── Configuration...
├── ─────────────
└── Exit
```

行为要求：

* `Export...` 打开一个独立 Export 拖放窗口。
* `Repair...` 打开一个独立 Repair 拖放窗口。
* `Configuration...` 打开一个独立 Configuration 窗口。
* 关闭任一工具窗口只关闭该窗口，不退出 FavoriteHelper，不卸载 Hook，不清除仍有效的 Source Session。
* 工具窗口不得创建第二个常驻 FavoriteHelper 实例、第二套托盘图标或第二套 keyboard hook。
* 同类型工具窗口重复打开时，优先复用 / 激活现有窗口，避免无意义的重复实例。

点击 `Exit` 后必须：

1. 停止接受新的显式打开 / 收藏 / 取消收藏 / Repair / Export 操作。
2. 停止或安全卸载键盘 Hook。
3. 使当前正在执行的文件修改完成到安全结束点，或安全回滚 / 清理临时产物。
4. 关闭 / 释放 Export、Repair、Configuration 工具窗口。
5. 释放 Source Session / Photos Session 状态。
6. 移除托盘图标。
7. 刷新并关闭日志。
8. 释放单实例锁。
9. 正常退出进程。

退出过程中不得留下被误认为正常收藏的半成品最终 `.lnk`。

---

# 5. 单实例运行

程序必须支持单实例。

第一次启动：

```text
启动程序
    ↓
正常运行
```

再次启动：

```text
检测到已有实例
    ↓
不启动第二个后台实例
```

第二次启动可以直接退出，或向用户提示“FavoriteHelper 已在运行”，但不得创建第二个常驻实例。

禁止：

* 多个程序实例同时驻留。
* 多个实例同时安装键盘 Hook。
* 一次快捷键触发多个实例同时执行文件操作。
* 多个实例各自持有互相冲突的 Source Session。

### 5.1 一次性 command mode（保留）

v6.3 保留 Round 3B 已完成的一次性命令接口：

```text
FavoriteHelper.exe --export "<shortcut1>" "<shortcut2>" ...
FavoriteHelper.exe --repair "<shortcut1>" "<shortcut2>" ...
```

它是兼容 / 测试 / 高级入口，不再依赖 Explorer 右键集成，也不是第一版主要用户入口。

要求：

* 仍然只能有一个**常驻托盘实例**。
* 一次性 command mode 不得安装键盘 Hook、建立 Photos Source Session 或创建第二个常驻托盘实例。
* command mode 必须继续直接复用既有 Export / Repair 核心安全逻辑。
* 参数必须完整支持 Unicode、空格和多选，且只能处理明确传入的路径。
* command mode 完成后应退出，不得长期驻留。
* 托盘 Export / Repair 窗口不得为了执行操作而额外启动 command-mode 子进程；优先在当前 resident process 中调用相同核心服务。

---

# 6. 当前图片识别架构：Explorer Source Session

## 6.1 v6.3 核心原则

v6.3 不要求 Microsoft Photos 直接暴露完整文件路径。

生产链路固定为：

```text
Explorer 中选中本地图片
        ↓
FavoriteHelper 显式打开快捷键
        ↓
确认 Explorer 前台 + exactly one selection
        ↓
从当前 Explorer Shell View 获取：
selectedPath
SourceSnapshot
file identity
        ↓
IFolderView2::InvokeVerbOnSelection(NULL)
        ↓
Microsoft Photos
        ↓
被动 UIA 获取当前 basename
        ↓
basename == selectedPath basename
        ↓
PendingSession → BoundSession
        ↓
Photos Previous / Next
        ↓
仅在 SourceSnapshot 中唯一映射 basename
        ↓
真实完整路径
```

完整路径不得由 Photos basename 单独推断。

## 6.2 Explorer 显式打开是唯一受支持的 Session 起点

第一版支持的正常入口：

```text
File Explorer
↓
选中 exactly one 本地图片
↓
按 FavoriteHelper 显式打开快捷键
↓
FavoriteHelper 捕获 SourceSnapshot
↓
调用当前 Explorer View 的默认 Open verb
↓
Microsoft Photos
```

以下方式第一版**不建立可用于收藏的 Source Session**：

* 用户普通鼠标双击图片。
* 用户在 Explorer 选中后直接按 Enter。
* 用户从其他程序打开 Photos。
* 用户直接启动 Photos 后浏览图库。
* 用户仅从 Explorer Alt+Tab / 点击切回一个已经打开的 Photos。
* 其他没有经过 FavoriteHelper 明确显式打开动作的 Photos 会话。

如果 Photos 当前没有有效 BoundSession：

```text
Ctrl + F / Ctrl + Shift + U
↓
安全拒绝
↓
提示用户从 Explorer 使用 FavoriteHelper 打开当前图片
```

不得根据当前 filename 搜索或推测路径。

## 6.3 Explorer SourceSnapshot 获取

显式打开动作发生时，FavoriteHelper 必须：

1. 确认当前前台应用是 File Explorer。
2. 确认能够确定当前前台对应的具体 Explorer Shell View。
3. 要求 exactly one selection。
4. 要求 selection 是可处理的物理文件系统图片。
5. 使用 Shell API 获取真实文件系统路径。
6. 从**同一个 Explorer Shell View** 捕获 SourceSnapshot。
7. 不使用 `Directory.GetFiles()` 或其他目录扫描来重建来源集合。
8. 尽可能保存稳定文件身份。

SourceSnapshot 每项至少包含：

```text
SourceItem
├── FullPath
├── Basename
└── FileIdentity
```

FileIdentity 应优先使用 Unicode 安全的 Windows 文件 API 获得，可包含：

```text
VolumeSerialNumber + FileId
```

完整实现不要求把具体 Win32 API 固化在需求层，但必须能验证“路径同名但文件对象已被替换”的情况。

## 6.4 第一版支持的 Explorer View

Phase 1 已证明：

> 普通物理文件系统目录中的 Explorer Shell View 可可靠捕获 selection、真实路径、视图项目和文件身份。

因此第一版正式支持：

* 普通本地物理文件夹。
* 普通可访问可移动磁盘上的物理文件夹（正式支持范围仍需发布前实测）。

第一版明确不保证：

* Search Results。
* Libraries。
* Quick Access 虚拟聚合视图。
* 其他虚拟 Shell Folder。
* UNC / 网络视图。
* 映射网络驱动器。
* 无法稳定转化为真实文件系统路径的 Shell Item。

上述场景应：

```text
拒绝建立 Source Session
↓
明确提示当前 Explorer 位置不受支持
```

不得降级为目录枚举或搜索。

## 6.5 显式打开

SourceSnapshot 成功捕获后，首选打开方式固定为：

```text
IFolderView2::InvokeVerbOnSelection(NULL)
```

目标：

> 在原 Explorer Shell View 上执行该 selection 的默认 verb，尽量保持 Windows 原始 Explorer → Photos 激活语义。

要求：

* 不修改系统默认文件关联。
* 不把 FavoriteHelper 注册为默认图片查看器作为第一版前提。
* 不以 `ms-photos:` URI 作为第一版主路径。
* 不以 `Process.Start(path)` 作为主路径。
* 如果默认处理程序不是 Microsoft Photos，则不得建立 Photos BoundSession。
* 打开失败时清除 PendingSession，并安全提示。

## 6.6 初始 Photos 绑定

调用默认 verb 后：

1. 等待目标 Microsoft Photos 出现 / 成为相关前台查看器。
2. 使用**被动、只读 UI Automation** 获取当前 Photos basename。
3. 必须满足：

```text
PhotosCurrentBasename
==
Path.GetFileName(SelectedPath)
```

4. 只有完全匹配才允许：

```text
PendingSession → BoundSession
```

5. PendingSession 一旦被消费，无论成功还是失败，都不得在未来重新绑定。
6. 旧 PendingSession 不得因后续 Photos 再次出现而“复活”。

如果：

* Photos 未启动；
* Photos 崩溃；
* basename 不匹配；
* basename 无法在限定时间内可靠读取；
* 目标默认应用不是 Photos；

则：

```text
丢弃 PendingSession
↓
不建立 BoundSession
↓
不猜测
```

## 6.7 Photos Session 身份

不得把不稳定的 `ApplicationFrameHost` wrapper HWND 当成唯一 Session 身份。

Phase 1 已证明 Photos 在正常运行过程中可能改变 wrapper HWND。

BoundSession 应优先绑定到：

> 已验证的 Microsoft Photos 进程身份 / PID，以及必要的当前窗口上下文。

当前 v6.3 已验证并要求保留的精确进程名：

```text
PhotosApp   # Windows 10 旧版 Photos
Photos      # 当前 Windows 11 新版 Photos
```

要求：

* Photos 身份判断应由集中方法维护，使用精确允许列表并支持正常大小写差异。
* 不得使用 `Contains("Photo")`、前缀猜测或其他宽泛进程名匹配。
* wrapper HWND 变化本身不得导致错误重绑定。
* `HWND = 0` 必须视为 non-target / unknown，禁止通过桌面枚举“顺手找到一个 Photos”。
* Photos 进程终止后 Session 必须失效。

## 6.8 Photos Previous / Next 后的当前路径映射

BoundSession 建立后，Photos 可正常使用上一张 / 下一张浏览。

每次需要确定当前路径时：

```text
被动读取 Photos 当前 basename
        ↓
在 BoundSession.SourceSnapshot 中精确匹配
        ↓
matches == 1 ?
```

判定规则：

```text
matches == 1
→ 得到唯一 SourceItem
→ 验证 FullPath 当前存在
→ 验证 FileIdentity 未被替换
→ 当前真实路径成立

matches == 0
→ Session 无法继续可靠映射
→ 失效

matches > 1
→ 存在歧义
→ 失效
```

禁止：

* 到 SourceSnapshot 外搜索。
* 到图片目录重新扫描。
* 根据视图顺序“猜上一张/下一张”。
* 仅根据旧路径或历史路径推测。
* 同名冲突时任选一个。

Phase 1 已实际验证：

* 多次 Next / Previous。
* 第一张 / 最后一张边界重复按键。
* 返回初始图片。
* 中文、日文和空格路径。
* Photos 已关闭后启动。
* Photos 已运行时打开新的显式 Source Session。

## 6.9 文件身份变化

BoundSession 不能只相信路径字符串。

如果：

```text
D:\A\01.jpg
```

在 Session 建立后：

* 被删除；
* 被另一个文件替换；
* 文件身份发生变化；

则当前 SourceItem 必须判定失效。

不得因为：

```text
路径仍然叫 D:\A\01.jpg
```

就继续把它视为原 Session 中的图片。

如果用户主动覆盖 / 替换了源图片，FavoriteHelper 应安全拒绝后续收藏操作，并要求重新显式打开建立新 Session。

## 6.10 basename 暂时不可用的 Grace

Photos 在 wrapper / UI 状态切换过程中可能短暂暴露空 basename。

第一版允许一个：

> **最长 2 秒的有界 grace period**

规则：

```text
basename 暂时为空
↓
不执行任何收藏 / 取消操作
↓
不沿用旧 basename 推断当前图片
↓
等待恢复
```

如果 2 秒内恢复：

```text
重新按完整规则验证
```

如果持续不可用：

```text
BoundSession 失效
```

Grace 只允许“延迟判断”，绝不能允许文件操作基于旧状态继续执行。

## 6.11 Session 生命周期

BoundSession 可以在普通：

```text
Photos
↓ Alt+Tab
浏览器 / Explorer
↓ Alt+Tab
Photos
```

过程中保留。

但以下事件应使 Session 失效或被安全替换：

* 新的、已验证的 FavoriteHelper Explorer 显式打开成功建立新 Session。
* Photos 进程退出 / 崩溃。
* 当前 basename 无法唯一映射 SourceSnapshot。
* SourceItem 不存在。
* SourceItem FileIdentity 改变。
* basename 长时间不可读。
* 其他无法继续证明映射正确的状态。

一个新的显式打开必须先成功验证新 Photos basename，才能替换旧 BoundSession。

失败的新 PendingSession 不得破坏仍可安全识别的旧 Session，除非实现无法证明两者独立，此时优先安全失效。

---

# 7. 快捷键

## 7.1 Explorer 显式打开快捷键

第一版必须提供一个：

> **只在 File Explorer 前台生效的 FavoriteHelper 显式打开快捷键。**

Phase 1 技术验证时使用的组合为：

```text
Ctrl + Shift + O
```

该组合证明了生产键盘机制能够可靠识别 Explorer 显式打开动作。

Round 1 结束后的生产快捷键决策将第一版默认值调整为：

```text
Ctrl + Shift + P
```

因此 v6.3 的当前默认配置为：

```json
"open_hotkey": "Ctrl+Shift+P"
```

该默认值变化仅属于 UX / 键位决策，不改变已验证的 `WH_KEYBOARD_LL` 架构、作用域、透传和安全要求；正式集成仍必须按 §29.3 使用新默认组合重跑键盘矩阵。

行为：

```text
Explorer 前台
↓
Ctrl + Shift + P
↓
FavoriteHelper 接管该组合
↓
捕获 SourceSnapshot
↓
显式打开选中图片
```

如果：

* Explorer 不在前台；
* 当前不是受支持 Shell View；
* selection 数量不是 1；
* selection 不是受支持本地图片；

则不得建立 Session。

## 7.2 收藏快捷键

默认：

```text
Ctrl + F
```

执行逻辑：

```text
快捷键触发
    ↓
键盘层仅完成作用域判断并发出轻量 trigger action
    ↓
在 callback 外建立与本次触发一致的 SourceItem 绑定
    ↓
形成不可重新绑定的操作请求
    ↓
定位“收藏”目录
    ↓
按四态模型收藏
```

## 7.3 取消收藏快捷键

默认：

```text
Ctrl + Shift + U
```

执行逻辑：

```text
快捷键触发
    ↓
键盘层仅完成作用域判断并发出轻量 trigger action
    ↓
在 callback 外建立与本次触发一致的 SourceItem 绑定
    ↓
形成不可重新绑定的操作请求
    ↓
检查对应 .lnk
    ↓
仅在确认安全时删除
```

---

# 8. 键盘机制、作用域与透传

## 8.1 生产键盘机制

第一版生产实现采用：

```text
WH_KEYBOARD_LL
```

要求：

* 使用独立 message-loop 线程。
* 修饰键状态必须从 Hook 自己收到的 key-down / key-up 事件流维护。
* **不得在 LowLevelKeyboardProc 中依赖 `GetAsyncKeyState()` 判断当前 hook event 的 Ctrl / Shift / Alt 状态。**
* `GetAsyncKeyState()` 如用于前台切换后的状态 reconciliation，只能在 callback 外或不用于识别当前事件。
* 注入输入应默认忽略。
* 长按 final key 时必须抑制 auto-repeat，直到该 final key release。
* 未处理事件必须 `CallNextHookEx`。
* 只有 FavoriteHelper 明确处理的完整组合才允许返回非零进行拦截。
* `HWND = 0` 必须视为 non-target。
* 不得因为当前 foreground 不确定而枚举桌面并猜测 Photos。

## 8.2 Hook callback 必须极小

Hook callback 只允许进行：

```text
读取当前 hook event
↓
维护 modifier / repeat 状态
↓
最小前台分类
↓
判断是否属于 FavoriteHelper
↓
enqueue lightweight trigger action
↓
立即 return
```

禁止在 callback 内进行：

* Explorer COM 遍历。
* Shell View 枚举。
* UI Automation tree 遍历。
* Photos basename 读取。
* SourceSnapshot 唯一映射。
* FileIdentity 文件系统验证。
* 创建完整的 filesystem operation request。
* 文件系统 I/O。
* 创建 / 删除 `.lnk`。
* 阻塞式日志。
* 长时间锁。
* 等待 worker。
* 任何高延迟工作。

实际 SourceItem 解析、绑定和文件操作必须在 callback 外的 worker / 串行执行层完成。

### 8.2.1 “触发时目标绑定”的语义

§17 中的“快捷键触发时绑定目标”是**安全语义要求**，不表示在 `LowLevelKeyboardProc` 内执行 UIA 或文件系统工作。

正确边界为：

```text
Hook callback
→ 识别本次物理快捷键 + 目标应用作用域
→ 发出轻量、不可变的 trigger action
→ 立即 return

callback 外的绑定层
→ 使用能够证明与该 trigger action 对应的状态
→ 验证 BoundSession
→ 唯一解析 SourceItem
→ 验证 FileIdentity
→ 冻结为 immutable bound operation request

串行文件操作层
→ 只针对已冻结的 SourceItem 执行
```

实现可以使用：

* callback 外持续维护并原子发布的已验证 current-item snapshot；或
* 其他能够保持输入事件顺序、并证明解析结果仍对应本次触发的机制。

但不得采用：

```text
先接受 A 图快捷键
↓
排队等待
↓
稍后读取此时 Photos 已切到 B 图
↓
把原请求绑定到 B
```

如果实现无法证明得到的 SourceItem 对应本次已接受触发，则：

```text
fail closed
↓
不形成文件修改请求
```

## 8.3 作用域规则

### Explorer 前台

启用：

```text
Ctrl + Shift + P
```

Favorite / Unfavorite 不执行。

### Microsoft Photos 前台

启用：

```text
Ctrl + F
Ctrl + Shift + U
```

FavoriteHelper 应接管并阻止 Photos 同时处理对应组合。

### 其他应用前台

FavoriteHelper：

```text
不执行
不吞键
不改变原应用行为
```

例如：

```text
Edge / Chrome 前台
↓
Ctrl + F
↓
浏览器自己的 Find 正常出现
```

`Ctrl + Shift + P` 与 `Ctrl + Shift + U` 在非目标应用中也必须正常透传。

## 8.4 前台切换与 modifier reconciliation

必须处理：

* 按住 Ctrl / Shift / Alt 时切换前台。
* 不常见的 modifier release 顺序。
* Explorer → Photos → Browser → Photos。
* Hook 安装期间普通输入。
* held-key auto-repeat。

目标：

> 不产生“粘住”的逻辑修饰键状态，不发生非目标应用误触发。

Phase 1 已验证该机制总体可行；v6.3 的正式集成版仍必须使用当前生产快捷键 **P/F/U** 重跑完整键盘矩阵，尤其是 `HWND=0` fail-closed 和 foreground switching while modifiers are held。

---

# 9. 原始图片保护

收藏系统不得修改原始图片。

禁止修改：

* 图片文件内容。
* EXIF。
* XMP。
* 文件属性。
* 文件名。
* 图片编码。
* 图片时间戳（除非操作系统自身产生不可避免的访问行为）。

收藏与取消收藏仅允许操作：

```text
当前图片目录\<当前 favorite_folder_name>\*.lnk
```

不得对原图片执行写入。

---

# 10. 收藏目录结构

收藏目录始终创建于：

> 当前图片所在目录内部。

收藏目录名称由：

```text
config.json → favorite_folder_name
```

决定。

默认值：

```text
Favorite
```

例如默认配置：

```text
E:\ACG art works\yande.re\梱枝りこ korie_riko\
│
├──123456.jpg
├──123457.jpg
├──123458.png
│
└──Favorite\
    ├──123456.jpg.lnk
    └──123458.png.lnk
```

每个图片目录拥有自己的独立收藏目录。

如果用户把 `favorite_folder_name` 改为：

```text
My Favorites
```

以后 Favorite / Unfavorite / Repair / Export 的当前收藏目录结构判断均以：

```text
<图片目录>\My Favorites\
```

为准。

旧名称目录不迁移、不扫描、不合并、不删除，也不作为当前收藏状态来源。若用户以后把配置名称改回旧名称，该目录才重新成为当前配置对应的收藏目录。

---

# 11. 收藏目录规则与安全边界

## 11.0 名称来源与校验

`favorite_folder_name` 必须表示**单个目录名称**，不得包含路径层级。

第一版保存配置时至少必须拒绝：

* 空字符串或纯空白。
* `.`、`..`。
* `\`、`/` 等路径分隔符。
* Windows 文件名非法字符。
* 结尾空格或结尾 `.`。
* Windows 保留设备名，例如 `CON`、`PRN`、`AUX`、`NUL`、`COM1`–`COM9`、`LPT1`–`LPT9`（包括会被 Windows 视为同类保留名的形式）。

必须支持合法 Unicode，例如：

```text
Favorite
收藏
お気に入り
我的收藏
```

配置保存失败或名称不合法时：

```text
保留旧配置
↓
不改变当前运行中的 favorite_folder_name
↓
明确提示错误
```

不得把非法名称静默修正成另一个目录名。

## 11.1 当前目录行为

首次收藏时当前配置目录不存在：

```text
自动创建
```

当前配置目录已经存在：

```text
直接复用
```

不得扫描或复用名称不同的旧收藏目录或名称相似目录。

## 11.2 Reparse Point 防护

如果：

```text
当前图片目录\<当前 favorite_folder_name>
```

已存在，必须确认它是安全使用的普通目录。

如果是：

* Symbolic Link。
* Junction。
* 其他 Reparse Point。
* 任何可能把写入重定向到图片目录外的机制。

第一版：

```text
拒绝收藏 / 取消收藏 / Repair / Export 中依赖该目录的操作
↓
提示收藏目录结构异常或不受支持
```

不得跟随重定向。

如果收藏目录在检查后、真正写入、删除、Repair 或 Export 提交之前发生替换，也必须再次验证；无法证明仍为安全目录时放弃操作。

---

# 12. 收藏状态模型

不使用任何额外收藏数据库。

禁止：

* SQLite 收藏数据库。
* JSON / XML 收藏索引。
* 独立收藏清单。
* 图片 hash 历史索引。

对于当前图片：

```text
123456.jpg
```

对应：

```text
<当前 favorite_folder_name>\123456.jpg.lnk
```

收藏状态分为四类：

1. 未收藏。
2. 已收藏。
3. 失效收藏。
4. 快捷方式冲突。

## 12.0 四态判定顺序

```text
1. .lnk 不存在
   → 未收藏

2. .lnk 无法安全读取 / 损坏 / 元数据不足
   → 失效收藏

3. 存在本项目可验证的有效相对寻址信息
   → 由 .lnk 自身位置计算相对目标
      ├─ 目标不存在 → 失效收藏
      ├─ 目标存在且就是当前已绑定图片 → 已收藏
      └─ 目标存在但不是当前图片 → 快捷方式冲突

4. 缺少可验证相对寻址信息
   → 若能仅通过安全读取元数据无歧义确认它对应另一个有效目标
      → 冲突
   → 否则
      → 失效
```

不得因为 Shell 最终通过旧绝对路径、Distributed Link Tracking 或搜索找到目标，就把快捷方式判为“已收藏”。

## 12.1 未收藏

对应 `.lnk` 不存在：

```text
未收藏
```

## 12.2 已收藏

必须同时满足：

1. 对应 `.lnk` 存在。
2. `.lnk` 中存在本项目可验证的相对寻址信息。
3. 从 `.lnk` 自身位置按相对关系得到目标。
4. 目标存在。
5. 目标就是当前 BoundSession 已可靠确定的图片。

## 12.3 失效收藏

包括：

* 相对寻址信息缺失或无效。
* 目标不存在。
* `.lnk` 损坏。
* 无法安全读取。
* 无法无歧义判断。

不得静默当作正常收藏。

## 12.4 快捷方式冲突

同名 `.lnk` 存在，并且能够安全确认其有效目标不是当前图片。

要求：

* 不视为已收藏。
* 不覆盖。
* 不删除。
* 明确提示冲突。

---

# 13. 快捷方式规则

## 13.1 文件名

原图片：

```text
123456.jpg
```

创建：

```text
123456.jpg.lnk
```

必须保留完整原文件名和扩展名。

## 13.2 相对寻址

`.lnk` 必须依靠自身和目标图片的相对关系，使外层路径变化不破坏收藏关系。

逻辑关系：

```text
<当前 favorite_folder_name>\123456.jpg.lnk
        ↓
..\123456.jpg
```

这里的 `..\123456.jpg` 是**逻辑关系表达**，不是规定某个 API 参数必须传这个字符串。

## 13.3 Windows Shell Link 实现要求

实现时：

* 优先使用 `IShellLinkW`、`IPersistFile` 等 Unicode Windows Shell API。
* 不得机械假定 `SetRelativePath()` 参数就是 `..\filename`。
* Phase 1 已观察到 Shell 生成的 `.lnk` 内部 `RelativePath` 为 `..\{unique filename}`；正确调用语义必须按照 Windows Shell API 实际行为实现。
* 项目有效性判定不得依赖旧绝对路径、Distributed Link Tracking 或搜索兜底。
* 正式实现仍必须完成真实 Shell launch 的移动 / 改名 / copy-delete / 跨盘验收。

## 13.4 不允许用 Shell Resolve 冒充成功

不得仅以：

```text
IShellLink::Resolve()
```

最终找到了文件，就判定收藏有效。

验收必须结合：

* `.lnk` 相对元数据。
* 相对目标计算。
* 原路径已不存在。
* 移动 / 改名 / copy-delete。
* 实际 Shell 打开结果。

## 13.5 解析不等于执行

程序运行时判断 `.lnk` 是否有效：

> 只读取和解析元数据，不执行未知 `.lnk`。

禁止为了状态判断执行：

```text
ShellExecute(.lnk)
```

实际打开 `.lnk` 仅用于受控验收测试，不用于生产状态判断。

---

# 14. 目录移动与重命名行为

必须支持：

* 整个图片目录移动。
* 上级父目录改名。
* 图片目录本身改名。
* 在目标环境和 Shell 能力允许时，整个图片目录跨盘 / 盘符变化。
* 目录副本在删除原目录后仍能依靠内部相对关系使用副本中的 `.lnk`。

不要求支持：

* 单独移动图片。
* 图片文件改名。
* 单独移动收藏目录。
* 自动历史追踪。

不得增加数据库、索引、全盘搜索或哈希历史系统追踪上述变化。

---

# 15. 收藏操作

当前图片必须已经由有效 BoundSession 确定，并按 §8.2.1 / §17 建立与本次 trigger 一致的不可变目标绑定。

流程：

```text
Favorite hotkey
    ↓
Hook 仅接管 + enqueue lightweight trigger
    ↓
callback 外可靠绑定 SourceItem
    ↓
验证 FullPath + FileIdentity
    ↓
形成 immutable operation request
    ↓
确认媒体 / 来源受支持
    ↓
确定图片目录
    ↓
确认当前配置收藏目录安全
    ↓
四态判断
```

未收藏：

```text
安全创建相对 .lnk
```

## 15.1 重复收藏

已收藏：

```text
不重复创建
↓
提示已收藏
```

## 15.2 失效快捷方式

失效：

```text
不覆盖
↓
明确提示
```

## 15.3 冲突快捷方式

冲突：

```text
不覆盖
不删除
↓
明确提示
```

## 15.4 创建文件安全

即使检查阶段认为最终 `.lnk` 不存在，真正提交前仍必须保证不覆盖新出现的同名文件。

推荐：

```text
生成临时 .lnk
↓
验证元数据和相对关系
↓
重新验证当前配置收藏目录安全
↓
以 no-overwrite 语义提交最终路径
↓
成功
```

失败 / 崩溃不得留下被误认为正常收藏的半成品最终 `.lnk`。

---

# 16. 取消收藏操作

当前图片必须已经由有效 BoundSession 确定，并按 §8.2.1 / §17 建立与本次 trigger 一致的不可变目标绑定。

查找：

```text
当前图片目录\<当前 favorite_folder_name>\完整图片名.lnk
```

只有“已收藏”状态允许删除。

未收藏：

```text
提示尚未收藏
```

失效：

```text
不删除
```

冲突：

```text
不删除
```

永远不得删除原图片。

## 16.1 删除安全与 TOCTOU

验证 `.lnk` 与真正删除之间必须防止或检测 TOCTOU。

要求：

* 实际删除对象仍必须是刚刚验证的同一文件 / 同一已验证内容。
* 如果 `.lnk` 在验证后被替换、修改或重建，放弃删除。
* 路径名称相同不代表文件对象相同。
* 当前配置收藏目录重新验证安全。
* 放弃时给出安全提示并写日志。

---

# 17. 操作串行化、目标绑定与连续按键

单实例不等于单操作。

为了避免用户快速连续按键、键盘自动重复、快速切换图片或收藏 / 取消同时触发造成竞争：

> 收藏与取消收藏操作必须串行执行，并且每次被接受的文件操作请求必须绑定到能够证明与该次快捷键触发一致的 SourceItem。

## 17.1 操作目标绑定

### 输入阶段

`LowLevelKeyboardProc` 只负责：

```text
识别物理快捷键
↓
最小前台分类
↓
作用域命中时拦截该组合
↓
enqueue lightweight trigger action
↓
立即 return
```

不得在 Hook callback 内执行 UIA、Explorer COM 或文件系统验证。

### 目标绑定阶段（callback 外）

在把 trigger 接受为真正的 Favorite / Unfavorite 文件操作请求前，必须：

```text
证明该 trigger 属于目标 Photos 会话
↓
确认有效 BoundSession
↓
取得与该 trigger 一致的当前 basename / current-item snapshot
↓
SourceSnapshot 内唯一匹配
↓
验证文件存在 + FileIdentity
↓
创建 immutable operation request
    action = Favorite / Unfavorite
    image = 已验证 SourceItem
↓
进入串行文件操作队列
```

“与该 trigger 一致”是强制要求。

实现不得简单地：

```text
快捷键发生在 A.jpg
↓
worker 延迟
↓
用户已经切到 B.jpg
↓
worker 此时才读取 B.jpg
↓
把原请求绑定到 B.jpg
```

如果无法证明目标仍对应原 trigger：

```text
不接受为文件操作请求
↓
fail closed
```

一个 operation request 一旦完成 SourceItem 绑定：

> 后续即使 Photos 切换到其他图片，也只能作用于原先绑定的 SourceItem。

队列执行时不得重新读取 Photos，并把请求重新绑定到当前图片。

但真正修改文件系统前仍必须重新验证：

* 已绑定文件仍存在。
* FileIdentity 未被替换。
* 当前配置收藏目录安全。
* `.lnk` 当前状态适合操作。

## 17.2 串行化

* 同时最多一个文件修改操作。
* Favorite / Unfavorite 不得并发修改同一 `.lnk`。
* 不得因并发覆盖用户新创建文件。
* 不得因并发误删用户替换文件。

## 17.3 连续按键与 auto-repeat

键盘层应在输入阶段抑制明显 auto-repeat。

操作层仍需保证：

* 同一图片重复 Favorite 可安全合并 / 拒绝。
* Favorite → Unfavorite 保持顺序。
* Unfavorite → Favorite 保持顺序。
* 不同图片已接受的操作各自绑定目标。
* 不允许简单 debounce 丢失语义相反操作。

---

# 18. 图片格式与媒体类型

对象仍是：

> Microsoft Photos 正在显示、且属于当前 BoundSession SourceSnapshot 的本地图片。

第一版不应维护过度严格、易过时的扩展名白名单。

可以结合：

* Shell / 系统媒体类型信息。
* 默认打开结果。
* Photos 当前显示状态。
* 文件系统可访问性。

可靠判断是否为图片。

视频、非图片媒体或无法可靠判断的对象：

```text
不执行收藏
```

---

# 19. Unicode 与路径要求

所有路径和文件名处理必须完整支持 Unicode。

包括：

```text
E:\ACG art works\梱枝りこ\测试 图片 01.jpg
E:\FavoriteHelper 测试\中文 日本語 space\02 次の画像.jpg
```

要求：

* 使用 Unicode Win32 / Shell API。
* 不得使用 ANSI API 造成文件身份或路径丢失。
* 不乱码、不截断。
* `.lnk` 目标正确。

Phase 1 曾发现 ANSI 文件访问导致 identity 空白，最终已通过 Unicode 行为修正；生产实现不得回退到 ANSI 路径 API。

## 19.1 长路径

尽可能正确支持 Windows 长路径。

禁止：

* 静默截断。
* 因 `MAX_PATH` 产生错误目标。
* 把截断结果当作正常收藏。

底层 API 无法安全处理：

```text
停止操作
↓
明确提示
```

---

# 20. 用户反馈

所有实际操作必须有明确反馈机制。

普通状态：

* 收藏成功。
* 已收藏。
* 取消收藏成功。
* 尚未收藏。
* Source Session 建立成功（可选普通提示）。
* 显式打开成功（可选普通提示）。
* Export / Repair 窗口批量完成结果。
* Configuration 保存成功。

安全 / 错误：

* 当前 Photos 会话未绑定来源。
* 当前 Explorer 位置不受支持。
* 当前选择不是 exactly one 图片。
* 默认打开程序不是 Microsoft Photos。
* Source Session 建立失败。
* 当前 basename 无法可靠读取。
* 当前 basename 无法在 SourceSnapshot 唯一映射。
* 无法证明快捷键 trigger 与待绑定 SourceItem 一致。
* 源文件已删除或被替换。
* 收藏目录结构异常。
* `favorite_folder_name` 不合法或配置保存失败。
* 快捷方式失效。
* 快捷方式冲突。
* 权限错误。
* 文件系统错误。
* Repair 成功 / AlreadyCurrent / 拒绝 / 失败及批量汇总。
* Export 成功 / 已存在跳过 / 非法快捷方式跳过 / 失败及批量汇总。
* 拖放输入为空、不可读取或不是受支持的文件系统对象。

典型提示：

```text
当前 Photos 会话未由 FavoriteHelper 建立。
请从文件资源管理器选择图片并使用“FavoriteHelper 打开”快捷键。
```

```text
无法可靠确定当前图片，未执行操作。
```

非目标应用中按快捷键：

```text
不提示
不执行
正常透传
```

---

# 21. 通知方式

v6.3 使用当前已在 Windows 11 实机验证通过的**非阻塞、不抢焦点通知弹窗**。

要求：

* `WS_EX_NOACTIVATE` / `ShowWithoutActivation` 等不抢焦点语义必须保留。
* 通知可以 TopMost，但不得激活自身或阻塞 Photos / Explorer。
* 标题和正文必须依据实际字体度量自适应布局，不得依赖会在 DPI / 字体差异下截断文本的固定高度。
* 当前实现可使用 `TextRenderer.MeasureText` 或等价可靠方法测量文本。
* 文本区域可在合理宽度范围内自适应；超出最大宽度时换行，并按完整正文增加高度。
* 不得通过 `AutoEllipsis` 静默截断安全/错误信息。
* 通知自动关闭；常规反馈不得使用模态对话框。
* 不强制引入完整 App Toast 激活体系。
* Export / Repair 工具窗口本身可以显示批量结果明细；通知只承担简短完成/错误反馈，不要求替代窗口内结果展示。

当前 Windows 11 已真实确认：Source Session connected、Favorite、Already favorited、Unfavorite、Not favorited、Unbound/invalid Session 以及较长配置错误通知均完整显示且不抢焦点。Windows 10 最终视觉回归仍属于正式发布 Gate。

## 21.1 `enable_notification`

`true`：

* 显示普通和错误通知。

`false`：

可以关闭：

* 收藏成功。
* 取消成功。
* 已收藏。
* 尚未收藏。
* 普通 Session 成功状态。
* 普通 Repair / Export 完成汇总。
* Configuration 保存成功。

但不能完全静默：

* Session 无效 / 未绑定。
* SourceSnapshot 歧义。
* 文件身份改变。
* 不支持来源。
* 收藏目录异常。
* `.lnk` 失效 / 冲突。
* Repair 资格验证失败、TOCTOU 主动放弃或原子替换异常。
* Export 目标不安全、reparse point、权限 / 文件系统错误。
* `favorite_folder_name` 无效或配置写入失败。
* 目标绑定无法证明与 trigger 一致而主动放弃。

安全错误必须同时进入日志。

---

# 22. 日志系统

目录：

```text
FavoriteHelper\
└──logs\
    └──app.log
```

建议记录：

* FavoriteHelper 版本。
* Windows edition/build。
* Microsoft Photos package/version。
* 启动 / 退出。
* 单实例状态。
* `WH_KEYBOARD_LL` 安装 / 卸载 / 异常。
* 前台应用分类。
* open/favorite/unfavorite 快捷键触发。
* lightweight trigger action 接受 / 拒绝。
* trigger → SourceItem 绑定结果及拒绝原因。
* Explorer Shell View 识别结果。
* selection 数量与是否受支持。
* SourceSnapshot 建立 / 拒绝原因。
* SourceSnapshot item count。
* PendingSession 建立 / 消费 / 清除。
* Photos PID 绑定。
* basename 验证。
* BoundSession 建立 / 失效原因。
* 文件身份验证结果。
* 当前 `favorite_folder_name` 及配置加载 / 保存结果（避免记录不必要隐私）。
* `.lnk` 创建 / 读取 / 相对关系 / 删除。
* Repair 窗口打开 / 拖放批次 / 资格判定 / 临时重建 / 原子替换 / AlreadyCurrent / 拒绝与失败原因。
* Export 窗口打开 / 拖放批次 / 每项合法性 / 目标路径 / 复制 / 跳过 / 失败汇总（避免记录不必要隐私）。
* Unicode / 长路径错误。
* 文件系统错误。
* 异常信息。

隐私：

* 不记录图片内容或缩略图。
* 不上传。
* 不记录不必要的用户隐私数据。
* 如记录完整路径用于排错，应仅保存在本机，并可考虑在普通日志级别减少路径暴露。
* 日志应有限制大小 / 轮换。

---

# 23. 配置文件

使用：

```text
config.json
```

至少：

```json
{
  "open_hotkey": "Ctrl+Shift+P",
  "favorite_hotkey": "Ctrl+F",
  "unfavorite_hotkey": "Ctrl+Shift+U",
  "enable_notification": true,
  "favorite_folder_name": "Favorite"
}
```

要求：

* 位于程序目录。
* 随程序移动。
* 不依赖注册表。
* 配置损坏采用安全默认值或停止相应功能。
* 快捷键冲突 / 无法安全实现时应拒绝启用并提示，不得静默吞掉其他应用输入。
* 当前第一版生产默认键位统一为 **P/F/U**：Explorer Open = `Ctrl+Shift+P`，Photos Favorite = `Ctrl+F`，Photos Unfavorite = `Ctrl+Shift+U`。
* `favorite_folder_name` 默认值固定为 `Favorite`。
* Configuration 第一版只暴露 `favorite_folder_name` 一项，不要求同时提供快捷键 / 通知 GUI 设置。
* 合法名称按 §11.0 校验。
* 保存成功后应立即更新当前运行实例中的配置，无需重启。
* 修改名称后，只按新名称执行后续 Favorite / Unfavorite / Repair / Export 结构判断；旧名称目录完全忽略。
* 不维护收藏目录名称历史，不自动迁移旧目录或旧 `.lnk`。
* 若配置保存失败，旧内存配置和旧磁盘配置必须保持可用，不得留下损坏的半写 `config.json`；应使用安全写入 / 原子替换或等价机制。
* 未来若调整默认快捷键，必须先更新本 Source of Truth，并重新执行相关键盘作用域 / 透传验证；不得仅在单轮 Codex Prompt 中以 override 形式改变产品行为。

第一版不要求复杂 GUI 设置界面；Configuration 只需一个简洁的单项设置窗口。

---

# 24. 错误与安全原则

总原则：

> **无法确定时，宁可不操作，也不能错误修改用户文件。**

必须：

* 无 BoundSession → 不收藏 / 不取消。
* SourceSnapshot 匹配 0 或 >1 → 不操作。
* basename 不可靠 → 不操作。
* 无法证明 trigger 对应正确 SourceItem → 不形成文件修改请求。
* SourceItem 不存在 / FileIdentity 改变 → 不操作。
* 不支持 Explorer View → 不建立 Session。
* 默认 handler 不是 Photos → 不建立 Session。
* 不通过 Photos File Info 取路径。
* 不基于 filename 搜索。
* 不根据 foreground transition 猜文件。
* 不覆盖未知 `.lnk`。
* 不删除失效 / 冲突 `.lnk`。
* 不执行未知 `.lnk`。
* 不删除 / 修改原图片。
* 不自动移动 / 重命名图片。
* 不进行不必要大范围扫描。
* 不用 Distributed Link Tracking / Shell 搜索结果作为收藏有效性的唯一依据。
* 创建 `.lnk` 防覆盖竞争。
* 删除 `.lnk` 防 TOCTOU。
* 不跟随当前配置收藏目录 Reparse Point 写入。
* 文件操作失败不留下正常外观半成品。
* 输入前台未知（包括 `HWND=0`）时按 non-target 处理。
* 不能为了“触发时绑定”而把 UIA、Explorer COM、文件 I/O 移入 `LowLevelKeyboardProc`。
* Repair / Export 只处理用户通过工具窗口拖入或 command mode 明确传入的路径，不得自动扫描目录、收藏集合或磁盘。
* 拖放窗口不得把“拖入文件所在目录的所有 `.lnk`”扩展成 selection。
* Repair 不执行旧 `.lnk`，不信任旧绝对目标，不调用 `IShellLink::Resolve()` 作为目标来源，不使用 Distributed Link Tracking。
* Repair 提交前必须重新验证原 `.lnk` 身份/内容、目标文件身份和相关目录安全，不能证明时不替换。
* Export 只按项目可验证 RelativePath 计算目标，不执行 `.lnk`，不搜索目标。
* Export 只复制，不移动、不删除、不修改原图片或原 `.lnk`，并采用 no-overwrite。
* Export 输出目录若为 reparse point 或无法证明安全，整项/整批按安全策略拒绝，不得跟随重定向。
* `favorite_folder_name` 只决定当前收藏目录名称，不得触发旧目录扫描、迁移、合并、重命名或删除。
* Configuration 不得接受可逃逸出图片目录的路径型名称。
* 第一版不得为了 Export / Repair 用户入口重新引入 Explorer 右键注册、COM Shell Extension、MSIX / sparse package 或 Explorer 进程内扩展。

---

# 25. 第一版 MVP

必须实现：

* Windows 10 / 11 x64 Portable。
* self-contained x64 正式构建。
* 后台托盘。
* 托盘 `Export...` / `Repair...` / `Configuration...` / `Exit`。
* Export 独立拖放窗口。
* Repair 独立拖放窗口。
* Configuration 单项设置窗口。
* 单实例。
* `WH_KEYBOARD_LL` foreground-scoped 输入层。
* Explorer `Ctrl+Shift+P` 显式打开快捷键。
* 正常物理 Explorer Folder SourceSnapshot。
* exactly-one selection。
* 真实 filesystem path。
* Unicode FileIdentity。
* `IFolderView2::InvokeVerbOnSelection(NULL)`。
* Microsoft Photos 前台 / hosted PhotosApp 识别。
* 被动 Photos basename 读取。
* PendingSession → BoundSession 验证。
* Photos Previous / Next snapshot-only 映射。
* 源文件删除 / 替换失效。
* 2 秒 basename grace。
* `Ctrl+F` 收藏。
* `Ctrl+Shift+U` 取消。
* 非目标应用完全透传。
* Hook callback 极小，SourceItem 绑定在 callback 外安全完成。
* 已接受文件操作请求与原 trigger 的 SourceItem 绑定不可被后续切图重新绑定。
* 四态 `.lnk` 状态。
* `.lnk` 相对寻址。
* no-overwrite 安全创建。
* TOCTOU 安全删除。
* 当前配置收藏目录 Reparse Point 防护。
* `favorite_folder_name` 可配置，默认 `Favorite`。
* 名称变化后旧目录忽略、不迁移、不扫描。
* Unicode。
* 长路径安全失败。
* 串行操作队列。
* Windows 非阻塞通知。
* 日志。
* config.json 安全保存。
* 不修改原图。
* 不使用收藏数据库。
* Windows 10 `PhotosApp` + Windows 11 `Photos` 精确 Photos 身份兼容。
* 旧 Win10 收藏快捷方式显式 Repair（单个 + 批量）。
* 合法收藏快捷方式显式 Export（单个 + 批量）。
* 一次性 `--export` / `--repair` command mode 保留，但不是主要 UI。

---

# 26. 第一版明确不做

第一版不要求 / 不支持：

* 从任意 Photos 会话直接反查完整路径。
* Photos File Info 路径读取。
* 普通 Explorer 双击自动透明绑定。
* Explorer Enter 自动透明绑定。
* 根据 Explorer → Photos foreground transition 自动猜 Source Session。
* 全局鼠标双击时间推断。
* Source Session 自动恢复式文件搜索。
* Search Results / Libraries / Quick Access 等虚拟 View 正式支持。
* 收藏数据库。
* 图片历史移动追踪。
* 图片重命名追踪。
* 自动修复单独移动图片。
* 自动修复单独移动收藏目录。
* 未经用户显式拖入 / 传入的自动 Repair / 自动扫描旧 `.lnk`。
* 收藏目录名称历史数据库。
* 修改 `favorite_folder_name` 后自动迁移 / 重命名 / 合并旧目录。
* 自动覆盖冲突 `.lnk`。
* 云同步。
* 纯云端 Photos 项目。
* 网络路径完整兼容保证。
* 图片内容分析。
* 图片哈希索引。
* 全盘搜索。
* 复杂 GUI。
* 修改 / 注入 / Patch Photos。
* 默认修改用户图片文件关联。
* 以 `ms-photos:` 作为核心生产路径。
* 为常规通知强制引入完整 App Toast 激活体系。
* Explorer 自定义右键菜单入口。
* HKCU / HKLM Shell verb 注册作为 Repair / Export 产品入口。
* `IExecuteCommand` / `IExplorerCommand` 生产 Shell Extension。
* MSIX / sparse package / package identity / Explorer 进程内 COM Shell Extension。

---

# 26A. 旧收藏快捷方式 Repair

## 26A.1 目标

Repair 用于解决旧 Windows 10 环境创建、在当前 Windows 11 中 Explorer/Photos 无法正确打开的收藏 `.lnk`。

已确认的真实根因类别是：旧 `.lnk` 的 PIDL 与 LinkInfo 同时保留旧机器/旧卷绝对目标状态；即使 RelativePath 仍正确、FavoriteHelper 四态仍判为 `Favorited`，Explorer/Photos 仍可能使用旧目标描述并显示“文件可能已移动或重命名”。

Repair 的目标不是放宽四态或启用 Shell tracking，而是：

> 在严格 RelativePath 已经能够证明当前真实目标的前提下，安全地重建 Shell Link 的当前目标描述，同时保持收藏关系不变。

## 26A.2 用户入口

第一版主要用户入口：

```text
右键 FavoriteHelper 托盘图标
↓
Repair...
↓
打开独立 Repair 窗口
↓
用户从 File Explorer 把一个或多个 .lnk 拖入窗口
↓
用户确认执行 Repair
```

窗口关闭只关闭该窗口，不退出 FavoriteHelper。

一次性 command mode 仍可显式调用：

```text
FavoriteHelper.exe --repair "<shortcut1>" "<shortcut2>" ...
```

不得：

* 自动扫描整个磁盘。
* 自动扫描所有收藏目录。
* 根据 basename 或旧绝对路径搜索目标。
* 在后台自动修复用户未明确拖入 / 传入的 `.lnk`。

## 26A.3 单链接 Repair 资格

现有 `ShortcutMigrationService.Migrate(shortcutPath)` 的安全模型作为 v6.3 Repair 基础，不应重新实现第二套迁移算法。

只有同时满足以下条件才允许 Repair：

1. 指定文件存在且扩展名为 `.lnk`。
2. `.lnk` 本身不是 reparse point。
3. Shell Link 数据可安全读取。
4. 存在非空、项目可验证的 RelativePath。
5. 从 `.lnk` 当前所在目录严格解析 RelativePath 成功。
6. 严格目标当前存在且文件身份可读取。
7. 结构精确符合：

```text
<图片目录>\<当前 favorite_folder_name>\<完整图片文件名>.lnk
```

8. 使用现有收藏四态逻辑可证明该 `.lnk` 对当前严格目标为 `Favorited`。
9. 若当前存储目标已经等于严格目标，则返回 `AlreadyCurrent`，不得重写。

名称不同的旧收藏目录在当前配置下视为结构不符并拒绝，不得为了“兼容历史名称”自动扫描或猜测。

Broken、Conflict、malformed、missing target、结构不符或无法证明安全的 `.lnk` 必须拒绝。

## 26A.4 安全替换

需要 Repair 时：

```text
验证原链接
→ 固定原链接身份 + SHA-256
→ 在同目录创建全新临时链接
→ 回读并验证 RelativePath / 当前存储目标 / 目标文件身份
→ 重新验证当前配置收藏目录安全
→ 重新验证目标文件身份
→ 重新验证原链接身份 + SHA-256
→ 原子替换原链接
→ 成功后清理临时/备份文件
```

要求：

* 不执行旧 `.lnk`。
* 不信任旧 LocalBasePath / LinkInfo 作为当前目标来源。
* 不调用 `IShellLink::Resolve()` 来猜目标。
* 不使用 Distributed Link Tracking 或搜索。
* 并发替换、内容改变或目录安全变化时 fail closed。
* 失败时原 `.lnk` 必须保持不变；若提交异常无法证明已恢复，必须保留可用于恢复的备份并明确报告。
* Repair 不得修改目标图片内容、文件名、时间戳或属性。

## 26A.5 批量 Repair

Repair 窗口通过一次 Explorer FileDrop 接收用户明确拖入的路径集合。

窗口层只负责 selection transport、显示与浅层输入反馈，不得重新实现 `.lnk` 合法性模型；真正 Repair 必须逐项调用既有 Repair 核心。

批量操作必须逐 `.lnk` 独立验证和提交：

```text
Repaired
AlreadyCurrent
Skipped/Rejected
Failed
```

单个失败不得导致其他已验证项目被错误回滚或误处理。

完成后窗口应提供批量汇总，例如：

```text
Repaired: N
Already current: N
Skipped: N
Failed: N
```

---

# 26B. 批量 Export

## 26B.1 用户行为

第一版主要用户入口：

```text
右键 FavoriteHelper 托盘图标
↓
Export...
↓
打开独立 Export 窗口
↓
用户从 File Explorer 把一个或多个 .lnk 拖入窗口
↓
用户确认执行 Export
```

窗口关闭只关闭该窗口，不退出 FavoriteHelper。

对每个合法收藏快捷方式，在该 `.lnk` 所在目录下使用固定输出目录：

```text
FavoriteHelper export
```

例如当前：

```text
favorite_folder_name = Favorite
```

则：

```text
Favorite\
├── A.jpg.lnk
├── B.png.lnk
└── FavoriteHelper export\
    ├── A.jpg
    └── B.png
```

Export 的语义是：

> **复制由合法收藏 `.lnk` 的 RelativePath 严格证明的当前目标图片。**

一次性 command mode 仍可显式调用：

```text
FavoriteHelper.exe --export "<shortcut1>" "<shortcut2>" ...
```

## 26B.2 合法性

Export 只接受能够按现有项目 RelativePath / 四态规则，并满足当前 `favorite_folder_name` 结构要求，证明为合法收藏关系的 `.lnk`。

允许旧 Win10 `.lnk` 在以下情况下直接 Export，而不要求先 Repair：

* RelativePath 仍可严格解析到当前存在目标；
* `.lnk` 当前所在目录名称等于当前 `favorite_folder_name`；
* 该目标就是按项目结构对应的图片；
* 四态逻辑能够安全判定为 `Favorited`。

旧 LocalBasePath、PIDL、LinkInfo 是否过期，不影响 Export 的目标选择，因为 Export 不执行 `.lnk`，也不依赖这些旧绝对目标描述。

不得：

* 执行 `.lnk`。
* 通过 Shell Resolve / tracking 得到目标。
* 根据文件名搜索目录或磁盘。
* 接受 Broken / Conflict / malformed / missing target。
* 自动扫描名称不同的历史收藏目录。

## 26B.3 输出安全

要求：

* Export 只执行 copy，不执行 move/delete。
* 不修改原图片。
* 不修改原 `.lnk`。
* 输出目录不存在时可以创建。
* 输出目录已存在时必须确认是普通安全目录；若是 symbolic link、junction 或其他 reparse point，拒绝使用。
* 输出文件采用 **no-overwrite**；已存在同名文件时跳过并报告，不静默覆盖。
* Unicode 和空格路径必须完整支持。
* 复制前后应保持原图片内容和元数据不被 FavoriteHelper 主动修改。
* 单个项目失败不得阻止其他独立合法项目继续处理，除非发现影响整批输出目录安全的条件。

批量结果至少区分：

```text
Exported
SkippedAlreadyExists
SkippedInvalid
Failed
```

并在窗口中给出汇总：

```text
Exported: N
Skipped: N
Failed: N
```

---

# 26C. Tray Export / Repair 拖放窗口

## 26C.1 目标

v6.3 不再把 Explorer 自定义右键菜单作为 Repair / Export 的产品入口。

统一用户入口：

```text
FavoriteHelper tray
├── Export...
├── Repair...
├── Configuration...
└── Exit
```

Export / Repair 点击后打开各自独立的普通工具窗口。

## 26C.2 拖放 selection

Export / Repair 窗口必须支持从 File Explorer 拖入：

* 单个 `.lnk`。
* 多个 `.lnk`。
* Unicode / 空格路径。
* mixed input，用于安全拒绝非法项并展示结果。

拖放应使用 Windows / WinForms 支持的 FileDrop / OLE drag-and-drop 数据，不通过目录扫描重建 selection。

一次 drop event 提供的路径集合即为该次用户明确输入集合。

禁止：

* 根据拖入路径所在目录补充未拖入 `.lnk`。
* 根据 basename 搜索。
* 执行拖入 `.lnk` 来判断目标。
* 在窗口 UI 层复制一套 Four-State / Repair / Export 合法性算法。

## 26C.3 执行与窗口生命周期

推荐工作流：

```text
拖入路径
↓
窗口列出当前 selection
↓
用户点击 Export / Repair
↓
冻结本批次显式输入
↓
在 UI callback 外执行核心服务
↓
窗口显示汇总 / 失败项
```

要求：

* 拖入本身不立即执行 Repair；必须经过显式执行按钮。
* Export 与 Repair 为保持一致，也采用“拖入 → 确认执行”。
* 真正文件操作不得阻塞 UI DragDrop callback。
* 批次开始后不得因为 Explorer 后续 selection 改变而重新绑定输入。
* 工具窗口关闭不退出 resident FavoriteHelper。
* 工具窗口关闭过程中如已有文件操作，必须按既有安全退出 / 完成策略处理，不能制造半成品。
* 同一时间的文件修改仍必须遵守项目既有串行化、FileIdentity、hash、TOCTOU、no-overwrite 和 fail-closed 原则。

## 26C.4 与 command mode 的关系

Tray window 与 command mode 必须复用同一生产核心：

```text
Tray Export window ─┐
                    ├→ ExportService
--export -----------┘

Tray Repair window ─┐
                    ├→ ShortcutMigrationService
--repair -----------┘
```

不得出现：

* “GUI 版 Export 算法”。
* “CLI 版 Export 算法”。
* “GUI 版 Repair 算法”。
* 第二套 shortcut validity model。

---

# 26D. Configuration

## 26D.1 第一版范围

托盘：

```text
Configuration...
```

打开一个简洁独立窗口。

第一版只提供一个配置项：

```text
Favorite folder name
[ Favorite ]
```

以及：

```text
Save
Cancel / Close
```

不要求复杂设置页或其他 GUI 配置项。

## 26D.2 保存语义

默认：

```text
favorite_folder_name = Favorite
```

用户例如改为：

```text
Starred
```

保存成功后：

```text
立即使用 Starred
```

以后 Favorite / Unfavorite / Repair / Export 只把：

```text
<图片目录>\Starred\
```

视为当前收藏目录。

已有：

```text
<图片目录>\Favorite\
```

必须：

```text
不重命名
不迁移
不扫描
不合并
不删除
不作为当前收藏状态来源
```

如果用户未来再次把配置改回 `Favorite`，该目录才重新按当前规则参与状态判断。

## 26D.3 配置一致性

`favorite_folder_name` 必须具有单一权威来源。

所有生产路径必须读取同一当前配置值：

* Favorite。
* Unfavorite。
* Four-State classification 中的项目目录结构判断。
* Repair eligibility。
* Export eligibility。
* Reparse Point 目录安全检查。

不得在多个服务中继续硬编码 `收藏` 或 `Favorite` 作为各自独立的生产目录规则。

---

# 27. 核心目录模型

默认配置：

```text
favorite_folder_name = Favorite
```

目录模型：

```text
图片目录\
│
├──001.jpg
├──002.png
├──003.webp
│
└──Favorite\
    ├──001.jpg.lnk
    └──003.webp.lnk
```

逻辑关系：

```text
Favorite\001.jpg.lnk
        ↓
相对寻址
        ↓
..\001.jpg
```

如果当前配置改成其他合法名称，则仅替换上述 `Favorite` 目录名；RelativePath 的核心相对关系保持不变。

因此：

```text
整个“图片目录”整体移动
        ↓
图片与当前收藏目录的相对关系不变
        ↓
收藏关系继续有效
```

这里的 `..\001.jpg` 是项目的**逻辑相对关系表达**，不是对某个 Windows Shell API 参数格式的机械规定。

名称不同的旧收藏目录不属于当前目录模型，除非用户把 `favorite_folder_name` 明确改回该名称。

---

# 28. 已验证基础、Round 状态与正式开发优先级

## 28.1 Phase 1 / Round 1 总体状态

Phase 1 已完成核心技术可行性验证；Round 1 Core 已按既定流程完成正式核心链路集成并被接受进入下一阶段，同时保留已经记录的环境性限制。

后续开发原则：

* 不再为了把历史 `PASS WITH LIMITATIONS` 标签强行改成 `PASS` 而重复研究难以自然制造、且已有确定性生产状态机 seam 覆盖的场景。
* 已验证的 Source Session、Explorer SourceSnapshot、Photos basename/PID binding、FileIdentity、`WH_KEYBOARD_LL` 作用域与 modifier 机制不得无理由重构。
* 新阶段如发现具体生产缺陷，可以做最小修复并进行相关回归；不得借机恢复已被 Phase 1 否定的旧架构。

## 28.2 Production Keyboard Scope

Phase 1 / Round 1 的生产键盘机制结果可作为架构证据：

```text
PRODUCTION KEYBOARD SCOPE PASS WITH LIMITATIONS
```

已验证的机制包括：

* dedicated `WH_KEYBOARD_LL`。
* event-stream modifier tracking。
* minimal callback + worker。
* injected input ignored。
* auto-repeat suppression。
* Explorer open trigger。
* Photos Favorite / Unfavorite。
* Browser `Ctrl+F` passthrough。
* 其他应用透传。
* 普通输入不受影响。
* unusual modifier release。
* foreground switching。
* hook responsive。
* `HWND=0` fail-closed 修复方向。

历史技术验证使用过：

* Explorer Open：`Ctrl+Shift+O`。
* Photos Favorite：`Ctrl+F`。
* Photos Unfavorite：`Ctrl+Shift+F`。

**这些旧组合仅作为历史验证证据，不再是 v6.3 的生产默认值。**

v6.3 当前生产默认值固定为：

* Explorer Open：`Ctrl+Shift+P`。
* Photos Favorite：`Ctrl+F`。
* Photos Unfavorite：`Ctrl+Shift+U`。

新默认组合必须在正式集成版按 §29.3 完整重跑，不得仅凭旧组合验证结果宣称键位本身已经通过。

关键实现决策：

> 不得在 `LowLevelKeyboardProc` 中依赖 `GetAsyncKeyState()` 识别当前事件的修饰键；也不得把 SourceItem resolve / UIA / 文件系统操作塞入 callback。

## 28.3 `.lnk` 当前状态

Phase 1 的相对 `.lnk` 基础已在后续生产实现和 Windows 11 实机中完成进一步验证。

当前已验证：

* 正常创建 PASS。
* `.lnk` 内部存在预期相对元数据。
* FavoriteHelper 四态只信任项目可验证的 RelativePath 关系，不以 Shell tracking / Resolve 作为成功依据。
* parent move 后真实 Windows Shell launch：PASS。
* image-directory rename 后真实 Windows Shell launch：PASS。
* same-volume copy-delete 且旧源完全不存在：PASS。
* D: → C: cross-volume copy-delete 且旧源完全不存在：PASS。
* 上述每项均使用随机唯一 basename，并确认 FavoriteHelper 仍判为 `Favorited`、Shell 实际打开迁移后唯一图片。

另外，旧 Win10 `.lnk` 在当前 Win11 中的真实打不开问题已经完成字段隔离诊断：RelativePath 可以仍然正确，但旧 PIDL + 旧 LinkInfo 的组合会使 Explorer/Photos 使用过期旧目标描述。当前新链接创建实现没有因此被证明有缺陷；正确产品行为是使用显式安全 Repair 重建旧快捷方式，而不是放宽四态或启用 Shell 搜索/Tracking。

Windows 10 上对当前新链接及 Repair 的最终物理回归仍属于发布 Gate。

v6.3 新增的可配置收藏目录名称只改变目录名来源，不改变 RelativePath / Four-State / Shell Link 的核心安全模型。

## 28.4 Round 3C Explorer 集成结论

v6.2 曾计划使用 Explorer 右键菜单作为 Repair / Export 用户入口。

当前 Windows 11 25H2 x64 build `26200.7623` 已获得以下实机证据：

* HKCU static verb 多种注册位置均成功写入，但 `.lnk` 菜单不可见，包括 `Show more options`。
* `IExecuteCommand + DelegateExecute` out-of-process COM probe 注册成功，但 `.lnk` 菜单仍不可见。
* `IExplorerCommand + sparse package` spike 因当前机器缺少受支持的 MSVC / Windows SDK / AppX packaging/signing toolchain 而被阻塞；该结果不是机制不可行证据。

v6.3 产品决策：

> 第一版停止继续开发 Explorer 自定义右键入口，不安装额外 C++/SDK 工具链来完成该入口；改用常驻托盘 Export / Repair 拖放窗口。

相关 spike 文件可以保留为技术证据，但不得继续驱动当前产品架构。

## 28.5 当前正式开发优先级

截至 v6.3：

1. Source Session 核心模型和 Session 生命周期：**已完成并在 Win11 真实通过。**
2. `WH_KEYBOARD_LL` 输入层与 worker queue：**已完成。**
3. Explorer Shell View / SourceSnapshot / Photos basename / FileIdentity：**已完成。**
4. `.lnk` 四态、安全创建/删除、Reparse Point、串行化：**已完成。**
5. 通知 / 日志 / config / 托盘 / 单实例 / Portable 产品化：**已完成核心实现；Win10 最终回归待做。**
6. Windows 11 新版 Photos `Photos.exe` 身份兼容：**已完成并实机通过。**
7. 旧 Win10 `.lnk` 安全 Repair 核心服务：**已完成并实机通过。**
8. 批量 Export 核心：**已完成并实机通过。**
9. 一次性 `--export` / `--repair` command mode：**已完成并实机通过。**
10. Explorer 自定义右键入口：**从 v6.3 第一版范围移除。**
11. Tray Export / Repair 拖放窗口 + Configuration + configurable favorite folder：**下一阶段。**
12. Win10 最终物理回归 + v6.3 双环境 Release Gate：**最后完成。**

不得为了实现新的 Tray UI / Configuration 重构已经验证通过的 Photos Source Session、键盘 Hook、Export 核心、Repair 核心或收藏四态安全模型。

## 28.6 v6.3 后续 Codex 执行批次

### Round 3C — Tray Batch UI + Configuration

范围：

* 删除 / 禁用未完成且不再需要的生产 ExplorerIntegration 路径与托盘 Install/Remove 项。
* 保留 Explorer integration spikes 作为证据，不把它们接入生产。
* 托盘改为 `Export...` / `Repair...` / `Configuration...` / `Exit`。
* Export / Repair 独立 WinForms 拖放窗口。
* FileDrop 单选 / 多选 / Unicode / spaces。
* 拖入后显式点击执行，不在 DragDrop callback 内做文件操作。
* GUI 直接复用 `ExportService` / `ShortcutMigrationService`。
* `config.json` 新增 `favorite_folder_name`，默认 `Favorite`。
* Configuration 第一版只编辑该字段。
* 对 Favorite / Unfavorite / Four-State structure / Repair / Export / reparse checks 统一改为读取当前配置名称，不保留硬编码生产目录名。
* 名称变更后旧目录不迁移、不扫描、不合并、不删除。
* focused automated tests。

### Round 3D — Focused Runtime / Safety Verification

优先真实用户流程：

* tray → Export window → single / multi drop → Export。
* tray → Repair window → single / multi drop → Repair。
* 窗口关闭不退出 resident、不破坏 Hook / Source Session。
* Unicode / spaces。
* mixed valid/invalid drop。
* Win10 legacy sample Repair。
* current link → `AlreadyCurrent`。
* Broken / Conflict / malformed / missing target。
* output already exists。
* output/shortcut/current favorite folder reparse point。
* 并发替换与失败路径。
* 原图片 / 原 `.lnk` 完整性。
* Configuration default `Favorite`。
* 合法自定义目录名立即生效。
* 非法目录名拒绝且旧配置保持。
* 改名后旧收藏目录被忽略；改回旧名后重新参与当前状态判断。
* config safe-save / reload / Portable move。

不得用单元测试或静态分析代替真实工具窗口拖放与执行证据。

### Final v6.3 Release Verification

最后执行：

* Windows 11 完整 v6.3 用户流程。
* Windows 10 关键旧功能与 Repair / Export / Configuration / 通知回归。
* self-contained Portable 移动测试。
* 最终安全矩阵、构建、Git diff/status 与发布 Gate。

---

# 29. 核心验收标准

验证优先级固定为：

1. 真实用户工作流。
2. 功能 / 边界行为。
3. 用户文件安全。
4. 相关回归。
5. 最后才是静态检查。

静态分析、单元测试和代码审阅不能替代真实运行。

## 29.1 基本 v6.3 核心用户流程

默认：

```text
favorite_folder_name = Favorite
```

```text
1. 启动 FavoriteHelper。
2. 在普通物理 File Explorer 文件夹选择 exactly one 图片。
3. 按 Ctrl + Shift + P。
4. 确认 Photos 正确打开该图片。
5. 确认 BoundSession 建立。
6. 在 Photos 使用 Right / Left 浏览数张图片。
7. 按 Ctrl + F。
8. 当前真实图片目录创建 Favorite 目录。
9. 创建 完整图片文件名.lnk。
10. 读取 .lnk 元数据，确认相对关系。
11. 再次 Ctrl + F，提示已收藏且不重复。
12. Ctrl + Shift + U。
13. 仅删除正确 .lnk。
14. 原图片内容 / 文件名 / 元数据不变。
```

还必须测试：

```text
15. 直接普通双击打开 Photos，不经过 FavoriteHelper。
16. 按 Ctrl + F。
17. 必须安全拒绝，因为无有效 BoundSession。
```

## 29.2 Source Session 验证

必须覆盖：

* Photos 初始关闭。
* Photos 已运行。
* Unicode / 空格目录。
* multiple Next。
* multiple Previous。
* first/last boundary。
* 返回初始图片。
* Alt+Tab 离开再回来。
* 新显式打开替换旧 Session。
* missing source。
* replaced FileIdentity。
* basename matches = 0。
* basename matches > 1（可人工构造可支持场景时）。
* transient basename empty ≤ 2s。
* persistent basename missing > 2s。
* Photos process exit。
* `HWND=0` 前台瞬态。
* unsupported Explorer View。
* zero selection。
* multiple selection。
* non-image selection。
* default handler not Photos。
* initial Photos basename mismatch。
* Photos launch failure。

任何不确定情况都必须 fail closed。

## 29.3 键盘范围与透传

使用 v6.3 当前生产默认键完整重跑：

1. Explorer + `Ctrl+Shift+P`。
2. exactly one action per physical press。
3. held-key auto-repeat。
4. `Ctrl+Shift+P` outside Explorer passthrough。
5. Photos + `Ctrl+F`。
6. Photos + `Ctrl+Shift+U`。
7. Browser + `Ctrl+F` 正常 Find。
8. Other app + `Ctrl+Shift+U` 正常。
9. Other app + `Ctrl+Shift+P` 正常。
10. Normal typing unaffected。
11. unusual modifier release。
12. foreground switch while modifiers held。
13. Explorer → Photos → Browser → Photos。
14. Hook 长时间仍 responsive。
15. `HWND=0` 不得误分类。
16. 验证 Hook callback 不执行 UIA / Explorer COM / 文件系统操作，且长时间交互不因 target binding 阻塞 Hook。

## 29.4 Unicode 与路径

至少：

```text
E:\FavoriteHelper 测试\梱枝りこ\中文 日本語 space\02 次の画像.jpg
```

并至少设置一次 Unicode 收藏目录名，例如：

```text
favorite_folder_name = お気に入り
```

验证：

* Explorer 路径。
* SourceSnapshot。
* FileIdentity。
* Photos basename。
* 收藏目录名。
* `.lnk` filename。
* `.lnk` metadata。
* Favorite / Unfavorite。
* Export / Repair drag-drop。
* 整体移动。

## 29.5 冲突与失效

### 失效 `.lnk`

构造损坏 / 相对关系无效快捷方式：

* 不覆盖。
* 不删除。
* 明确提示。

### 冲突 `.lnk`

同名 `.lnk` 有效但指向其他目标：

* 不覆盖。
* 不删除。
* 明确提示。

### Source file invalidation

删除或替换 SourceItem：

* Session / 当前映射失效。
* 不创建 / 删除 `.lnk`。

## 29.6 TOCTOU 与 Reparse Point

必须验证：

* 当前配置收藏目录在检查后替换为 Symbolic Link / Junction。
* `.lnk` 在验证后、删除前被替换。
* `.lnk` 在不存在检查后、创建提交前由其他进程创建。
* 最终不会写入预期目录外。
* 不覆盖新出现文件。
* 不删除后来替换文件。

真实破坏性 / 竞争测试只允许使用 disposable test directories / images；不得为了验证而破坏真实用户图片集合。

## 29.7 连续按键、快速切图、退出与文件系统错误

测试：

* 长按 Favorite。
* Favorite → Unfavorite。
* Unfavorite → Favorite。
* A 图触发后立即切 B 图。
* 多图片连续请求。
* 收藏目录无权限。
* `.lnk` 创建失败。
* 路径突然不存在。
* 长路径失败。
* 配置文件损坏。
* 配置保存失败。
* Export / Repair 操作进行中关闭工具窗口。
* 文件操作进行中 Exit。

要求：

* Hook callback 始终保持轻量、响应及时。
* 文件操作请求只有在能够证明 trigger 对应正确 SourceItem 后才被接受。
* 已接受 request 始终绑定正确 SourceItem。
* A 图触发后立即切 B 图时，绝不得把 A 的已接受请求重新绑定到 B；无法证明时必须拒绝而不是猜测。
* Export / Repair drop batch 一旦确认执行，不因 Explorer 后续 selection 改变而变化。
* 安全串行。
* 不留下半成品。
* 关闭工具窗口不等于 Exit。
* Exit 停止接受新操作。
* 当前文件修改安全结束 / 回滚。
* 单实例锁最终释放。

## 29.8 相对 `.lnk` 移动 / 复制验证

使用随机唯一文件名，避免同名文件被 Shell 搜索误命中。

测试时以**当前 `favorite_folder_name`** 替代下文 `<FavoriteFolder>`。

### Parent move

1. 创建收藏并记录 `.lnk` 相对元数据。
2. 将整个图片目录移动到新的父目录。
3. 确认旧位置不存在原目录 / 原图片。
4. **实际通过 Windows Shell 启动移动后的 `.lnk`**。
5. 必须打开新位置中的正确图片。
6. FavoriteHelper 四态判定仍为 `Favorited`。

### Image-directory rename

1. 将图片目录本身改名。
2. 确认旧目录名不存在。
3. 实际通过 Windows Shell 启动改名后的 `.lnk`。
4. 必须打开改名目录中的正确图片。
5. FavoriteHelper 四态判定仍为 `Favorited`。

### Copy-delete

1. 准备新的随机唯一测试目录 A 并创建收藏。
2. 整体复制目录 A 为 B。
3. 完全删除 A，确认旧原路径不存在。
4. 实际通过 Windows Shell 启动 `B\<FavoriteFolder>\xxx.ext.lnk`。
5. 必须打开 B 中对应图片。
6. FavoriteHelper 四态判定仍为 `Favorited`。

### Cross-volume

如果验证环境存在第二个安全可写卷：

1. 将整个图片目录移动 / 复制到另一卷，确保原位置不存在。
2. 实际通过 Windows Shell 启动目标卷中的 `.lnk`。
3. 验证打开正确图片并保持 `Favorited`。

如果没有第二个可写卷：

```text
CROSS-VOLUME NOT VERIFIED
```

不得仅凭相对元数据计算、`IShellLink::Resolve()` 或理论推断宣称 cross-volume 支持已通过。

所有移动 / copy-delete 验收均必须结合：

* `.lnk` 相对元数据。
* 相对目标计算。
* 旧路径不存在。
* 实际 Shell 打开结果。

## 29.9 Portable 验证

```text
1. 记录本次验证的 FavoriteHelper 版本、Windows edition/build，以及 Microsoft Photos package/version。
2. 在目录 A 启动 FavoriteHelper。
3. 正常使用收藏、Export / Repair 工具窗口与 Configuration。
4. 退出程序。
5. 将整个 FavoriteHelper 程序目录移动到目录 B。
6. 直接启动。
7. 无需安装 .NET Runtime。
8. 配置和日志仍从新程序目录正常读取。
9. favorite_folder_name 保持。
10. 收藏、Export / Repair 工具窗口与 Configuration 继续正常。
```

## 29.10 非支持场景验证

至少验证一个当前无法可靠建立受支持 Source Session 的场景。

要求：

```text
FavoriteHelper
↓
识别无法可靠处理
↓
明确提示或安全拒绝
↓
不创建、不覆盖、不删除任何 .lnk
```

不得为了让测试“通过”而猜测路径。

## 29.11 Windows 10 / Windows 11 Photos 与通知兼容

Windows 11 必须确认：

* 当前稳定 Photos 使用 `Photos.exe` 时可建立 BoundSession。
* 多个 Photos PID 存在时仍绑定到实际验证的目标 PID，不按宽泛名称猜测。
* Source Session、Previous/Next、Favorite/Unfavorite 与通知均真实通过。
* 通知长文本完整、不重叠、不截断、不抢焦点。
* Tray Export / Repair / Configuration 窗口能够正常打开 / 关闭，不破坏 resident 状态。

Windows 10 必须最终物理回归：

* `PhotosApp.exe` 仍被正确识别。
* Source Session 核心流程不因 Win11 兼容改动退化。
* 通知自适应布局正常。
* 当前新链接创建 / Shell launch 正常。
* Repair 对当前链接正确 `AlreadyCurrent` 或按规则处理。
* Tray Export / Repair / Configuration 基本交互正常。

## 29.12 Repair 验收

真实用户流程必须覆盖：

1. 托盘 → `Repair...` → 打开 Repair 窗口。
2. 对真实 disposable Win10 legacy `.lnk`，Repair 前手动双击确认旧链接表现为错误页/无法正确打开。
3. 从 Explorer 把该 `.lnk` 拖入 Repair 窗口并执行。
4. Repair 后再次从 Explorer 手动双击同一链接，确认正确图片内容实际显示。
5. RelativePath 保持正确，严格目标不变，存储目标描述更新到当前位置。
6. 原图片 SHA-256、长度、CreationTimeUtc、LastWriteTimeUtc、属性不变。
7. current link → `AlreadyCurrent` 且 `.lnk` SHA-256 不变。
8. Broken / Conflict / malformed / missing target / 结构不符 → 拒绝且原 `.lnk` 不变。
9. Unicode / spaces → 成功；再次 Repair → `AlreadyCurrent`。
10. 临时创建失败 → 原链接不变、无临时残留。
11. 提交前原链接被替换 → 不覆盖替换者。
12. `.lnk` / 当前收藏目录 reparse point → fail closed。
13. 批量 Repair 中单个失败不得导致其他合法项目被误处理。
14. mixed drop 不得通过目录扫描扩展 selection。
15. 关闭 Repair 窗口不退出 resident FavoriteHelper。

## 29.13 Export 验收

真实用户流程必须覆盖：

1. 托盘 → `Export...` → 打开 Export 窗口。
2. 单个合法收藏 `.lnk` 拖入并执行 → 正确图片复制到同目录 `FavoriteHelper export`。
3. 多个合法 `.lnk` 一次拖入 → 一次逻辑批次处理全部明确输入项。
4. 输出图片文件名与原图片完整 basename 一致。
5. 旧 Win10 `.lnk` 若 RelativePath 与当前 favorite-folder structure 仍合法，即使未 Repair，也能按严格目标正确 Export。
6. Broken / Conflict / malformed / missing target → 不导出。
7. mixed valid/invalid drop → 合法项按规则处理，非法项独立跳过/失败并汇总。
8. 已存在同名输出 → 不覆盖，报告 skipped。
9. 输出目录 reparse point → 拒绝，不跟随。
10. Unicode / spaces → 正确。
11. 原图片 SHA-256、文件长度、时间戳、属性不被 FavoriteHelper 主动修改。
12. 原 `.lnk` 内容不变。
13. 不执行 `.lnk`、不搜索目标、不得以 Shell tracking/Resolve 作为目标来源。
14. 关闭 Export 窗口不退出 resident FavoriteHelper。

## 29.14 Tray Batch UI / Configuration / Command Mode 验收

必须覆盖：

### Tray / windows

* 托盘显示 `Export...` / `Repair...` / `Configuration...` / `Exit`。
* 不再显示 Install / Remove Explorer integration。
* Export / Repair 支持 Explorer FileDrop 单选、多选、Unicode、spaces。
* 一次 drop 的 selection 不被自动扩展。
* 拖入后需要用户显式点击执行。
* DragDrop UI callback 不直接执行长文件操作。
* 同类型窗口重复打开不会产生失控的重复工具实例。
* 关闭工具窗口不退出 resident，不新增 tray icon / hook / Photos BoundSession。

### Configuration

* 缺少 `favorite_folder_name` 时使用默认 `Favorite`。
* Configuration 初始显示当前值。
* 合法新名称保存后立即生效，无需重启。
* 程序重启后新名称仍从 `config.json` 恢复。
* 空白、`.`、`..`、路径分隔符、非法字符、结尾空格/点、保留设备名被拒绝。
* 非法保存后旧配置保持不变。
* Unicode 名称可正常创建 / 识别收藏目录。
* 名称从 A 改为 B 后，A 目录不迁移、不扫描、不删除，并被当前操作忽略。
* 改回 A 后，原 A 目录重新按当前规则参与状态判断。
* 所有 Favorite / Unfavorite / Repair / Export / Four-State structure / reparse checks 使用同一当前配置值。

### Command mode

* `--export` / `--repair` 继续正常工作。
* command mode 不产生第二个常驻托盘、第二套 keyboard hook 或新的 Photos BoundSession。
* command mode 与 GUI 使用同一核心服务与当前 `favorite_folder_name` 规则。

---

# 30. 完成标准

第一版只有同时满足以下条件才算完成：

1. v6.3 Explicit Source Session 在声明支持的普通物理 Explorer Folder 场景稳定工作。
2. 未经 FavoriteHelper 显式打开的 Photos 会话安全拒绝收藏，不猜路径。
3. SourceSnapshot 完整路径来自 Explorer Shell View，不来自目录扫描 / 搜索。
4. 初始 Photos basename 必须严格验证后才能建立 BoundSession。
5. Photos Previous / Next 始终只在 SourceSnapshot 内唯一映射。
6. Source file missing / replaced identity 能安全失效。
7. transient empty basename 不导致旧路径误操作。
8. `WH_KEYBOARD_LL` 在 Explorer / Photos 正确接管，在其他程序完全透传。
9. v6.3 默认键 **P/F/U** 的 modifier / repeat / foreground switching / `HWND=0` 回归通过。
10. Hook callback 保持极小，不执行 UIA、Explorer COM、SourceItem resolve 或文件系统 I/O。
11. trigger → SourceItem 绑定在 callback 外完成，并能证明对应本次触发；不能证明时 fail closed。
12. 已接受 operation request 不因排队期间切图而重新绑定。
13. `.lnk` 四态正确。
14. `.lnk` 创建 no-overwrite。
15. `.lnk` 删除 TOCTOU 安全。
16. 当前配置收藏目录 Reparse Point 安全拒绝。
17. `favorite_folder_name` 默认 `Favorite`，合法自定义名称立即生效并持久化。
18. 名称变化后旧目录不迁移、不扫描、不合并、不删除，并被当前操作忽略。
19. 所有收藏结构判断共享同一当前 `favorite_folder_name` 权威值。
20. 相对 `.lnk` 完成实际 parent move、目录改名、copy-delete 的真实 Shell launch 验收。
21. cross-volume 只有实际测试后才能声明支持。
22. 能证明 `.lnk` 成功不是旧路径、Distributed Link Tracking 或 Shell 搜索假阳性。
23. Unicode 路径与 Unicode 收藏目录名真实通过。
24. 连续按键 / 快速切图 / 相反操作顺序不误绑定。
25. Exit 和文件系统错误安全结束，不留半成品。
26. Portable self-contained 构建移动后正常，Configuration 与 `favorite_folder_name` 随程序目录移动。
27. 正式验收记录 FavoriteHelper / Windows / Photos 实际版本。
28. 所有核心真实用户流程都有执行证据。
29. Windows 10 `PhotosApp` 与 Windows 11 `Photos` 均完成实际兼容验证。
30. Win11 通知自适应布局通过，Win10 最终视觉回归通过。
31. Legacy Repair 只能作用于用户明确拖入 / 传入且严格合格的 `.lnk`。
32. Repair 的真实旧链接手动 Explorer launch 前/后证据通过。
33. Repair 的 AlreadyCurrent、错误路径、TOCTOU、reparse、Unicode、批量行为通过。
34. Export 仅通过 RelativePath 严格目标复制，绝不执行/搜索 `.lnk`。
35. Export 单选、多选、no-overwrite、reparse、Unicode、mixed selection 通过。
36. Export 不修改原图片和原 `.lnk`。
37. Tray Export / Repair 窗口真实 FileDrop 用户流程通过，关闭窗口不退出 resident。
38. Configuration 非法值 fail closed，安全保存不损坏 `config.json`。
39. 一次性 command mode 不破坏常驻单实例、Hook、Source Session 或既有文件安全边界。
40. 第一版不依赖 Explorer 自定义右键注册、COM Shell Extension、MSIX / sparse package。
41. Windows 10 / Windows 11 的 v6.3 关键真实用户流程均有执行证据。

静态检查、单元测试、代码审阅不能替代真实运行验证。

---

# 31. Phase 1 / Round 1 验证记录（设计依据）

本节用于防止后续开发重新走已经验证失败的路线，并区分“历史验证键位”与“当前生产默认键位”。

## 31.1 Phase 1 验证环境

Phase 1 主要实机环境：

```text
Windows 10 Pro 21H1
Build 19043.1023
x64

Microsoft Photos:
Microsoft.Windows.Photos_2024.11070.11002.0_x64__8wekyb3d8bbwe

Foreground wrapper:
ApplicationFrameHost.exe

Hosted process:
PhotosApp.exe
```

该环境不是永久兼容承诺；它是 v6/v6.1 当前设计的关键实机证据来源。

## 31.2 Photos Direct Path — FAIL

被动 UIA：

* 无 absolute path。
* 无 directory。
* 无 authoritative folder + filename。
* 只有 basename。

完整 Raw View 检查仍失败。

结论：

> 生产代码不得再假定 Photos 能直接提供完整路径。

## 31.3 Photos File Info — FAIL / 禁用

重复自动 Invoke 和手动点击均导致 PhotosApp crash。

结论：

> 当前已验证环境中禁止调用 File Info 作为路径来源。

正式产品也不依赖 File Info，因此不存在必须冒该风险的理由。

## 31.4 Transparent Session — FAIL

Explorer double-click / Enter：

```text
InvokePattern.InvokedEvent = 0
EVENT_OBJECT_INVOKED = 0
```

不能可靠证明具体文件 invocation。

结论：

> 普通双击 / Enter 不自动建立 Session。

Foreground correlation 不是证据。

## 31.5 Explicit Source Session — PASS WITH LIMITATIONS

通过确定性显式触发完成：

```text
Explorer selection
→ SourceSnapshot
→ InvokeVerbOnSelection(NULL)
→ Photos
→ basename verify
→ BoundSession
→ Previous / Next mapping
```

作为 v6.1 建立、由 v6.2 验证扩展并由 v6.3 继续保留的核心架构。

Round 1 正式核心集成已进一步覆盖并接受进入下一阶段，包括：

* Unicode explicit open / snapshot / Photos PID binding / SourceItem resolution。
* 多次 Previous / Next 与返回初始图片。
* Alt+Tab 离开 / 返回保持已绑定 Session。
* Photos PID exit 后 Session 失效。
* 普通图片打开不建立可用 Source Session，Photos Favorite 安全拒绝。
* Explorer zero / multiple / non-image / virtual context fail-closed。
* 源文件删除 / 替换导致 Session 安全失效。
* empty-basename grace / recovery / expiry 与 FileIdentity 变化的确定性生产状态机覆盖。

## 31.6 Production Keyboard Scope — PASS WITH LIMITATIONS

`WH_KEYBOARD_LL` 证明可用于：

* Explorer explicit open。
* Photos Favorite / Unfavorite。
* 非目标应用 passthrough。

根因修复：

> modifier 必须从 hook event stream 跟踪，而不是在 callback 中依赖当前 `GetAsyncKeyState()`。

Round 1 已覆盖：

* 非目标 `Ctrl+F` 正常透传。
* foreground switching while modifiers are held。
* 非常规 modifier release 顺序。
* 随后的普通按键正常透传。
* 无 unintended FavoriteHelper action / stuck modifier state。
* Hook 在 Photos 中持续响应。

注意：Phase 1 / 早期 Round 1 的具体键位验证曾使用 O/F/F；v6.1 已将生产默认改为 P/F/U，v6.2 与 v6.3 继续保持 P/F/U，因此正式 Release 仍按 §29.3 使用当前生产组合完成相关回归。

## 31.7 `.lnk` Phase 1 与后续生产证据

Phase 1 已证明：

* 正常创建。
* `.lnk` 内部存在预期相对元数据。
* move / rename / copy-delete 后按相对关系计算目标正确。
* copy-delete 时原目录已删除。
* 不以 `IShellLink::Resolve()` 作为唯一成功依据。

后续 Windows 11 生产验证已经补齐：

* parent move 后真实 Shell launch：PASS。
* image-directory rename 后真实 Shell launch：PASS。
* same-volume copy-delete 后真实 Shell launch：PASS。
* D: → C: cross-volume copy-delete 后真实 Shell launch：PASS。
* 所有测试使用随机唯一 basename 并确认旧源不存在，避免 Shell 搜索假阳性。

因此当前新链接创建格式已经有真实移动/改名/跨卷 Shell 启动证据；仍需 Windows 10 最终回归确认旧环境没有被后续兼容改动破坏。

## 31.8 后续开发原则

从 v6.3 开始：

* `FavoriteHelper-项目需求-v6.3-开发执行版.md` 是唯一产品 Source of Truth。
* v6.2 及更早版本只作为历史参考；如与 v6.3 冲突，以 v6.3 为准。
* Phase 1 spike 代码仅用于证据、回归和参考。
* Round 1 已验证生产核心架构不得无理由重写。
* 不得为了“更自动”恢复透明双击绑定。
* 不得为了“成功率”恢复 Photos 路径搜索 / 猜测。
* 不得为了“trigger-time binding”破坏 Hook callback 的低延迟边界。
* 后续 Codex Prompt 不应重抄本文大段规范；应引用本文作为 Source of Truth，仅写本轮 scope、关键新增约束、verification 与 output。
* 如果未来要支持新 Windows / Photos 行为，必须作为新的兼容性 Spike 单独验证，再修改需求版本。

---

## 31.9 Windows 11 Photos / 通知兼容记录

当前 Windows 11 实机使用：

```text
Microsoft.Windows.Photos 2026.11060.2004.0
process: Photos.exe
```

已确认：

* 旧代码只识别 `PhotosApp`，导致 Win11 Photos 无法进入正确会话链路。
* 当前集中分类方法精确接受 `PhotosApp` 与 `Photos`。
* `HWND=0` 继续 fail closed。
* 多 PID 场景下真实 BoundSession 成功绑定到正确 `Photos` PID。
* Explorer explicit open → PendingSession → BoundSession → Previous/Next → Favorite/Unfavorite 在 Win11 实机通过。
* 未经 FavoriteHelper 建立的 Photos 会话继续安全拒绝。
* 浏览器 `Ctrl+F` 继续透传。

通知方面，旧固定 340×86 / 固定文本高度方案在 Win11 DPI/字体下会截断。当前改为按实际字体度量自适应宽高，并已人工确认多个短/长通知完整显示、不重叠、不截断、不抢焦点。

正式 Release 前仍需记录该次 Windows 11 edition/build，并完成 Windows 10 视觉/行为回归。

## 31.10 旧 Win10 `.lnk` 故障与 Repair 证据

真实旧样本满足：

* RelativePath 正确指向当前位置图片。
* FavoriteHelper 四态判为 `Favorited`。
* Explorer 手动双击却在当前 Win11 Photos 显示“文件可能已移动或重命名”。

字段隔离实验确认：

* 旧 PIDL + 旧 LinkInfo → 错误页。
* 保留旧 PIDL、移除 LinkInfo → 正常。
* 新 PIDL + 旧 LinkInfo → 正常。
* 旧 PIDL + 新 LinkInfo → 正常。
* 仅凭 RelativePath 的变体不保证 Explorer 可直接启动。

结论：

> 收藏状态的 RelativePath 模型仍然正确；旧 `.lnk` 的 Shell 启动兼容问题来自过期目标描述组合，应通过显式安全 Repair 重建，而不是通过搜索/Tracking/放宽四态解决。

当前 `ShortcutMigrationService.Migrate(shortcutPath)` 已完成：

* real legacy sample Repair + Explorer 手动双击成功。
* AlreadyCurrent no-op。
* missing / malformed / Broken / Conflict / structure mismatch 拒绝。
* Unicode。
* 临时创建失败安全回滚。
* 提交前并发替换检测。
* reparse point fail closed。
* 图片 SHA-256、长度、时间戳、属性不变。

v6.3 的工作是把该已验证内部服务接入新的 Tray Repair 拖放窗口，而不是重写 Repair 核心。

---

# 32. v6.3 修订摘要

相对 v6.2，v6.3 只调整 Repair / Export 用户入口与收藏目录配置，不改变 Source Session、键盘 Hook、`.lnk` RelativePath/Four-State、Repair/Export 核心安全模型：

1. 正式停止第一版 Explorer 自定义右键集成路线；static verb 与 `IExecuteCommand` 在当前 Win11 `.lnk` 实机不可见，sparse-package spike 因缺少受支持工具链被阻塞，上述记录保留为技术证据。
2. 第一版不再要求 HKCU Shell verb、COM Shell Extension、MSIX、sparse package、证书或 package identity。
3. 托盘菜单统一为 `Export...` / `Repair...` / `Configuration...` / `Exit`。
4. Export / Repair 改为独立 WinForms 拖放窗口：用户从 Explorer 明确拖入一个或多个 `.lnk`，再点击执行。
5. 拖放窗口只承担 selection transport / UI，不复制第二套 shortcut validity、Export 或 Repair 算法。
6. 已完成的 `ExportService` 与 `ShortcutMigrationService.Migrate` 继续分别作为唯一 Export / Repair 核心。
7. 已完成的 `--export` / `--repair` command mode 保留为兼容 / 测试 / 高级入口，但不再依赖 Explorer 右键集成。
8. 新增 Configuration 窗口；第一版只提供 `favorite_folder_name`。
9. 收藏目录不再固定为 `收藏`；`favorite_folder_name` 默认值为 `Favorite`。
10. 所有 Favorite / Unfavorite / Four-State structure / Repair / Export / reparse checks 必须读取同一当前 `favorite_folder_name`，不得继续保留多个硬编码目录名来源。
11. 修改收藏目录名称后立即对后续操作生效；名称不同的旧目录不迁移、不扫描、不合并、不删除，也不作为当前收藏状态来源。
12. 如果用户以后把配置改回旧名称，对应旧目录才重新按当前规则参与状态判断；不维护历史名称列表。
13. `favorite_folder_name` 必须是安全单目录名，拒绝空白、`.` / `..`、路径分隔符、Windows 非法字符、结尾空格/点和保留设备名；合法 Unicode 名称必须支持。
14. Configuration 保存失败时旧配置保持可用；`config.json` 不得因半写而损坏。
15. Round 3C 重新定义为 **Tray Batch UI + Configuration**；Round 3D 改为真实拖放 / 配置 / 文件安全验证。
16. v6.3 继续要求真实用户工作流 > 功能/边界 > 用户文件安全 > 相关回归 > 静态检查。
17. v6.3 取代 v6.2，成为后续开发唯一 Source of Truth。
