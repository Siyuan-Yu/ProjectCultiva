# ADR-0041：秘境任务临时同行与 Snapshot v11

状态：Producer Accepted / Sealed（2026-09-25）。对应 process 264。

Temporary quest companion participates in party travel and combat, but is not a player-controllable character.
临时同行跟随受控队伍、随队进入 Separate Space、通过 NPC AI 参战并占用容量；可以选中查看，不能成为 ActiveCharacter、不能接受玩家手动战斗命令或普通 Stop Follow。可控性从永久 Character roster / 既有玩家势力管理 authority 派生，首先排除 QuestCompanion binding；UI、手动命令后端、自动 Active 候选和恢复共用判定。成员、空间和自动战斗 authority 保留。

仅 Active secretRealm 实例派生社交话题，NPC→actor门槛20；绑定真实 EntityId + QuestInstanceId + OriginalSquadId + Active/PendingDeparture。membership仍由Squad管理，不改faction/tag/永久roster。任务结束后仅生命周期安全离队，不能普通StopFollow。
Snapshot v11保存绑定并严格校验成员，保留已有调度wire；不存派生话题或controllability。旧版本拒绝、不运行迁移。不能安全恢复原Squad则就地singleton。Dynamic Opportunity NPC目前拒绝。制作人主流程及最终控制/StopFollow P1已验收。
