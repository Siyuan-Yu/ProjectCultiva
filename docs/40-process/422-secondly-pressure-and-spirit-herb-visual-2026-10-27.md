# 每秒违规压力与药田灵气视觉

制作人确认：玩家在任一当班主管／巡卫的45格巡视范围内，未按当前课表休息／工作，被发现后每秒加1点主管压力；按既有WorldTick口径，倍速推进、暂停停止，多个观察者重叠不叠加。满100仍接既有真实追赶与战斗，解放后原强制劳役惩戒停用，其余观察资格不变。

实施同时检查真实执行回执：ObservedLaborComponent此前未进入Entity白名单，Host农作写入会失败，须补全；仅实际农作Work阶段续回执，停止、失能、取消或离开工作阶段清理，不把单纯站在田旁当作工作。真实Rest／BedRest与既有劳动动作仍沿原判定。

药田保留现有植物贴图与四阶段，将正式贴图分支也接入蓝绿冷色调；成长与成熟期稀疏显示静态青蓝灵光，不逐格添加动画Update／灯光／粒子。成熟仍能辨认，损坏仍暗淡，不改农田、田块尺寸、5日／10日周期和产量。

状态：Design Confirmed；基础编译与最少静态引用检查后单独记录Implemented／待人工验收。不启动Unity、不运行自动测试、不自动提交推送。现有当前版本存档可继续，版本／Snapshot不变。

人工验收：1倍速违规且在任一巡视圈内连续观察5秒，应约加5点；重叠圈仍相同速度，暂停不涨，倍速相应增加。真实睡眠／耕作停止增长，离开圈停止；正常工作被取消后不能继续免罚。查看药田空地／成长期／成熟／损坏与农田颜色差异，成熟草药仍辨识，灵光不闪烁。

## 实施结果

Implemented / Producer Acceptance Pending。实际改动：SupervisorAngerDriver移除五Tick累计，保留LastObservationTick防重复；ObservedLaborComponent补Entity白名单；HostFarmFieldLabor仅Work阶段续写并在非工作／ClearActivity清除；HostMapPlotCell正式贴图分支接HerbTint与稀疏HerbSpiritGlint，贴图共享、无逐格更新组件。

Core 362、Data 86、Unity 175、Unity.Editor 7源文件离线基础编译通过，最少静态引用／diff检查完成。未启动Unity、未运行自动测试，不将编译视作运行验收。无需重新发布地图或增加Snapshot字段，版本0.0.47／v21保持，未提交推送。
