# PJCultiva 美术素材批次 Batch_20261016_01

## 这次已经替你做好的事

- 10 张 ChatGPT 原始生成图已按 **现有 273 的 AssetID** 重命名，**画面像素未更改**（只做了文件复制与命名）。
- 组合图仍然是组合图，不等于单件 Sprite 已裁切或可用于动画。
- 图片全部是“AI 首版候选”，未筛选、未拆图、未 Unity 接入、未人工验收。
- `ArtSource/BatchInfo/Batch_20261016_01/asset_manifest.json` 是每张图片的精确对应清单。
- `ArtSource/References/ART-REF-001_huangcun_style_reference.png` 是参考样张，不是待接入的素材。

## 如何使用

1. 先让 Cursor 按上一个指令创建/确认固定美术目录。
2. **将本 ZIP 直接解压至 Unity 项目根目录（与 Assets、Content、docs 同级）**，ZIP 内部已经有 `ArtSource/Inbox/` 路径。
3. 对 Cursor 说：“整理 ArtSource/Inbox/ 中刚收到的 Batch_20261016_01。优先读取 ArtSource/BatchInfo/Batch_20261016_01/asset_manifest.json，按对应关系处理；不要靠图片文件名猜。只检查、建候选记录和预览，暂不接入 Unity，不提交 Git。”
4. 新生成的图片仍可先投到 `ArtSource/Inbox/`，如果未命名，让 Cursor 结合图片与 273 标成“待确认”，不可盲猜。

## 特别关注

- `ART-ROOM-008` 组合图的**控制台有高台和台阶，不符合开面无台阶要求**，只能暂列返工候选；旗帜可独立检查。
- `ART-GROUND-005` 合并了围栏和成熟作物，不能取代分阶段生长层。
- `ART-ROOM-001` 组合图需分拆验证拼接与门洞；`ART-CHAR-*` 只是静态人物，不是动画。
- 参考样张并非真实游戏截图，所有视角、比例、遮挡仍须在 LevelTester 运行验证。

## 收到文件后 Cursor 可用的简短指令

整理美术收件箱。当前批次为 Batch_20261016_01。以 ArtSource/BatchInfo/Batch_20261016_01/asset_manifest.json 为图与 AssetID 的明确映射；它已经过人工整理，不要使用模糊文件名猜测替代。保留原图及 SHA-256，不覆盖任何文件；对组合图建立子件记录并标识待裁切，输出检查及总览。按 273 维护候选追踪，保持真实“未接入/未验收”状态。不要修改 Unity/Content、不要 Git 提交。
