# PJCultiva 美术总包｜第一、二批

## 内容

- 素材原图：20张PNG（第一批10张，第二批10张），所有原图均已按现有AssetID命名。
- 风格参考：1张，放在References，单独计数。
- 总清单：`ArtSource/BatchInfo/All_20261016_01_02/asset_manifest.json`。
- 中文用途索引：`ArtSource/BatchInfo/All_20261016_01_02/asset_index_中文.md`。
- 20张总览：`ArtSource/BatchInfo/All_20261016_01_02/contact_sheet_all_20.jpg`。
- 保留第一批BatchInfo，并补齐第二批BatchInfo。

## 放到项目里

将压缩包内的`ArtSource`文件夹合并到Unity项目根目录（与Assets、Content、docs同级）。
不要再套一层同名文件夹。请不要强制覆盖自己已经修改过的同名文件。
本包不含项目全局ArtSource/README，因此不会替换已有收图工作流说明。

第一批文件名和字节保留原样；已经保存或整理过第一批时，Cursor根据SHA-256跳过重复，只处理第二批新增文件。
我方没有访问或改变你本机工程，本次完成的是交付包。

## 一句交接

读取`ArtSource/BatchInfo/All_20261016_01_02/asset_manifest.json`，按清单整理`ArtSource/Inbox`；按SHA-256跳过已整理图片，保留原图，暂不接入Unity。

## 状态与限制

所有文件都是AI首版候选；原图未切片、未接入、未运行验收。
组合图、状态图、地表块并不自动等于可直接运行的Sprite、无缝图集或独立分层。
本包保留已生成内容，包括已指出需返工的控制台，以及需求有重叠的药田A/B方案。
具体问题见manifest各行notes，不为适配图片而更改玩法规则。
名称归属以manifest为准，不要求Cursor猜中文自动文件名。
本包没有修改273 TSV、XLSX、正式资源引用、Content JSON或Git工作区。
源PNG均逐字节保留；缩略总览是另行生成的查看文件，不替代原图。
