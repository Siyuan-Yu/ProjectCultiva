#!/usr/bin/env node
/**
 * 把本地 Markdown 全量同步到飞书云文档。
 *
 * 单向同步：本地是唯一真源，飞书端每次被完全覆盖。
 * 因此不要在飞书里直接改内容（会被下次同步冲掉），飞书只当阅读与分享层。
 *
 * 用法：
 *   node tools/feishu-sync.mjs --check          仅检查凭据与文档权限
 *   node tools/feishu-sync.mjs                  同步 feishu-map.json 中全部条目
 *   node tools/feishu-sync.mjs --only vision    只同步指定 key
 */

import { readFile } from 'node:fs/promises';
import { homedir } from 'node:os';
import path from 'node:path';

const BASE = 'https://open.feishu.cn/open-apis';
const ROOT = path.resolve(import.meta.dirname, '..');
const MAP_FILE = path.join(ROOT, 'tools', 'feishu-map.json');
const MAX_BLOCKS_PER_CALL = 900; // 官方上限 1000，留余量

/** Windows 上的编辑器常写入 UTF-8 BOM，JSON.parse 不接受 */
async function readJson(file) {
  const raw = await readFile(file, 'utf8');
  return JSON.parse(raw.replace(/^\uFEFF/, ''));
}

async function loadCredentials() {
  if (process.env.FEISHU_APP_ID && process.env.FEISHU_APP_SECRET) {
    return { appId: process.env.FEISHU_APP_ID, appSecret: process.env.FEISHU_APP_SECRET };
  }
  // 复用本机 ChatCCC 已配置的飞书应用凭据，避免把密钥写进仓库
  const fallback = path.join(homedir(), '.chatccc', 'config.json');
  const cfg = await readJson(fallback);
  if (!cfg?.feishu?.appId || !cfg?.feishu?.appSecret) {
    throw new Error(`未找到飞书凭据。请设置环境变量 FEISHU_APP_ID / FEISHU_APP_SECRET，或检查 ${fallback}`);
  }
  return { appId: cfg.feishu.appId, appSecret: cfg.feishu.appSecret };
}

async function getToken({ appId, appSecret }) {
  const res = await fetch(`${BASE}/auth/v3/tenant_access_token/internal`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json; charset=utf-8' },
    body: JSON.stringify({ app_id: appId, app_secret: appSecret }),
  });
  const json = await res.json();
  if (json.code !== 0) throw new Error(`获取 token 失败: [${json.code}] ${json.msg}`);
  return json.tenant_access_token;
}

async function api(token, method, endpoint, body) {
  const res = await fetch(`${BASE}${endpoint}`, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json; charset=utf-8',
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const json = await res.json().catch(() => ({ code: -1, msg: `非 JSON 响应 (HTTP ${res.status})` }));
  if (json.code !== 0) {
    const err = new Error(`[${json.code}] ${json.msg}`);
    err.code = json.code;
    err.detail = json.error;
    throw err;
  }
  return json.data;
}

/** 表格块的 merge_info 是只读字段，回传会报错 */
function stripReadonlyFields(blocks) {
  for (const b of blocks) {
    if (b.table?.merge_info) delete b.table.merge_info;
  }
  return blocks;
}

/**
 * 按第一级块切分成多批，保证每批的块总数不超上限，且父子关系不被拆散。
 */
function splitIntoBatches(firstLevelIds, blocks) {
  const byId = new Map(blocks.map((b) => [b.block_id, b]));
  const childrenOf = new Map();
  for (const b of blocks) {
    if (!b.parent_id) continue;
    if (!childrenOf.has(b.parent_id)) childrenOf.set(b.parent_id, []);
    childrenOf.get(b.parent_id).push(b.block_id);
  }

  const subtreeOf = (rootId) => {
    const out = [];
    const stack = [rootId];
    while (stack.length) {
      const id = stack.pop();
      const node = byId.get(id);
      if (node) out.push(node);
      for (const c of childrenOf.get(id) ?? []) stack.push(c);
    }
    return out;
  };

  const batches = [];
  let current = { childrenId: [], descendants: [] };
  for (const id of firstLevelIds) {
    const subtree = subtreeOf(id);
    if (current.childrenId.length && current.descendants.length + subtree.length > MAX_BLOCKS_PER_CALL) {
      batches.push(current);
      current = { childrenId: [], descendants: [] };
    }
    current.childrenId.push(id);
    current.descendants.push(...subtree);
  }
  if (current.childrenId.length) batches.push(current);
  return batches;
}

async function clearDocument(token, docId) {
  const data = await api(token, 'GET', `/docx/v1/documents/${docId}/blocks/${docId}`);
  const childCount = data?.block?.children?.length ?? 0;
  if (childCount === 0) return 0;
  await api(
    token,
    'POST',
    `/docx/v1/documents/${docId}/blocks/${docId}/children/batch_delete?document_revision_id=-1`,
    { start_index: 0, end_index: childCount },
  );
  return childCount;
}

async function syncFile(token, { key, file, docId, title }) {
  const abs = path.join(ROOT, file);
  let markdown = await readFile(abs, 'utf8');

  // 文档标题由飞书侧维护，正文里的一级标题会重复，故在同步时可选地补一行来源提示
  markdown = `${markdown}\n\n---\n\n> 本页由本地文档自动同步，请勿直接在飞书编辑。源文件：\`${file}\`\n`;

  const converted = await api(token, 'POST', '/docx/v1/documents/blocks/convert', {
    content_type: 'markdown',
    content: markdown,
  });

  const firstLevelIds = converted.first_level_block_ids ?? [];
  const blocks = stripReadonlyFields(converted.blocks ?? []);
  if (firstLevelIds.length === 0) throw new Error('转换结果为空，检查 Markdown 内容');

  const removed = await clearDocument(token, docId);

  const batches = splitIntoBatches(firstLevelIds, blocks);
  let insertedAt = 0;
  for (const batch of batches) {
    await api(token, 'POST', `/docx/v1/documents/${docId}/blocks/${docId}/descendant?document_revision_id=-1`, {
      children_id: batch.childrenId,
      index: insertedAt,
      descendants: batch.descendants,
    });
    insertedAt += batch.childrenId.length;
  }

  console.log(
    `  OK  ${key.padEnd(16)} ${title ?? file}  (清除 ${removed} 块 → 写入 ${blocks.length} 块 / ${batches.length} 批)`,
  );
}

async function checkAccess(token, entries) {
  let ok = 0;
  for (const e of entries) {
    try {
      const d = await api(token, 'GET', `/docx/v1/documents/${e.docId}`);
      console.log(`  OK  ${e.key.padEnd(16)} 《${d?.document?.title || '(无标题)'}》`);
      ok++;
    } catch (err) {
      console.log(`  FAIL ${e.key.padEnd(16)} ${err.message}`);
      if (err.code === 99991672) {
        console.log('       → 应用缺少权限，需在开放平台开通 docx:document 与 docx:document.block:convert 并发布版本');
      } else if (err.code === 1770001 || err.code === 131005) {
        console.log('       → 文档未共享给应用，需在文档「分享」中把应用添加为可编辑');
      }
    }
  }
  console.log(`\n可访问 ${ok}/${entries.length} 篇。`);
  return ok === entries.length;
}

async function main() {
  const args = process.argv.slice(2);
  const checkOnly = args.includes('--check');
  const onlyIdx = args.indexOf('--only');
  const onlyKey = onlyIdx >= 0 ? args[onlyIdx + 1] : null;

  const mapping = await readJson(MAP_FILE);
  let entries = Object.entries(mapping.documents ?? {}).map(([key, v]) => ({ key, ...v }));
  if (onlyKey) {
    entries = entries.filter((e) => e.key === onlyKey);
    if (entries.length === 0) throw new Error(`feishu-map.json 中没有 key: ${onlyKey}`);
  }
  entries = entries.filter((e) => e.docId && !e.docId.startsWith('<'));
  if (entries.length === 0) throw new Error('feishu-map.json 中没有已填写 docId 的条目');

  const creds = await loadCredentials();
  const token = await getToken(creds);
  console.log(`应用 ${creds.appId} 认证成功\n`);

  if (checkOnly) {
    const allOk = await checkAccess(token, entries);
    process.exit(allOk ? 0 : 1);
  }

  console.log(`开始同步 ${entries.length} 篇：`);
  let failed = 0;
  for (const e of entries) {
    try {
      await syncFile(token, e);
    } catch (err) {
      failed++;
      console.error(`  FAIL ${e.key.padEnd(16)} ${err.message}`);
    }
  }
  console.log(failed === 0 ? '\n全部完成。' : `\n完成，${failed} 篇失败。`);
  process.exit(failed === 0 ? 0 : 1);
}

main().catch((err) => {
  console.error(`错误：${err.message}`);
  process.exit(1);
});
