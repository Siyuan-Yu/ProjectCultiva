# AI 协作约定

本文件供 Cursor / AI 助手在本项目中自动读取。目标是让**不同时间、不同设备、不同 AI 会话**产出一致的结果。

## 开工前必读

任何会话开始时，先读这三份，不要凭猜测动手：

1. `docs/00-project/00-overview.md` — 最高层大纲与文档索引（**入口**）
2. `docs/10-benchmark/14-borrow-and-differentiate.md` — 设计约束（借鉴什么、不做什么）
3. `docs/40-process/42-devlog.md` 最新 2 条 — 当前进展与阻塞

需要某个系统的细节时，从总览第六节的索引进入 `docs/20-systems/`，不要在总览里找细节。

## 硬性规则

1. **文档先于代码。** 系统设计文档（`docs/20-systems/`）未定稿时，不要生成该系统的实现代码；应先补文档。
1.1 **总览只放大纲。** `00-overview.md` 保持最高层结构；任何细节、流程、数值、示例都写进 `docs/20-systems/` 下对应文档。
2. **命名走术语表。** 所有标识符必须匹配 `docs/00-project/03-glossary.md`。新概念先登记再使用，禁止同义词混用。
3. **逻辑层不许引用 UnityEngine。** `XianXia.Core` 与 `XianXia.Data` 内禁止 `using UnityEngine`，包括 `Random`、`Debug`、`Time`、`Mathf`。
   - 随机 → 注入的 `IRandomSource`
   - 日志 → 注入的日志接口
   - 时间 → Tick 计数
4. **数值不写死。** 任何游戏数值进配置表，不出现在代码字面量里（纯技术常量除外）。
5. **数值必须可溯源。** 属性计算走统一 Modifier 管道，每个 Modifier 带来源标签。
6. **改了实质内容就更新 devlog。** 在 `docs/40-process/42-devlog.md` 顶部追加，重点写判断与理由，不是罗列文件。
7. **贵的决定要写 ADR。** 涉及版本、数据格式、架构边界、第三方依赖时，用 `docs/90-templates/adr-template.md` 建 ADR，不要直接改代码了事。
8. **明确不做的东西不要提议。** 见 `14-borrow-and-differentiate.md` 第 3 节（即时动作战斗、3D 开放世界、多人联网等）。

## 范围纪律

这是单人 + AI 的项目，最大风险是范围膨胀。

- 提方案时优先给**能在垂直切片里验证**的最小实现
- 新增系统前先问"它为玩家体验贡献了什么"，答不出就不做
- 不要为了"以后可能需要"提前抽象；但**架构边界（程序集分离、Modifier 溯源、配置表驱动）例外**，这几项后补极贵

## 代码约定

- C#，命名遵循 .NET 规范（PascalCase 类型与方法，camelCase 局部变量与参数，`_camelCase` 私有字段）
- 逻辑层优先写可单元测试的纯函数
- 注释只解释"为什么"和非显而易见的约束，不解释代码在做什么
- 配置表用文本格式（CSV/JSON）作为唯一真源；ScriptableObject 只作为导入后的运行时/编辑器缓存

## 回答语言

中文。
