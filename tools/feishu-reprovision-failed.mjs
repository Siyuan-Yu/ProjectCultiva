#!/usr/bin/env node
/**
 * 为缺少应用写入权限的映射条目新建飞书文档（应用归属），回写 docId 并同步正文。
 */
import { readFile, writeFile } from 'node:fs/promises';
import { homedir } from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

const BASE = 'https://open.feishu.cn/open-apis';
const ROOT = path.resolve(import.meta.dirname, '..');
const MAP_FILE = path.join(ROOT, 'tools', 'feishu-map.json');
const OPEN_ID = 'ou_d1adece22dbf98d9c6ba5441145dc208';

const KEYS = [
  'recent-updates-2026-08-14',
  'npc-dialogue-host-ux-2026-08-14',
  'housing-control-core-2026-08-15',
  'cultivation-breakthrough-host-2026-08-15',
  'quest-manual-api-2026-08-15',
  'jiang-lao-cave-manual-2026-08-15',
  'combat-arts-physique-2026-08-16',
  'control-core-chase-spawn-zone-2026-08-16',
  'defeat-teleport-cave-spawn-gui-2026-08-16',
  'world-graph-editor-usage',
  'world-graph-host-travel-isolation-2026-08-16',
  'local-place-editor-usage',
  'skill-mastery-study-ritual-2026-08-16',
  'skill-mastery-config-absolute-tiers-2026-08-16',
  'manual-art-editor-and-cleanup-2026-08-16',
  'spirit-veil-ranged-normal-attack-2026-08-16',
  'world-object-inspect-and-tree-chop-2026-08-16',
  'farm-field-zone-labor-2026-08-16',
  'skill-mastery-farm-veil-chop-rollup-2026-08-17',
];

async function readJson(file) {
  const raw = await readFile(file, 'utf8');
  return JSON.parse(raw.replace(/^\uFEFF/, ''));
}

async function loadCredentials() {
  const cfg = await readJson(path.join(homedir(), '.chatccc', 'config.json'));
  return { appId: cfg.feishu.appId, appSecret: cfg.feishu.appSecret };
}

async function getToken(creds) {
  const res = await fetch(`${BASE}/auth/v3/tenant_access_token/internal`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json; charset=utf-8' },
    body: JSON.stringify({ app_id: creds.appId, app_secret: creds.appSecret }),
  });
  const json = await res.json();
  if (json.code !== 0) throw new Error(`token: [${json.code}] ${json.msg}`);
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
  const json = await res.json();
  if (json.code !== 0) throw new Error(`[${json.code}] ${json.msg}`);
  return json.data;
}

async function main() {
  const mapping = await readJson(MAP_FILE);
  const token = await getToken(await loadCredentials());
  const created = [];

  for (const key of KEYS) {
    const entry = mapping.documents?.[key];
    if (!entry) {
      console.log(`SKIP ${key} (map 中不存在)`);
      continue;
    }
    const title = entry.title || key;
    process.stdout.write(`  新建 ${key} ... `);
    const data = await api(token, 'POST', '/docx/v1/documents', { title });
    const docId = data?.document?.document_id;
    if (!docId) throw new Error(`${key}: 未返回 document_id`);
    mapping.documents[key].docId = docId;
    created.push({ key, docId, title, url: `https://my.feishu.cn/docx/${docId}` });
    console.log(`OK  ${docId}`);
    await new Promise((r) => setTimeout(r, 400));
  }

  await writeFile(MAP_FILE, `${JSON.stringify(mapping, null, 2)}\n`, 'utf8');
  console.log(`\n已回写 ${created.length} 个 docId 到 feishu-map.json\n`);

  for (const item of created) {
    process.stdout.write(`  同步 ${item.key} ... `);
    const sync = spawnSync(process.execPath, ['tools/feishu-sync.mjs', '--only', item.key], {
      cwd: ROOT,
      encoding: 'utf8',
    });
    if (sync.status !== 0) {
      console.log('FAIL');
      console.log(sync.stdout || sync.stderr);
      continue;
    }
    console.log('OK');
    process.stdout.write(`  分享 ${item.key} ... `);
    const share = spawnSync(
      process.execPath,
      ['tools/feishu-sync.mjs', '--share', '--openid', OPEN_ID, '--only', item.key],
      { cwd: ROOT, encoding: 'utf8' },
    );
    console.log(share.status === 0 ? 'OK' : 'FAIL');
  }

  console.log('\n新建文档链接：');
  for (const item of created) console.log(`  ${item.key}\n    ${item.url}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
