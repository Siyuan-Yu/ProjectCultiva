# 修仙游戏项目（暂定名 XianXia）

单人开发的修仙题材游戏。Unity + Cursor。本仓库同时存放**策划文档**与**游戏工程**，用 Git 实现跨设备开发与开发史留存。

## 当前阶段

**Architecture Freeze v0.2（待人工审核）。** Demo 已停扩；正式 Core 编码前须确认 Freeze 与 [Core M1 实施计划](docs/40-process/51-core-milestone-1-implementation-plan-v0.1.md)。  
Unity 版本锁定 **2022.3.6f1 Built-in**（ADR-0001）。

远端：https://github.com/Siyuan-Yu/ProjectCultiva

## AI 多会话协作

三类长期工作流（架构／Unity 开发／剧情文案）的职责、同步与变更流程见：

**[`docs/40-process/52-ai-collaboration-protocol.md`](docs/40-process/52-ai-collaboration-protocol.md)**

（编号说明：`46` 已用于 Demo 美术资源表，本规范为 **52**。）

## 从哪开始读

新接手这个项目（包括几个月后的自己、别人、或新的 AI 会话），按这个顺序读：

1. `AGENTS.md` — AI 硬约束与当前阶段  
2. `docs/40-process/52-ai-collaboration-protocol.md` — 多会话职责与真源规则  
3. `docs/00-project/00-overview.md` — 最高层大纲与系统索引（**入口**）  
4. `docs/00-project/03-glossary.md` — 术语表  
5. `docs/30-tech/33-architecture-core-rules-freeze-v0.2.md` — 架构主契约  
6. `docs/00-project/04-reading-guide.md` — 通读顺序  
7. `docs/40-process/42-devlog.md` — 最近决策与理由  
8. 最新的 `docs/40-process/44-session-handoff-*.md` — 跨设备快速恢复（若有）  
9. （Demo 参考）`45`～`49` — 原型范围与美术；正式语义以 Freeze／`32` 为准  
10. `docs/10-benchmark/14-borrow-and-differentiate.md` — 借鉴与不做

## 文档结构

```
docs/
├── 00-project/          项目定位与约束
│   ├── 01-vision.md             愿景、目标玩家、成功标准、待确认问题
│   ├── 02-scope-and-constraints.md  现实约束与范围控制原则
│   └── 03-glossary.md           术语表（命名唯一真源）
├── 10-benchmark/        竞品拆解
│   ├── 11-tale-of-immortal.md          鬼谷八荒
│   ├── 12-cultivation-simulator.md     了不起的修仙模拟器
│   ├── 13-comparison.md                横向对照
│   └── 14-borrow-and-differentiate.md  借鉴/差异化/不做
├── 20-systems/          系统设计（一系统一文档）
├── 30-tech/             技术架构
├── 40-process/          研发管理
│   ├── 41-roadmap.md            里程碑与完成标准
│   ├── 42-devlog.md             开发日志（倒序）
│   ├── 43-decisions/            ADR 架构决策记录
│   ├── 51-…-implementation-plan  Core M1 实施计划
│   └── 52-ai-collaboration-protocol.md  多 AI 会话协作规范
└── 90-templates/        模板
```

## 维护规范

这套规范存在的唯一目的：让**三个月后的你**和**接手的人**能在半小时内恢复完整上下文。

1. **文档先于代码**：系统设计文档没定稿，不写该系统的代码。
2. **devlog 必须写**：每次有实质进展就在 `42-devlog.md` 顶部追加一条，重点写"为什么"。
3. **贵的决定写 ADR**：以后改起来会痛的决定（版本、格式、架构边界、引入插件）必须留记录。
4. **命名走术语表**：新概念先登记 `03-glossary.md`，再写代码。
5. **文档头部三件套**：状态、优先级、最后更新日期，改内容就改日期。
6. **提交信息带前缀**：`feat/fix/docs/refactor/data/chore`。
7. **多 AI 会话**：遵守 `52-ai-collaboration-protocol.md`；重要结论必须落文档，禁止只在聊天里定案。

## 跨设备开发

```bash
# 首次
git clone <远端地址> && cd XianXia

# 每次开工前
git pull

# 每次收工
git add -A && git commit -m "docs: 更新xxx" && git push
```

远端仓库：`https://github.com/Siyuan-Yu/ProjectCultiva`
Unity 版本：`2022.3.6f1（Built-in，见 ADR-0001）`
