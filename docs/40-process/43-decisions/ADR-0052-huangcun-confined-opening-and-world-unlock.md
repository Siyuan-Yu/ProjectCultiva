# ADR-0052：荒村封闭开局与外界解锁

> 状态：Accepted（制作人2026-10-23明确授权）／Implemented／人工验收待完成。实施与验收记录：[332](../332-huangcun-confined-opening-and-liberation-2026-10-23.md)。

正式荒村开局在真实取得荒村所有权前限制于当前作者围墙围成的区域，外界自主模拟暂缓；取得荒村后永久解除这一开局限制并启用外界及势力体验。参考开局不启用。

- 准入由Core统一校验，Host只转换坐标、阻止非法表现移动和显示原因，不能用UI隐藏代替规则。范围以当前Surface作者几何绑定，行政控制和围墙耐久仍沿用既有authority。
- 启用与解锁使用已有WorldFlagBoard持久旗标；区域几何从内容重绑，不新增Snapshot数值或第二份所有权真源。解锁依据既有真实Site占领，不设新的境界门槛，也不以对话或独立代替占领。
- 外部真实Character、库存、关系和地图仍保留身份，暂缓其自主变化；不删除、不克隆、不以匿名人口替代。WorldTick仍唯一，外界恢复时不补算封闭期的需求和旅行预算。
- 这是制作人对此前自由出村首段的定向覆写，不改变已冻结Continuous Surface、Squad、CharacterEncounter和Core／Data边界。
