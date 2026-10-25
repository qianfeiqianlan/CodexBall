# Avalonia 最小验证

此程序独立于正式 WPF 应用，不加入原有包含 Windows 打包工程的解决方案，不读写正式 settings.json，不启动 Codex。用于判断是否值得继续迁移，不是跨平台正式版。

## 运行

需要 .NET 9 SDK；Live2D 需要 OpenGL 3.3。准备脚本需要 PowerShell 7，在仓库根目录执行：

```powershell
pwsh -File scripts/Setup-AvaloniaProbe.ps1
# 如果尚未导入模型：先按现有说明准备 tmp/ 中的 ZIP，再执行
pwsh -File scripts/Import-Live2DModels.ps1
dotnet run --project CodexBall.AvaloniaProbe
```

Setup-AvaloniaProbe.ps1 使用与正式程序相同的 Native 5 R4.1 ZIP 和 SHA-256，分别准备 Windows x64、Linux x64、macOS universal x64/arm64 原生库及许可证。CodexBall.Rendering/Native/ 与角色素材均被 Git 忽略，不升级 SDK。正式 WPF Setup-Live2D.ps1 不受影响。跨平台运行时通过独立的原生库解析器加载对应文件。

缺少原生库或模型时仍可运行小球；模型未准备好时托盘省略小春。角色加载失败回退小球并在验证面板显示错误。只暴露内置小春，没有用户素材目录扫描。

## 操作与范围

- 启动显示验证面板及 64 × 64 DIP 透明、无边框、不在任务栏显示的小球。
- 拖动小球/角色移动；点击小球切换单环/双环；点击角色播放模型动作。
- 外观在小球与小春间切换，小春为 240 × 260 DIP 加 20 DIP 页脚。
- 角色下方仅有细进度条，点击在验证面板显示演示用量。全部用量为演示数据，不显示真实 Codex 状态，小球始终可见。
- 托盘右键提供外观、隐藏/恢复、面板和退出；悬浮窗没有右键菜单。
- 按四边归一化间距选择隐藏方向，保留 6 DIP 悬停条；恢复先前位置。隐藏暂停渲染，切回小球释放模型与私有 GL 上下文。
- Windows 原生注册 Alt+H 隐藏/恢复、Alt+C 切换外观。快捷键冲突会显示错误（1409 常见于正式应用也在运行），不抢占其他程序快捷键。macOS/Linux 尚未实现全局快捷键，请使用面板或托盘。
- 验证面板可切换置顶。关闭面板后从托盘打开，退出请使用退出按钮或托盘菜单。
- 文本在启动时根据系统语言选择中文/英文；不含正式语言菜单、设置持久化、自动启动或更新。

## 自动 GPU 冒烟检查

```powershell
dotnet run --project CodexBall.AvaloniaProbe -- --smoke
```

需要真实图形桌面、已准备的 Native 库和小春资源。程序完成后自动退出，退出码 0 表示通过，1 表示失败。结果写入执行目录下的 `artifacts/avalonia-probe/`：

- `smoke.json`：平台、实际透明级别、渲染器、有效帧、透明/非透明像素、合成缩放、隐藏暂停/恢复、资源释放及三次重复加载。
- `live2d-frame.png`：原始离屏模型帧。
- `koharu.png`：Avalonia 控件树合成结果（含页脚），不是操作系统桌面截图。

快捷键注册状态只是报告字段，自动冒烟不模拟真人按键或验收托盘交互。不把快捷键冲突误报为渲染失败。

## 2026-10-24 本机结果

Windows 10 x64、RTX 2070、OpenGL 4.6、桌面缩放 175%：实际透明级别为 Transparent；角色有透明背景与变化帧；小球/角色隐藏恢复位置、隐藏暂停和恢复、切回小球释放、三次重新加载通过。两个快捷键注册成功。Release 构建零警告。现有非图形回归测试 49 项通过。

视觉检查发现并修复了非 96 DPI WriteableBitmap 显示裁切：现在离屏像素为画布两倍，位图保持 96 DPI，Image 控件显式按角色 DIP 尺寸缩放。合成像素面积检查用于防止同类明显裁切回归。

这些结果不代表测得桌面帧率或长期功耗；单帧计时仅覆盖模型渲染、读回与位图更新。仍使用 GPU 回读，尚未实现共享纹理。

## 跨平台发布与实机验收

可在 Windows 生成用于转移的框架依赖目录；目标机器需要 .NET 9 Runtime：

```powershell
dotnet publish CodexBall.AvaloniaProbe -c Release -r win-x64 --self-contained false -o artifacts/avalonia-probe/win-x64
dotnet publish CodexBall.AvaloniaProbe -c Release -r linux-x64 --self-contained false -o artifacts/avalonia-probe/linux-x64
dotnet publish CodexBall.AvaloniaProbe -c Release -r osx-x64 --self-contained false -o artifacts/avalonia-probe/osx-x64
dotnet publish CodexBall.AvaloniaProbe -c Release -r osx-arm64 --self-contained false -o artifacts/avalonia-probe/osx-arm64
```

进入对应目录运行 `dotnet CodexBall.AvaloniaProbe.dll`；附加 `--smoke` 可生成该机器的报告。转移整个目录，包括 runtimes、Skins、图标和许可，不仅复制 DLL。原生库仍需先执行准备脚本再发布。发布目录不是安装包，也没有 macOS .app、签名和公证。

| 验收项目 | Windows 本机 | macOS M1 Pro | Linux |
| --- | --- | --- | --- |
| Release 构建、生成目标目录 | 已验证 | ARM64 自带运行时版本已运行；Intel 仅交叉构建 | 仅交叉构建 |
| 透明窗口、角色真实渲染、控件合成 | 自动检查及合成图检查通过 | 用户实机自动检查通过 | 待实机验证 |
| 位置恢复、动画暂停、释放及重复加载 | 自动检查通过 | 用户实机自动检查通过 | 待实机验证 |
| 全局快捷键 | 原生注册成功，实际按键待人工检查 | 尚未实现 | 尚未实现 |
| 托盘菜单、真实拖动、悬停唤出、焦点行为 | 待人工验收 | 待人工验收 | 待人工验收 |
| 透明区域鼠标穿透 | 未实现专门的平台命中处理 | 待设计与验证 | 待设计与验证 |
| 混合 DPI 多屏、长期稳定性和功耗 | 待验证 | 待验证 | 待验证 |

Linux 先以有图形桌面、合成器和托盘支持的 X11 环境为基线；Wayland 的绝对定位、置顶、越界隐藏不能假定与 Windows 一致。macOS 使用前向兼容 Core OpenGL 上下文。M1 Pro 实机报告确认窗口透明与小球隐藏恢复通过，随后发现社区适配层的 GLSL 120 着色器不被 Core 上下文支持。验证程序现通过独立 OpenTK 桥将该固定版本的着色器转换为 GLSL 330 Core（attribute/varying、texture2D、片元输出），不修改 Native Core 或生产 WPF 播放器。Windows 强制 Core 上下文 GPU 冒烟已通过，Mac 修复版已由用户在 M1 Pro 实机复测通过。Linux ARM64 不在本次 Native 库范围内。

Core 本次仅复用素材协议和贴边几何。虽然 Core 目标是 net9.0，CodexLocator 当前仍调用 where.exe；接入真实 Codex 前必须补齐 Unix 查找与启动验证，不能把当前 Core 视为已完整跨平台。

验证通过后已抽取 CodexBall.Rendering 共享渲染器，验证程序与正式 CodexBall.Desktop 使用同一个 Avalonia 播放器和原生库。OpenTkApi 通过链接复用。完整业务版已接入 Codex、窗口行为及平台服务，见 [跨平台业务版](cross-platform-desktop.md)。

### Core profile 回归

```powershell
dotnet run --project CodexBall.AvaloniaProbe -c Release -- --smoke --gl-core
```

`--gl-core` 可在 Windows/Linux 上强制启用与 Mac 相同的着色器适配路径。报告 `coreProfile` 标识实际选择。加载失败报告现在保留完整底层异常和初始化阶段，资源缺失也不会静默退出切换。强制 Core 检查不能替代 Apple 驱动上的实机检查。

### macOS ARM64 用户实机结果

用户回传报告：Darwin 24.6.0、Apple M1 Pro、OpenGL `4.1 Metal - 89.4`、缩放 200%、Core profile，`result: passed`。渲染 40 帧，透明像素 166185、非透明像素 83415，帧间画面变化正常，控件合成像素面积比约 1.010。隐藏暂停、恢复更新、角色位置恢复、切回小球释放及三次重新加载均通过。最后一帧模型渲染与回读耗时 1.3742 ms；单个样本不代表平均帧率或长期功耗。

Mac 可使用自带运行时版本，避免目标机器缺少 .NET 9：

```powershell
dotnet publish CodexBall.AvaloniaProbe -c Release -r osx-arm64 --self-contained true -o artifacts/avalonia-probe/osx-arm64-self-contained
```

在 Mac 对应目录运行 `./CodexBall.AvaloniaProbe --smoke`，不通过系统 `dotnet` 启动。此包尚未签名、公证；下载隔离导致的动态库拦截属于分发验收待办。仅在确认文件来自本次可信构建时，可用 `xattr -r -d com.apple.quarantine .` 移除当前验证目录的隔离标记，再执行 `chmod +x CodexBall.AvaloniaProbe`。正式发行应完成签名、公证。
