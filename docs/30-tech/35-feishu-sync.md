# 飞书文档同步

> 状态：脚本已就绪，等待开通应用权限 | 最后更新：2026-07-30

## 1. 这是什么

把本地 Markdown 推送到飞书云文档，让策划案在飞书里有良好的阅读体验，同时源文件仍在 Git 里由 Cursor 维护。

**单向同步：本地是唯一真源。** 每次同步会清空飞书端内容再重写，所以**不要在飞书里直接编辑正文**，改动会被下次同步覆盖。飞书只当阅读层与分享层。

评论例外：飞书的评论挂在块上，全量重写会导致评论失去锚点。若需要评论讨论，建议在飞书里另开一篇「讨论页」，不要评在同步页上。

## 2. 实现方式

复用本机 ChatCCC 已配置的飞书自建应用（App ID `cli_aae0ade8d5389bdf`），凭据从 `~/.chatccc/config.json` 读取，**不写入仓库**。也可用环境变量 `FEISHU_APP_ID` / `FEISHU_APP_SECRET` 覆盖。

调用链（飞书官方推荐路径）：

```
读取本地 md
  → POST /docx/v1/documents/blocks/convert        Markdown 转文档块
  → GET  /docx/v1/documents/{doc}/blocks/{doc}    取根块现有子块数
  → POST .../children/batch_delete                清空旧内容
  → POST .../descendant                           批量写入新块（每批 ≤900）
```

已处理的坑：
- 清空子块接口是 `DELETE`，不是 `POST`（写成 POST 会得到 HTTP 404）
- 表格块的 `table.property.merge_info` 与 `table.cells` 是只读字段，回传会报 `open schema mismatch`，同步前剥离
- 嵌套关系以 `children` 数组为准，`parent_id` 在 convert 结果里常为空；切批时必须走 `children`，不能依赖 `parent_id`
- 单次插入上限 1000 块，按第一级块切批且不拆散父子关系
- Windows 编辑器写入的 UTF-8 BOM 会让 `JSON.parse` 失败，读取时统一剥离
- 飞书开放平台偶发 TLS 断连，API 调用对网络错误自动重试

## 3. 一次性配置（需要你在飞书侧操作）

### 3.1 开通应用权限

访问：<https://open.feishu.cn/app/cli_aae0ade8d5389bdf/auth?q=docx:document,docx:document.block:convert,docs:permission.member:create,drive:drive&op_from=openapi&token_type=tenant>

开通这些权限：

| 权限 | 用途 |
|---|---|
| `docx:document` | 读写文档、删除与创建块 |
| `docx:document.block:convert` | Markdown 转文档块 |
| `docs:permission.member:create` | 把应用新建的文档自动分享给你 |
| `drive:drive`（可选，更宽） | 云空间元数据、文件夹与分享相关能力 |

若只想让脚本**自动新建**文档，`docx:document` 已足够；但要把新建文档分享到你的飞书账号，还需要 `docs:permission.member:create`。

### 3.2 发布应用版本

自建应用的权限变更需要在开放平台「版本管理与发布」创建并发布新版本后才生效。

### 3.3 把已有文档分享给应用

飞书云文档权限是**按文档授予**的。对**你本人创建**、需要让脚本写入的文档（例如总览）：

打开文档 → 右上角「分享」→ 添加协作者 → 搜索该应用（即给你发消息的那个机器人）→ 权限设为**可编辑**。

脚本用 `--provision` 新建的文档默认归属应用，之后由 `--share --openid ...` 分享给你；这两类文档的授权方向相反。

## 4. 日常使用

```bash
# 检查凭据与文档权限（不写入任何内容，安全）
node tools/feishu-sync.mjs --check

# 为映射里缺少 docId 的条目自动新建飞书文档，并回写 feishu-map.json
node tools/feishu-sync.mjs --provision

# 把已映射文档分享给指定用户（需要 docs:permission.member:create）
node tools/feishu-sync.mjs --share --openid ou_xxxxxxxx

# 同步全部已配置文档（会把本地相对链接改写成飞书链接，并在文末加导航）
node tools/feishu-sync.mjs

# 只同步一篇
node tools/feishu-sync.mjs --only vision
```

文档映射配置在 `tools/feishu-map.json`。新增一篇的推荐做法：

1. 在 `feishu-map.json` 加条目（`docId` 可先留占位）
2. 跑 `--provision` 自动建文档
3. 跑 `--share --openid ...` 分享给你
4. 跑同步写入正文

也可以继续手建：在飞书里建好空文档 → 从链接 `/docx/` 后面复制 ID → 填进映射 → 把文档分享给应用 → 跑同步。

## 5. 常见错误码

| 错误码 | 含义 | 处理 |
|---|---|---|
| 99991672 | 应用未开通所需权限 | 见 3.1、3.2 |
| 1770001 / 131005 | 文档未共享给应用，或 docId 有误 | 见 3.3，并确认 ID 取自 `/docx/` 之后 |
| 1069910 | 文件扩展名与声明不一致 | 仅导入接口相关，本脚本不涉及 |

## 6. 已知限制

- 飞书 Markdown 转换不支持 Mermaid 图，流程图会退化成代码块
- 飞书没有对应「折叠块」的标准 Markdown 语法，复杂排版会被简化
- 文档标题由飞书侧维护，脚本不修改标题（避免覆盖你在飞书里的命名）

## 7. 链接改写的两个飞书侧约束

这两点决定了脚本必须先改写链接，不能原样上传：

1. **相对路径链接会被丢弃。** 形如 `[开局体验](../20-systems/20-opening-experience.md)` 的链接，转换后只剩纯文本，链接本身消失。因此所有指向已映射文档的引用都要先换成飞书绝对链接。
2. **行内代码不能作为链接文字。** `[`22-realms.md`](url)` 转换后会丢字符。脚本改为用映射里的文档标题作为链接文字。

另外，`--only` 只限制「写哪几篇」，链接改写与文末导航始终基于完整映射表；否则单篇同步会把该页指向其他文档的链接全部写丢。
