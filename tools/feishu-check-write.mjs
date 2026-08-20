#!/usr/bin/env node
/**
 * 检查应用对 feishu-map 中各文档是否具备写入权限（插入块探测）。
 * 用法：node tools/feishu-check-write.mjs
 */
import { readFile } from 'node:fs/promises';
import { homedir } from 'node:os';
import path from 'node:path';

const BASE = 'https://open.feishu.cn/open-apis';
const ROOT = path.resolve(import.meta.dirname, '..');
const MAP_FILE = path.join(ROOT, 'tools', 'feishu-map.json');

async function readJson(file) {
  const raw = await readFile(file, 'utf8');
  return JSON.parse(raw.replace(/^\uFEFF/, ''));
}

async function loadCredentials() {
  const fallback = path.join(homedir(), '.chatccc', 'config.json');
  const cfg = await readJson(fallback);
  return { appId: cfg.feishu.appId, appSecret: cfg.feishu.appSecret };
}

async function getToken(creds) {
  const res = await fetch(`${BASE}/auth/v3/tenant_access_token/internal`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json; charset=utf-8' },
    body: JSON.stringify({ app_id: creds.appId, app_secret: creds.appSecret }),
  });
  const json = await res.json();
  if (json.code !== 0) throw new Error(`获取 token 失败: [${json.code}] ${json.msg}`);
  return json.tenant_access_token;
}

async function canWrite(token, docId) {
  const bid = `perm-probe-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  const res = await fetch(`${BASE}/docx/v1/documents/${docId}/blocks/${docId}/descendant`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json; charset=utf-8',
    },
    body: JSON.stringify({
      children_id: [bid],
      index: -1,
      descendants: [{
        block_id: bid,
        block_type: 2,
        text: { elements: [{ text_run: { content: 'perm-probe' } }] },
      }],
    }),
  });
  const json = await res.json();
  if (json.code === 0) {
    // 清理探测块，避免污染文档
    await fetch(`${BASE}/docx/v1/documents/${docId}/blocks/${docId}/children/batch_delete`, {
      method: 'DELETE',
      headers: {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json; charset=utf-8',
      },
      body: JSON.stringify({ block_ids: [bid] }),
    }).catch(() => {});
    return true;
  }
  return false;
}

async function main() {
  const mapping = await readJson(MAP_FILE);
  const token = await getToken(await loadCredentials());
  const entries = Object.entries(mapping.documents ?? {})
    .filter(([, v]) => v.docId && !String(v.docId).startsWith('<'))
    .map(([key, v]) => ({ key, docId: v.docId, title: v.title || key }));

  let ok = 0;
  const failed = [];
  for (const e of entries) {
    process.stdout.write(`  ${e.key.padEnd(36)} `);
    try {
      if (await canWrite(token, e.docId)) {
        ok++;
        console.log('OK');
      } else {
        failed.push(e);
        console.log('FAIL');
      }
    } catch (err) {
      failed.push(e);
      console.log(`ERR ${err.message}`);
    }
    await new Promise((r) => setTimeout(r, 120));
  }
  console.log(`\n可写入 ${ok}/${entries.length} 篇。`);
  if (failed.length) {
    console.log('\n缺少写入权限：');
    for (const e of failed) console.log(`  ${e.key}\t${e.docId}\t${e.title}`);
  }
  process.exit(failed.length ? 1 : 0);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
