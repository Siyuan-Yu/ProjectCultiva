# 修仙游戏项目（暂定名 XianXia）

单人开发的修仙题材游戏。Unity + Cursor。本仓库同时存放**策划文档**与**游戏工程**，用 Git 实现跨设备开发与开发史留存。

## 当前阶段

**M1 — Demo v0.1 原型开发。** 当前以可替换 Sprite 和占位素材实现荒村垂直切片，不等待最终美术。

Unity 版本锁定为 **2022.3.6f1 Built-in**。`Assets/Scenes/Demo_v0_1.unity`、占位 PNG 与 Prefab 已生成；需要时可从 `XianXia` 编辑器菜单重建。

远端：https://github.com/Siyuan-Yu/ProjectCultiva  
最新跨设备交接：`docs/40-process/44-session-handoff-2026-07-31.md`

## 从哪开始读

新接手这个项目（包括几个月后的自己、别人、或新的 AI 会话），按这个顺序读：

1. 最新的 `docs/40-process/44-session-handoff-*.md` — 跨设备快速恢复上下文
2. `docs/00-project/00-overview.md` — 最高层大纲与系统索引（**入口**）
3. `docs/40-process/45-demo-v0.1.md` — 第一个可验证 Demo 的范围（只做闭环，不做完整游戏）
4. `docs/40-process/46-demo-v0.1-art-assets.md` — Demo Prototype 美术资源、AI Prompt 与导入规格
5. `docs/40-process/47-demo-v0.1-ai-art-batches.md` — AI 素材分批执行计划（先 ≤10 验风格）
6. `docs/40-process/48-demo-v0.1-minimum-art-integration.md` — 当前占位素材、目录与替换方式
7. `docs/10-benchmark/14-borrow-and-differentiate.md` — 借鉴什么、做什么不一样、明确不做
8. `docs/40-process/42-devlog.md` — 一路上发生了什么、为什么这么选
9. `docs/00-project/01-vision.md` — 愿景与成功标准
10. `docs/00-project/03-glossary.md` — 命名规范（写代码前必读）

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

远端仓库：`https://github.com/Siyuan-Yu/ProjectCultiva`
Unity 版本：`2022.3.6f1（Built-in，见 ADR-0001）`
