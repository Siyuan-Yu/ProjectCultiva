#!/usr/bin/env node
/**
 * 把本地 Markdown 全量同步到飞书云文档。
 *
 * 单向同步：本地是唯一真源，飞书端每次被完全覆盖。
 * 因此不要在飞书里直接改内容（会被下次同步冲掉），飞书只当阅读与分享层。
 *
 * 用法：
 *   node tools/feishu-sync.mjs --check              仅检查凭据与文档权限
 *   node tools/feishu-sync.mjs --provision          为缺少 docId 的条目自动新建飞书文档，并回写映射
 *   node tools/feishu-sync.mjs --share --openid ou_xxx
 *                                                   把已映射文档分享给指定用户（需要 docs:permission.member:create）
 *   node tools/feishu-sync.mjs                      同步全部已配置文档（会把本地相对链接改写成飞书链接）
 *   node tools/feishu-sync.mjs --only vision        只同步指定 key
 */

import { readFile, writeFile } from 'node:fs/promises';
import { homedir } from 'node:os';
import path from 'node:path';

const BASE = 'https://open.feishu.cn/open-apis';
const DOC_BASE = 'https://my.feishu.cn/docx';
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
  let lastErr;
  for (let attempt = 0; attempt < 5; attempt++) {
    try {
      const res = await fetch(`${BASE}${endpoint}`, {
        method,
        headers: {
          Authorization: `Bearer ${token}`,
          'Content-Type': 'application/json; charset=utf-8',
        },
        body: body === undefined ? undefined : JSON.stringify(body),
      });
      const text = await res.text();
      let json;
      try {
        json = JSON.parse(text);
      } catch {
        const err = new Error(`非 JSON 响应 (HTTP ${res.status}): ${text.slice(0, 200)}`);
        err.code = -1;
        throw err;
      }
      if (json.code !== 0) {
        const err = new Error(`[${json.code}] ${json.msg}`);
        err.code = json.code;
        err.detail = json.error;
        throw err;
      }
      return json.data;
    } catch (e) {
      lastErr = e;
      // 业务错误不重试；仅网络抖动重试
      if (e.code !== undefined && e.code !== -1) throw e;
      if (attempt < 4) await new Promise((r) => setTimeout(r, 1000 * (attempt + 1)));
    }
  }
  throw lastErr;
}

function isConfiguredDocId(docId) {
  return Boolean(docId && !String(docId).startsWith('<'));
}

function feishuUrl(docId) {
  return `${DOC_BASE}/${docId}`;
}

/** 转换结果里有些字段是只读的，原样回写会报 schema mismatch / invalid param */
function sanitizeBlocks(blocks) {
  for (const b of blocks) {
    delete b.parent_id;
    delete b.revision_id;
    if (b.table) {
      delete b.table.cells;
      if (b.table.property) delete b.table.property.merge_info;
    }
  }
  return blocks;
}

/**
 * 按第一级块切分成多批，保证每批的块总数不超上限，且父子关系不被拆散。
 * 注意：convert 结果里嵌套关系以 children 数组为准；parent_id 可能为空，不可依赖。
 */
function splitIntoBatches(firstLevelIds, blocks) {
  const byId = new Map(blocks.map((b) => [b.block_id, b]));

  const subtreeOf = (rootId) => {
    const out = [];
    const stack = [rootId];
    while (stack.length) {
      const id = stack.pop();
      const node = byId.get(id);
      if (!node) continue;
      out.push(node);
      const kids = node.children ?? [];
      for (let i = kids.length - 1; i >= 0; i--) stack.push(kids[i]);
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
    'DELETE',
    `/docx/v1/documents/${docId}/blocks/${docId}/children/batch_delete?document_revision_id=-1`,
    { start_index: 0, end_index: childCount },
  );
  return childCount;
}

/**
 * 把本地相对路径 / 反引号文件名，改写成飞书文档链接，方便在飞书里点来点去。
 * 只改写已映射且已有 docId 的文档。
 *
 * 注意两个飞书侧的坑：
 * 1. 相对路径（非 http）在 Markdown 转换时会被直接丢弃，只剩纯文本，所以必须先改成绝对链接。
 * 2. 链接文字若是行内代码（[`x.md`](url)）会被转换器吃掉字符，因此统一改用文档标题作为链接文字。
 */
function rewriteLinksForFeishu(markdown, currentFile, entries) {
  const byNorm = new Map();
  for (const e of entries) {
    if (!isConfiguredDocId(e.docId)) continue;
    const abs = path.resolve(ROOT, e.file).replace(/\\/g, '/').toLowerCase();
    byNorm.set(abs, e);
    byNorm.set(path.basename(e.file).toLowerCase(), e);
  }

  const resolveTarget = (rawTarget) => {
    const cleaned = rawTarget.trim().replace(/\\/g, '/').split('#')[0].split('?')[0];
    if (!cleaned || cleaned.startsWith('http://') || cleaned.startsWith('https://')) return null;
    if (!cleaned.endsWith('.md')) return null;
    const fromDir = path.dirname(path.resolve(ROOT, currentFile));
    const abs = path.resolve(fromDir, cleaned).replace(/\\/g, '/').toLowerCase();
    return byNorm.get(abs) || byNorm.get(path.basename(cleaned).toLowerCase()) || null;
  };

  // [text](relative.md) → [text](https://my.feishu.cn/docx/...)
  let out = markdown.replace(/\[([^\]]+)\]\(([^)]+)\)/g, (full, text, target) => {
    const hit = resolveTarget(target);
    if (!hit) return full;
    return `[${text}](${feishuUrl(hit.docId)})`;
  });

  // `22-realms-and-abilities.md` → [系统 22 境界与机制能力](url)
  // 不保留反引号：飞书对「行内代码作为链接文字」会丢字符
  out = out.replace(/`([^`]+?\.md)`/g, (full, name) => {
    const hit = resolveTarget(name) || byNorm.get(path.basename(name).toLowerCase());
    if (!hit) return full;
    return `[${hit.title}](${feishuUrl(hit.docId)})`;
  });

  return out;
}

/** 文档分组：更具体的路径写在前面，避免 ADR 被并入「过程与记录」大杂烩 */
const NAV_GROUPS = [
  { label: '00 项目总纲', prefix: 'docs/00-project/' },
  { label: '10 竞品与差异化', prefix: 'docs/10-benchmark/' },
  { label: '20 系统设计', prefix: 'docs/20-systems/' },
  { label: '30 技术架构', prefix: 'docs/30-tech/' },
  { label: '43 架构决策 ADR', prefix: 'docs/40-process/43-decisions/' },
  { label: '40 过程与记录', prefix: 'docs/40-process/' },
];

function buildNavFooter(entries, currentKey, hubEntry) {
  const configured = entries.filter((e) => isConfiguredDocId(e.docId));
  const lines = ['', '---', '', '## 文档导航', ''];

  if (hubEntry && hubEntry.key !== currentKey) {
    lines.push(`回到总纲：[${hubEntry.title}](${feishuUrl(hubEntry.docId)})`, '');
  }

  const assigned = new Set();
  for (const group of NAV_GROUPS) {
    const items = configured.filter((e) => {
      if (assigned.has(e.key)) return false;
      const f = e.file.replace(/\\/g, '/');
      return f.startsWith(group.prefix);
    });
    if (items.length === 0) continue;
    for (const e of items) assigned.add(e.key);
    lines.push(`**${group.label}**`, '');
    for (const e of items) {
      const mark = e.key === currentKey ? '（当前页）' : '';
      lines.push(`- [${e.title}](${feishuUrl(e.docId)})${mark}`);
    }
    lines.push('');
  }
  return lines.join('\n');
}

async function syncFile(token, entry, allEntries, hubEntry) {
  const { key, file, docId, title } = entry;
  const abs = path.join(ROOT, file);
  let markdown = await readFile(abs, 'utf8');

  markdown = rewriteLinksForFeishu(markdown, file, allEntries);
  markdown += buildNavFooter(allEntries, key, hubEntry);
  markdown += `\n\n---\n\n> 本页由本地文档自动同步，请勿直接在飞书编辑。源文件：\`${file}\`\n`;

  const converted = await api(token, 'POST', '/docx/v1/documents/blocks/convert', {
    content_type: 'markdown',
    content: markdown,
  });

  const firstLevelIds = converted.first_level_block_ids ?? [];
  const blocks = sanitizeBlocks(converted.blocks ?? []);
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
    `  OK  ${key.padEnd(18)} ${title ?? file}  (清除 ${removed} 块 → 写入 ${blocks.length} 块 / ${batches.length} 批)`,
  );
  console.log(`       ${feishuUrl(docId)}`);
}

async function createDocument(token, title) {
  const data = await api(token, 'POST', '/docx/v1/documents', { title });
  return data?.document?.document_id;
}

async function provisionMissing(token, mapping) {
  let created = 0;
  for (const [key, v] of Object.entries(mapping.documents ?? {})) {
    if (isConfiguredDocId(v.docId)) continue;
    const title = v.title || key;
    process.stdout.write(`  新建 ${key.padEnd(18)} 《${title}》 ... `);
    try {
      const docId = await createDocument(token, title);
      if (!docId) throw new Error('未返回 document_id');
      mapping.documents[key].docId = docId;
      created++;
      console.log(`OK  ${feishuUrl(docId)}`);
      // 飞书限制同一文件夹并发创建，串行并稍作间隔
      await new Promise((r) => setTimeout(r, 400));
    } catch (err) {
      console.log(`FAIL ${err.message}`);
      if (err.code === 99991672) {
        console.log('       → 需要开通 docx:document / docx:document:create 并发布应用版本');
      }
    }
  }
  await writeFile(MAP_FILE, `${JSON.stringify(mapping, null, 2)}\n`, 'utf8');
  console.log(`\n新建 ${created} 篇，映射已写回 tools/feishu-map.json`);
  return created;
}

async function shareDocument(token, docId, openId) {
  await api(token, 'POST', `/drive/v1/permissions/${docId}/members?type=docx`, {
    member_type: 'openid',
    member_id: openId,
    perm: 'full_access',
  });
}

async function shareAll(token, entries, openId) {
  let ok = 0;
  let failed = 0;
  for (const e of entries) {
    if (!isConfiguredDocId(e.docId)) continue;
    try {
      await shareDocument(token, e.docId, openId);
      console.log(`  OK  ${e.key.padEnd(18)} 已分享给 ${openId}`);
      ok++;
      await new Promise((r) => setTimeout(r, 200));
    } catch (err) {
      failed++;
      console.log(`  FAIL ${e.key.padEnd(18)} ${err.message}`);
      if (err.code === 99991672) {
        console.log(
          '       → 应用缺少分享权限。请开通并发布：docs:permission.member:create 或 drive:drive',
        );
        console.log(
          '       → https://open.feishu.cn/app/cli_aae0ade8d5389bdf/auth?q=docs:permission.member:create,drive:drive&op_from=openapi&token_type=tenant',
        );
        break;
      }
    }
  }
  console.log(`\n分享成功 ${ok} 篇${failed ? `，失败/中断 ${failed}` : ''}。`);
  return failed === 0;
}

async function checkAccess(token, entries) {
  let ok = 0;
  for (const e of entries) {
    try {
      const d = await api(token, 'GET', `/docx/v1/documents/${e.docId}`);
      console.log(`  OK  ${e.key.padEnd(18)} 《${d?.document?.title || '(无标题)'}》`);
      console.log(`       ${feishuUrl(e.docId)}`);
      ok++;
    } catch (err) {
      console.log(`  FAIL ${e.key.padEnd(18)} ${err.message}`);
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

function parseArgs(argv) {
  const args = {
    checkOnly: argv.includes('--check'),
    provision: argv.includes('--provision'),
    share: argv.includes('--share'),
    onlyKey: null,
    openId: process.env.FEISHU_SHARE_OPENID || null,
  };
  const onlyIdx = argv.indexOf('--only');
  if (onlyIdx >= 0) args.onlyKey = argv[onlyIdx + 1];
  const openIdx = argv.indexOf('--openid');
  if (openIdx >= 0) args.openId = argv[openIdx + 1];
  return args;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const mapping = await readJson(MAP_FILE);
  const creds = await loadCredentials();
  const token = await getToken(creds);
  console.log(`应用 ${creds.appId} 认证成功\n`);

  if (args.provision) {
    console.log('开始为缺少 docId 的条目新建飞书文档：');
    await provisionMissing(token, mapping);
    // 重新读取，后续步骤使用最新映射
  }

  const latest = await readJson(MAP_FILE);
  // 全量清单：链接改写与底部导航必须基于它，否则 --only 会把其他文档的链接写丢
  const allEntries = Object.entries(latest.documents ?? {}).map(([key, v]) => ({ key, ...v }));
  const allConfigured = allEntries.filter((e) => isConfiguredDocId(e.docId));
  const hubEntry = allConfigured.find((e) => e.key === 'overview') ?? null;

  let entries = allEntries;
  if (args.onlyKey) {
    entries = entries.filter((e) => e.key === args.onlyKey);
    if (entries.length === 0) throw new Error(`feishu-map.json 中没有 key: ${args.onlyKey}`);
  }
  const configured = entries.filter((e) => isConfiguredDocId(e.docId));

  if (args.share) {
    if (!args.openId) {
      throw new Error('分享需要 --openid ou_xxx 或环境变量 FEISHU_SHARE_OPENID');
    }
    if (configured.length === 0) throw new Error('没有已配置 docId 的文档可分享');
    console.log(`开始分享 ${configured.length} 篇给 ${args.openId}：`);
    const ok = await shareAll(token, configured, args.openId);
    process.exit(ok ? 0 : 1);
  }

  if (configured.length === 0) {
    throw new Error('feishu-map.json 中没有已填写 docId 的条目。可先运行 --provision');
  }

  if (args.checkOnly) {
    const allOk = await checkAccess(token, configured);
    process.exit(allOk ? 0 : 1);
  }

  console.log(`开始同步 ${configured.length} 篇：`);
  let failed = 0;
  for (const e of configured) {
    try {
      await syncFile(token, e, allConfigured, hubEntry);
    } catch (err) {
      failed++;
      console.error(`  FAIL ${e.key.padEnd(18)} ${err.message}`);
    }
  }
  console.log(failed === 0 ? '\n全部完成。' : `\n完成，${failed} 篇失败。`);
  process.exit(failed === 0 ? 0 : 1);
}

main().catch((err) => {
  console.error(`错误：${err.message}`);
  process.exit(1);
});
