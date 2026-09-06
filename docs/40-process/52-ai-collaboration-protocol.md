# AI 多会话协作规范

> 状态：现行 | 最后更新：2026-09-06
> 文件：`docs/40-process/52-ai-collaboration-protocol.md`  
> （原拟编号 `46`；`46` 已用于 Demo 美术资源表，故本规范使用 **52**。）  
> 适用：Architecture／Development／Narrative 三类长期 AI 会话  
> **不替代** Architecture Freeze、系统文档或 ADR；本文件只定协作边界。

## 1. 文档目的

**聊天记录不是项目唯一真源。**  
会话里的口头结论若未写入仓库文档，视为**未生效**。

| 角色 | 作用 |
|---|---|
| 聊天 | 讨论、澄清、草稿 |
| 仓库文档 | **唯一真源**；跨会话、跨设备、跨 AI 的同步介质 |

**项目真实状态优先读取：**

1. Architecture 文档（`docs/30-tech/`，尤其当前 Architecture Freeze）  
2. System Design（`docs/20-systems/`）  
3. ADR（`docs/40-process/43-decisions/`）  
4. Devlog（`docs/40-process/42-devlog.md`）  
5. Glossary（`docs/00-project/03-glossary.md`）

另：总览 `00-overview.md`、本规范、`AGENTS.md` 为会话启动入口。

---

## 2. 三个 AI 角色

### 2.1 Architecture AI（架构／系统设计会话）

| | |
|---|---|
| **职责** | 核心架构、系统边界、数据模型、技术取舍、大方向决策 |
| **典型输出** | ADR、`30-tech` 架构文档、决策记录、Freeze 修订、实施计划（文档） |
| **可改** | `30-tech`、相关 `20-systems` 边界条款、Glossary 中的架构术语、Roadmap／Devlog |
| **禁止** | 编写具体 Unity／玩法实现代码；随意改写剧情正文／角色台词／章节体验文案 |
| **发现剧情缺口时** | 可提 **Content / Narrative Request**，由 Narrative AI 补内容文档 |

### 2.2 Development AI（Unity 开发实现会话）

| | |
|---|---|
| **职责** | Unity 实现、C#、工具、Editor、性能、测试、按已批准计划落地 Core／表现层 |
| **开工必读** | `AGENTS.md`、`README.md`、当前 Architecture Freeze、相关 System Design、相关 ADR、Core M1 计划（若在编码期） |
| **可改** | `Assets/`、测试、工程配置、实现向注释；**不得**在未走变更流程时改玩法语义文档冒充「实现细节」 |
| **禁止** | 自行改变游戏规则；自行新增系统；把聊天里的新点子直接写成正式规则 |
| **发现架构问题** | 提出 **Architecture Change Request（ACR）**，停手等待架构文档／ADR 更新后再改规则侧代码 |

### 2.3 Narrative AI（剧情／文案设计会话）

| | |
|---|---|
| **职责** | 世界观叙事、剧情、NPC 人物、第一章流程体验、情绪节奏、任务故事、文案 |
| **典型输出** | `20`／`2G`／`29` 等体验与故事向文档、角色与关卡叙述、文案表（未来内容包） |
| **禁止** | 自行改变技术架构；自行决定数据结构／ID 方案／存档形状／程序集边界 |
| **剧情需要系统支撑时** | 提出 **System Design Request（SDR）**，由 Architecture AI（或负责人）更新 `20-systems`／必要时 ADR 后再当作可实现需求 |

---

## 3. 共享信息入口

### 3.1 所有 AI 启动时优先读（共同核心）

1. `AGENTS.md`  
2. `README.md`  
3. `docs/00-project/00-overview.md`  
4. `docs/00-project/03-glossary.md`  
5. **当前 Architecture Freeze**（现为 `docs/30-tech/33-architecture-core-rules-freeze-v0.2.md`）  
6. 本规范：`docs/40-process/52-ai-collaboration-protocol.md`  
7. `docs/40-process/42-devlog.md` 最近 2～3 条  

### 3.2 按方向追加

| 角色 | 追加阅读 |
|---|---|
| Architecture | `docs/30-tech/` 全套相关页；ADR 索引；审计报告；实施计划 |
| Development | `30-tech`（Freeze／34／35／36／31／32）+ 本次任务相关的 `20-systems` + ADR-0022 等范围 ADR |
| Narrative | `00-project`（愿景／范围）+ `20`／`2G`／`29`／`28` 等体验文档 + 术语表；**只读** Freeze 中与生命周期／Focus／开局 Membership 相关的冻结条款 |

飞书仅为阅读层；**以本地 Markdown／Git 为准**（见 `37-feishu-sync.md`）。

---

## 4. 变更流程（强制）

```text
提出需求（聊天或 Request 模板）
  → 判断影响范围（Architecture / System Design / Content）
  → 更新对应文档
  → 必要时新增或修订 ADR
  → 更新 Devlog
  → 其他会话／AI 读取最新文档后再行动
```

**禁止：** 只在聊天里拍板后直接实现或直接改代码冒充定案。

冻结期内（见 `AGENTS.md`）：未批准前默认**不写正式代码**；文档变更仍走本流程。

---

## 5. 三类变更

### 5.1 Architecture Change

**示例：** 数据模型、时间系统、Entity 结构、存档、Mod／ContentPackage 形状、程序集边界、关系真源、地图层级。

**必须：** 更新 `30-tech`（及冲突的系统页）+ **ADR** + Devlog；升 Freeze 版本号（若动冻结条文）。

**发起物：** Architecture Change Request（见 §9）。

### 5.2 System Design Change

**示例：** 修炼规则、战斗规则、NPC／关系规则、势力规则、义务与隐匿玩法细则。

**必须：** 更新对应 `docs/20-systems/2X-*.md`；若触及已冻结架构边界 → 升级为 Architecture Change。  
**通常需要 ADR：** 仅当改变已采纳 ADR 或 Freeze 条文时。

**发起物：** System Design Request（见 §9）。

### 5.3 Content Change

**示例：** 新角色、新任务、新剧情、新地图叙述／关卡内容、文案。

**必须：** 更新内容／流程类文档（如 `2G`、开局、未来 Content 表）。  
**不影响架构时无需 ADR。**  
若内容隐含新系统字段或新模拟规则 → 先走 System Design Request。

---

## 6. 会话交接模板（Session Handoff）

重要讨论结束时，在 `docs/40-process/` 新增或追加交接页（命名建议：`44-session-handoff-YYYY-MM-DD.md`，或主题短名），并在 Devlog 留一行链接。

```markdown
# Session Handoff — YYYY-MM-DD

- 会话角色：Architecture / Development / Narrative
- 日期：
- 当前阶段：（如 Architecture Freeze v0.2／Core M1 规划中）

## 当前状态
（世界现在以文档为准的一句话）

## 已确定
- 

## 待确定
- 

## 影响文档
- （路径列表）

## 下一步
- 

## 风险
- 

## 未写入文档的聊天结论（一律视为无效，除非补文档）
- （应为空；若有，必须立刻补文档或删除本条）
```

---

## 7. 冲突处理原则

三者诉求冲突时，**执行优先级：**

1. **已冻结 Architecture**（当前 Freeze + 已采纳 ADR）  
2. **已批准 System Design**（状态为已冻结／已定稿的 `20-systems`）  
3. **当前剧情需求**（体验与文案）  
4. **开发便利性**

任何方向都可提出修改申请（ACR／SDR／Content），但**申请≠生效**；生效以文档更新为准。

---

## 8. 与 Demo／Core 的关系

- Development AI **不扩展 Demo** 作为正式规则实验场（除非任务明确且不污染 Freeze）。  
- 正式实现以 Freeze + 已批准计划为准（如 Core M1 Implementation Plan）。  
- Narrative 产出不得要求 Development「先做未设计系统」。

---

## 9. 请求模板（短表）

### Architecture Change Request（ACR）

```markdown
## ACR — 标题
- 提出者角色：Development / Narrative / 人工
- 日期：
- 问题：
- 建议改动：
- 影响面：（Entity／Tick／存档／Mod…）
- 相关文档：
- 阻塞的实现：（若有）
```

### System Design Request（SDR）

```markdown
## SDR — 标题
- 提出者角色：Narrative / Development / 人工
- 日期：
- 剧情或实现需求：
- 希望系统如何表现：
- 是否可能影响架构：（是／否／不确定）
- 相关文档：
```

---

## 10. 合规检查清单（每次会话开始）

- [ ] 已读共同核心文档（§3.1）  
- [ ] 知悉自己的角色禁止项（§2）  
- [ ] 不把聊天当定案  
- [ ] 准备改规则／系统／内容时，已定位要改的文件  
- [ ] 冻结阶段未擅自编码（除非任务明确授权且范围在计划内）

---

## 11. 项目开发文档语言规范

项目内面向设计、开发流程、架构说明、验收与 Devlog 的文档正文，默认使用中文。代码标识符、API、类名、方法名、字段名、枚举、JSON key、文件路径、DefinitionId 等技术标识保持原文，不为翻译而改变其正式名称。

除非制作人明确要求英文，所有新建项目文档及对现行文档新增的说明均遵守本规则。该规则不要求批量翻译历史归档，避免制造与当前工作无关的大型文档 diff。
