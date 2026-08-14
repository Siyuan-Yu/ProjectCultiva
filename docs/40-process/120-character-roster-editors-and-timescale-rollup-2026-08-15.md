# 120 · 收束：人物／工区编辑器、名册刷人、倍速、对话发任务可见性（2026-08-15）

> 状态：**已落地**｜日期：2026-08-15  
> 相对提交：`fa4f01d` 之后 → 本轮 `main`  
> 飞书：https://my.feishu.cn/docx/CP2OddgK4ofYFzxPfr5cSYC0nG3  
> 相关：[118 工区编辑器](118-npc-behavior-editor.md)｜[119 工区 vs 人物](119-npc-character-vs-role-template-editors.md)｜[117 对话／流速](117-npc-dialogue-host-ux-rollup-2026-08-14.md)｜[111 事件编辑器](111-content-studio-event-editor-usage.md)｜[SCHEMA](../../Content/BaseGame/Data/SCHEMA.md)

---

## 1. 一句话

去掉「职业身份」硬绑；工区／人物分工具编辑；Level Tester 用 **characterRoster** 刷人；Host **倍速统一驱动 Tick＋移动**；对话发任务的关联在 **事件**（`onTalk`＋`npcDefinitionId`＋`startQuest`），并补齐编辑器可见性。

---

## 2. 交付对照

| 主题 | 做什么 | 入口 |
|------|--------|------|
| **无职业 Job** | 清空 `job_woodcutter` 等；活动＝能力＋优先级＋偏好工区 | `ActivityResolver`、`WorkAreaAvailability`（暂恒可用） |
| **WorkAreaEditor** | 只编工区 `allowedActivities`／地点 | `启动-WorkAreaEditor.cmd` |
| **CharacterNpcEditor** | 人物属性／灵根 0–30／闲时倾向／可控制／场景出场／导出名册 | `启动-CharacterNpcEditor.cmd` |
| **characterRoster** | `Data/Rosters/level_tester_roster.json`；Host `characterRosterId` | SCHEMA／`PlayableHostBootstrap` |
| **保存修复** | 「保存场景出场」「导出名册」深拷贝 JSON，另存为默认当前路径，不退出 | CharacterNpcEditor |
| **倍速** | Tick 自动步进 × 倍率；移动用 `PresentationDeltaTime`；顶栏走 `SetSpeedMultiplier` | `PlayableHostBootstrap`／`HostMoveController`／`HostFormalHud` |
| **对话发任务可见** | 事件编辑器补 `npcDefinitionId`；人物页只读显示关联 onTalk 事件 | EventEditor／CharacterNpcEditor |

---

## 3. 数据分层（制作人）

```text
人物 Characters/*.json
  ├─ 数值／灵根／activityCapabilities／Priorities／preferredWorkAreaIds／playerControllable
  └─ 不声明「会发哪些任务」

工区 WorkAreas/*.json
  └─ allowedActivities → locationId

出场 Scenarios/*.json spawns[]
  └─ definitionId／entityKind／scheduleId／…

名册 Rosters/*.json（characterRoster）
  └─ entries[]：试玩刷谁（非 Unity 场景摆角色）

事件 Events/*.json（contentEvent）
  └─ onTalk + npcDefinitionId → 对话
      choices.outcomes.startQuest → 发任务
```

**主管发任务不是硬编码：** 右键对话 → `TryTalkToNpc(definitionId)` → 匹配 `trigger=onTalk` 且 `npcDefinitionId` 相同的事件 → 选项里 `startQuest`。

给其他人也能对话发任务：同一套路新建事件即可；运行时靠 `conditions`／flag／章节 dayBeat／代码 `TryPresentById` 动态出现。

---

## 4. 倍速约定（Host）

| 倍速 | 现实 1 秒 | 说明 |
|------|-----------|------|
| 1x | 1 tick＝5 游戏分 | 工作／休息／吃饭／作息靠 Tick |
| 5x | 5 tick＝25 游戏分 | 同上加快；移动／分离用 `unscaledDeltaTime × 倍率` |
| 暂停 | Tick 停、移动 dt＝0 | 对话／打断仍可交互 |

真·战斗结算 Host 尚未实现；追击走移动，已随倍速。

---

## 5. 工具怎么开

1. 首次或改完 WPF：`ExternalTools/ContentAuthoring/publish.ps1`（或 `发布-所有编辑器.cmd`）  
2. `启动-CharacterNpcEditor.cmd`／`启动-WorkAreaEditor.cmd`／`启动-EventEditor.cmd`  
3. 从 `Apps\<名>\*.exe` 打开，不要用 `.build` 中间产物

---

## 6. 手操验收

1. CharacterNpcEditor：保存人物；保存出场／导出名册弹出另存为且**不退出**  
2. 选主管：基本页显示关联 `base:event_ch01_ref_supervisor_talk` 等  
3. EventEditor：主管训话事件可见 `npcDefinitionId`  
4. Level Tester Play：名册刷人；取消暂停 → 5x → 时钟与走路明显加快；站桩采集 Tick 加快  
5. 右键主管对话 → 不语 → 仍可接到惩罚任务  

---

## 7. 非目标／后续

- 工区占用／资源耗尽真实规则  
- Host 战斗结算  
- 开局站位自动分散  
- 运行时全面消费闲时权重选事（倾向数据已有）
