import type { ContentFile, DefRef, PackageState, JsonDict } from './types';
import { LOCATION_FIELDS, TYPE_FIELDS } from './schemaFields';
import type { ValidationIssue } from './types';

function basename(p: string) {
  const parts = p.replace(/\\/g, '/').split('/');
  return parts[parts.length - 1] || p;
}

export async function loadPackage(root: string): Promise<PackageState> {
  const api = window.studioApi;
  if (!api) throw new Error('studioApi 不可用：请用 Electron 启动（npm run dev），不要只开浏览器。');

  const paths = await api.listJsonFiles(root);
  const files: ContentFile[] = [];
  const defs: DefRef[] = [];
  const byId: Record<string, DefRef> = {};

  for (const filePath of paths) {
    const text = await api.readText(filePath);
    let parsed: JsonDict;
    try {
      parsed = JSON.parse(text) as JsonDict;
    } catch {
      continue;
    }
    const definitions = Array.isArray(parsed.definitions) ? (parsed.definitions as JsonDict[]) : [];
    const schemaVersion = typeof parsed.schemaVersion === 'number' ? parsed.schemaVersion : 1;
    files.push({ path: filePath, schemaVersion, definitions });

    definitions.forEach((raw, index) => {
      const id = String(raw.id ?? '');
      const type = String(raw.type ?? '');
      if (!id || !type) return;
      const ref: DefRef = {
        id,
        type,
        name: String(raw.name ?? ''),
        filePath,
        index,
        raw
      };
      defs.push(ref);
      byId[id] = ref;
    });
  }

  return { root, files, defs, byId };
}

export async function saveDefinition(state: PackageState, def: DefRef, nextRaw: JsonDict): Promise<PackageState> {
  const api = window.studioApi!;
  const file = state.files.find((f) => f.path === def.filePath);
  if (!file) throw new Error('找不到来源文件: ' + def.filePath);

  const definitions = file.definitions.slice();
  definitions[def.index] = nextRaw;
  file.definitions = definitions;

  const payload = {
    schemaVersion: file.schemaVersion || 1,
    definitions
  };
  const text = JSON.stringify(payload, null, 2) + '\n';
  await api.writeText(file.path, text);

  return loadPackage(state.root);
}

export async function appendDefinition(
  state: PackageState,
  targetFilePath: string,
  raw: JsonDict
): Promise<PackageState> {
  const api = window.studioApi!;
  let file = state.files.find((f) => f.path === targetFilePath);
  let definitions: JsonDict[] = [];
  let schemaVersion = 1;

  if (file) {
    definitions = file.definitions.slice();
    schemaVersion = file.schemaVersion || 1;
  } else if (await fileExists(targetFilePath)) {
    const text = await api.readText(targetFilePath);
    const parsed = JSON.parse(text) as JsonDict;
    definitions = Array.isArray(parsed.definitions) ? (parsed.definitions as JsonDict[]) : [];
    schemaVersion = typeof parsed.schemaVersion === 'number' ? parsed.schemaVersion : 1;
  }

  definitions.push(raw);
  const payload = { schemaVersion, definitions };
  await api.writeText(targetFilePath, JSON.stringify(payload, null, 2) + '\n');
  return loadPackage(state.root);
}

async function fileExists(p: string) {
  try {
    await window.studioApi!.readText(p);
    return true;
  } catch {
    return false;
  }
}

export function validatePackage(state: PackageState): ValidationIssue[] {
  const issues: ValidationIssue[] = [];
  const ids = new Set<string>();

  for (const def of state.defs) {
    if (ids.has(def.id)) {
      issues.push({
        level: 'error',
        message: `重复 id：${def.id}`,
        definitionId: def.id,
        filePath: def.filePath
      });
    }
    ids.add(def.id);

    const allow = TYPE_FIELDS[def.type];
    if (!allow) {
      issues.push({
        level: 'error',
        message: `未知 type：${def.type}`,
        definitionId: def.id,
        filePath: def.filePath
      });
      continue;
    }

    for (const key of Object.keys(def.raw)) {
      if (!allow.has(key)) {
        issues.push({
          level: 'error',
          message: `${def.id} 含未知字段「${key}」（type=${def.type}）`,
          definitionId: def.id,
          filePath: def.filePath
        });
      }
    }

    if (def.type === 'worldRegion') {
      const locs = def.raw.locations;
      if (!Array.isArray(locs) || locs.length === 0) {
        issues.push({
          level: 'error',
          message: `${def.id} 缺少 locations`,
          definitionId: def.id,
          filePath: def.filePath
        });
      } else {
        for (const loc of locs as JsonDict[]) {
          const lid = String(loc.id ?? '');
          for (const key of Object.keys(loc)) {
            if (!LOCATION_FIELDS.has(key)) {
              issues.push({
                level: 'error',
                message: `${def.id} 地点 ${lid || '?'} 未知字段「${key}」`,
                definitionId: def.id,
                filePath: def.filePath
              });
            }
          }
        }
      }
      const start = String(def.raw.startLocationId ?? '');
      if (start && Array.isArray(locs) && !(locs as JsonDict[]).some((l) => l.id === start)) {
        issues.push({
          level: 'error',
          message: `${def.id} startLocationId 不存在：${start}`,
          definitionId: def.id,
          filePath: def.filePath
        });
      }
    }

    if (def.type === 'quest' || def.type === 'contentEvent') {
      checkRefArrays(def, state, issues);
    }
  }

  return issues;
}

function checkRefArrays(def: DefRef, state: PackageState, issues: ValidationIssue[]) {
  const checkCond = (arr: unknown, label: string) => {
    if (!Array.isArray(arr)) return;
    for (const c of arr as JsonDict[]) {
      const kind = String(c.kind ?? '');
      const id = String(c.id ?? '');
      if (!kind) {
        issues.push({
          level: 'warn',
          message: `${def.id} ${label} 缺 kind`,
          definitionId: def.id,
          filePath: def.filePath
        });
      }
      if (
        id &&
        (kind === 'exploredLocation' || kind === 'atLocation') &&
        !locationExists(state, id)
      ) {
        issues.push({
          level: 'error',
          message: `${def.id} ${label} 地点不存在：${id}`,
          definitionId: def.id,
          filePath: def.filePath
        });
      }
      if (id && (kind === 'hasManual' || kind === 'knowsSite' || kind === 'stockAtLeast') && !state.byId[id]) {
        // stock/manual may be ok if resource exists
        if (kind === 'stockAtLeast' && !state.byId[id]) {
          issues.push({
            level: 'warn',
            message: `${def.id} ${label} 资源 id 未在包中：${id}`,
            definitionId: def.id,
            filePath: def.filePath
          });
        }
        if (kind === 'hasManual' && !state.byId[id]) {
          issues.push({
            level: 'error',
            message: `${def.id} ${label} 功法不存在：${id}`,
            definitionId: def.id,
            filePath: def.filePath
          });
        }
      }
    }
  };

  if (def.type === 'quest') {
    checkCond(def.raw.offerConditions, 'offerConditions');
    checkCond(def.raw.completeConditions, 'completeConditions');
  }
  if (def.type === 'contentEvent') {
    checkCond(def.raw.conditions, 'conditions');
    const loc = String(def.raw.locationId ?? '');
    if (loc && !locationExists(state, loc)) {
      issues.push({
        level: 'error',
        message: `${def.id} locationId 不存在：${loc}`,
        definitionId: def.id,
        filePath: def.filePath
      });
    }
  }
}

export function locationExists(state: PackageState, locationId: string): boolean {
  for (const def of state.defs) {
    if (def.type !== 'worldRegion') continue;
    const locs = def.raw.locations;
    if (!Array.isArray(locs)) continue;
    if ((locs as JsonDict[]).some((l) => l.id === locationId)) return true;
  }
  return false;
}

export function allLocationIds(state: PackageState): string[] {
  const ids: string[] = [];
  for (const def of state.defs) {
    if (def.type !== 'worldRegion') continue;
    const locs = def.raw.locations;
    if (!Array.isArray(locs)) continue;
    for (const l of locs as JsonDict[]) {
      if (l.id) ids.push(String(l.id));
    }
  }
  return ids.sort();
}

export function fileLabel(filePath: string) {
  return basename(filePath);
}

export function asArray(v: unknown): JsonDict[] {
  return Array.isArray(v) ? (v as JsonDict[]) : [];
}

export function asStringArray(v: unknown): string[] {
  if (!Array.isArray(v)) return [];
  return v.map((x) => String(x));
}

/** In-memory upsert; call persistPackage to write disk. */
export function upsertDefinition(state: PackageState, def: DefRef, previousId?: string): PackageState {
  const lookupId = previousId || def.id;
  const files = state.files.map((f) => ({ ...f, definitions: f.definitions.slice() }));
  const defs = state.defs.slice();
  const byId = { ...state.byId };

  let filePath = def.filePath;
  if (!/[\\/]/.test(filePath) && !/^[A-Za-z]:/.test(filePath)) {
    filePath = joinData(state.root, filePath);
  }

  let file = files.find((f) => f.path === filePath);
  if (!file) {
    file = { path: filePath, schemaVersion: 1, definitions: [] };
    files.push(file);
  }

  const old = byId[lookupId];
  if (old) {
    // remove from old file slot if moved
    const oldFile = files.find((f) => f.path === old.filePath);
    if (oldFile && old.filePath === filePath) {
      oldFile.definitions[old.index] = def.raw;
      const updated: DefRef = {
        ...def,
        filePath,
        index: old.index,
        name: String(def.raw.name ?? def.name),
        id: String(def.raw.id ?? def.id),
        type: String(def.raw.type ?? def.type)
      };
      if (lookupId !== updated.id) delete byId[lookupId];
      byId[updated.id] = updated;
      const di = defs.findIndex((d) => d.id === lookupId || d.id === updated.id);
      if (di >= 0) defs[di] = updated;
      return { ...state, files, defs, byId };
    }
  }

  file.definitions.push(def.raw);
  const updated: DefRef = {
    ...def,
    filePath,
    index: file.definitions.length - 1,
    name: String(def.raw.name ?? def.name),
    id: String(def.raw.id ?? def.id),
    type: String(def.raw.type ?? def.type)
  };
  if (old && lookupId !== updated.id) {
    delete byId[lookupId];
    const di = defs.findIndex((d) => d.id === lookupId);
    if (di >= 0) defs.splice(di, 1);
  }
  defs.push(updated);
  byId[updated.id] = updated;
  return { ...state, files, defs, byId };
}

export async function persistPackage(state: PackageState, onlyPaths?: string[]): Promise<void> {
  const api = window.studioApi!;
  const targets = onlyPaths
    ? state.files.filter((f) => onlyPaths.includes(f.path))
    : state.files;
  for (const file of targets) {
    const payload = {
      schemaVersion: file.schemaVersion || 1,
      definitions: file.definitions
    };
    await api.writeText(file.path, JSON.stringify(payload, null, 2) + '\n');
  }
}

export function touchedPaths(state: PackageState, dirtyIds: Iterable<string>): string[] {
  const paths = new Set<string>();
  for (const id of dirtyIds) {
    const def = state.byId[id];
    if (def) paths.add(def.filePath);
  }
  return [...paths];
}

export function suggestQuestFile(state: PackageState): string {
  const hit = state.files.find((f) => /quest/i.test(f.path));
  return hit?.path || joinData(state.root, 'quests.json');
}

export function suggestEventFile(state: PackageState): string {
  const hit = state.files.find((f) => /event/i.test(f.path));
  return hit?.path || joinData(state.root, 'content_events.json');
}

export function joinData(root: string, name: string) {
  const sep = root.includes('\\') ? '\\' : '/';
  return root.replace(/[\\/]$/, '') + sep + 'Data' + sep + name;
}
