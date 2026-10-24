# 运行对象生命周期与NPC访问边界复查

> 2026-10-23，继续323／329已授权的全项目审查与优化；不增加美术精细度，不修改未定玩法。本页记录当前定向增量，不要求另起一轮重复验收。当前Implemented / Producer Acceptance Pending，未人工验收；不启动Unity、不创建或运行测试、不提交推送。

实施前证据：HostDynamicOpportunityObjectPresenter只有ID字典、没有世界实例身份，世界替换且ID重用时可能复用旧视图；每帧新建HashSet和List。HostGroundLootPresenter在Session.World为空时直接返回、不清理视图，移除单件也没有显式从物件拾取登记注销，且每次刷新创建旧ID列表。ChessRecreationService.TryJoin只为优先座位检查路径，左侧不可达时跳过整张棋盘，即使另一侧空闲。洞府手动战斗需继续核对同源视线守门。

本轮范围：修复上述可见对象清理、会话替换、临时分配及空闲座位选择；复用已有Host逻辑网格统一手动交战／反击视线，不改伤害、射程、资源、AI性格或离屏战斗抽象。保留所有既有内容、场景、Prefab、美术和工作区修改。

追加已确认的同范围问题：HostFormalHud另有一份任务目标摘要，未同步329的故事旗／功法名称显示；改为共用Core只读查询。任务日志窗口对IMGUI同帧多事件重复构造条目，使用按帧／世界／主体验证的短缓存，接取领奖放弃及事件到达显式失效，不缓存玩法结算。

## 实际完成

| 项目 | 最终行为 | 主要文件 |
|---|---|---|
| 动态机会世界替换 | 记录SimulationWorld实例，换存档／新开局先清理本人旧视图，复用ID不会沿用旧世界的位置。外部已销毁视图先移除旧字典键再创建，避免Add重复键异常 | HostDynamicOpportunityObjectPresenter.cs |
| 动态机会清理与分配 | wanted／remove集合复用；移除先禁用视图再Destroy，登记只删本显示器拥有的ID，不清空其他拥有者的全局条目；禁用物件不能拾取／解析 | HostDynamicOpportunityObjectPresenter.cs、HostDynamicWorldObjectRegistry.cs |
| 地面物品清理 | Session为空立即清理；单物件移除显式注销Plot，整体清理注销Owner；缓存Plot组件与旧ID列表。隐藏物件不继续被选择，旧选框在目标隐藏后清除 | HostGroundLootPresenter.cs、HostMapObjectRegistry.cs、HostHousingAreaSelection.cs |
| 下棋独立访问 | 同一棋盘按原优先顺序尝试每个空闲座位；A不可达则继续检查B，不因一边被挡就拒用另一边。保留敌对共用门禁、原距离／日程与独立娱乐规则 | ChessRecreationService.cs |
| 洞府手动交战 | 手动攻击／反击／弹体命中补同源WalkGrid视线；被墙隔开时寻找有视线的可达接敌点。命中额外复核射手可战斗；不改伤害／射程／自动不补刀政策 | HostNpcMeleeAssault.cs、HostMoveController.CharacterSpacing.cs |
| 任务显示一致性 | 日志和HUD共用QuestJournalQuery.DescribeObjectives，移除Host中重复且落后的两份摘要；故事标记、功法和地点名称不因切换界面退回内部字段 | QuestJournalQuery.cs、HostFormalHud.cs |
| 任务窗口分配 | 按同帧／同世界／同主体复用列表，事件到达、开窗、会话清理、接取／领奖／放弃显式失效。只缓存UI投影，不缓存接取与奖励资格 | HostQuestJournal.cs |

独立遭遇的视线辅助也转用同一个MoveController入口；角色脚点预约不加入视线遮挡。隐藏的可破坏物既不响应鼠标，也不作为现场最近资源候选，但索引保留其条目以支持重新启用。

## 验证结果

- 8个程序集离线基础编译全部通过；最后两处隐藏目标接线之后又单独编译Unity程序集通过。原CS0105、CS1718警告保留，未新增警告。
- `git diff --check`无空白错误；本轮未改Content数据、场景、Prefab、美术资源、Packages、ProjectSettings或存档格式。没有必要为这批纯运行增量重新发布外置编辑器。
- 未编写／运行自动测试、PlayMode、存档回放或headless，未启动Unity；未实测FPS和GC。编译通过不代表运行验收。
- 保留329及此前全部工作区修改，未提交、推送或封板。

## 最短人工复看

1. 使用有地上物品／动态机会的存档，再读另一个存档或新开局：旧物件、名称与选框不留在原地；新物件只出现一次。拾取／解决后不能再点中旧物件。
2. 棋盘一侧被建造物阻挡时，仍有空闲可达另一侧可用；保持原单人即娱乐、另一人退出不连带中断。
3. 洞府内两人隔墙时不直接互砍／射穿，绕到有视线处后继续；正常开阔地伤害与反击保持。
4. 追踪认人或功法任务，HUD目标和J日志描述一致；接取、领奖、放弃后列表仍更新，不因短缓存保留旧资格。

## 后续值得投入的工作

下一步优先制作人连续试玩现有首段和现场看首次地图加载／密集人物／墙体排序开销，再按证据处理地形解码、纹理上传或感知热点；不预先通过提高美术精度或全框架改造解决。329列出的主管交权门槛、告密、高阶能力和冷区技能抽象仍是未定规则，本轮没有越过它们。
