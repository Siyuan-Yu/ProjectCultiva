# CW-03 新 WorldSite／势力旗核心闭环交接（2026-09-13）

> **2026-09-13 制作人补充反馈：** CW-03 主体“差不多验收完成”，按已确认的建站主体范围记录；不推定 A～E 全边界已逐项通过。后续主线调整为 CW-U0～U4，未开始 CW-04。

## 状态

- **CW-02：Producer Accepted — 当前交付范围。** 依据为制作人反馈，准确范围与延期项见 [218](218-cw-02-pause-ownership-and-party-incapacitation-safety-exit-2026-09-12.md)。
- **CW-03：Implementation Completed / Producer Acceptance Pending。** 本页记录实现与可执行验收路线，不代表 Unity 行为已经由制作人签收。
- 未进入 CW-04；复杂范围重叠、历史先占、升级扩张与资产管理接续仍按 [ADR-0032](43-decisions/ADR-0032-sitecore-administrative-and-construction-range.md) 后续迁移。

## 已实现闭环

正常 `[建筑]` 入口中的 `base:building_faction_control_post` 现在创建一个 runtime `WorldSite` 及其唯一关联旗核心。预览与提交都使用鼠标落点经 Continuous mapper 得到的同一 `SurfaceId + WorldPosition + StrategicAnchor` 请求；提交重新校验当前 Surface、loaded neighborhood、CompositeWalkGrid、操作距离、地形／占地和 SiteCore 规则。校验完成后才扣材料，并原子注册 Site 与核心；失败回滚材料，不留下半 Site 或孤立旗。

新 Site 保存稳定 SiteId、名字、Owner、来源 Surface、核心精确坐标、初始等级、矩形基础范围、核心状态及关联 FlagId。Site Owner 是政治权威，统一 Owner 服务同步核心的 faction 缓存；领地投影跳过关联旗，避免 Site 与旗成为两个控制源。新 Site 不创建 LocalMap、居民、守军、出生点或人物归属。

基础范围使用真实世界坐标查询，当前 BaseGame 一级调参值为 `4.2 × 2.8` 世界单位。同 Hex 可保存多个 Site 摘要，身份仍按 SiteId；CW-03 为无争议建站暂时拒绝范围重叠和已有有效控制冲突，不把这一限制写成最终永久规则。

地面核心表现按稳定 FlagId 管理当前 Surface 已加载区域内的多个 View。跨 Hex 但仍在 loaded neighborhood 时保留，真正 Chunk unload 时仅销毁表现，返回后由同一领域对象恢复。精确坐标还进入现有动态 blocker 合成；拆除只撤销该核心 blocker，不清除墙、河或其他障碍。WorldMap 以 Site marker 显示新 Site，关联 Flag marker 被抑制，避免重复图标；Site 检视显示名称、Owner、等级、核心状态、Surface、精确坐标和范围。

Strategic Snapshot 现在保存 runtime Site 全身份与核心关联。恢复先建立 runtime Site 壳与核心关联，再恢复 Owner／控制并重建派生索引，最后复用 CW-01 hard rebind 绑定表现。读取建站前档会移除当前 World 多出的 runtime Site；读取建站后档恢复原 SiteId／FlagId；随后继续建站使用恢复后的 ID 分配状态。

拆旗或合法摧毁关联核心后，Site 历史身份保留但 `IsCoreActive=false`，不再提供控制或地图标记；只移除该核心表现与 blocker。主动拆除按现有 50% 配置返还一次，战斗摧毁不返还。建筑、库存、农田、伤损及人物不随核心删除。

## Content 与兼容边界

`BuildingDefinition` 新增并实际消费：`createsWorldSite`、`createdSiteName`、`createdSiteType`、`initialSiteLevel`、`siteRangeWidth`、`siteRangeHeight`。loader 会拒绝建站建筑的非正等级／范围。当前势力控制建筑仍消耗 `base:resource_rough_wood × 10`，一级范围 `4.2 × 2.8` 是调参值。

- Content 预设 Site 保留既有 SiteId、名字、Owner 与 baked ControlCore；启动时只重绑核心 metadata，不生成第二核心。预设议政厅不可经通用拆旗入口删除。
- 旧档没有 runtime Site 字段时仍可读。旧孤立旗只有同时拥有可靠 `SurfaceId` 与精确世界坐标时才幂等迁移为 Site；空间身份不足时保留 legacy 旗并输出诊断，不使用主控当前位置伪造。
- 旧 standalone Flag 仍可由兼容 Hex 查询和旧表现回退读取；新建 Site 核心必须拥有精确 Surface 位置。
- 复杂范围争议、已有建筑管理接续、无管理者生产暂停属于 CW-04/05；本轮只保证无幽灵控制且不删除资产。

## 制作人人工验收路线

本轮未启动 Unity。以下路线使用 `Assets/Scenes/LevelTester.unity` 的正常 New Game、正式 UI 与正式存读档。

### A. 材料、非法点与一次提交

1. New Game 后在荒村先保存“CW03-A-建站前”。背包不足 10 粗木时打开 HUD `[建筑]`，卡片应显示材料不足且不能进入成功提交。
2. 在荒村右键可砍伐的树选择“砍伐”。一棵中树当前正常产出 10 粗木；若现场只有小树，则继续砍至背包达到 10。该过程走 CW-01 动态物件与正式背包，不使用免费立旗或测试注入。
3. 打开 WorldMap，选择普通前往到无 Site 的可通行测试空地 **Hex `(9,4)`**，到达后关闭地图。在河水、墙体／树体、未加载远点、超出操作距离或现有核心范围内预览，应为非法；取消后粗木保持不变。
4. 在 `(9,4)` 周围已加载的开阔草地打开 `[建筑]` → `势力控制建筑` → `建造`，把绿色预览放在主控附近合法空格并左键一次。应只扣 10 粗木，只生成一个新 Site 和一个核心；快速重复点击不应产生第二份。

### B. 真实落点、地图与人物身份

1. 记住鼠标落点；核心应出现在该点，不在玩家脚下或 Hex 格心吸附。
2. 右键核心查看：名称、Owner、等级 1、核心有效和 `4.2 × 2.8` 范围应可见。
3. 打开 WorldMap：同位置只有一个新 Site marker，不另画一枚无关 Flag marker。原荒村、林间镇等预设 Site 仍存在。
4. 返回地面检查主控与附近人物；他们的 Party、Faction、HomeSite、控制权和当前位置不因建站自动改变，也没有凭空生成居民。

### C. 跨 Hex、卸载与多核心

1. 在核心仍处于 Continuous loaded neighborhood 时步行跨过 Hex 边界，核心 View 不应仅因 `CurrentHex` 改变而消失。
2. 继续走远至该核心 Chunk 真正卸载，View 应移除而 Site／WorldMap marker 保留；返回后同一核心在原精确位置恢复一次，不 clone。
3. 取得另一份 10 粗木，前往与第一处范围不重叠的可通行空地 **Hex `(12,4)`**，按同一路径建立第二 Site。两处同时进入 loaded neighborhood 时应各有一个核心 View，互不替换。

### D. A／B／C 档往返

1. 第一处建站后保存“CW03-B-一处站”。读取 A：新增 Site／核心应消失，粗木恢复为 A 档数值；再读 B：恢复同一 SiteId、FlagId、Owner、精确位置、等级、范围和材料数，不生成第三份。
2. 在 B 的当前 World 建第二处并保存“CW03-C-两处站”。反复读取 B／C；B 只有第一处，C 有两处，身份稳定，后续新写入属于当前 World，旧 World 的 View／回调不应泄漏。

### E. 拆旗与无幽灵控制

1. 在 B 或 C 中右键己方新建核心，走现有拆旗确认。应只返还 5 粗木一次；该核心 View 与自身 blocker 消失，关联 Site 变为失活且不再提供控制／地图 marker。
2. 其他 Site、建筑、树墙、库存、伤损和人物必须保留。再次点击旧位置不能重复返料。
3. 拆除后另存并读取；失活核心不得复活，也不得残留幽灵控制。预设议政厅不能从该入口删除。

## 检查边界

执行现有 Core／Data／Unity Host offline compile、Content JSON 解析、相关调用点静态核对和 `git diff --check`。未启动 Unity，未运行 EditMode、PlayMode、Test Runner、batchmode、自动测试或 Bake；上述路线全部等待制作人执行。
