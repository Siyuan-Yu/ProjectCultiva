# 技术架构

> 状态：**架构冻结阶段** | 最后更新：2026-07-31  
> 主契约：[`33-architecture-core-rules-freeze-v0.1.md`](33-architecture-core-rules-freeze-v0.1.md)  
> 桥接：[`32-prototype-to-product-bridge.md`](32-prototype-to-product-bridge.md)  
> **本阶段不写实现代码。** Demo 原型仅作语义参考。

## 0. 文档分工

| 文档 | 内容 |
|---|---|
| **`33` 架构核心规则冻结** | 总边界、时间、实体、命令、事件、地图、多队、战斗、AI、军队 — **主契约** |
| **`34` 实体与能力模块** | IEntity、Character 组件、四层升降级 |
| **`35` Order／Action** | 指令与行动生命周期（无公开 Intent） |
| **`2C`／`2E`** | Modifier 公式；DomainEvent／ScheduledEvent／WorldLedger |
| **`32` Demo→正式桥接** | Demo 类 → 正式概念映射 |
| **`36` ContentPackage／Mod Ready** | 官方与 Mod 统一管线；阶段 A 不写加载器 |
| **本文 `31`** | 程序集边界、数据驱动、工程与待定工程选项 |
| **`37` 飞书同步** | 本地→飞书工具说明 |

## 1. 已定原则

### 1.1 逻辑与表现分离（最重要）

游戏核心逻辑（境界、属性、事件、战斗结算）写成**不依赖 UnityEngine 的纯 C#**，放在独立程序集里。

```
XianXia.Core/        纯 C#，无 UnityEngine 引用，游戏规则全在这
XianXia.Data/        配置表定义与加载
XianXia.Unity/       表现层：MonoBehaviour、UI、输入、渲染
XianXia.Tests/       针对 Core 的单元测试
```

用 Assembly Definition (`.asmdef`) 强制这个边界，让"不小心 using UnityEngine"直接编译报错。

### 1.2 数据驱动

- 所有可增删的内容（功法、神通、词条、丹药、事件、妖兽、掉落）走配置表
- 配置源用 **CSV/JSON 文本格式**，不用纯 ScriptableObject 资产作为真源
- 每张表有版本号与校验步骤，加载失败要报出具体行号

### 1.3 数值可溯源（细节见 `33` §1）

属性计算走统一 AttributeModifier 管道；禁止直接改 Final。

### 1.4 确定性与随机

- 所有随机走可注入的随机源（seed 可保存）
- 禁止在逻辑层直接调 `UnityEngine.Random`

### 1.5 时间推进（细节见 `33` §2）

- **1 Tick = 15 游戏分钟；1 日 = 96 Tick**
- 逻辑层只认 Tick；`GameClock` 为表现层
- 禁止各系统自行用真实时间做逻辑结算

### 1.6 实体分层（细节见 `33` §3）

四层：可控修士全模拟／关键 NPC 全模拟／普通修士群体抽象／凡人统计。

## 2. 待定项（工程选项，非玩法形状）

| 项 | 选项 | 状态 |
|---|---|---|
| Unity 版本 | 2022.3.6f1 | 已定，见 ADR-0001 |
| 渲染管线 | Built-in | 已定，见 ADR-0001 |
| UI 方案 | UGUI／UI Toolkit | 待定（ADR-0002） |
| 存档 | JSON（建议先用）／二进制 | 待定 |
| 事件脚本化 | 纯配置表／轻量表达式 | 待定；依赖 `2E` |

## 3. 工程约定

- 目录结构、命名规范：待 `34-conventions.md` 补充
- Git：`.gitignore` 用 Unity 官方模板；大文件考虑 Git LFS
- 分支：个人开发用 `main` + 功能分支即可
- 提交信息带类型前缀（`feat/fix/docs/refactor/data`）

## 4. 跨设备开发方案

1. 全部内容（含 docs）在同一个 Git 仓库
2. 远端私有仓库
3. Unity 的 `Library/`、`Temp/`、`Logs/` 不入库
4. Unity 版本锁定，避免 Hub 自动升级

## 5. 实现期入口顺序（确认规则后再做）

见 `32` 第 5 节：asmdef → Tick → Modifier → Action → 实体分层 → 第一次突破。
