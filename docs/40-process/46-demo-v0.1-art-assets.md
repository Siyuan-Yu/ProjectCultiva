# Demo v0.1 美术资源需求表

> 状态：Prototype 资源规划草案 | 优先级：P0 | 最后更新：2026-07-31
> 上级：`docs/40-process/45-demo-v0.1.md`
> 关联：`docs/20-systems/23-combat.md`、`docs/20-systems/24-world-and-settlements.md`、`docs/20-systems/26-territory-management.md`、`docs/20-systems/27-characters-and-population.md`
> 已进入 Demo v0.1 原型开发；缺失资源用可替换占位 Sprite，不等待最终美术。
> 分批执行计划：`47-demo-v0.1-ai-art-batches.md`（第一批 ≤10，先验风格）。
> 当前接入清单：`48-demo-v0.1-minimum-art-integration.md`。

## 0. 分类规则

全文统一使用三类优先级：

| 分类 | 含义 | Prototype 处理 |
|---|---|---|
| **A｜必须拥有（影响玩法验证）** | 缺失后无法理解目标、状态或交互 | 必须有可辨识资源；允许低品质，但不能语义不清 |
| **B｜可以使用占位素材** | 缺失不阻断闭环，只影响观感 | 可用色块、几何图形、通用图标、单帧或复用素材 |
| **C｜后期再替换** | 正式版品质、美术差异化或高成本动画 | v0.1 不制作，先记录替换目标 |

核心原则：

1. **可读性优先于品质。** 玩家必须分清角色、敌我、灵地、资源、控制核心与可交互物。
2. **统一规格优先于单张精美。** 像素密度、视角、光源、轮廓与调色需一致。
3. **先做静态与占位，再做动画。** 不能因等待完整动画阻塞核心循环验证。
4. **AI 适合生成概念稿、静态角色、图标和单体物件；不擅长直接稳定生成可用的多方向逐帧 Sprite Sheet。**

---

## 一、整体美术风格定义

### 1.1 游戏视角

- **2D 俯视角**
- 推荐采用约 **3/4 俯视**，能同时看见地面与建筑正面
- 角色脚底占一个格子，身体可以向上超出格子
- 所有角色、建筑、树木保持同一俯视角度，不混用正侧视素材

### 1.2 Prototype 美术风格建议

推荐：**低细节中式修仙像素风**。

理由：

- 易于用格子拼接荒村地图
- 角色与资源在缩放后仍容易辨识
- 可以用低成本占位素材迭代
- 适合后续 AI 生成概念稿，再人工像素化、裁切与统一

正式版是否继续像素风，**待后续决定**。Demo 只验证玩法，不以最终美术品质作为成功标准。

### 1.3 色彩方向

| 场景／状态 | 推荐色彩 | 用途 |
|---|---|---|
| 荒村日常 | 土黄、灰褐、暗绿 | 表现贫困、劳役与普通生活 |
| 森林／农田 | 低饱和绿、棕色 | 自然资源区 |
| 隐藏灵地 | 青蓝、青绿、少量紫色 | 与凡俗区域形成明显区别 |
| 灵力／炼气 | 青白、淡蓝 | 统一修士能力视觉语言 |
| 危险／敌对 | 暗红、紫黑 | 主管敌意、妖兽危险、暴露风险 |
| 控制／管理 | 金色、暖白 | 控制核心、管理权限、选中状态 |

不要用高饱和特效覆盖角色轮廓。灵气效果要明显，但不能让地图常态处于“满屏发光”。

### 1.4 是否需要统一风格

**需要。** 即使使用占位素材，也至少统一：

- 视角
- 像素密度
- 轮廓粗细
- 光源方向（推荐左上）
- 阴影方向
- 饱和度范围
- 透明背景与裁切规则

可以暂时混用来源不同的素材，但必须经过统一缩放、调色与描边后再导入。

### 1.5 推荐尺寸规范（Prototype）

以下为 **v0.1 推荐基准**，不是正式版不可修改的最终标准：

| 资源 | 推荐源尺寸／单帧尺寸 | 备注 |
|---|---|---|
| 地图 Tile | **32×32 px** | 1 格基准 |
| 角色单帧画布 | **64×64 px** | 实际人物约 32×48 px；脚底锚点居中靠下 |
| 角色静态 Sprite | 64×64 px | 与动画帧共用画布 |
| 4 方向行走表 | 4 方向 × 4 帧；单帧 64×64 | 可组织为 256×256 Sheet |
| 8 方向行走表 | 8 方向 × 4～6 帧 | **C｜后期再替换** |
| 小型环境物件 | 32×32 或 64×64 px | 草药、石头、矿石等 |
| 建筑 | 32 px 网格倍数 | 如 4×5 格＝128×160 px，占位尺寸可调整 |
| 战斗特效单帧 | 64×64、96×96 或 128×128 px | 按覆盖范围分档 |
| UI 图标 | **64×64 px** 源图 | 游戏内可显示 32～48 px |
| 角色头像 | **256×256 px** | Demo 可由角色 Sprite 裁切放大 |
| NPC 对话头像 | 256×256 px | B 类可用同一头像变体 |
| UI 参考画布 | 1920×1080 | 使用可缩放布局；不锁死实际分辨率 |
| 9-Slice 面板 | 64×64 或 128×128 px | 边角保持清晰 |

### 1.6 后续 Unity 导入参考

进入实现阶段后建议：

- 像素资源：Filter Mode 使用 **Point**
- Compression：Prototype 阶段优先 **None**
- Sprite Mode：单图用 Single；Sprite Sheet 用 Multiple
- Pixels Per Unit：推荐 **32**
- Pivot：角色用 **Bottom Center**；物件按落地中心设置
- 透明背景：PNG RGBA
- Tile 边缘必须无白边、无半透明污染
- 文件名只用英文、数字、下划线

---

## 二、角色资源列表

### 2.1 角色总表

| 名称 | 类型 | 数量 | 用途 | 分类 | AI 生成需求 |
|---|---|---:|---|---|---|
| 初始角色 A | 玩家角色 | 1 | 三人分工；可被选为突破者 | **A** | 需要独立外观概念；无固定职业 |
| 初始角色 B | 玩家角色 | 1 | 三人分工；护法／采集／战斗 | **A** | 与 A 轮廓、服色明显不同 |
| 初始角色 C | 玩家角色 | 1 | 三人分工；探索／情报／战斗 | **A** | 与 A/B 明显不同 |
| 荒村主管 | NPC／敌对筑基修士 | 1 | 配额压迫、最终卡点、主管战 | **A** | 需要威压感；外观明显高于劳役者 |
| 商人 | NPC | 1 | 交易、情报、可能承接功法线索 | **A** | 静态概念与头像即可起步 |
| 守卫 | NPC／潜在敌人 | 1～2 个外观 | 门禁、巡逻、举报风险 | **A** | 可复用同一基础体型与换色 |
| 普通村民 | 群体表现 | 3 个外观变体 | 劳作、人口存在感 | **B** | 通用男女老少变体，可大量复用 |
| 学校候选人才 | 关键人才 | 最少 2 个候选 | 夺权后收弟子／任命管事 | **A（头像／卡片）** | 至少一名偏修炼、一名偏治理 |
| 初级妖兽 | 敌人 | 1 种 | 首场战斗教学 | **A** | 轮廓与村民、角色明显不同 |

### 2.2 初始三人要求

- 不固定职业，不做“战士／法师／牧师”制服。
- 三人通过发型、体型、服装颜色、随身工具区分。
- 服装仍是底层劳役者，不能开局就像高阶仙人。
- 三套外观都要支持后续成为修士：炼气后可以叠加灵力特效，不必重画整套角色。
- Prototype 可共用骨骼／帧结构，只替换人物图层。

### 2.3 每类角色的 Sprite 与动画需求

| 角色 | 静态 Sprite | 行走 | 普通攻击 | 技能释放 | 受伤 | Demo 处理 |
|---|---|---|---|---|---|---|
| 初始三人 | **A** | **A：4方向×4帧** | **A：可共用动作** | **A：仅突破者需要 1 个** | **B：闪白／后退代替** | 先共用动画框架，三种外观 |
| 主管 | **A** | **A：4方向，可减帧** | **A** | **B：1 个简化特效动作** | **B：闪白代替** | 必须能战斗、能被击败 |
| 商人 | **A** | **B：2～4帧或原地** | 不需要 | 不需要 | 不需要 | 可长期站定 |
| 守卫 | **A** | **A：4方向** | **B：与主管／玩家共用** | 不需要 | **B** | 巡逻可读性优先 |
| 普通村民 | **B** | **B：2～4帧** | 不需要 | 不需要 | 不需要 | 复用 3 个外观即可 |
| 学校候选 | **A：头像** | **B：复用村民** | 不需要 | 不需要 | 不需要 | 候选卡可先只有头像 |
| 初级妖兽 | **A** | **A：4方向或朝向翻转** | **A** | 不需要独立技能 | **B：闪白代替** | 1 种妖兽完成教学 |

### 2.4 角色资源的后期替换项

- 8 方向移动与战斗：**C**
- 每名角色独立攻击动作：**C**
- 独立受伤、倒地、死亡动画：**C**
- 主管独立法宝／筑基表现：**C**
- 村民年龄、职业、季节服装大规模变体：**C**
- 角色纸娃娃、换装系统：**C**

---

## 三、地图 Tile 与环境资源

### 3.1 地形 Tile

| 项目 | 数量建议 | 分类 | 外部 Sprite | 内部地图 | 可交互状态 |
|---|---:|---|---|---|---|
| 草地 | 3～5 个随机变体 | **A** | 是 | 否 | 否 |
| 土路 | 直线、转角、交叉、边缘 | **A** | 是 | 否 | 否 |
| 河流 | 水面、岸边、转角 | **B** | 是 | 否 | 否 |
| 石地 | 2～3 个变体 | **A** | 是 | 否 | 否 |
| 森林地表 | 暗草／落叶地 | **A** | 是 | 否 | 否 |
| 山地／崖壁 | 边缘与阻挡 Tile | **B** | 是 | 否 | 阻挡 |
| 农田 | 耕地、作物 1～2 阶段 | **A** | 是 | 否 | 工作／分配 |
| 隐藏灵地地面 | 青蓝／灵纹变体 | **A** | 是 | 否 | 修炼 |
| 妖兽区域地面提示 | 枯草、抓痕、骨骸 | **B** | 是 | 否 | 否 |

Prototype 不要求每种 Tile 都拥有完整自动拼接规则；能拼出一张可读地图即可。

### 3.2 环境物件

| 项目 | 数量建议 | 分类 | 外部 Sprite | 内部地图 | 可交互状态 |
|---|---:|---|---|---|---|
| 树 | 2～3 个变体 | **A** | 是 | 否 | 可采木材／阻挡 |
| 石头 | 2 个变体 | **A** | 是 | 否 | 可采集／阻挡 |
| 普通草药 | 1～2 个变体 | **A** | 是 | 否 | 可采集 |
| 灵草／敛息草 | 各 1 个 | **A** | 是 | 否 | 可采集；需高辨识度 |
| 木材资源堆 | 1 个 | **A** | 是 | 否 | 任务资源 |
| 矿石／矿堆 | 1～2 个 | **A** | 是 | 否 | 任务资源 |
| 农作物 | 1～2 个 | **B** | 是 | 否 | 群体生产 |
| 栅栏／木门 | 直线、转角、门 | **B** | 是 | 否 | 门可开关或直接穿越占位 |
| 火把／篝火 | 1 个 | **B** | 是 | 否 | 夜间提示 |
| 箱子／货架 | 各 1 个 | **B** | 是 | 否 | 仓库／商人提示 |
| 灵气粒子／漂浮光点 | 1 套 | **A** | 是 | 否 | 标记隐藏灵地 |

### 3.3 建筑资源

| 建筑 | 分类 | 外部 Sprite | 内部地图 | 可交互状态 | Demo 要求 |
|---|---|---|---|---|---|
| 凡人住宅 | **A** | 需要 | **不需要** | 人口容量／查看 | 1 个基础型可重复摆放 |
| 主管住所／主管府 | **A** | 需要 | **B：不进入也可** | 控制核心、耐久、夺取 | 必须与普通住宅明显不同 |
| 管理堂 | **B** | 可与主管府合并 | 不需要 | 打开管理 UI | v0.1 可复用主管府 |
| 仓库 | **A** | 需要 | 不需要 | 存储／任务交付 | 可用单一外观 |
| 学校／学塾 | **A** | 需要 | 不需要 | 打开人才候选 UI | 夺权后启用 |
| 洞府入口 | **B** | 需要 | 不需要 | 打开修炼界面 | 可与灵地交互点合并 |
| 山洞入口 | **A** | 需要 | **B：可用小型独立房间或遮罩区** | 进入隐藏灵地 | 必须易发现但不显眼 |
| 简易守卫岗／门禁 | **A** | 需要 | 不需要 | 守卫巡逻／门禁 | 标记夜间限制 |

### 3.4 建筑状态变体

| 状态 | 分类 | 处理 |
|---|---|---|
| 正常 | **A** | 基础 Sprite |
| 选中／可交互 | **A** | 轮廓高亮即可 |
| 控制核心受攻击 | **A** | 血条 + 闪白／抖动即可 |
| 控制核心耐久归零 | **A** | 破损遮罩或变暗 |
| 已占领 | **A** | 旗帜／颜色标记 |
| 建造中／升级中 | **C** | 正式版再做 |
| 多级破损 | **C** | 正式版再做 |

---

## 四、战斗资源

| 资源 | 用途 | 分类 | Prototype 方案 |
|---|---|---|---|
| 近战普通攻击轨迹 | 玩家／主管攻击反馈 | **A** | 1 套白色／淡黄弧线，可共用 |
| 命中效果 | 所有攻击命中反馈 | **A** | 小型闪光 + 闪白 |
| 炼气灵力攻击 | Demo 唯一主动技能 | **A** | 青白色短程弹体或掌风 |
| 灵力护体 | 炼气状态／护盾层反馈 | **A** | 半透明青色轮廓／圆盾 |
| 灵力消耗／恢复 | 灵力条变化的世界反馈 | **B** | 身体光点或 UI 动画 |
| 妖兽攻击效果 | 教学战反馈 | **A** | 抓击弧线／冲撞尘土 |
| 主管攻击／筑基威压 | 最终战威胁感 | **B** | 放大版灵力攻击 + 暗红提示 |
| 受伤反馈 | 命中可读性 | **A** | 闪白、红色数字、轻微后退 |
| 选择圈 | RTS 选中单位 | **A** | 玩家绿／友方蓝／敌方红 |
| 移动目标标记 | 右键移动反馈 | **A** | 地面短暂圆环 |
| 攻击目标标记 | 右键攻击反馈 | **A** | 红色准星／轮廓 |
| 控制核心受击 | 夺府玩法反馈 | **A** | 建筑血条、抖动、碎屑 |
| 突破灵气汇聚 | 第一次突破仪式感 | **A** | 向中心聚拢的青白粒子 |
| 敛息效果 | 隐藏修为状态 | **A** | 灵光收束／透明度短变，不长期常亮 |
| 完整法宝、复杂阵法、屏幕震动组合 | 正式战斗品质 | **C** | v0.1 不做 |

特效必须区分：

- 普通肉身攻击：白／黄
- 炼气灵力：青白／淡蓝
- 敌对危险：暗红／紫黑
- 敛息：光效“收回”，不是新增更亮光圈

---

## 五、UI 资源

### 5.1 UI 总表

| UI | 必要元素 | 分类 | 占位方式 |
|---|---|---|---|
| 战斗 HUD | 选中单位、目标、血／灵力、技能 | **A** | 纯色面板 + 文字 |
| 生命条 | 当前／最大生命 | **A** | 红色条 |
| 灵力条 | 炼气后出现 | **A** | 青蓝条；感应境隐藏或置灰 |
| 技能栏 | Demo 只需 1 个技能槽，可预留 6 格外框 | **A** | 1 个可用图标 + 5 个锁定格 |
| 角色头像／队伍栏 | 三人选择与状态 | **A** | Sprite 裁切头像 |
| 时间控制 | 暂停、1x、2x、5x | **A** | 通用播放图标 |
| 时间表界面 | 查看／夺权后修改工作、休息、娱乐 | **A** | 色块时间条 |
| 工作任务面板 | 今日配额、进度、截止时间 | **A** | 文字列表 |
| NPC 对话／信息 | 姓名、头像、关系、任务／交易 | **A** | 通用窗口 |
| 商店 UI | 买卖敛息草、药材等 | **B** | 列表窗口 |
| 修炼 UI | 地点灵气、修炼者、进度、开始／停止 | **A** | 通用面板 |
| 突破 UI | 资格、地点、状态、护法者、开始突破 | **A** | 图标 + 文本，不需复杂仪式界面 |
| 境界显示 | 感应境／炼气 | **A** | 文字标签 |
| 灵根显示 | 属性倾向数值 | **A** | 文字 + 简单属性图标 |
| 功法显示 | 第一份功法、是否运行 | **A** | 单卡片 |
| 敛息状态 | 剩余时效、资源数量、暴露风险 | **A** | 状态图标 + 计时 |
| 人口管理 | 总人口、岗位分配 | **A** | 数字与加减／比例控件 |
| 建筑管理 | 建筑列表、状态、控制核心耐久 | **A** | 列表 + 高亮 |
| 学校人才 | 候选头像、灵根／悟性／神识／性格／Tag | **A** | 候选卡片 |
| 任命管事 | 人选与治理职责 | **A** | 候选卡 + 任命按钮 |
| 主管愤怒／风险 | 当前风险反馈 | **A** | 单条风险条或阶段标签 |
| 小地图 | 地图导航 | **B** | Demo 地图小可暂不做 |
| 完整设置／存档 UI | 完整产品功能 | **C** | v0.1 可用开发按钮 |

### 5.2 UI 视觉建议

- Prototype 使用深灰／棕色半透明底板，青色表示灵力，金色表示控制权。
- 同一功能只用一种颜色，不让“青色”同时表示灵力、友方、可交互三种含义。
- 所有重要 UI 必须有文字，不依赖图标猜测。
- 面板框、按钮、标签可先共用 1 套 9-Slice。

---

## 六、图标资源

| 图标 | 数量 | 分类 | 备注 |
|---|---:|---|---|
| 灵石 | 1 | **B** | Demo 若无灵石经济可后置 |
| 普通草药 | 1 | **A** | 采集／交易 |
| 灵草 | 1 | **A** | 修炼资源 |
| 敛息草 | 1 | **A** | 必须与普通灵草明显不同 |
| 木材 | 1 | **A** | 工作任务 |
| 矿石 | 1 | **A** | 工作任务 |
| 肉食／妖兽材料 | 1～2 | **B** | 教学战掉落 |
| 第一份功法 | 1 | **A** | 功法卡／背包 |
| 唯一技能 | 1 | **A** | 技能栏 |
| 灵力护体 | 1 | **A** | 状态／技能 |
| 感应境 | 1 | **B** | 可先用文字 |
| 炼气 | 1 | **B** | 可先用文字 |
| 灵根属性 | 至少 Demo 实际用到的 1～3 个 | **B** | 无需一次做全属性 |
| 工作／休息／娱乐 | 各 1 | **A** | 时间表 |
| 修炼／护法／放哨／采集／探索 | 各 1 | **A** | 三人分工 |
| 人口／农民／工匠／管事／弟子 | 各 1 | **A** | 管理与人才 |
| 控制核心／占领 | 各 1 | **A** | 夺府反馈 |
| 暂停／播放／2x／5x | 各 1 | **A** | 时间控制 |

图标源文件统一 64×64 px、透明 PNG。Prototype 可使用单色剪影；正式版再替换为带材质与边框的图标。

---

## 七、动画需求

### 7.1 角色动画

| 动画 | v0.1 要求 | 分类 | 帧数建议 |
|---|---|---|---:|
| 待机 | 可静态或 2 帧呼吸 | **B** | 1～2 |
| 4 方向移动 | 玩家、主管、守卫、妖兽需要 | **A** | 每方向 4 |
| 8 方向移动 | 不影响核心验证 | **C** | 每方向 4～6 |
| 普通攻击 | 玩家、主管、妖兽 | **A** | 3～5 |
| 技能释放 | 选中的炼气角色 | **A** | 3～5；也可复用攻击动作 |
| 受伤 | 闪白／后退代替 | **B** | 0～2 |
| 倒地／死亡 | 单帧倒地图可占位 | **B** | 1 |
| 修炼 | 角色静坐 + 灵气特效 | **A** | 1～4 |
| 突破 | 静坐 + 聚气特效 | **A** | 角色动作可复用修炼 |
| 采集／工作 | 通用挥动动作 | **B** | 2～4 |
| 放哨／护法 | 待机 + 状态图标 | **B** | 0 |

### 7.2 环境动画

| 动画 | 分类 | Prototype 方案 |
|---|---|---|
| 水流 | **B** | 2～4 帧循环；无河流可不做 |
| 火焰／篝火 | **B** | 3～4 帧循环 |
| 灵气漂浮 | **A** | 4～8 帧／粒子循环 |
| 隐藏灵地呼吸光 | **A** | 缓慢明暗变化 |
| 农田劳作 | **B** | 村民通用工作动作 |
| 树叶摇动 | **C** | 正式版再做 |
| 天气、昼夜完整变化 | **C** | Demo 可用整体色调叠加 |

### 7.3 动画制作顺序

1. 单帧验证尺寸与锚点
2. 玩家 4 方向移动
3. 妖兽移动与攻击
4. 玩家／主管普通攻击
5. 修炼与突破特效
6. 其他 NPC 移动
7. 正式版再补 8 方向、独立受伤与复杂技能

---

## 八、AI 生成提示词模板

### 8.1 通用风格尾缀

所有 Prompt 都追加同一段：

```text
2D top-down 3/4 view game asset, low-detail Chinese xianxia pixel art style,
32-pixel tile scale, consistent proportions, muted earthy palette,
top-left lighting, clean readable silhouette, transparent background,
no text, no watermark, no UI frame
```

负面提示词：

```text
photorealistic, 3D render, side view, front portrait view, isometric mismatch,
blurry, anti-aliased white edge, complex background, text, logo, watermark,
extra limbs, inconsistent costume, cropped feet
```

> 注意：AI 生成的像素图常含伪像素、尺寸不一致与透明边污染。生成后必须人工裁切、降色、统一像素密度和锚点。

### 8.2 角色静态 Sprite 模板

```text
[CHARACTER DESCRIPTION],
full-body standing sprite, feet visible, centered,
simple readable clothing layers, clear silhouette,
2D top-down 3/4 view game asset, low-detail Chinese xianxia pixel art style,
64x64 sprite canvas, 32-pixel tile scale,
transparent background, consistent top-left lighting,
no text, no watermark
```

示例变量：

- `[CHARACTER DESCRIPTION]`：young impoverished village laborer, dark brown linen clothes, tied hair, carrying a woodcutting tool
- 主管：foundation-establishment cultivator supervisor, dark teal robe, restrained spiritual aura, authoritative silhouette
- 商人：traveling village merchant, layered cloth robe, shoulder bag, friendly but cautious

### 8.3 角色方向稿／动画参考模板

AI 不直接交付最终 Sprite Sheet，先生成方向参考：

```text
character turnaround reference sheet for [CHARACTER DESCRIPTION],
four directions: front, back, left, right,
same exact costume and proportions in every view,
neutral standing pose, separated figures,
flat neutral background, no perspective distortion,
[COMMON STYLE SUFFIX]
```

生成后由人工／专用 Sprite 工具制作逐帧动画。

### 8.4 妖兽模板

```text
small low-level xianxia beast, [BEAST DESCRIPTION],
hostile but suitable for early-game tutorial,
compact readable silhouette, full body, feet visible,
top-down 3/4 view, 64x64 sprite canvas,
[COMMON STYLE SUFFIX]
```

### 8.5 地图 Tile 模板

```text
top-down fantasy village tileset,
[TILE TYPE: grass / dirt road / river bank / stone ground / farmland],
Chinese xianxia cultivation game style,
seamless 32x32 pixel tile, tileable edges,
low-detail pixel art, muted earthy palette,
orthographic top-down 3/4 compatible, no objects, no text
```

### 8.6 环境物件模板

```text
[OBJECT DESCRIPTION],
single isolated environment prop for a top-down xianxia village game,
fits a 32x32 or 64x64 pixel canvas,
clear interaction silhouette, transparent background,
[COMMON STYLE SUFFIX]
```

### 8.7 建筑模板

```text
[BUILDING DESCRIPTION],
small rural Chinese xianxia village building,
top-down 3/4 view, complete exterior, visible roof and front wall,
aligned to a 32-pixel grid, footprint [WIDTH]x[HEIGHT] tiles,
low-detail pixel art, muted wood and earth materials,
transparent background, no surrounding terrain, no text
```

主管府提示词应加入：`more authoritative than ordinary houses, visible courtyard gate, control-core landmark`.

### 8.8 特效模板

```text
[EFFECT DESCRIPTION],
2D top-down game VFX sprite, xianxia spiritual energy,
cyan-white glow, readable compact shape, transparent background,
sprite sheet with [FRAME COUNT] evenly spaced frames,
no character, no environment, no text, no watermark
```

### 8.9 UI 图标模板

```text
single game UI icon of [ITEM OR ACTION],
Chinese xianxia pixel art, centered object,
strong readable silhouette, limited color palette,
64x64 pixels, transparent background,
no text, no border, no watermark
```

### 8.10 AI 生成验收清单

- [ ] 视角与现有资源一致
- [ ] 轮廓在 50% 缩放下仍可辨识
- [ ] 无文字、水印、额外肢体
- [ ] 透明背景干净
- [ ] 脚底／建筑底边未被裁切
- [ ] 可对齐 32 px 网格
- [ ] 同角色不同方向服装、发型、颜色一致
- [ ] 色彩未超出统一调色范围

---

## 九、文件目录与命名建议

进入 Unity 实现阶段后建议：

```text
Assets/
└── Art/
    ├── Characters/
    │   ├── Players/
    │   │   ├── Player_A/
    │   │   ├── Player_B/
    │   │   └── Player_C/
    │   ├── NPCs/
    │   │   ├── Supervisor/
    │   │   ├── Merchant/
    │   │   ├── Guard/
    │   │   ├── Villagers/
    │   │   └── Talents/
    │   └── Enemies/
    │       └── Beast_01/
    ├── Environment/
    │   ├── Tiles/
    │   │   ├── Ground/
    │   │   ├── Water/
    │   │   ├── Farmland/
    │   │   └── Cliffs/
    │   ├── Props/
    │   │   ├── Trees/
    │   │   ├── Rocks/
    │   │   ├── Plants/
    │   │   └── Resources/
    │   └── Buildings/
    │       ├── Houses/
    │       ├── SupervisorHouse/
    │       ├── Warehouse/
    │       ├── School/
    │       └── Cave/
    ├── Effects/
    │   ├── Combat/
    │   ├── Cultivation/
    │   ├── Breakthrough/
    │   └── Concealment/
    ├── UI/
    │   ├── HUD/
    │   ├── Panels/
    │   ├── Schedule/
    │   ├── Population/
    │   ├── Cultivation/
    │   └── Talent/
    ├── Icons/
    │   ├── Items/
    │   ├── Skills/
    │   ├── Actions/
    │   ├── Realms/
    │   └── Management/
    ├── Portraits/
    ├── Palettes/
    ├── Source/
    │   ├── AI_Generated/
    │   └── Working/
    └── Placeholder/
```

### 9.1 命名规则

```text
角色：CHR_PlayerA_Walk_Down_00.png
NPC：CHR_NPC_Supervisor_Attack_Left_02.png
敌人：CHR_Enemy_Beast01_Idle_Down_00.png
地块：TILE_Ground_Grass_01.png
物件：PROP_Herb_Concealment_01.png
建筑：BLD_SupervisorHouse_Normal.png
特效：VFX_QiAttack_00.png
UI：UI_Panel_Schedule.png
图标：ICON_Item_Herb_Concealment.png
头像：PORTRAIT_PlayerA.png
```

Source 原图与游戏导入成品分开保存。AI 生成原图不得直接覆盖裁切后的正式导入文件。

---

## 十、优先级与开发顺序

### 第一批｜Demo 必须（先验证灰盒到可读）

1. 统一色块／占位调色板
2. 草地、土路、石地、农田、森林、灵地地面
3. 三名玩家静态 Sprite + 4 方向移动
4. 主管、守卫、妖兽静态 Sprite + 最小移动／攻击
5. 主管府控制核心、住宅、仓库、学校、山洞入口
6. 树、石头、木材、矿石、普通草药、敛息草
7. 选择圈、移动标记、攻击标记
8. 普通攻击、命中、灵力攻击、灵力护体、突破聚气、敛息反馈
9. 生命条、灵力条、1 格技能栏、三人队伍栏、时间控制
10. 时间表、工作任务、修炼／突破、人口、学校人才的占位 UI
11. 必需图标：木材、矿石、草药、敛息草、功法、技能、时间表动作、分工、人口与占领

完成条件：无需文字解释也能分清“谁、在哪、能做什么、是否暴露、是否夺权”。

### 第二批｜增强体验（不阻塞核心闭环）

1. 商人独立行走、村民 3 个变体与简单劳作动画
2. 河流、水流、篝火、更多环境装饰
3. NPC 对话头像与学校候选独立头像
4. 主管独立技能特效、妖兽受伤／倒地
5. 建筑受损遮罩、占领旗帜
6. UI 面板统一皮肤与 9-Slice
7. 灵地呼吸光、昼夜整体色调
8. 更多物品与状态图标

### 第三批｜正式游戏后期再替换

1. 8 方向完整移动／攻击／受伤／死亡动画
2. 每个角色独立战斗动作、换装与纸娃娃
3. 高品质功法、法宝、阵法、境界异象
4. 完整建筑内部、建造／升级／多级破损
5. 多生物群系、季节、天气与昼夜动画
6. 正式 UI 皮肤、完整图标库、角色立绘
7. 多种妖兽、更多主管／宗门角色
8. 正式版最终画风重制

## 11. 资源总量粗估（v0.1）

| 类别 | 第一批粗估 |
|---|---:|
| 独立角色／敌人外观 | 3 玩家 + 主管 + 守卫 + 妖兽＝6 套 |
| NPC／村民占位外观 | 商人 + 3 村民变体＝4 套 |
| 角色头像 | 3 玩家 + 主管 + 商人 + 2 人才＝7～8 张 |
| 地形 Tile／边缘件 | 约 20～35 个 |
| 环境物件 | 约 12～20 个 |
| 建筑 | 约 6～8 个 |
| 战斗／修炼特效 | 约 8～12 套 |
| UI 面板／控件 | 约 10～15 类 |
| 图标 | 约 20～30 个 |

这是用于评估工作量的粗估，不是必须一次生成完的采购清单。

## 12. 未决问题

- [ ] Prototype 最终选像素风，还是无像素的低细节手绘风？本文暂按 32px 像素基准规划。
- [ ] 荒村地图精确格子尺寸与建筑占地。
- [ ] 初级妖兽具体种类。
- [ ] 第一份功法与唯一技能的视觉属性。
- [ ] Demo 是否出现河流；若不出现，河流 Tile 推迟到第二批。
- [ ] 学校候选首批数量与外观差异。
- [ ] 主管府是否允许进入内部；建议 v0.1 只做外部攻击与管理面板。
- [ ] 美术素材来源许可与商用授权记录方式。
