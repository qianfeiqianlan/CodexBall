# Windows / macOS 完整业务版

`CodexBall.Desktop` 是 Avalonia 正式业务实现，Windows 与 macOS 使用同一套界面、状态模型、素材目录与设置结构。它接入真实 Codex，不再使用验证程序中的演示额度。原 WPF 工程 `CodexBall.App` 和 Store 打包工程保留，作为兼容版本。

## 启动

已生成的自带运行时安装目录/ZIP 位于 `artifacts/desktop/`：

- `win-x64/CodexBall.Desktop.exe`。
- `osx-arm64/CodexBall.app`，适用于 Apple Silicon。
- `osx-x64/CodexBall.app`，适用于 Intel Mac。

解压整个 ZIP。Mac 将 CodexBall.app 放到 Applications 后双击打开。属于菜单栏应用，不显示 Dock 图标；小球只有在检测到外部 Codex 进程时显示，角色始终显示。启动时没有面板是正常行为，所有设置从系统托盘/菜单栏图标进入。退出旧 WPF/验证程序，避免全局快捷键冲突；Windows 业务版与原 WPF 使用相同的单实例互斥名称。

本地开发需要 .NET 9 SDK、PowerShell 7，准备步骤如下：

```powershell
pwsh -File scripts/Setup-Desktop.ps1
# tmp/ 已有模型 ZIP 时：
pwsh -File scripts/Import-Live2DModels.ps1
# 没有下载 ZIP 可先执行 scripts/Download-Live2DModels.ps1，随后以 artifacts/model-downloads 为 SourceDirectory 导入。
dotnet run --project CodexBall.Desktop
```

Finder/登录启动的 PATH 往往不含终端里的 Node/npm 路径。查找器除 PATH 外，还检查 Homebrew、`~/.local/bin`、`~/.npm-global/bin`、NVM 的 Node 版本目录及 Codex.app 的 Resources。启动脚本的 PATH 加入对应目录，继续使用 `codex app-server --stdio` 及本机登录；不要求 API Key，不读取私有令牌或浏览器数据。

## 功能对照

| 功能 | 共用业务 / 平台处理 |
| --- | --- |
| 真实用量 | 短/长期额度、重置时间、更新时间、状态提示，60 秒轮询与额度通知去抖刷新 |
| Codex 活动 | 每三秒检查外部进程；排除自己启动的 app-server 子进程；小球跟随活动，角色独立可见 |
| 小球 | 64 × 64 DIP；剩余额度环、可选重置环；点击切换单/双环；重新选择小球恢复默认显示 |
| 外观 | 托盘统一入口；CodexBall → Koharu → Haruto → Mao → Mark；无效素材省略；失效保存项回退小球 |
| 角色 | 使用模型画布加 20 DIP 页脚；细进度条点击查看用量；窗口内视线跟随、眨眼、头部点击、拖动摇摆/松手回弹 |
| 行为 | 三分钟无互动请求 Sleep；额度低于 20% 触发，恢复至 25% 以上重新允许；真实重置恢复反馈；直接互动优先，不无限排队 |
| 窗口 | 透明、无边框、不在任务栏/Dock、不主动激活；置顶和透明度读取设置；托盘切换置顶 |
| 贴边 | 按四边归一化间距隐藏；3 DIP 可见用量条；悬停恢复原位置，离开后延迟重新隐藏；隐藏暂停动画；拖动时暂时取消贴边，松手距工作区任一边不超过 12 DIP 或越界时自动隐藏，拖回内部则保持显示 |
| 快捷键 | Windows Alt+H / Alt+C；Mac Option+H / Option+C；注册组合键，不安装全局输入监听钩子；小球初始不可见仍注册 |
| 语言 | system / zh-CN / en-US；启动时解析系统语言；托盘切换立即生效并保存 |
| 托盘 | 剩余额度数字图标、刷新、外观、语言、贴边、置顶、启动项、项目主页、升级与退出；窗口没有右键菜单 |
| 开机启动 | Windows 当前用户 Run 项；Mac 当前用户 LaunchAgent（下次登录生效），记录当前发布位置 |
| 更新 | 仅匹配 `CodexBall-Desktop-<版本>-<rid>.zip`，避免误装 WPF/其他架构；下载解压后启动独立辅助进程等待退出、覆盖并重启 |
| 商店包 | Windows 检测包身份；Mac 商店构建可在程序目录放置 store-managed 标记；隐藏启动/自升级并跳过 GitHub 检查 |

动作依赖素材包映射，没有对应专属动作时回退待机；没有新增换装、用户素材导入、AI 聊天或后台服务。PNG 玩家保留为内部兼容实现，经 `ICompanionPlayer` 提供语义动作；用户菜单只提供四个精选角色。

## 设置与诊断

Windows 继续使用 `%LOCALAPPDATA%\CodexBall\settings.json`，与 WPF 字段兼容，直接继承原有设置。Mac 使用 `~/Library/Application Support/CodexBall/settings.json`。位置在 Avalonia 的平台屏幕坐标中保存，不跨设备同步。启动时限制在有效工作区；缩放/屏幕配置改变会重新计算。保存原子替换，损坏的 JSON 回退默认设置。两套 Windows 应用不要同时运行。

Mac 日志：`~/Library/Logs/CodexBall/desktop.log`；Windows 日志位于设置目录中的 desktop.log。

验证完整 UI 业务（临时设置，固定测试快照，不连接 Codex、不触碰正式设置或开机启动）：

```powershell
dotnet run --project CodexBall.Desktop -c Release -- --smoke
# 在 Windows 强制测试 Mac 的 Core profile 路径：
dotnet run --project CodexBall.Desktop -c Release -- --smoke --gl-core
# 从保存为隐藏角色的状态启动：
dotnet run --project CodexBall.Desktop -c Release -- --smoke-restore --gl-core
```

报告与四个角色的控件合成图在当前目录 `artifacts/desktop-smoke/`。测试覆盖四个真实模型、暂停/恢复、切换释放、Codex 关闭角色保持可见/小球隐藏、未知额度、即时翻译和设置持久化。它不会自动修改启动项或实际下载安装更新。

单独验证本机真实 Codex 通信（复用现有登录，报告不含令牌）：

```powershell
dotnet run --project CodexBall.Desktop -c Release -- --check-codex
```

结果写入 `artifacts/desktop-smoke/codex.json`，包括找到的可执行路径、账户状态、是否有短/长期窗口。Mac 已发布包可在终端执行：

```bash
/Applications/CodexBall.app/Contents/MacOS/CodexBall.Desktop --smoke
/Applications/CodexBall.app/Contents/MacOS/CodexBall.Desktop --check-codex
```

## 构建、测试、打包

Windows 原解决方案仍可 `dotnet build` / `dotnet test --filter "Category!=Live2DGraphics"`，包含新业务测试。Mac/Linux 使用不含 WPF 与 Windows 包项目的方案：

```powershell
dotnet test solutions/CodexBall.Desktop.sln -c Release
pwsh -File scripts/Publish-Desktop.ps1 -Runtime win-x64
pwsh -File scripts/Publish-Desktop.ps1 -Runtime osx-arm64
pwsh -File scripts/Publish-Desktop.ps1 -Runtime osx-x64
```

发布包自带 .NET Runtime、模型、匹配的 Cubism Core 和许可。Mac ZIP 保存 Unix 可执行权限，并生成带 LSUIElement 的 .app。CI 在三平台编译及测试共用业务；标签发布同时产出 Desktop Windows/Mac ZIP，原 WPF 发布流程保留。

## 当前验证边界

本次 Windows 本机已通过四角色真实 GPU 业务冒烟、真实 app-server 额度读取；原有 49 项非图形测试与新增 18 项兼容性测试通过。前次 Mac M1 Pro 已验证共享渲染路径；本次新增的完整业务版（尤其 Carbon 快捷键、LaunchAgent、Codex 查找/进程监测）需在 Mac 再验收。启动项和自升级实现已完成，但测试不改变本机启动项，也没有发布新的远程版本以实际升级。

发布包尚未 Apple Developer ID 签名和公证，下载隔离仍可能阻止启动。仅对确定来自自己构建的包，可移除该 .app 的隔离标记：`xattr -r -d com.apple.quarantine /Applications/CodexBall.app`。正式分发应配置签名、公证，不关闭系统全局安全检查。

混合 DPI 多屏拖动、透明区域命中、长期功耗与自升级在受保护安装路径下的权限处理仍需实际使用验证。Mac 升级需要应用所在目录可写。Linux 保留可编译基础与 X11 定位，但本次没有实现 Linux 全局快捷键、登录启动或发布验收，不能视为已完整支持 Linux。


## 2026-10-25 拖动自动贴边修复

### Windows 交叉发布包的 Mac 启动签名

Apple Silicon 实机反馈确认：Windows 上交叉发布的 `CodexBall.Desktop` apphost 可能完全未签名，即使清除隔离标记仍被系统立即终止。清除隔离标记不能补上代码签名。仅对本项目可信的本地测试包，在 Mac 执行：

```bash
codesign --force --sign - "/Applications/CodexBall.app/Contents/MacOS/CodexBall.Desktop"
codesign --force --sign - "/Applications/CodexBall.app"
codesign --verify --deep --strict --verbose=2 "/Applications/CodexBall.app"
"/Applications/CodexBall.app/Contents/MacOS/CodexBall.Desktop" --smoke
```

发布脚本现会在 macOS 上从内到外为 Mach-O 文件和 .app 做本地 ad-hoc 签名并检查签名；Windows 交叉发布时明确提示需要到 Mac 签名。CI 的 Mac 打包任务使用 macOS 执行，因此会执行该步骤。本地 ad-hoc 签名不等于 Developer ID 签名或 Apple 公证，不能据此声称已完成正式分发认证。

迁移版原先仅支持菜单/快捷键隐藏，缺少拖动松手后的自动判定。现在在限制窗口位置之前检测工作区四条边，距边缘不超过 12 DIP 或已经越界时自动启用隐藏；拖回内部保持显示。正常松手和拖动期间指针捕获丢失共用完成处理，原 WPF 版同步采用相同判定。

新增四边/阈值/负坐标多屏及缩放几何测试；Windows 自动业务检查覆盖小球和四个角色的四边释放、恢复位置及内部释放不隐藏。80 项非 GPU 回归测试及 Core profile 完整桌面冒烟通过。Mac 包已交叉编译更新，实际拖动仍需 Mac 实机确认。

### Mac 本地安装脚本

Mac 发布 ZIP 现在同时包含 `Install-CodexBall.command` 与 `INSTALL-MAC.txt`。退出旧程序后，在解压目录运行 `bash ./Install-CodexBall.command`。脚本先复制到临时目录并在目标 Mac 上完成逐个 Mach-O 文件及 bundle 的 ad-hoc 签名/验证，通过后才备份并替换原应用，最后打开应用；签名失败不替换旧版。默认安装到 /Applications，不可写时使用 ~/Applications。用户设置保持不变。Windows 只能验证打包和脚本语法，不能代替 Mac 上实际执行签名及安装。
