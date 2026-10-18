# Batch_ModularRig_002 生成说明

生成方式：Codex 内置 `image_gen`，透明背景；以 `Batch_ModularRig_001/Originals/ART-CHAR-001_HumanCommonRig_parts_v01.png` 为九部件排布、比例和画风参考，逐套独立编辑。每套要求保留 1254×1254 的 3×3 格：正面无袖躯干、正面头、右臂；左臂、左腿、右腿；背面无袖躯干、后脑、侧面无袖躯干。部件应独立，背景透明，无文字和整身人物。侧头图使用相应部件图作身份参考，东向侧脸，西向由运行骨架镜像。

| 输出 | 身份与绘画提示 |
| --- | --- |
| `ART-CHAR-002_companion_a_HumanCommonRig_parts_v01.png` | 年轻女性同伴，青绿与米白旅装、束发、温和而利落的面容 |
| `ART-CHAR-002_companion_b_HumanCommonRig_parts_v01.png` | 成年男性同伴，赭红与炭黑旅装、束发、不同于主角的面容 |
| `ART-CHAR-003_supervisor_HumanCommonRig_parts_v01.png` | 村落主管，靛蓝与褐色层叠执事袍、深色冠帽、严肃成年面容 |
| `ART-CHAR-005_hoe_HumanCommonRig_parts_v01.png` | 田间劳作者，土黄与橄榄绿短衫、卷袖、头巾和绑腿 |
| `ART-CHAR-005_apron_HumanCommonRig_parts_v01.png` | 围裙居民，赭黄与灰蓝衣服、明显的米色围裙、女性面容 |
| `ART-CHAR-005_basket_HumanCommonRig_parts_v01.png` | 背篓居民，褐绿厚实短衫、肩带、较年长面容；背篓本体独立挂点 |
| `ART-CHAR-006_merchant_HumanCommonRig_parts_v01.png` | 商店/拍卖执事共用服装，暗紫金边、整洁发冠、成年面容 |
| `ART-CHAR-007_other_HumanCommonRig_parts_v01.png` | 采药/伐木/散人通用衣装，松绿与炭灰短衫、年长女性面容 |
| `ART-CHAR-007_jiang_lao_HumanCommonRig_parts_v01.png` | 江老独立老人外观，银发、白须、藏蓝灰衣，保持九部件结构 |
| `ART-CHAR-002_003_005_HumanCommonRig_sideheads_v01.png` | 同伴 A、同伴 B、主管、锄具居民四名东向侧头，四列独立 |
| `ART-CHAR-005_006_007_HumanCommonRig_sideheads_v01.png` | 围裙、背篓、执事、通用劳作者四名东向侧头，四列独立 |
| `ART-CHAR-007_jiang_lao_HumanCommonRig_sidehead_v01.png` | 江老银发白须东向单侧头 |

首版图实际可能触及分格边界，运行时使用严格单格切片；交付后还需在 Unity 常规镜头检查接缝、裁切和剪影。生成图与运行副本的字节哈希见 `manifest.json`。
