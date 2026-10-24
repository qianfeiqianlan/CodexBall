# 桌宠外观包 v1

> 当前产品仅通过托盘“外观”菜单提供 CodexBall、小春、春翔、Mao 和马克君。已移除 Haru 和桌面右键菜单，不开放用户外观目录、重新加载及文件夹入口。以下格式和安装说明保留为开发资料，不代表当前用户功能。

> 当前默认播放器已升级为 Live2D，参见 [技术验证记录](live2d-technical-preview.md)。下述 PNG 格式作为兼容格式继续可用。

## Live2D 外观

在基础配置中提供 `live2DModel`（包内 `.model3.json` 相对路径）即可使用 Live2D 播放器，此时不要求 PNG `actions.Idle`。通过 `motionGroups` 将语义动作映射到模型的动作组；缺失映射回到 `Idle`。`Drag`、`Drop` 使用程序化摇摆和回弹。

```json
{
  "schemaVersion": 1,
  "id": "my-live2d",
  "name": "我的 Live2D 角色",
  "width": 240,
  "height": 320,
  "head": { "x": 55, "y": 10, "width": 130, "height": 115 },
  "orb": { "x": 196, "y": 240, "width": 38, "height": 38 },
  "live2DModel": "model/character.model3.json",
  "motionGroups": { "Idle": "Idle", "Pat": "TapBody", "QuotaRecovered": "TapBody" }
}
```

将完整导出的模型资源放在 `model/` 内；文件引用必须留在模型目录中。已验证资源存在性、类型、大小与路径范围；原生加载仅应用于可信来源模型。当前适配层匹配 Native 5 R4.1，不保证支持最新 Cubism 导出特性。单个引用资源最多 32 MB、最多 512 个资源、合计 128 MB，模型配置最多 512 KB。

当前固定目录/ID 为 `codexball`（无模型目录）、`local-koharu`、`local-haruto`、`local-mao_pro`、`local-mark_free_t04`。目录必须位于应用的 `Skins/` 下且通过校验才进入菜单；Haru 已移除，不再从旧 PNG 迁移到 Haru。

## 当前使用和开发者接入

运行前按 [本地模型接入记录](imported-live2d-models.md) 准备 SDK 和模型。从系统托盘“外观”选择角色，或按 Alt+C 循环；选择 CodexBall 回到 64 × 64 DIP 小球。角色下方为细用量条，点击显示详情，旧 `orb` 配置不再决定当前桌宠用量控件的位置。

桌宠在 Codex 关闭时仍可陪伴；小球沿用 Codex 活动显示规则。设置保存在 `%LOCALAPPDATA%\CodexBall\settings.json`。语言入口也位于托盘，可选跟随系统、中文和 English。

`cream-pink` PNG 三姿态包与 `PngCompanionPlayer` 保留为兼容和测试资料，不出现在当前外观菜单。开发者添加新包需要准备可信且完整的运行素材、manifest，并显式更新 `SkinCatalog` 的固定目录列表后构建。不会扫描 `%LOCALAPPDATA%\CodexBall\skins`，没有重载或文件夹入口。

一套外观对应一套完整角色/服装素材，目前不支持衣服、发型与饰品自由分层组合。下文 PNG 示例描述播放器格式，不代表用户可直接安装外观。

## 最小配置示例

以下包只需一张透明 `idle.png`；所有缺失动作自动回退到待机。

```json
{
  "schemaVersion": 1,
  "id": "my-first-outfit",
  "name": "我的第一套外观",
  "author": "素材作者",
  "license": "填写实际素材授权",
  "width": 144,
  "height": 180,
  "head": { "x": 20, "y": 5, "width": 105, "height": 72 },
  "orb": { "x": 54, "y": 86, "width": 34, "height": 34 },
  "actions": {
    "Idle": {
      "loop": true,
      "frames": [{ "file": "idle.png", "durationMs": 1000 }]
    }
  }
}
```

`width`、`height` 和交互区域使用 DIP，不是素材像素；每边允许 64–512 DIP。图片按比例居中放入画布，建议素材纵横比与画布相同。透明部分不会绘制不透明背景。

`usageBarOffsetY` 可选，默认 0，使用 DIP 调整角色下方用量条的垂直位置；负值向上移动，范围为负画布高度至 0。用于抵消模型脚下的透明留白，位置固定，不随动画抖动。Mark 当前为 -40 DIP。

`head` 是摸头区域，`orb` 为早期能量球位置的兼容字段；当前桌宠 UI 使用角色下方用量条。角色其余可见部分可以拖动。动作级 `orb` 仍可被播放器读取，当前窗口不会据此绘制额外能量球。

## 动作与帧

动作名称区分大小写：`Idle`、`Pat`、`Drag`、`Drop`、`Sleep`、`QuotaLow`、`QuotaRecovered`。仅 `Idle` 必需；未知名称忽略。建议 `Idle`、`Drag`、`Sleep` 使用 `loop: true`，其余为单次动作；单次结束后回待机。

每帧用 `file` 指定包内 PNG 相对路径，`durationMs` 为 40–60000 ms，默认 150 ms。多帧按顺序播放。可选 `crop` 用整数像素指定图集区域：

```json
{
  "loop": false,
  "imageBounds": { "x": 10, "y": 0, "width": 124, "height": 180 },
  "orb": { "x": 108, "y": 117, "width": 34, "height": 34 },
  "frames": [
    { "file": "pat.png", "durationMs": 150, "crop": { "x": 0, "y": 0, "width": 300, "height": 450 } },
    { "file": "pat.png", "durationMs": 600, "crop": { "x": 300, "y": 0, "width": 300, "height": 450 } }
  ]
}
```

`imageBounds` 可选，使用 DIP 指定当前动作图片在画布中的展示区域。用统一透明画布、落地基线及此定位区域对齐动作，避免姿态切换时人物跳动。未指定时使用整个画布。

同一动作可以通过 `variants` 提供多个帧序列，每次触发随机选一个。`frames` 如存在，也参与随机选择：

```json
{
  "variants": [
    [{ "file": "pat-happy.png", "durationMs": 1200 }],
    [{ "file": "pat-shy.png", "durationMs": 1200 }]
  ]
}
```

不能提供空变体。`ICompanionPlayer` 定义播放、停止、暂停、恢复、帧更新与完成通知；当前实现包括 `PngCompanionPlayer` 与 `Live2DCompanionPlayer`。

拖动优先于其他动作；额度事件不打断摸头和拖动，也不排队。低额度在剩余低于 20% 时触发，恢复到 25% 以上后才重新允许低额度提示，避免阈值附近反复触发。额度恢复要求同一时长窗口的原重置时间已到、新重置时间向后推进且剩余额度增加；首次读取与断连后首次恢复数据不庆祝。应用内三分钟无互动请求 `Sleep`，不监控全局输入。

## 校验与资源限制

- 配置最多 128 KB；版本必须为 1。
- 已知动作总帧数（包含变体）最多 256。
- 单张 PNG 最多 16 MB，全部引用图片合计最多 64 MB。
- 单边最多 4096 像素，全部不同图片合计最多 1600 万像素。
- 不允许包外路径、远程地址、符号链接或目录联接；不执行脚本。
- 图片解码、裁切和配置全部验证通过后才替换当前外观。选择无效包保留当前外观；启动时所选包失效则回退 CodexBall 小球。

静态循环单帧不启动动画刷新计时器。隐藏或小球模式暂停播放器；换装后释放旧播放器和帧引用。

## 原始概念素材记录

素材生成使用内置 imagegen。提示词核心为：原创软萌治愈二次元 Q 版小人、两至三头身、奶油白短发与浅粉发梢、宽松奶油粉外套、透明背景、三个一致角色姿态（抱薄荷绿能量球待机、闭眼摸头反应、后领提起悬空），干净二维赛璐璐绘制，无文字和水印。当前用于原型验证，尚不是完整动画素材集。
