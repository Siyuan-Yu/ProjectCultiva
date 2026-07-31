# 属性与 AttributeModifier 管道（2C）

> 状态：**已冻结（对齐 Freeze v0.2）** | 优先级：P0 | 最后更新：2026-07-31  
> 依赖：`33` v0.2、`2B`、`34`  
> **正式隐匿状态值名：`PersonalConcealmentRisk`。**

## 1. 这个系统解决什么问题

保证任意最终属性都能回答「怎么算出来的」，并强制所有长期属性变化走同一管道，避免各系统直接改 Final。

## 2. 冻结公式

```text
Raw =
  (Base + Σ Fixed)
  × (1 + Σ Percentage)

Final =
  Clamp(
    ApplyAllowedSpecialRules(Raw),
    Min,
    Max
  )
```

规则：

1. **Fixed 先加**到 Base。  
2. 普通 **Percentage 进入同一个加算池**（例如 +20% 与 +30% → 合计 +50%，再乘一次）。  
3. **暂不**设置多个独立乘区。  
4. **不允许**每种功法拥有独立计算顺序。  
5. 百分比基数为 `Base + Σ Fixed`（即公式中的括号部分）。

### 2.1 示例

| 项 | 值 |
|---|---|
| Base 攻击 | 100 |
| Fixed（装备） | +10 |
| Percentage（火灵根） | +0.20 |
| Percentage（火系功法） | +0.30 |
| Raw | (100+10)×(1+0.50) = 165 |
| SpecialRule | 无 |
| Final | Clamp(165, Min, Max) |

## 3. 管道强制

- 所有**长期属性变化**必须通过 `AttributeModifier`。  
- 禁止直接写：`attack += 20` 或直接赋值 `finalValue = ...`。  
- 移除来源时，只能按 `SourceRef`／`ModifierId` 撤掉 Modifier，不得倒算硬改 Final。  
- 最终结果必须能在调试／UI 展开完整来源链。

## 4. AttributeModifier 字段（最小集）

| 字段 | 说明 |
|---|---|
| `ModifierId` | 实例 ID |
| `TargetAttributeId` | 目标属性 |
| `Operation` | `Fixed` / `Percentage` /（Special 见下） |
| `Value` | 整数或缩放整数（百分比建议用 10000=100%） |
| `SourceRef` | 来源 |
| `ReasonRef` | 原因 |
| `StartTick` | 生效 Tick |
| `EndTick` | 可空；到期由 ScheduledEvent 移除 |
| `StackingKey` | 叠加入口键 |
| `StackingRule` | 见 §5 |
| `ConditionId` | 可空；条件定义引用 |
| `Priority` | 特殊规则排序等 |

### 4.1 SourceKind（来源类型）

至少支持：`Talent`、`SpiritRoot`、`Manual`、`Skill`、`Realm`、`Environment`、`Building`、`StatusEffect`、`Equipment`、`Event`。

## 5. 叠加规则（第一版白名单）

仅允许：

| StackingRule | 含义 |
|---|---|
| `Stack` | 同 Key 可叠加 |
| `Replace` | 后来者替换 |
| `HighestOnly` | 只保留最高 |
| `LowestOnly` | 只保留最低 |

## 6. SpecialRule 白名单

仅允许：

| SpecialRule | 含义 |
|---|---|
| `ClampMin` | 提高下限 |
| `ClampMax` | 降低上限 |
| `Override` | 覆盖 Raw（必须带来源，慎用） |
| `Disable` | 使某属性／效果失效 |
| `Convert` | 将 A 属性按规则转入 B（登记制） |

**禁止**内容配置任意执行 C# 或任意表达式修改 Final。

## 7. 属性 vs 状态值／资源池

### 7.1 属性（走 Modifier 管道）

示例：

- `MaxHealth`
- `MaxQi`
- `Attack`
- `Defense`
- `MoveSpeed`
- `CultivationEfficiency`
- `ConcealmentAbility`

### 7.2 状态值／资源池（不硬塞进 Modifier 管道）

示例：

- `CurrentHealth`
- `CurrentQi`
- `CultivationProgress`
- `PersonalConcealmentRisk`（个人隐匿风险累计；**禁止**正式名 ExposureAccumulation／ExposureRisk）
- `InventoryAmount`
- `RelationshipValue`（**只读聚合**；写入必须经 RelationshipLedger 事件）
- `TaskProgress`

状态值通过资源交易、行动结果、事件结算改变；可以**间接**创建 AttributeModifier（例如受伤状态加防御 Fixed），但状态值本身不是 Modifier 目标。

## 8. 事件 → Modifier 的合法流程

```text
DomainEvent
  → StatusEffect / Equipment / Environment 状态
  → 创建 AttributeModifier
  → 到期 ScheduledEvent
  → 按 SourceRef 移除 Modifier
```

禁止事件处理函数直接改属性 Final。

## 9. 数值约定（与 `33` 对齐）

- 核心规则尽量使用整数或缩放整数。  
- 概率：0～10000。  
- 百分比倍率：10000 = 100%。  
- Unity 插值／动画可用 float；逻辑属性计算在 Core 用整数／缩放整数。

## 10. 与内容「词条」的关系

- 配置中的「词条」（`Affix`）是内容数据。  
- 进入运行时必须落地为一条或多条 `AttributeModifier`。  
- 文档与代码优先写 **AttributeModifier**，避免与词条混称。

## 11. 仍待确定

- [ ] 完整 AttributeId 枚举与配置表列  
- [ ] Override／Convert 的审批与极少使用准则  
- [ ] UI 溯源面板信息架构  
- [ ] 条件 `ConditionId` 表达式语言范围（必须仍无任意脚本）

## 12. 验证方式（实现期）

- 给定 Base 与 Modifier 集合，Final 与手算一致（含同池百分比）  
- 移除某一 `SourceRef` 后 Final 回到无该来源时的值  
- 单元测试禁止对 Final 字段直接赋值  
- 非法 SpecialRule／未知 StackingRule 在数据校验期直接失败
