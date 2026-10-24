# 本地下载模型接入记录

2026-10-24，来源为用户放在 `tmp/` 内的官方免费示例 ZIP。只复制导出的运行素材与原始 ReadMe，不修改模型设计，不将 PSD、编辑工程和背景图片打包进桌宠。

| 压缩包 | 接入角色 | 外观 ID | 处理结果 |
| --- | --- | --- | --- |
| `koharu_haruto_ja.zip` | 小春 Koharu | `local-koharu` | 可用，240 × 260 DIP，点击播放 `Tap` 动作 |
| 同上 | 春翔 Haruto | `local-haruto` | 可用，240 × 260 DIP，点击播放 `Tap` 动作 |
| `mao_zh-Hans.zip` | 虹色 Mao | `local-mao_pro` | 可用，240 × 320 DIP，点击映射到原始空名称动作组 |
| `mark_zh-Hans.zip` | 马克君 Mark | `local-mark_free_t04` | 可用，240 × 320 DIP，点击播放 `Tap` 动作 |
| `param_ctrl_zh-Hans.zip` | 参数控制器示例 | — | 只有 `.cmo3`、`.can3` 等编辑工程，没有 `.moc3` / `.model3.json`，暂不加载 |

## 体验与恢复

```powershell
pwsh -NoProfile -File .\scripts\Import-Live2DModels.ps1
dotnet run --project .\CodexBall.App
```

导入脚本要求 PowerShell 7。运行后从托盘“外观”菜单选择四个角色。指针在窗口内移动时视线跟随，点击头部触发动作，拖动有摇摆和回弹，下方细进度条显示剩余额度，点击可查看详情。

导入的运行素材位于 `CodexBall.App/Skins/local-*/model`，原始说明为同目录的 `Source-ReadMe.txt`。这些文件从 Git 排除，外观配置、脚本和兼容代码纳入项目；重新克隆后需保留原始 ZIP 并重新执行导入脚本。没有本地素材的环境会忽略相应外观，仍可使用 CodexBall 小球；Haru 已移除。

## 兼容与验证

旧版小春／春翔使用 `PARAM_ANGLE_X`、`PARAM_EYE_L_OPEN` 等参数名，播放器现在同时识别该命名与现代 `ParamAngleX`、`ParamEyeLOpen`，并重新绑定呼吸参数。

已用实际 Cubism Core 与 OpenGL 渲染四个角色，检查非空画面、动作和视线输入后的帧变化，输出预览至 `artifacts/import-models/previews`。主窗口验证了 CodexBall 与角色切换、全外观循环及回绕、设置保存和无角色右键菜单。

素材仍遵守随包 ReadMe、Live2D 无偿素材条款与角色个别条件；这次是本地体验接入，未发布安装包。

## 编辑工程与运行模型

`.cmo3` 是 Cubism 模型编辑工程，`.can3` 是动画编辑工程，不能直接交给运行播放器。请在 Cubism Editor 导出运行模型，提供包含 `.model3.json`、`.moc3`、贴图和被引用的动作/表情/物理等文件的完整目录，保留相对路径。仅有编辑工程时不能直接集成；当前四个角色来自 ZIP 内已有的运行导出素材。

目前只有开发者脚本可准备这些经过筛选的资源，应用没有用户导入、重载或打开外观目录入口。
