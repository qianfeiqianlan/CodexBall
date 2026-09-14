# 64px Codex 状态球——产品与技术设计文档

> Working Name：CodexBall
> Version：0.1
> Platform：Windows
> Runtime：.NET 9 / WPF
> 当前开发环境：.NET SDK 9.0.315

---

# 1. 产品概述

CodexBall 是一个运行在 Windows 桌面的轻量级 Codex 用量状态工具。

应用以一个 **64×64 的悬浮状态球**常驻桌面，通过圆环和中心数字实时展示 Codex 当前主要额度窗口的剩余百分比。

用户无需打开 Codex Settings、CLI 或其他页面，即可快速了解：

* 当前 Codex 剩余额度；
* 短周期额度窗口；
* 长周期额度窗口；
* 距离下一次额度重置的时间；
* Codex 是否正常登录和连接；
* 后续可以扩展 Token Usage、每日趋势等信息。

产品第一阶段仅支持 Windows。

---

# 2. 产品目标

核心目标不是打造完整的 Codex Dashboard，而是解决一个非常明确的问题：

> 当我持续使用 Codex Coding 时，我希望随时知道还有多少额度，而不需要中断当前工作流去查看 Usage 页面。

CodexBall 应满足三个核心特点：

| 特点                 | 目标                                  |
| ------------------ | ----------------------------------- |
| Always Visible     | 用量信息始终可以一眼看到                        |
| Lightweight        | 长时间后台运行，占用尽可能少                      |
| Zero Configuration | 自动复用本机 Codex 登录状态，不要求用户再次输入 API Key |

第一阶段不建设账号系统、后端服务和云同步。

---

# 3. UI 产品形态

## 3.1 默认状态

状态球尺寸：

```text
64 × 64 DIP
```

WPF 中的 Width / Height 使用 DIP（Device Independent Pixel），在 Windows 100% DPI 下基本对应 64px；125%、150% 等缩放下会根据 DPI 自动缩放。

基础形态：

```text
       ╭──────╮
     ╱          ╲
    │     72     │
     ╲          ╱
       ╰──────╯
```

其中：

```text
圆环进度 = 剩余额度

72 = Remaining 72%
```

Codex 接口返回：

```text
usedPercent
```

客户端统一转换为：

```text
remainingPercent = 100 - usedPercent
```

例如：

```text
usedPercent = 28

remainingPercent = 72
```

---

# 4. 状态定义

建议定义四种 UI 状态：

| 状态          | 条件                     | 显示   |
| ----------- | ---------------------- | ---- |
| Healthy     | Remaining >= 50%       | 正常状态 |
| Warning     | 20% <= Remaining < 50% | 警告状态 |
| Critical    | Remaining < 20%        | 紧急状态 |
| Unavailable | 无法获取数据                 | `--` |

不要仅依靠颜色表达状态，中心数字仍然是主要信息。

断开 Codex 时显示：

```text
--
```

而不是错误地显示：

```text
0%
```

因为 0% 表示额度已耗尽，而 `--` 表示状态未知。

---

# 5. 用户交互

## 默认

状态球：

```text
64 × 64
TopMost
不显示任务栏图标
透明背景
无窗口边框
```

## Hover

显示简短 Tooltip：

```text
Codex

5h       72% remaining
Weekly   43% remaining

5h resets in 2h 18m
```

窗口名称不要在代码层硬编码成 `5h / Weekly`，应根据 `windowDurationMins` 格式化。

例如：

```text
300     → 5h
1440    → 24h
10080   → 7d
```

Codex 当前协议的额度窗口提供 `usedPercent`、`windowDurationMins` 和 `resetsAt`。

## 单击

展开 Usage Popup：

```text
┌─────────────────────────────┐
│ Codex                       │
│                             │
│ 5 hour                      │
│ ███████░░░      72% left    │
│ Reset in 2h 18m             │
│                             │
│ 7 day                       │
│ █████░░░░░      43% left    │
│ Reset Sep 18 14:35          │
│                             │
│ Last update 17:02           │
└─────────────────────────────┘
```

## 拖动

用户可拖动状态球。

需要区分：

```text
Mouse Down
    ↓
移动距离 < 4 DIP
    ↓
Click

Mouse Down
    ↓
移动距离 >= 4 DIP
    ↓
Drag
```

避免拖动时意外弹出详情窗口。

## 右键

MVP：

```text
Refresh
Always On Top ✓
Exit
```

后续增加：

```text
Start with Windows
Opacity
Settings
About
```

---

# 6. 技术选型

## 6.1 技术栈

```text
Language     C#
Runtime      .NET 9
Desktop      WPF
Serialization System.Text.Json
IPC          stdin / stdout
Protocol     Codex App Server JSON-RPC
Process      System.Diagnostics.Process
Storage      JSON File
```

MVP 不引入：

```text
SQLite
Entity Framework
ASP.NET Core
WebView
Electron
Node.js
```

.NET SDK 自带 `wpf` 项目模板，.NET 9 SDK 默认创建 .NET 9 WPF 项目。

---

# 7. 项目初始化

首先检查环境：

```powershell
dotnet --version
```

预期：

```text
9.0.315
```

查看 WPF 模板：

```powershell
dotnet new list wpf
```

创建仓库：

```powershell
mkdir CodexBall
cd CodexBall
```

创建 Solution：

```powershell
dotnet new sln -n CodexBall
```

建议拆成三个项目。

创建 WPF：

```powershell
dotnet new wpf -n CodexBall.App -f net9.0
```

创建核心库：

```powershell
dotnet new classlib -n CodexBall.Core -f net9.0
```

创建测试：

```powershell
dotnet new xunit -n CodexBall.Tests -f net9.0
```

加入 Solution：

```powershell
dotnet sln add .\CodexBall.App\CodexBall.App.csproj
dotnet sln add .\CodexBall.Core\CodexBall.Core.csproj
dotnet sln add .\CodexBall.Tests\CodexBall.Tests.csproj
```

添加依赖：

```powershell
dotnet add .\CodexBall.App\CodexBall.App.csproj reference .\CodexBall.Core\CodexBall.Core.csproj

dotnet add .\CodexBall.Tests\CodexBall.Tests.csproj reference .\CodexBall.Core\CodexBall.Core.csproj
```

Restore：

```powershell
dotnet restore
```

Build：

```powershell
dotnet build
```

启动：

```powershell
dotnet run --project .\CodexBall.App
```

WPF 项目的最终 TargetFramework 应类似：

```xml
<TargetFramework>net9.0-windows</TargetFramework>
```

---

# 8. 推荐目录结构

```text
CodexBall/
│
├── CodexBall.sln
│
├── README.md
├── TECHNICAL_DESIGN.md
├── AGENTS.md
│
├── CodexBall.App/
│   ├── App.xaml
│   ├── App.xaml.cs
│   │
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   │
│   ├── Views/
│   │   ├── StatusBall.xaml
│   │   └── UsagePopup.xaml
│   │
│   ├── ViewModels/
│   │   └── StatusBallViewModel.cs
│   │
│   └── Services/
│       ├── SettingsService.cs
│       └── WindowPositionService.cs
│
├── CodexBall.Core/
│   │
│   ├── Codex/
│   │   ├── CodexLocator.cs
│   │   ├── CodexProcess.cs
│   │   ├── CodexRpcClient.cs
│   │   ├── CodexUsageService.cs
│   │   └── CodexMessageRouter.cs
│   │
│   ├── Models/
│   │   ├── RateLimitSnapshot.cs
│   │   ├── RateLimitWindow.cs
│   │   ├── UsageSnapshot.cs
│   │   └── AccountUsage.cs
│   │
│   └── Utils/
│       ├── TimeFormatter.cs
│       └── RateLimitFormatter.cs
│
└── CodexBall.Tests/
    ├── CodexRpcClientTests.cs
    ├── RateLimitParserTests.cs
    └── RateLimitFormatterTests.cs
```

第一版不需要复杂的 Clean Architecture。

核心只需要保持：

```text
UI
 ↓
UsageService
 ↓
RpcClient
 ↓
Codex App Server
```

即可。

---

# 9. WPF Status Ball

主窗口基本配置：

```xml
<Window
    Width="64"
    Height="64"
    WindowStyle="None"
    AllowsTransparency="True"
    Background="Transparent"
    ResizeMode="NoResize"
    ShowInTaskbar="False"
    Topmost="True">
</Window>
```

内部组件：

```text
Grid
 ├── Background Circle
 ├── Usage Progress Ring
 └── Percentage Text
```

状态数据不要直接写在 Code Behind 中。

推荐：

```text
StatusBallViewModel
```

提供：

```text
RemainingPercent
UsedPercent
WindowLabel
ResetTime
ConnectionState
LastUpdatedAt
```

---

# 10. Codex 数据获取架构

这是整个项目最重要的部分。

不读取：

```text
ChatGPT Cookie
浏览器 Session
Codex Token 文件
私有 Web API
```

而是直接启动本机：

```powershell
codex app-server --stdio
```

架构：

```text
CodexBall.exe
      │
      │ spawn
      ▼
codex app-server --stdio
      │
      │ stdin
      ▼
JSON Request
      │
      │ stdout
      ▼
JSON Response / Notification
```

CodexBall 本身不持有 ChatGPT Token。

Codex App Server 负责复用现有 Codex 登录状态。

---

# 11. CodexProcess

使用：

```csharp
System.Diagnostics.Process
```

启动 App Server：

```text
CodexProcess
       │
       ├── StartAsync()
       ├── StopAsync()
       ├── RestartAsync()
       │
       ├── StandardInput
       ├── StandardOutput
       └── StandardError
```

Windows 上需要考虑用户的 Codex 安装方式不同。

首先尝试：

```powershell
where.exe codex
```

CodexLocator 返回真实路径。

如果找到：

```text
codex.exe
```

直接执行。

如果找到：

```text
codex.cmd
```

可以通过：

```text
cmd.exe /d /s /c
```

调用。

不要假定 Codex 一定安装在某个固定 npm 目录。

---

# 12. JSON-RPC Client

Codex App Server 的 stdout 是一个消息流。

不能假定：

```text
发送 request
下一行一定就是 response
```

因为已经实际可以看到类似：

```json
{
  "method": "remoteControl/status/changed",
  "params": {}
}
```

这种 Server Notification 插在 Response 中间。

因此需要：

```text
CodexRpcClient
```

内部维护：

```csharp
Dictionary<long, TaskCompletionSource<JsonElement>>
```

流程：

```text
SendAsync()
     │
     ├── 创建 Request ID
     │
     ├── 保存 TaskCompletionSource
     │
     └── 写 stdin
              ↓
         App Server
              ↓
         stdout reader
              ↓
       Deserialize JSON
          ┌────┴─────┐
          │          │
        有 id      有 method
          │          │
      Response    Notification
          │          │
      TCS.Set    Event Handler
```

---

# 13. App Server 初始化

CodexBall 启动后：

```text
Start Codex
    ↓
initialize
    ↓
initialized
    ↓
account/read
    ↓
account/rateLimits/read
```

Initialize：

```json
{
  "method": "initialize",
  "id": 1,
  "params": {
    "clientInfo": {
      "name": "codex-ball",
      "title": "Codex Ball",
      "version": "0.1.0"
    },
    "capabilities": {
      "experimentalApi": true
    }
  }
}
```

随后：

```json
{
  "method": "initialized"
}
```

这里 `initialized` 是 Notification，因此没有 `id`。

---

# 14. 检查 Codex 登录状态

调用：

```json
{
  "method": "account/read",
  "id": 2,
  "params": {
    "refreshToken": false
  }
}
```

如果：

```text
account != null
```

则进入正常状态。

否则：

```text
Codex Not Logged In
```

状态球显示：

```text
--
```

Popup：

```text
Codex is not logged in.

Open Codex and sign in first.
```

CodexBall MVP 不自己实现 OpenAI Login。

---

# 15. 获取 Codex Rate Limit

核心请求：

```json
{
  "method": "account/rateLimits/read",
  "id": 3
}
```

当前 Codex 协议会返回一个兼容的 `rateLimits`，并可能同时返回按照 `limit_id` 分类的 `rateLimitsByLimitId`。

重要结构：

```text
rateLimits

 ├── limitId
 ├── limitName
 ├── planType
 │
 ├── primary
 │     ├── usedPercent
 │     ├── windowDurationMins
 │     └── resetsAt
 │
 └── secondary
       ├── usedPercent
       ├── windowDurationMins
       └── resetsAt
```

例如：

```json
{
  "rateLimits": {
    "limitId": "codex",
    "primary": {
      "usedPercent": 28,
      "windowDurationMins": 300,
      "resetsAt": 1789372800
    },
    "secondary": {
      "usedPercent": 57,
      "windowDurationMins": 10080,
      "resetsAt": 1789790000
    }
  }
}
```

Codex 官方协议将 `resetsAt` 定义为 Unix 时间戳，窗口长度由 `windowDurationMins` 提供。

---

# 16. Rate Limit 选择算法

不要：

```text
primary = 5 Hour

secondary = Weekly
```

硬编码。

正确方式：

```text
rateLimitsByLimitId["codex"]
```

优先。

不存在时：

```text
rateLimits
```

作为 fallback。

然后读取：

```text
primary
secondary
```

根据：

```text
windowDurationMins
```

排序。

最短窗口：

```text
Short Window
```

最长窗口：

```text
Long Window
```

例如：

```text
300 mins
 ↓
5h

10080 mins
 ↓
7d
```

Codex 当前实现本身也会处理 `codex` 和额外 `codex_other` 等多个 limit bucket，因此客户端不要假定将来永远只有一个 bucket。

---

# 17. 状态球应该显示哪个额度

V0.1 中心数字：

```text
Short Window Remaining
```

例如：

```text
5h Remaining = 72%
```

理由是短周期额度更容易在一次高强度 Coding Session 内耗尽。

计算：

```csharp
remaining = Math.Clamp(
    100 - usedPercent,
    0,
    100
);
```

Popup 同时展示：

```text
Short Window
Long Window
```

未来可以支持：

```text
Single Ring
Dual Ring
Auto
```

---

# 18. Rate Limit 实时更新

Codex App Server 存在：

```text
account/rateLimits/updated
```

Notification。

它属于 sparse update，即通知中的字段可能并不是完整 snapshot。Codex 协议明确建议客户端把更新合并到现有 snapshot，或者重新执行一次 `account/rateLimits/read`。

MVP 建议选择更简单、安全的实现：

```text
account/rateLimits/updated
            ↓
       debounce 500ms
            ↓
account/rateLimits/read
            ↓
       更新 UI
```

不要一开始实现复杂 Merge Logic。

---

# 19. Refresh 策略

采用：

```text
Startup
   ↓
立即 Fetch

Notification
   ↓
立即 Fetch

Fallback Poll
   ↓
每 5 分钟 Fetch

Right Click → Refresh
   ↓
立即 Fetch
```

这样主要依靠事件更新，同时避免 App Server Notification 异常时状态永久过期。

Codex 自己当前的状态实现将超过 15 分钟的 snapshot 视为 stale，因此 5 分钟 fallback 是一个较保守的客户端策略。

---

# 20. Token Usage

Rate Limit 和 Token Usage 是两个概念。

Rate Limit：

```text
还能用多少 Codex
```

Token Usage：

```text
使用了多少 Token
```

后续可调用：

```json
{
  "method": "account/usage/read",
  "id": 4
}
```

当前协议返回：

```text
summary
dailyUsageBuckets
threadUsage
```

其中 `summary` 当前包含：

```text
lifetimeTokens
peakDailyTokens
longestRunningTurnSec
currentStreakDays
longestStreakDays
```

每日 bucket：

```json
{
  "startDate": "2026-09-14",
  "tokens": 18342133
}
```

这是当前 Codex 公共协议中定义的数据结构。

V0.1 状态球不依赖 Token Usage。

V0.2 Popup 可以增加：

```text
Today
18.3M tokens

Lifetime
1.28B tokens
```

---

# 21. UsageService

建议业务层提供统一对象：

```text
CodexUsageSnapshot

AccountState

ShortWindow
 ├── UsedPercent
 ├── RemainingPercent
 ├── WindowMinutes
 └── ResetsAt

LongWindow
 ├── UsedPercent
 ├── RemainingPercent
 ├── WindowMinutes
 └── ResetsAt

LastUpdatedAt
```

UI 不直接理解 App Server JSON。

这样将来 Codex Protocol 变化，只需要修改：

```text
CodexUsageService
```

而不需要修改：

```text
StatusBall
UsagePopup
```

---

# 22. Reset Time

`resetsAt`：

```text
Unix timestamp seconds
```

转换：

```csharp
DateTimeOffset
    .FromUnixTimeSeconds(resetsAt)
    .ToLocalTime();
```

UI 同时支持两种显示。

距离较近：

```text
Reset in 2h 18m
```

较远：

```text
Reset Sep 18, 14:32
```

系统时间变化或者睡眠恢复以后，需要重新计算倒计时。

不要每秒重新请求服务器。

只需要：

```text
服务器提供绝对 resetsAt
         ↓
本地 Timer 计算倒计时
```

---

# 23. 本地配置

保存到：

```text
%LOCALAPPDATA%\CodexBall\
```

例如：

```text
C:\Users\A\AppData\Local\CodexBall\
```

结构：

```text
CodexBall/
├── settings.json
└── logs/
    └── app.log
```

settings.json：

```json
{
  "left": 1720,
  "top": 920,
  "alwaysOnTop": true,
  "opacity": 1.0
}
```

MVP 不需要数据库。

---

# 24. 日志设计

记录：

```text
Application started
Codex located
Codex process started
RPC initialized
Rate limit fetched
Codex process exited
RPC timeout
JSON parse error
```

不要记录：

```text
Access Token
Authorization Header
Cookie
Codex credential
完整账户敏感数据
```

默认日志级别：

```text
Information
```

开发环境可以：

```text
Debug
```

---

# 25. 异常处理

必须考虑：

| 场景                           | 行为                     |
| ---------------------------- | ---------------------- |
| 未安装 Codex                    | `--` + Codex not found |
| Codex 未登录                    | `--` + Please sign in  |
| app-server 启动失败              | `--` + Retry           |
| app-server Crash             | 自动重新启动                 |
| JSON 无法解析                    | 保留最后 snapshot          |
| API 超时                       | 保留最后 snapshot，标记 stale |
| 无 primary                    | 尝试 secondary           |
| primary / secondary 均无       | `--`                   |
| Codex 协议增加字段                 | 忽略未知字段                 |
| Codex Notification 混入 stdout | MessageRouter 分流       |

重启策略：

```text
1s
2s
5s
10s
30s
```

最大保持：

```text
30s
```

成功连接后重置 Backoff。

---

# 26. 单实例运行

CodexBall 不应该启动多个：

```text
CodexBall.exe
CodexBall.exe
CodexBall.exe
```

否则每个都会启动：

```text
codex app-server
```

建议使用：

```csharp
Mutex
```

例如：

```text
Global\CodexBall
```

检测到实例存在：

```text
退出新实例
```

后续可以进一步实现：

```text
启动第二实例
    ↓
唤醒第一个实例
```

但不属于 MVP。

---

# 27. MVP 范围

## V0.1 必须完成

### 桌面

```text
64 × 64 Status Ball
Transparent Window
Always On Top
No Taskbar
Drag Position
Persist Position
```

### Codex

```text
自动寻找 codex
启动 app-server --stdio
initialize
account/read
account/rateLimits/read
account/rateLimits/updated
```

### Usage

显示：

```text
Short Window Remaining %
Long Window Remaining %
Reset Time
Last Updated
```

### Interaction

```text
Hover Tooltip
Click Popup
Right Click Refresh
Right Click Exit
```

### Reliability

```text
Codex Not Found
Not Logged In
RPC Timeout
Codex Process Exit
Auto Reconnect
```

这就是第一版完整 MVP。

---

# 28. 明确不属于 MVP 的功能

V0.1 不做：

```text
Token History Chart

Claude Code
Gemini CLI
Cursor

云端账号
CodexBall Login
服务端数据库
用户注册
云同步

Microsoft Store
自动更新

多语言
主题商城
复杂动画

API Cost
项目维度 Token
Session 分析
Agent 分析
```

MVP 的成功标准不是功能数量。

而是：

> 用户愿不愿意让这个 64px 球每天常驻桌面。

---

# 29. V0.2 —— Usage

第二阶段增加：

```text
account/usage/read
```

Popup：

```text
Codex
────────────────

5h
72% remaining

Weekly
43% remaining

────────────────

Today
18.3M tokens

Lifetime
1.28B tokens

Current streak
7 days
```

加入简单 7-Day Usage：

```text
▂ ▃ ▅ █ ▆ ▃ ▄
M T W T F S S
```

---

# 30. V0.3 —— Status Ball UX

增强：

```text
Double Ring
```

例如：

```text
内环 = Short Window
外环 = Long Window
中心 = Short Remaining
```

增加：

```text
Opacity
Lock Position
Click Through
Snap To Screen Edge
Always On Top
Auto Hide
```

Click Through 对持续放在编辑器上方会非常有价值。

---

# 31. V0.4 —— Windows Integration

增加：

```text
System Tray
Start With Windows
Notification
Global Shortcut
```

例如：

```text
Remaining < 20%

CodexBall Notification
Codex 5h usage is at 82%.
Reset in 1h 12m.
```

---

# 32. V0.5 —— Codex Analytics

开始读取：

```text
Account Usage
Thread Usage
Session Usage
```

产品从：

```text
Quota Monitor
```

逐步升级为：

```text
Codex Observability
```

可以增加：

```text
Today tokens
Session tokens
Peak day
Coding streak
Burn rate
Average consumption
Estimated exhaustion
```

例如：

```text
5h remaining     34%
Current burn     8.2% / hour

Estimated exhaustion
~ 4h 12m
```

---

# 33. V1 —— AI Coding Monitor

如果 CodexBall 本身验证成功，可以逐渐抽象为：

```text
AI Coding Usage Monitor
```

支持：

```text
Codex
Claude Code
Gemini CLI
Cursor
GitHub Copilot
```

状态球可以切换：

```text
Codex
Claude
All
```

Popup：

```text
AI Coding
────────────────────

Codex
5h       72%
Weekly   43%

Claude
5h       81%
Weekly   56%

Gemini
Daily    64%
```

这时产品价值会从：

```text
Codex Feature
```

升级为：

```text
AI Developer Desktop Infrastructure
```

---

# 34. 测试策略

Core 层应该优先测试。

重点测试：

```text
JSON Response Parsing

Notification / Response 混排

remainingPercent 计算

0 / 100 边界

primary null

secondary null

rateLimitsByLimitId["codex"]

多个 Limit Bucket

Unix Timestamp 转换

RPC Timeout

App Server Exit

Invalid JSON

Unknown JSON Fields
```

例如：

```text
usedPercent = 28

Expected:
remainingPercent = 72
```

边界：

```text
usedPercent = -1  → 100
usedPercent = 120 → 0
```

UI 不需要一开始做大量自动化测试。

业务 Parser / RPC Client 更值得测试。

---

# 35. Build

开发：

```powershell
dotnet build
```

测试：

```powershell
dotnet test
```

运行：

```powershell
dotnet run --project .\CodexBall.App
```

Release：

```powershell
dotnet build -c Release
```

---

# 36. Windows 发布

自己开发机器使用，可以先：

```powershell
dotnet publish .\CodexBall.App\CodexBall.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false
```

如果发给普通用户，希望对方无需安装 .NET Runtime：

```powershell
dotnet publish .\CodexBall.App\CodexBall.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true
```

微软也将 self-contained `dotnet publish` 作为直接下载分发 Windows 桌面应用的一种方式。

如果希望 Single EXE：

```powershell
dotnet publish .\CodexBall.App\CodexBall.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

`.NET` 官方文档说明 `PublishSingleFile` 可以创建单文件发布；如果需要把原生 Runtime Binary 一并包含，需要 `IncludeNativeLibrariesForSelfExtract=true`。

MVP 暂时不要开启：

```text
PublishTrimmed
```

优先保证 WPF/XAML 运行稳定。

---

# 37. 推荐开发顺序

Phase 1：

```text
WPF 64px Ball
```

先完全不用 Codex。

做到：

```text
透明
圆形
可拖动
TopMost
显示 72
```

Phase 2：

```text
CodexRpcClient
```

Console / Unit Test 先验证：

```text
initialize
account/read
account/rateLimits/read
```

Phase 3：

```text
UsageService
```

实现：

```text
JSON
 ↓
RateLimitSnapshot
 ↓
remainingPercent
```

Phase 4：

```text
StatusBall
    ↑
ViewModel
    ↑
UsageService
```

把真实额度显示到球上。

Phase 5：

```text
Click Popup
Hover
Refresh
Exit
```

Phase 6：

```text
Notification
Reconnect
Persist Position
Single Instance
```

到这里发布：

```text
v0.1.0
```

---

# 38. MVP 技术架构最终形态

```text
                    CodexBall.exe
                         │
             ┌───────────┴───────────┐
             │                       │
          WPF UI                  Core
             │                       │
     StatusBallViewModel       UsageService
             │                       │
             │                  RpcClient
             │                       │
             │                 MessageRouter
             │                       │
             │                  CodexProcess
             │                       │
             └───────────────────────┤
                                     │
                                     ▼
                        codex app-server --stdio
                                     │
                    ┌────────────────┼───────────────┐
                    │                │               │
              account/read   rateLimits/read   usage/read
                                     │
                                     ▼
                         rateLimits/updated
```

依赖方向保持：

```text
WPF UI
   ↓
Application Service
   ↓
Codex Protocol
```

而不是：

```text
MainWindow.xaml.cs
   ↓
到处 Process.Start
   ↓
到处解析 JSON
```

这是整个代码结构里最重要的约束。

---

# 39. MVP 完成标准

达到以下状态即可发布第一版：

```text
启动 CodexBall.exe
        ↓
桌面出现 64px 球
        ↓
自动发现 Codex
        ↓
启动 App Server
        ↓
读取当前登录用户
        ↓
读取 Rate Limit
        ↓
球显示 72
        ↓
额度变化
        ↓
自动变成 68
        ↓
点击
        ↓
看到两个额度窗口和 Reset Time
```

整个过程：

```text
不输入 API Key
不配置 URL
不配置账号
不启动后端
```

用户需要满足的唯一前置条件是：

> 本机已经安装 Codex，并且 Codex 已经正常登录。

---

# 40. 产品验证指标

发布 MVP 后，不要首先关心下载量。

真正应该验证：

```text
Installed
    ↓
Next-day still running
    ↓
7-day still running
    ↓
User keeps it on desktop
```

核心指标建议：

```text
D1 Running Retention
D7 Running Retention

Daily App Launch
Daily Active Duration

Popup Opens / Day
Manual Refresh / Day
```

如果用户安装以后：

```text
一直把球留在桌面
```

说明产品价值成立。

如果大家：

```text
安装
看一下
关闭
卸载
```

说明 Codex Usage 更适合作为其他开发工具中的一个 Feature，而不是独立产品。

---

# 41. 第一阶段技术原则

整个 V0.1 坚持：

```text
Local First
Windows First
Codex First
No Backend
No Account
No Database
No API Key
No Overengineering
```

第一版真正需要写好的只有三个东西：

```text
64px Status Ball

CodexRpcClient

CodexUsageService
```

其它功能全部围绕这三个核心能力展开。
