# 技术架构（初稿）

> 状态：初稿，Q1–Q3 确认后定稿 | 最后更新：2026-07-29

## 1. 已定原则

### 1.1 逻辑与表现分离（最重要）

游戏核心逻辑（境界、五行、事件、战斗结算）写成**不依赖 UnityEngine 的纯 C#**，放在独立程序集里。

理由：
- 可以写单元测试，模拟类游戏的数值 bug 靠手点是找不完的
- 换 UI / 换表现层不影响逻辑
- AI（Cursor）改纯逻辑代码的正确率远高于改耦合了 Unity 生命周期的代码
- 交接时对方能单独读懂规则

```
XianXia.Core/        纯 C#，无 UnityEngine 引用，游戏规则全在这
XianXia.Data/        配置表定义与加载
XianXia.Unity/       表现层：MonoBehaviour、UI、输入、渲染
XianXia.Tests/       针对 Core 的单元测试
```

用 Assembly Definition (`.asmdef`) 强制这个边界，让"不小心 using UnityEngine"直接编译报错。

### 1.2 数据驱动

- 所有可增删的内容（功法、神通、词条、丹药、事件、妖兽、掉落）走配置表
- 配置源用 **CSV/JSON 文本格式**，不用纯 ScriptableObject 资产
  - 理由：文本可 diff、可 Git 合并、可被 AI 批量生成和修改；`.asset` 二进制化后这些都做不到
  - 需要在 Editor 里方便编辑时，做一个把文本导入成 SO 的中间层，但**文本是唯一真源**
- 每张表有版本号与校验步骤，加载失败要报出具体行号

### 1.3 数值可溯源

任何最终数值必须能回答"它是怎么算出来的"。实现方式：属性计算走统一的 Modifier 管道，每个 Modifier 记录来源标签，UI 悬浮时可展开完整计算链。

这是差异化 4 的技术地基，**后期补做代价极高，必须一开始就有**。

### 1.4 确定性与随机

- 所有随机走可注入的随机源（seed 可保存），便于复现 bug 与做"同种子重玩"
- 禁止在逻辑层直接调 `UnityEngine.Random`

### 1.5 时间推进

模拟类游戏用统一的 Tick 驱动（例如 1 Tick = 游戏内 1 时辰），逻辑层只认 Tick，不认 `Time.deltaTime`。

## 2. 待定项

| 项 | 选项 | 状态 |
|---|---|---|
| Unity 版本 | 2022.3 LTS（本机已有，建议）/ 更新的 LTS | 待定，见 ADR-0001 |
| 渲染管线 | Built-in（2D 简单场景够用）/ URP（要后处理和光效） | 待定 |
| UI 方案 | UGUI（生态成熟、AI 熟悉）/ UI Toolkit（适合大量数据面板但坑多） | 待定 |
| 存档 | JSON（可读可改，建议先用）/ 二进制 | 待定 |
| 事件脚本化 | 纯配置表 / 内嵌轻量脚本（Lua、表达式解析器） | 待定，取决于事件复杂度 |

## 3. 工程约定

- 目录结构、命名规范：待 `34-conventions.md` 补充
- Git：`.gitignore` 用 Unity 官方模板；大文件（美术源文件）考虑 Git LFS
- 分支：个人开发用 `main` + 功能分支即可，不搞复杂流程
- 提交信息带类型前缀（`feat/fix/docs/refactor/data`），便于以后回溯开发史

## 4. 跨设备开发方案

1. 全部内容（含 docs）在同一个 Git 仓库
2. 远端用私有仓库（GitHub / Gitee，国内建议 Gitee 或 GitHub + 代理）
3. Unity 的 `Library/`、`Temp/`、`Logs/` 不入库
4. Unity 版本号写进 `docs/30-tech/31-architecture.md` 并锁定，避免换机器时被 Hub 自动升级导致工程差异
