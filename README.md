# 修仙游戏项目（暂定名 XianXia）

单人开发的修仙题材游戏。Unity + Cursor。本仓库同时存放**策划文档**与**游戏工程**，用 Git 实现跨设备开发与开发史留存。

## 当前阶段

**M0 — 定方向**。等待确认 `docs/00-project/01-vision.md` 中的 Q1–Q5。
在此之前不写游戏代码。

## 从哪开始读

新接手这个项目（包括几个月后的自己、别人、或新的 AI 会话），按这个顺序读：

1. `docs/00-project/01-vision.md` — 这是什么游戏
2. `docs/10-benchmark/14-borrow-and-differentiate.md` — 和竞品哪里不一样（**最重要**）
3. `docs/40-process/41-roadmap.md` — 现在做到哪了
4. `docs/40-process/42-devlog.md` — 一路上发生了什么、为什么这么选
5. `docs/30-tech/31-architecture.md` — 代码为什么长这样
6. `docs/00-project/03-glossary.md` — 命名规范（写代码前必读）

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
│   └── 43-decisions/            ADR 架构决策记录
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

## 跨设备开发

```bash
# 首次
git clone <远端地址> && cd XianXia

# 每次开工前
git pull

# 每次收工
git add -A && git commit -m "docs: 更新xxx" && git push
```

远端仓库：`[待配置]`
Unity 版本：`[待锁定，见 ADR-0001]`
