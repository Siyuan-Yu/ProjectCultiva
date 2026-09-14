# CW-04 Scale + Preset Core Coverage（2026-09-14）

## Final scale

- Site Level 1：150×150 Continuous Surface cells。
- Main Surface：0.028 world/cell，因此解析为4.2×4.2 world，即3×3个1.4-world chunks。
- Wilderness Encounter：独立保留500×500 cells，即14×14 world／约10×10 chunks。
- 新Level 1 CoreRange和baseline/initial Claim写4.2 world，不把150写进Claim。

Claim Snapshot正式格式升至V3。仅对可确认是Level 1 baseline/initial的已知开发期尺寸迁移：V1 raw-cell 500/250，V2 world-size 14/7；统一解析为当前4.2 world。Expansion或level/kind无法证明时返回`SnapshotInvalid`。

## Main Surface preset Site audit

下表来自`main_wilderness_surface_v1.siteRegions`、正式travel HexWorld Site元数据和实际Surface placements。Claim数表示正常New Game完成baseline后的预期。

| SiteId | Name | Owner | Type | 正式行政据点判定 | Continuous Core | Core asset / center / level | Claims | 结论 |
|---|---|---|---|---|---|---|---:|---|
| `base:site_chengzhen` | 青石镇 | 无 | Town | 否：当前无正式Owner | 否 | 无 | 0 | 不自动授予控制 |
| `base:site_guanai` | 青石关 | `base:faction_xijin` | Pass | 是 | 否 | 无 | 0 | 缺Continuous authored administrative center |
| `base:site_huangcun` | 青石荒村 | `base:sect_huangcun_labor` | Village | 是 | 是 | `base:site_huangcun:block_supervisor_mansion`；center=(6.105,11.21)；L1 | 1 | 真实议政厅，建立4.2×4.2 baseline |
| `base:site_lingdi` | 灵地 | `base:faction_shuofeng` | SpiritLand | 否：普通资源／灵地语义 | 否 | 无 | 0 | 不自动授予控制 |
| `base:site_linjian` | 林间 | `base:sect_huangcun_labor` | Forest | 否：普通森林地点 | 否 | 无 | 0 | 不自动授予控制 |
| `base:site_zhuangyuan` | 庄园 | `base:faction_nan_yan` | Village | 是 | 否 | 无 | 0 | 缺Continuous authored administrative center |
| `test:site_player_camp` | TEST 主角营地 | `base:faction_player` | Camp | 否：test/temporary Site | 否 | 无 | 0 | 不进入正式预设行政范围 |

本轮没有新补Core placement。青石关和庄园的Continuous source map都只有一棵明确标注为`Prototype marker`的树，没有建筑布局、议政厅或行政中心锚点；入口点和Strategic Hex均被明确禁止作为Core位置，因此不能可靠author。待制作人提供或确认真实Continuous行政建筑位置后，再补对应`controlCore`，无需修改runtime fallback。

Content校验现检查controlCore StableId/placement既有合法性，并新增：Site引用存在、同一Site只有一个controlCore、Level 1存在、placement位于所属Surface geography。baseline后及Snapshot restore后还验证每个active Core中心由自身Site实际管理。

Actual overlay继续直接消费world-space Actual Administrative Control geometry，不经过Hex摘要。状态：CW-04 Scale + Preset Core Coverage Implementation Completed / Producer Acceptance Pending。
