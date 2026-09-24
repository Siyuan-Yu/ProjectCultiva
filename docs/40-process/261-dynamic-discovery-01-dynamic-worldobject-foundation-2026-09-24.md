# DYNAMIC-DISCOVERY-01 — Dynamic WorldObject + Generic Discovery Foundation V1

> 状态：**Producer Accepted / Sealed**
> 日期：2026-09-24
> 范围：WorldOpportunity 动态物体、通用发现、onInspect、显式解决、Snapshot v9、制作工具与稳定验收内容

## 已实现契约

- `WorldOpportunity.spawnKind` 为 `npc | worldObject`，旧定义缺省 `npc`。NPC 继续使用 SpawnTable、真实 Entity 与 WorldPresence；动态物体直接保存精确 Surface 坐标，不伪造 Character。
- 动态物体实例使用 `worldObject:<OpportunityInstanceId>` 稳定身份。Active Board 同时维护 Entity 与 WorldObject 两套互斥索引；计数仍按 Opportunity Instance。
- 发现模式固定为 `worldVisible`、`publicNotice`、`hiddenUntilDiscovered`。旧 `hidden` 明确报错并提示新名称；V1 不允许隐藏 NPC。
- hidden 实例从生成起已存在于 Core。发现前无 View、Picker、Activity、Toast、定位或 onInspect；任一存活 PlayerParty 成员的领域位置进入 `discoveryRadiusWorld` 后永久发现，发布 `WorldOpportunityDiscovered`，创建 Activity 并播放一次发现 Toast。
- `HostDynamicOpportunityObjectPresenter` 只把当前 Surface、loaded chunk、且对玩家可见的动态物体物化。View 卸载不改变 Domain；所有 Collider 被禁用或移除，动态物体不进入导航阻挡。独立 `HostDynamicWorldObjectRegistry` 提供 transient bounds、稳定拾取与 approach point，不进入 Plot／Destructible 工作语义。
- `onInspect + worldObjectKind=opportunityObject + worldOpportunityId=<template>` 按运行实例所属模板匹配。`ContentInteractionContext.TargetDefinitionId` 保存模板 ID，`TargetKey` 保存稳定对象实例键，`TargetEntityId=None`。接近后仍按稳定身份重新解析与复核。
- Inspect 本身不结束 Opportunity。只有 `resolveCurrentOpportunity` 或到期进入 terminal；显式解决移出 Active Board、把已存在 Activity 转 History、发布 `WorldOpportunityResolved`。Opportunity Board 与 Activity Board 已加入 Outcome transaction memento，后续 Outcome 失败会回滚。
- 未发现 hidden 到期不生成玩家从未见过的 History；已发现 hidden 与 publicNotice 结束时转 History；worldVisible 没有 Activity 时不强行补 History。History 统一显示“已结束”。
- Snapshot 升到 v9，保存 `SpawnKind`、`WorldObjectInstanceId`、精确 X/Y、DiscoveryMode、IsDiscovered。v8 开发存档不猜测迁移；Load 恢复已发现状态与 Activity，但不重放发现 Toast。
- `Weight=0` 表示仅显式生成，Director 仍跳过；LevelTester 可稳定生成三个验收对象，不改变正式 density。

## 制作工具

- OpportunityEditor 可选择“随机人物／动态物体”，动态物体使用已登记的 MapKind prop 选择器，并编辑名称、宽高、三种发现方式及相应公告／发现字段。权重允许 0。
- EventEditor 的 onInspect 目标增加“动态机会物体”，只列 `spawnKind=worldObject` 的 Opportunity 模板；保存 `worldObjectKind=opportunityObject`、模板 `worldOpportunityId`，不写运行实例 ID。
- Outcome 编辑器增加“解决当前世界机会”，不显示无意义参数。

## 稳定验收内容

LevelTester → 内容页提供：

1. **生成 Hidden 石碑**：生成后普通 UI 不泄露；走入约 1.25 world units 后出现、Toast 与 Activity 同时建立。首次“查看碑文”后物体保留；再次调查选择“取走残片，结束调查”后消失并进入 History。
2. **生成 PublicNotice 包裹**：生成即有 Activity，定位只移动镜头；走近调查可显式结束。
3. **生成 WorldVisible 灵草**：loaded chunk 内直接可见，无 Toast、无 Activity，可调查。
4. **清理 DYNAMIC-DISCOVERY-01 验收物体**：显式结束本组仍 Active 的对象。

每次生成状态会报告 OpportunityInstanceId、WorldObjectInstanceId、Surface、精确 X/Y、DiscoveryMode 与 IsDiscovered。

### MAP-COORD-01 验收补充（2026-09-25）

生成 Hidden 石碑后不增加 Debug Locate。记录 LevelTester 输出的 Surface 与 WorldPosition，打开正式 WorldMap，使用右下角鼠标坐标、玩家坐标及地图边缘 major ticks 判断该坐标所在方向；确认地图上没有石碑 marker。关闭 WorldMap 后由玩家实际走向该区域，只有进入 `discoveryRadiusWorld` 后才出现石碑、Toast 与 Activity。缩放或平移地图后，把鼠标重新放到同一 Site／玩家 marker 上，坐标应保持一致到 UI 一位小数。

### Chunk 与存读档

发现石碑后不解决，走远使其离开 loaded chunk，再返回：同一对象重新物化，不重新发现、不重复 Toast。Save → Load 后应保持同一 ObjectInstanceId、同一坐标、发现状态与 Activity，且仍可 onInspect。

### NPC 回归

再生成两名临时行商，或等待受伤散修：确认 NPC Opportunity、publicNotice 定位、onTalk、人物委托与 expiry 仍工作。

## 边界

本轮没有实现 Knowledge Known/Suspected/Unknown、传闻传播、延迟事件、永久建筑转换、WorldSite promotion、动态导航障碍、容器库存、交易、制作、NPC AI 或程序化物体生成。`WorldOpportunityDiscovered` 仅作为未来知识系统接入点。

## 实施验证

- `tools/offline-compile.ps1`：`ALL_OK`；仅保留两个既有测试源码 warning。
- DYNAMIC-DISCOVERY-01／Host 定向 headless：13/13；覆盖三种 visibility/activity 语义、显式 Weight 0 生成、发现与终态、NPC expiry／roundtrip、v9 已发现与未发现往返、v8 明确拒绝、事务回滚、内容引用和稳定互动上下文。
- MAP-COORD-01 投影定向验证覆盖 world→map→world round-trip、zoom、pan、地图外失效与 1/2/5 major interval。
- EVENT-01／QUEST-INSTANCE-01／SAVE-01 回归：25/25。
- BaseGame Content load＋`ContentReferenceValidator`：通过；acceptance JSON 解析通过。
- EventEditor／OpportunityEditor Release build：均 0 warning、0 error；`git diff --check`：通过。

实施轮未启动 Unity，提交前工作区保持未暂存；最终人工验收结论与封板状态见下节。

## Producer Acceptance / Final Seal

制作人于 2026-09-24 完成人工验收：Hidden 石碑在未发现前不显示，进入发现半径后出现且 `IsDiscovered=true`；onInspect 正常且不会自动 Resolve；选择显式解决后对象消失并正确更新 Activity。Dynamic Discovery 主链通过，DYNAMIC-DISCOVERY-01 状态更新为 **Producer Accepted / Sealed**。配套 WorldMap 坐标验收见 [262](262-map-coord-01-worldmap-world-coordinate-readout-2026-09-25.md)。
