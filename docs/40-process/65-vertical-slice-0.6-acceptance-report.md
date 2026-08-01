# Vertical Slice 0.6 验收报告 — Playable Social Host

> 状态：**已通过（自动化门禁）**｜日期：2026-08-01  
> 计划：[64-vertical-slice-0.6-playable-social-host-plan-v0.1.md](64-vertical-slice-0.6-playable-social-host-plan-v0.1.md)

## 1. 完成内容

将 VS0.5 Social Alpha 接到 Unity 玩家路径（不新增 Core 社会规则、不升 Snapshot）：

1. Recruitable Npc EntityView 可见／可选  
2. HUD：名称、Personality、Relation、Faction、当前 Action／Schedule  
3. Help／Slight／Recruit：Unity → PlayerCommandRequest → PlayerInputPort → 既有 Social／RecruitService  
4. EventFeed 优先 RelationshipChanged／FactionMembershipChanged  
5. 自动化闭环：发现 NPC → 读性格 → Help → Recruit  

## 2. Commit 列表

| Phase | Commit | 说明 |
|---|---|---|
| V6-A | 3086064 | Npc views + 计划 64 |
| V6-B | 2190381 | Social HUD |
| V6-C | 05019e | Social commands |
| V6-D | 213c395 | Social event feed |
| V6-E | （本提交） | 整合验收 |

## 3. 测试结果

- EditMode：**157/157 Passed**（	ools/run-editmode-tests.ps1）  
- Snapshot schema 仍为 **v1**（社会状态不入档）  

## 4. 玩家体验流程（PlayableHost）

1. 打开场景，见 3 角色 + 1 可招 Npc（偏黄槽）  
2. 点选 Npc：F1 HUD 看 Personality／状态  
3. 先选 Character，再 Shift 选 Npc  
4. 键 **5** Help／**6** Slight／**7** Recruit（或顶部调试按钮）  
5. F2 事件面板看 * RelationshipChanged／招募相关事件  
6. 招募失败时底部／日志有 FAIL 状态（关系门槛未达）  

## 5. 当前剩余缺口

- 社会状态仍不进 Snapshot（读档后关系／隶属丢失）  
- 无正式 UI（调试 IMGUI）  
- Help 提高 actor→target；Recruit 看 target→actor，玩家需理解双向关系  
- 可招 Npc 仍为 Bootstrap 软编码单例  
- 无地图／寻路／战斗（按范围）  

## 6. 下一阶段建议

1. 产品决定是否做 **Snapshot 社会入档**（硬停确认 schema）  
2. 或 **VS0.7 Content 化**可招 NPC／关系种子  
3. 或薄正式 UI／多会话演示包装  
