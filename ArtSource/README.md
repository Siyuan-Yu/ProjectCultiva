# 美术源文件收件区

`ArtSource` 保存美术参考、生成原图和批次整理记录。它位于 Unity `Assets` 之外，不是运行资源目录，也不直接参与 Unity 导入。

## 日常使用

1. 把 ChatGPT 普通会话生成的 PNG、JPG、JPEG 或 WebP 图片放进 `ArtSource/Inbox/`。
2. 可以保留中文、空格文件名，不需要逐张改名、分类或指定 AssetID。
3. 告诉 Cursor / Codex“整理美术收件箱”，由其在仓库根目录运行：

   ```powershell
   python tools/organize-art-inbox.py
   ```

   如果本批附带 `ArtSource/BatchInfo/<批次ID>/asset_manifest.json`，则运行：

   ```powershell
   python tools/organize-art-inbox.py --source-manifest ArtSource/BatchInfo/<批次ID>/asset_manifest.json
   ```

   来源 manifest 中的图片路径、AssetID、子件、顺序和备注优先于文件名推断；缺图、清单外图片、未知 AssetID、哈希、尺寸或 Alpha 不一致会停止该批整理。

4. 工具只处理尚未入库的新内容。它按文件内容 SHA-256 去重，把原始字节复制到新批次，完成基础技术检查，并生成总览图和 `manifest.json`。
5. 在 `ArtSource/Batches/<批次ID>/Previews/contact-sheet.png` 查看总览。透明图片同时提供浅底和深底检查效果。

工具不会移动、删除或改写 `Inbox` 文件，因此不需要手动清空收件箱。相同内容再次放入会报告重复并跳过；同名但内容不同的文件不会覆盖已有原图。

## 目录职责

- `Inbox/`：唯一日常投放入口。
- `References/`：已确认的整体风格参考图，不等于运行 Sprite。
- `Batches/<批次ID>/Originals/`：保留 Inbox 相对路径、原文件名和原始字节的保真副本。
- `Batches/<批次ID>/Candidates/`：选图后制作的命名、裁切或加工候选；收件整理不会自动生成加工素材。
- `Batches/<批次ID>/Previews/`：单图检查预览和带编号、文件名的批次总览。
- `Batches/<批次ID>/manifest.json`：文件哈希、尺寸、Alpha、问题、候选映射及后续加工记录。

manifest 只负责文件追溯和候选映射。`docs/40-process/273-current-art-asset-register.tsv` 仍是全局制作、接入和验收进度的唯一可编辑真源，XLSX 只由 TSV 导出。

## 结果含义

- 技术检查区分“有 Alpha 通道”和“实际有透明像素”。画出来的棋盘格、纯黑或纯白背景仍是不透明像素。
- 透明边缘、疑似裁切、尺寸等只标记为“需要复核”，不能替代视觉验收。
- 文件名包含现有 AssetID 时，工具会对照 273 校验并记录候选映射。文件名没有 AssetID、ID 不存在或归属有歧义时，保持“待确认映射”。
- 一张组合图可以对应多个 AssetID 或子件，多张图片也可以作为同一 AssetID 的不同候选。工具不会按连通区域强制切图。
- 收图、选定、加工、Unity 接入和 LevelTester 人工验收是不同步骤。整理成功不会自动把资产标为“待接入”“已接入”或“已验收”。

选定图片后，再以批次原图为来源制作 `Candidates` 中的加工版本，并在 manifest 记录裁切区域、缩放和版本。加工不得覆盖 `Originals`。

## 命令

```powershell
# 整理新增图片
python tools/organize-art-inbox.py

# 只扫描和报告，不创建批次
python tools/organize-art-inbox.py --dry-run

# 指定批次 ID
python tools/organize-art-inbox.py --batch-id ART-20261016-A

# 使用本批提供的正式映射清单
python tools/organize-art-inbox.py --source-manifest ArtSource/BatchInfo/Batch_20261016_01/asset_manifest.json
```

脚本需要 Python 3 和 Pillow。依赖缺失时会明确报错且不会建立不完整批次。
