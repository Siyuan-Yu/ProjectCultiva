# ADR-0044：个体凡人、势力劳动、需求与俘虏方向

**Decision Accepted（2026-09-27）；Implementation Complete / Producer Acceptance Pending。** 来源：制作人 CIVILIAN-LIFE-01 设计冻结与实施指令。

## Context

旧文档允许普通凡人由 `MortalPopulation`／岗位组作为匿名权威，只在受关注时实体化并可能归并。该方向无法可靠表达谁劳动、谁饥饿、谁逃亡、谁被俘与招募，也与战后个体命运和真实住房容量冲突。

## Decision

- 每个凡人是持久真实 Character；人口、劳动力与职业统计由人物派生，匿名 `MortalPopulation` 不再是现行人物或劳动 authority。
- 有归属凡人使用真实 FactionMembership；无归属凡人不接受势力排程。FactionMembership、PlayerParty 与 ActiveCharacter 保持分离。
- V1 需求只含 Satiety 与 Energy，按危险／强制状态／紧急需求／Faction Mortal Daily Schedule／工作顺序仲裁。统一 Work/OffDuty profile 的钟点是集中可调默认值，不是永久规则；OffDuty 不主动开始专业工作，紧急 Rescue 仍可打断。
- 食物来源确定：势力居民使用同势力可访问 Site PublicStock；永久 PlayerParty 凡人先用当前己方 Site PublicStock，缺货后用 PartyInventory；无势力合法 Residence 居民仅有住所 Site 的 food-only access；Detained 仅使用拘留 Site PublicStock。普通凡人无个人背包。
- 每人最多一个专业职业：Unassigned、Farmer、HerbFarmer、Logger、Medic；所有有行动能力凡人均可搬运／救援与建设。
- Site 易主不改居民势力。战败势力居民按对当前 Faction 的 Loyalty 留下或真实 Fleeing；逃出 Site 后成为保留身份和位置的 Displaced，不删除。
- 捕获必须来自带 Capture/Subdue intent 的正常 CharacterEncounter。俘虏是同一 Character，由真实人物押送；没有合法 PrisonerOnly Residence 就不能成为 Detained。
- PrisonerOnly 复用现有 Residence 和真实容量。Detained 保留原 FactionId/Loyalty 与需求，不参加工作；获得食物和正常休息的日子才降低 Loyalty。
- 招募阈值为 Loyalty < 15；成功后加入 PlayerFaction 和现有 permanent player-manageable authority，建立新 Loyalty = 50，仍为凡人且不自动入 PlayerParty。玩家随后可在人事 UI 加入当前小队；入队后按永久可控角色切换 Active，离队保留身份与需求。Temporary Quest Companion 不享有此权限。释放转入 Fleeing；处决是囚犯操作。
- 住房仍使用当前可见表现。性能通过 Character LOD 演进，不使用住房黑箱、private presence、匿名囚犯或匿名劳动力。

## Supersession

本 ADR 定向取代旧文档中“普通凡人群体统计是真源、关注后才实体化、普通凡人可归并回人口池”的产品方向。历史 Freeze 与旧过程记录保留其时点事实；新实施不得据此恢复匿名凡人 authority。ADR-0024 关于**修士**为真实 Character 的规则继续有效。

## Consequences

既有农业／劳动已增量接入具名 worker；人员 UI 显示人物行，不采用大型工作矩阵。Snapshot v14 已增加需求、职业、逃亡、押送、拘留、Residence usage 与 Site 内建设工单／材料托运持久化，并严格拒绝 v1-v13。P1 realization note：Loyalty 已从 MortalCivilianBoard 移到通用 `CharacterFactionLoyaltyLedger`，由 CharacterId + 当前 FactionId 唯一确定；Snapshot v14 顶层独立保存，未封板 interim v14 不兼容。占领、拘留和招募只是该通用属性的第一批消费者；无势力人物不适用。实现说明：Rescue 以真实 Character WorldPresence 搬运，非 Medic 不直接治疗；Haul 将 Site PublicStock 资源送入工单 escrow；Construction 复用正式建筑完成事务且不二次扣材；现有势力旗拆除可由工人执行，Repair 与普通建筑拆除尚无正式玩法入口。产品规则不因该实现说明扩张；制作人人工验收待完成。

完整规则、非目标、可调项与未来人工验收愿景见 [267](../267-civilian-life-01-design-freeze-2026-09-27.md)。

## P1 Realization Note — Physical Character Actions

人物对人物的玩家命令以统一 approach authority 落地：合作交互可以 Hold，Attack/Capture pursuit 不冻结逃亡目标；远距离点击不能直接创建 Encounter、写 Capture request 或改变招募结果。Recruit 在接近后执行 5 个非缩放现实秒的可取消轻交互。Flee 使用正式 MovementIntent/Host A*/WorldPresence 链，保存来源 Site 并轮换确定性出口。StorageRoom 提供多访问槽并根据 Host 路径失败轮换。有效世界命令可解除 Manual Pause，Modal pause 保持硬门禁。生命状态保持第一发致命伤进入 Incapacitated，Dead 只由记录原因的显式确认产生；战术倍速不缩短现实秒倒计时。以上为既定产品规则的实现说明，不扩张里程碑范围，状态仍为 **Implementation Complete / Producer Acceptance Pending**。
