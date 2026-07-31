# 属性与 Modifier 管道（2C）

> 状态：**形状已冻结于 `33-architecture-core-rules-freeze-v0.1.md` §1**；本文件为系统展开入口 | 优先级：P0 | 最后更新：2026-07-31  
> 上级：`docs/00-project/00-overview.md`  
> 关联：`2B-attributes-and-affinity.md`、`33-architecture-core-rules-freeze-v0.1.md`、`31-architecture.md`  
> **本阶段不写实现代码。** 细则未填完前，不得开始属性相关正式编码。

## 1. 这个系统解决什么问题

保证任意最终数值都能回答「怎么算出来的」，并强制所有加成走同一管道，避免各系统直接改 Final。

## 2. 已冻结规则（勿在此重复改形状）

完整冻结条文见：[`33-architecture-core-rules-freeze-v0.1.md`](../30-tech/33-architecture-core-rules-freeze-v0.1.md) 第 1 节。

摘要：

- `Final = Base + Fixed + Percentage + SpecialRules`
- 必须 `AddModifier(source, …)`；禁止 `attr += x`
- Modifier 必须带来源；SourceKind 含天赋／灵根／功法／技能／境界／环境／建筑／状态／事件
- 内容「词条」落地为运行时 **AttributeModifier**

## 3. 待本文件展开（下一设计轮次）

- [ ] 属性 ID 枚举与配置表列
- [ ] Fixed／Percentage 运算顺序的最终公式表与单元测试用例
- [ ] SpecialRules 白名单
- [ ] 与灵力池、修为增长（非战斗属性）是否同管道或分管道
- [ ] UI 溯源面板信息架构

## 4. 验证方式（实现期）

- 给定 Base 与一组 Modifier，Final 与手算一致
- 移除某一 Source 后，Final 回到无该来源时的值
- 任何系统测试中不得出现对 Final 字段的直接赋值
